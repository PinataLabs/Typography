#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes DefaultIgnorables.harfbuzz.json, how HarfBuzz draws the default-ignorable characters of a text.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_default_ignorable_reference.py

After shaping, HarfBuzz gives each default-ignorable character that GSUB did not substitute no advance and no
offset. When the font has a glyph for U+0020 it draws the character with that glyph, and otherwise it takes the
glyph out of its output. The script builds two fonts to show both:

* DefaultIgnorables.ttf has U+0020, two Latin letters, an Arabic letter, a Mongolian letter, two Sinhala letters
  and the virama, and the default-ignorable characters ZWJ, ZWNJ, the Mongolian free variation selectors, the
  Mongolian vowel separator, COMBINING GRAPHEME JOINER, SOFT HYPHEN and WORD JOINER. Every glyph is 600 units wide.
  It also has the Hangul fillers and SHORTHAND FORMAT LETTER OVERLAP. Unicode makes them default-ignorable, but
  HarfBuzz draws them with their own glyphs.
  Its GSUB `ccmp` feature substitutes WORD JOINER, which HarfBuzz then draws as GSUB left it.
* DefaultIgnorablesNoSpace.ttf has the same glyphs without U+0020 and without GSUB.

The reference also has text with ZWJ and ZWNJ in test fonts of the repository: Segoe UI, Noto Sans Devanagari,
Noto Sans Bengali, and Open Sans, which has no glyph for ZWJ.

The reference holds the glyph ID and the place of each glyph that HarfBuzz gives, in font units from the start of
the line, and the advance of the whole text. DefaultIgnorableTests reads it.
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
REFERENCE = os.path.join(FONTS, "DefaultIgnorables.harfbuzz.json")

ZWNJ, ZWJ, WJ = 0x200C, 0x200D, 0x2060
IGNORABLES = [0x00AD, 0x034F, 0x180B, 0x180C, 0x180D, 0x180E, 0x180F, ZWNJ, ZWJ, WJ]

# The Hangul fillers U+115F, U+1160, U+3164 and U+FFA0, and SHORTHAND FORMAT LETTER OVERLAP.
DRAWN_IGNORABLES = [0x115F, 0x1160, 0x3164, 0xFFA0, 0x1BCA0]

# a, b, BEH, MONGOLIAN LETTER A, SINHALA KA, SINHALA YAYANNA, SINHALA AL-LAKUNA.
LETTERS = [0x61, 0x62, 0x0628, 0x1820, 0x0D9A, 0x0DBA, 0x0DCA]

BUILT_TEXTS = [
    [0x61, ZWJ, 0x62],
    [0x61, ZWNJ, 0x62],
    [ZWJ, 0x61],
    [0x61, ZWJ],
    [0x61, 0x00AD, 0x62],
    [0x61, 0x034F, 0x62],
    [0x61, WJ, 0x62],
    [0x61, ZWJ, ZWNJ, 0x62],
    [0x0628, ZWJ],
    [0x0628, ZWNJ, 0x0628],
    [0x1820, 0x180B, 0x1820],
    [0x1820, 0x180C],
    [0x1820, 0x180D, 0x1820],
    [0x1820, 0x180F, 0x1820],
    [0x1820, 0x180E, 0x1820],
    [0x0D9A, 0x0DCA, ZWJ, 0x0DBA],
    [0x0D9A, 0x0DCA, ZWNJ, 0x0DBA],
    [0x61, ZWJ, 0x62, 0x20, 0x61, ZWNJ, 0x62],
] + [[0x61, c, 0x62] for c in DRAWN_IGNORABLES]

# Text in fonts of the repository: (font, text).
FONT_TEXTS = [
    ("Segoeui.ttf", [0x0628, ZWNJ, 0x0628]),
    ("Segoeui.ttf", [0x0628, ZWJ]),
    ("Segoeui.ttf", [ZWJ, 0x0628]),
    ("Segoeui.ttf", [0x064A, ZWJ, 0x064A]),
    ("Segoeui.ttf", [0x0644, ZWNJ, 0x0627]),
    ("NotoSansDevanagari-Regular.ttf", [0x0915, 0x094D, ZWJ, 0x0937]),
    ("NotoSansDevanagari-Regular.ttf", [0x0915, 0x094D, ZWNJ, 0x0937]),
    ("NotoSansDevanagari-Regular.ttf", [0x0915, 0x094D, ZWJ]),
    ("NotoSansBengali-Regular.ttf", [0x0995, 0x09CD, ZWJ, 0x09B7]),
    ("NotoSansBengali-Regular.ttf", [0x09B0, 0x09CD, ZWJ, 0x09AF]),
    ("NotoSansBengali-Regular.ttf", [0x0995, 0x09CD, ZWNJ]),
    ("OpenSans-Regular.ttf", [0x05DC, ZWJ, 0x05DD]),
    ("OpenSans-Regular.ttf", [0x61, ZWNJ, 0x62]),
]


def build(file_name, code_points, features=None):
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    if features:
        names.append("wj.alt")
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
    path = os.path.join(FONTS, file_name)
    builder.font.save(path)
    return path


def shape(path, text):
    """Gives the glyph ID and place of each glyph, in font units from the start of the line, and the advance."""
    with open(path, "rb") as file:
        font = hb.Font(hb.Face(file.read()))
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
        x += position.x_advance
    return glyphs, x


def main():
    characters = sorted(LETTERS + IGNORABLES + DRAWN_IGNORABLES)
    features = "languagesystem DFLT dflt;\nfeature ccmp { sub u%04X by wj.alt; } ccmp;\n" % WJ
    built = [
        build("DefaultIgnorables.ttf", sorted([0x20] + characters), features),
        build("DefaultIgnorablesNoSpace.ttf", characters),
    ]

    rows = []
    for path in built:
        for text in BUILT_TEXTS:
            text = "".join(map(chr, text))
            rows.append([os.path.basename(path), text, *shape(path, text)])
    for file_name, text in FONT_TEXTS:
        text = "".join(map(chr, text))
        rows.append([file_name, text, *shape(os.path.join(FONTS, file_name), text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
