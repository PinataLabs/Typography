#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes HebrewMarks.harfbuzz.json, where HarfBuzz draws Hebrew letters and the GPOS marks on them.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_hebrew_marks_reference.py

The reference holds three cases:

* GposKernAcrossMark: Segoeui.ttf has a GPOS `kern` pair for lamed and final mem in a lookup that ignores marks.
  HarfBuzz skips the mark between the two letters and kerns them.
* ThreeMarks: TimesNewRoman.ttf and Segoeui.ttf attach qamats to an alef, meteg to the qamats and etnahta to the
  meteg. HarfBuzz attaches a mark to the mark before it when the two marks belong to the same base.
* KernTableMovesBase: HebrewKernMark.ttf, which the script builds, has a `kern` table and a GPOS table with a
  `mark` feature and no `kern` feature. HarfBuzz kerns with the `kern` table after the GPOS lookups, and then
  places each mark from its base, so a mark moves with a base that the `kern` table moved. The font has Latin
  letters too, for the same case in text that runs left to right.

For each text the reference holds the case, the font, and the glyph ID of each glyph with where HarfBuzz draws it,
in font units from the start of the line. HebrewMarkTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib import newTable
from fontTools.ttLib.tables._g_l_y_f import Glyph
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "HebrewMarks.harfbuzz.json")
KERN_MARK_FONT = "HebrewKernMark.ttf"

ALEF, BET, GIMEL, QAMATS, HOLAM = 0x05D0, 0x05D1, 0x05D2, 0x05B8, 0x05B9
LATIN_A, LATIN_B, ACUTE = 0x61, 0x62, 0x0301

# HarfBuzz uses the GPOS table for Hebrew text only when the table has the `hebr` script. The `kern` table has a
# different pair for each order of two letters, so the reference shows which order HarfBuzz looks up.
KERN_MARK_FEATURES = """
languagesystem DFLT dflt;
languagesystem hebr dflt;
languagesystem latn dflt;

markClass [qamats] <anchor 100 0> @BELOW;
markClass [holam acute] <anchor 50 700> @ABOVE;

feature mark {
    pos base [alef bet gimel a b] <anchor 300 0> mark @BELOW;
    pos base [alef bet gimel a b] <anchor 250 650> mark @ABOVE;
} mark;

table GDEF {
    GlyphClassDef [alef bet gimel a b], , [qamats holam acute], ;
} GDEF;
"""

KERN_PAIRS = {
    ("bet", "alef"): -100, ("alef", "bet"): -60, ("gimel", "bet"): -40, ("bet", "gimel"): -20, ("a", "b"): -80,
}

TEXTS = [
    ("GposKernAcrossMark", "Segoeui.ttf", [0x05DC, QAMATS, 0x05DD]),
    ("GposKernAcrossMark", "Segoeui.ttf", [ALEF, 0x05B1, 0x05DC, HOLAM, 0x05D4, 0x05B4, 0x05D9, 0x05DD]),
    ("ThreeMarks", "TimesNewRoman.ttf", [ALEF, QAMATS, 0x05BD, 0x0591]),
    ("ThreeMarks", "Segoeui.ttf", [ALEF, QAMATS, 0x05BD, 0x0591]),
    ("KernTableMovesBase", KERN_MARK_FONT, [BET, ALEF, QAMATS]),
    ("KernTableMovesBase", KERN_MARK_FONT, [ALEF, QAMATS, BET]),
    ("KernTableMovesBase", KERN_MARK_FONT, [BET, QAMATS, ALEF, HOLAM]),
    ("KernTableMovesBase", KERN_MARK_FONT, [GIMEL, BET, QAMATS, HOLAM, ALEF]),
    ("KernTableMovesBase", KERN_MARK_FONT, [LATIN_A, ACUTE, LATIN_B]),
    ("KernTableMovesBase", KERN_MARK_FONT, [LATIN_A, LATIN_B, ACUTE]),
]


def build_kern_mark_font():
    cmap = {ALEF: "alef", BET: "bet", GIMEL: "gimel", QAMATS: "qamats", HOLAM: "holam", LATIN_A: "a", LATIN_B: "b", ACUTE: "acute"}
    names = [".notdef", "alef", "bet", "gimel", "qamats", "holam", "a", "b", "acute"]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap(cmap)
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({name: (0 if name in ("qamats", "holam", "acute") else 600, 0) for name in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "HebrewKernMark", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    addOpenTypeFeaturesFromString(builder.font, KERN_MARK_FEATURES)

    subtable = KernTable_format_0()
    subtable.version = 0
    subtable.coverage = 1
    subtable.kernTable = dict(KERN_PAIRS)
    kern = newTable("kern")
    kern.version = 0
    kern.kernTables = [subtable]
    builder.font["kern"] = kern

    builder.font.save(os.path.join(FONTS, KERN_MARK_FONT))


def shape(font, text):
    """Gives the glyph ID of each glyph and where HarfBuzz draws it, in font units from the start of the line."""
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
        x += position.x_advance
    return glyphs


def main():
    build_kern_mark_font()
    fonts = {}
    rows = []
    for case, file_name, code_points in TEXTS:
        if file_name not in fonts:
            with open(os.path.join(FONTS, file_name), "rb") as file:
                fonts[file_name] = hb.Font(hb.Face(file.read()))
        text = "".join(map(chr, code_points))
        rows.append([case, file_name, text, shape(fonts[file_name], text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
