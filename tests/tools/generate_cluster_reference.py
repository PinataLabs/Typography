#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes Clusters.harfbuzz.json, how HarfBuzz shapes a sign that has no letter of its script before it.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_cluster_reference.py

The script does not read the HarfBuzz source. It builds ClustersBalinese.ttf, which has no GSUB table, so only the
shaper changes the order and the number of the glyphs. The font has Balinese letters and signs, the dotted circle,
and the characters of the Basic Latin block and some others that have the script Common. HarfBuzz shapes Balinese
text with its universal shaper.

The reference holds the glyph IDs that HarfBuzz gives, and where it draws each glyph, for:

* each sign alone, before a letter, and after a letter and a space. Such a sign starts a broken cluster, and
  HarfBuzz gives it a dotted circle as its base.
* each sign after each other sign. These pairs have no virama: HarfBuzz joins a virama and some of the signs next
  to it in one cluster, and the library does not.
* each Common character before a vowel sign that goes above or after the base, and before one that goes in front
  of it. The universal shaper takes most of these characters as the base of the sign.

ClusterTests reads the reference.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "Clusters.harfbuzz.json")

DOTTED_CIRCLE = 0x25CC
SPACE = 0x20

# Characters of the script Common. A caller of HarfBuzz gives them the script of the text around them.
# The universal shaper does not take a right square or curly bracket as a base.
COMMON = [c for c in range(0x20, 0x7F) if not chr(c).isalpha()] + [0xA0, 0xD7, 0x2010, 0x2013, 0x2014, 0x27E7, 0x3000]

# (font, ISO 15924 code, letter, sign above or after the base, sign in front of the base, other signs, virama)
SCRIPTS = [
    # KA, the vowel signs ULU and TALING, the signs SURANG, BISAH, REREKAN and SUKU, and ADEG ADEG.
    ("ClustersBalinese.ttf", "Bali", 0x1B13, 0x1B36, 0x1B3E, [0x1B03, 0x1B04, 0x1B34, 0x1B38], 0x1B44),
]


def build(file_name, code_points):
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({name: (600, 0) for name in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    path = os.path.join(FONTS, file_name)
    builder.font.save(path)
    with open(path, "rb") as file:
        return hb.Font(hb.Face(file.read()))


def shape(font, script, text):
    """Gives the glyph ID of each glyph and where HarfBuzz draws it, in font units from the start of the line."""
    # The text has one script, as a run that a caller of HarfBuzz itemizes. A space takes the script of the run.
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.script = script
    buffer.direction = "ltr"
    hb.shape(font, buffer)
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append([info.codepoint, x + position.x_offset])
        x += position.x_advance
    return glyphs


def main():
    rows = []
    for file_name, script, letter, after, front, others, virama in SCRIPTS:
        signs = sorted([after, front, *others])
        font = build(file_name, sorted({*COMMON, DOTTED_CIRCLE, letter, *signs, virama}))
        texts = []
        for sign in [*signs, virama]:
            texts += [[sign], [sign, letter], [letter, SPACE, sign]]
        for sign in signs:
            texts += [[other, sign] for other in signs]
        for character in COMMON:
            texts += [[character, after], [character, front]]
        for text in dict.fromkeys("".join(map(chr, text)) for text in texts):
            rows.append([file_name, script, text, shape(font, script, text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
