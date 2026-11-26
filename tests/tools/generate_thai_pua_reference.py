#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes ThaiPua.harfbuzz.json, how HarfBuzz draws Thai marks in a font that has no GSUB script for Thai.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_thai_pua_reference.py

When the GSUB table of a font has no `thai` script, HarfBuzz draws a Thai mark that would clash with its base or
with the mark before it with a glyph of the private use area, as Windows and Mac Thai fonts provide them: a tone
mark lower or further left, a vowel below a consonant with a descender lower, and YO YING and THO THAN without their
descender. It looks for the Windows code point first and then for the Mac one.

The script builds three fonts with Thai consonants, marks and SARA AM, and the space:

* ThaiPua.ttf also has the private use glyphs of Windows and of Mac, and no GSUB table.
* ThaiPuaMac.ttf has only the private use glyphs of Mac.
* ThaiPuaGsub.ttf has both and a GSUB table with the `thai` script, so HarfBuzz uses no private use glyph.

The reference holds the glyph IDs and where HarfBuzz draws each glyph for each consonant of each kind, a digit and a
space, followed by one or two marks, and the same texts in FDArrayTest257.otf. ThaiPuaTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "ThaiPua.harfbuzz.json")

# KO KAI, PO PLA, FO FA, YO YING, THO THAN, DO CHADA, TO PATAK, a digit and the space: consonants with no
# descender, with an ascender, with a descender that can go, and with one that stays, and characters that are not.
BASES = [0x0E01, 0x0E1B, 0x0E1F, 0x0E0D, 0x0E10, 0x0E0E, 0x0E0F, 0x0E51, 0x0020]

# MAI HAN-AKAT, SARA I, MAITAIKHU, NIKHAHIT (above), SARA U, PHINTHU (below), MAI EK, THANTHAKHAT (tone marks)
# and SARA AM.
MARKS = [0x0E31, 0x0E34, 0x0E47, 0x0E4D, 0x0E38, 0x0E3A, 0x0E48, 0x0E4C, 0x0E33]

THAI = sorted(set(BASES + MARKS + [0x0E32, 0x0E39, 0x0E49]))
WINDOWS_PUA = list(range(0xF700, 0xF71B))
MAC_PUA = list(range(0xF884, 0xF89F))

FDARRAY = "FDArrayTest257.otf"


def build(file_name, code_points, features=None):
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
    if features:
        addOpenTypeFeaturesFromString(builder.font, features)
    builder.font.save(os.path.join(FONTS, file_name))


def shape(font, text):
    """Gives the glyph ID and the place of each glyph, in font units from the start of the line."""
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
    build("ThaiPua.ttf", THAI + WINDOWS_PUA + MAC_PUA)
    build("ThaiPuaMac.ttf", THAI + MAC_PUA)
    build("ThaiPuaGsub.ttf", THAI + WINDOWS_PUA + MAC_PUA, "languagesystem thai dflt;\nfeature ccmp { sub u0E01 by u0E01; } ccmp;\n")

    texts = []
    for base in BASES:
        for first in MARKS:
            texts.append([base, first])
            texts += [[base, first, second] for second in MARKS]

    rows = []
    for file_name in ("ThaiPua.ttf", "ThaiPuaMac.ttf", "ThaiPuaGsub.ttf", FDARRAY):
        with open(os.path.join(FONTS, file_name), "rb") as file:
            font = hb.Font(hb.Face(file.read()))
        for text in texts:
            text = "".join(map(chr, text))
            rows.append([file_name, text, shape(font, text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
