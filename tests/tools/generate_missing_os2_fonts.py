#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Generates the NoOs2 fonts and NoOs2.harfbuzz.json, the test fonts for issue #148.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz 0.57.0 or later):

    python tests/tools/generate_missing_os2_fonts.py

AppleGothic and AppleMyungjo on macOS have no `OS/2` table, which the TrueType specification of Apple allows.
These two fonts have no `OS/2` table either. Both are in the family "NoOs2":

* NoOs2-Regular.ttf has TrueType outlines and `head.macStyle` 0.
* NoOs2-BoldCondensedItalic.otf has CFF outlines and `head.macStyle` Bold, Italic and Condensed.

Each glyph is one box, and each face gives its glyphs other advances, so a test can tell which face a layout drew.
For each font the JSON holds what HarfBuzz gives with no `OS/2` table: the style values of `hb_style_get_value`,
the metrics of `hb_ot_metrics_get_position_with_fallback`, and the glyphs and advance of each shaped text.
The output is deterministic, so a second run changes no file.
"""

import os

import harfbuzz_reference
import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

FAMILY = "NoOs2"
GLYPHS = [".notdef", "a", "b", "c"]
CMAP = {ord(name): name for name in GLYPHS[1:]}
TEXTS = ["abc", "cab", "a"]

MAC_BOLD = 1 << 0
MAC_ITALIC = 1 << 1
MAC_CONDENSED = 1 << 5

# (file name, name ID 2, head.macStyle, TrueType outlines, advance of a, b and c)
FACES = [
    ("NoOs2-Regular.ttf", "Regular", 0, True, 500),
    ("NoOs2-BoldCondensedItalic.otf", "Bold Condensed Italic", MAC_BOLD | MAC_ITALIC | MAC_CONDENSED, False, 400),
]

STYLES = {"weight": hb.StyleTag.WEIGHT, "width": hb.StyleTag.WIDTH, "italic": hb.StyleTag.ITALIC}

METRICS = {
    "ascender": hb.OTMetricsTag.HORIZONTAL_ASCENDER,
    "descender": hb.OTMetricsTag.HORIZONTAL_DESCENDER,
    "lineGap": hb.OTMetricsTag.HORIZONTAL_LINE_GAP,
    "strikeoutSize": hb.OTMetricsTag.STRIKEOUT_SIZE,
    "strikeoutPosition": hb.OTMetricsTag.STRIKEOUT_OFFSET,
    "underlineThickness": hb.OTMetricsTag.UNDERLINE_SIZE,
    "underlinePosition": hb.OTMetricsTag.UNDERLINE_OFFSET,
    "subscriptXSize": hb.OTMetricsTag.SUBSCRIPT_EM_X_SIZE,
    "subscriptYSize": hb.OTMetricsTag.SUBSCRIPT_EM_Y_SIZE,
    "subscriptXOffset": hb.OTMetricsTag.SUBSCRIPT_EM_X_OFFSET,
    "subscriptYOffset": hb.OTMetricsTag.SUBSCRIPT_EM_Y_OFFSET,
    "superscriptXSize": hb.OTMetricsTag.SUPERSCRIPT_EM_X_SIZE,
    "superscriptYSize": hb.OTMetricsTag.SUPERSCRIPT_EM_Y_SIZE,
    "superscriptXOffset": hb.OTMetricsTag.SUPERSCRIPT_EM_X_OFFSET,
    "superscriptYOffset": hb.OTMetricsTag.SUPERSCRIPT_EM_Y_OFFSET,
}


def draw(pen, width):
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((width - 50, 700))
    pen.lineTo((width - 50, 0))
    pen.closePath()


def build(file_name, style, mac_style, true_type, advance):
    advances = {name: advance + 100 * i for i, name in enumerate(GLYPHS)}
    builder = FontBuilder(1000, isTTF=true_type)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    if true_type:
        glyphs = {}
        for name in GLYPHS:
            pen = TTGlyphPen(None)
            draw(pen, advances[name])
            glyphs[name] = pen.glyph()
        builder.setupGlyf(glyphs)
    else:
        charStrings = {}
        for name in GLYPHS:
            pen = T2CharStringPen(advances[name], None)
            draw(pen, advances[name])
            charStrings[name] = pen.getCharString()
        builder.setupCFF(FAMILY + "-" + style.replace(" ", ""), {"FullName": FAMILY + " " + style}, charStrings, {})
    builder.setupHorizontalMetrics({name: (advances[name], 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200, lineGap=90)
    builder.setupNameTable({"familyName": FAMILY, "styleName": style})
    builder.setupPost(keepGlyphNames=False, underlinePosition=-100, underlineThickness=50)
    builder.updateHead(macStyle=mac_style)
    path = os.path.join(harfbuzz_reference.FONTS, file_name)
    builder.font.save(path)
    assert "OS/2" not in TTFont(path), path
    return path


def main():
    faces = []
    for file_name, style, mac_style, true_type, advance in FACES:
        path = build(file_name, style, mac_style, true_type, advance)
        font = harfbuzz_reference.load_font(path)
        texts = []
        for text in TEXTS:
            glyphs, width = harfbuzz_reference.shape_line(font, text)
            texts.append({"text": text, "glyphIds": [glyph[0] for glyph in glyphs], "advance": width})
        faces.append(
            {
                "file": file_name,
                "style": style,
                "styleValues": {key: font.get_style_value(tag) for key, tag in STYLES.items()},
                "metrics": {key: font.get_metric_position_with_fallback(tag) for key, tag in METRICS.items()},
                "texts": texts,
            }
        )

    reference = os.path.join(harfbuzz_reference.FONTS, "NoOs2.harfbuzz.json")
    harfbuzz_reference.write_reference(reference, faces, family=FAMILY)


if __name__ == "__main__":
    main()
