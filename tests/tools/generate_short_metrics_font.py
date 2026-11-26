#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Generates ShortMetrics.ttf and ShortMetrics.fonttools.json, the test font for issue #137.

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_short_metrics_font.py

The font has five glyphs. `.notdef` is 300 units wide and 400 units tall, and every other glyph is
600 units wide and 1000 units tall. fontTools stores only the long metrics up to the last change of
advance, so `hhea.numberOfHMetrics` and `vhea.numOfLongVerMetrics` are 2, and glyphs 2 to 4 take the
last advance of `hmtx` and `vmtx`. The JSON holds the advances that fontTools reads back from the saved
font, keyed by character. The output is deterministic, so a second run changes no file.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import fontTools
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
OUT = os.path.join(FONTS, "ShortMetrics.ttf")
REFERENCE = os.path.join(FONTS, "ShortMetrics.fonttools.json")

GLYPHS = [".notdef", "a", "b", "c", "d"]
CMAP = {ord(name): name for name in GLYPHS[1:]}
NOTDEF_ADVANCE = (300, 400)
SHARED_ADVANCE = (600, 1000)


def box(width):
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((width - 50, 700))
    pen.lineTo((width - 50, 0))
    pen.closePath()
    return pen.glyph()


def main():
    advances = {name: SHARED_ADVANCE for name in GLYPHS}
    advances[".notdef"] = NOTDEF_ADVANCE

    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    builder.setupGlyf({name: box(advances[name][0]) for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (advances[name][0], 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupVerticalMetrics({name: (advances[name][1], 100) for name in GLYPHS})
    builder.setupVerticalHeader(ascent=500, descent=-500)
    builder.setupNameTable({"familyName": "ShortMetrics", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    builder.font.save(OUT)

    font = TTFont(OUT)
    assert font["hhea"].numberOfHMetrics == 2, font["hhea"].numberOfHMetrics
    assert font["vhea"].numberOfVMetrics == 2, font["vhea"].numberOfVMetrics
    cmap = font.getBestCmap()
    reference = {
        "fontTools": fontTools.version,
        "numberOfHMetrics": font["hhea"].numberOfHMetrics,
        "numOfLongVerMetrics": font["vhea"].numberOfVMetrics,
        "glyphs": [
            {
                "char": chr(code),
                "glyphId": font.getGlyphID(name),
                "advanceWidth": font["hmtx"][name][0],
                "advanceHeight": font["vmtx"][name][0],
            }
            for code, name in sorted(cmap.items())
        ],
    }
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        json.dump(reference, file, indent=2)
        file.write("\n")

    print(os.path.relpath(OUT, ROOT))
    print(os.path.relpath(REFERENCE, ROOT))


if __name__ == "__main__":
    main()
