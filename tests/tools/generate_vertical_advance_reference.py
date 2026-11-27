#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds VerticalAdvance.ttf and VerticalAdvanceKern.ttf and writes VerticalAdvance.harfbuzz.json, the glyphs that HarfBuzz
gives for lines of the two fonts in horizontal and in vertical text (issue #170).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_vertical_advance_reference.py

Both fonts have empty glyphs and 1000 units to the em. The ideographs U+4E00, U+4E8C and U+4E09 are 900 wide and 1000 high,
and A and V are 600 wide and 1000 high, so that the advance across the line differs from the advance along it.

VerticalAdvance.ttf has a GPOS table:

* `kern`: A V moves the advance of A by -80 on x and by 50 on y. HarfBuzz takes only the x advance in horizontal text.
* `vkrn`: U+4E00 U+4E8C moves the advance of U+4E00 by -200 on y, U+4E8C U+4E09 that of U+4E8C by 300, and U+4E09 U+4E00
  that of U+4E09 by -1300 on y and by 77 on x. HarfBuzz takes only the y advance in vertical text, and its y advance grows
  downward in font space, so -200 makes the advance shorter and -1300 takes it below zero. HarfBuzz does not apply `vkrn`
  unless the caller turns it on, so the vertical rows turn it on.

VerticalAdvanceKern.ttf has no GPOS table and a `kern` table of two subtables: a horizontal one, in which A V is 50 units
closer, and a vertical one, in which U+4E00 U+4E8C is 200 units closer and U+4E8C U+4E09 300 units apart. HarfBuzz applies
the horizontal subtable to horizontal text and neither subtable to vertical text.

Each row has the font, the text, the direction and the features of the line, the glyphs in the order of the line as
[glyph ID, x advance, y advance, x offset, y offset] in the coordinates of HarfBuzz, in which y goes up, and the advances
that the font gives each glyph as [advance width, advance height].

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import harfbuzz_reference as ref
import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib import newTable
from fontTools.ttLib.tables._g_l_y_f import Glyph
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0

REFERENCE = os.path.join(ref.FONTS, "VerticalAdvance.harfbuzz.json")
GPOS_FONT = "VerticalAdvance.ttf"
KERN_FONT = "VerticalAdvanceKern.ttf"

IDEOGRAPHS = [0x4E00, 0x4E8C, 0x4E09]
LETTERS = [0x41, 0x56]
CODE_POINTS = LETTERS + IDEOGRAPHS
WIDTHS = {**{c: 900 for c in IDEOGRAPHS}, **{c: 600 for c in LETTERS}}
HEIGHT = 1000

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem hani dflt;

feature kern {
    pos u0041 u0056 <0 0 -80 50>;
} kern;

feature vkrn {
    pos u4E00 u4E8C <0 0 0 -200>;
    pos u4E8C u4E09 <0 0 0 300>;
    pos u4E09 u4E00 <0 0 77 -1300>;
} vkrn;
"""

ONE, TWO, THREE = "一", "二", "三"

# The font, the text, the direction and the features of each line.
CASES = [
    (GPOS_FONT, "AV", "ltr", {}),
    (GPOS_FONT, ONE + TWO + THREE + ONE, "ttb", {"vkrn": True}),
    (GPOS_FONT, THREE + ONE, "ttb", {"vkrn": True}),
    (KERN_FONT, "AV", "ltr", {}),
    (KERN_FONT, "AV", "ttb", {}),
    (KERN_FONT, ONE + TWO + THREE, "ttb", {}),
]


def name(code_point):
    return "u%04X" % code_point


def build_font(file_name, features=None, kern_tables=()):
    names = [".notdef"] + [name(c) for c in CODE_POINTS]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: name(c) for c in CODE_POINTS})
    builder.setupGlyf({n: Glyph() for n in names})
    builder.setupHorizontalMetrics({n: (WIDTHS.get(int(n[1:], 16), 600) if n != ".notdef" else 600, 0) for n in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupVerticalMetrics({n: (HEIGHT, 0) for n in names})
    builder.setupVerticalHeader(ascent=500, descent=-500)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    if features:
        addOpenTypeFeaturesFromString(builder.font, features)
    if kern_tables:
        table = newTable("kern")
        table.version = 0
        table.kernTables = []
        for coverage, pairs in kern_tables:
            subtable = KernTable_format_0()
            subtable.version = 0
            subtable.format = 0
            subtable.coverage = coverage
            subtable.kernTable = {(name(a), name(b)): value for (a, b), value in pairs.items()}
            table.kernTables.append(subtable)
        builder.font["kern"] = table
    path = os.path.join(ref.FONTS, file_name)
    builder.font.save(path)
    return path


def shape(font, text, direction, features):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    buffer.direction = direction
    hb.shape(font, buffer, features)
    glyphs = [[i.codepoint, p.x_advance, p.y_advance, p.x_offset, p.y_offset]
              for i, p in zip(buffer.glyph_infos, buffer.glyph_positions)]
    advances = [[font.get_glyph_h_advance(i.codepoint), -font.get_glyph_v_advance(i.codepoint)] for i in buffer.glyph_infos]
    return glyphs, advances


def main():
    fonts = {
        GPOS_FONT: ref.load_font(build_font(GPOS_FONT, features=FEATURES)),
        KERN_FONT: ref.load_font(build_font(
            KERN_FONT,
            kern_tables=[(1, {(0x41, 0x56): -50}), (0, {(0x4E00, 0x4E8C): -200, (0x4E8C, 0x4E09): 300})])),
    }
    rows = []
    for font_name, text, direction, features in CASES:
        glyphs, advances = shape(fonts[font_name], text, direction, features)
        row = {"font": font_name, "text": text, "direction": direction}
        if features:
            row["features"] = sorted(tag for tag, on in features.items() if on)
        row["glyphs"] = glyphs
        row["advances"] = advances
        rows.append(row)
    ref.write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
