#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds CentralBaselineTest.ttf, the asymmetric-metric font for the central baseline test (issue #50).

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/central_baseline/build_font.py

UPM 1000, ascent 1100, descent -200, line gap 100 in hhea, OS/2 typo and OS/2 win, with
USE_TYPO_METRICS set, so every engine reads the same used ascent and descent.
Latin A, B and C are rectangles that fill the font's ascent to descent range, so the ink
centre of a rotated Latin glyph is its central baseline. The kana are centred squares.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts", "CentralBaselineTest.ttf")

UPM = 1000
ASCENT = 1100
DESCENT = -200
LINE_GAP = 100

LATIN = {"A": 0x41, "B": 0x42, "C": 0x43}
KANA = {"a": 0x3042, "i": 0x3044, "u": 0x3046, "e": 0x3048, "o": 0x304A}


def rect(x0, y0, x1, y1):
    pen = TTGlyphPen(None)
    pen.moveTo((x0, y0))
    pen.lineTo((x0, y1))
    pen.lineTo((x1, y1))
    pen.lineTo((x1, y0))
    pen.closePath()
    return pen.glyph()


def main():
    names = [".notdef"] + list(LATIN) + ["kana_" + k for k in KANA]
    glyphs = {".notdef": TTGlyphPen(None).glyph()}
    metrics = {".notdef": (500, 0)}
    cmap = {}

    for name, cp in LATIN.items():
        glyphs[name] = rect(100, DESCENT, 500, ASCENT)
        metrics[name] = (600, 100)
        cmap[cp] = name

    for name, cp in KANA.items():
        glyph_name = "kana_" + name
        glyphs[glyph_name] = rect(100, 0, 900, 800)
        metrics[glyph_name] = (1000, 100)
        cmap[cp] = glyph_name

    fb = FontBuilder(UPM, isTTF=True)
    fb.setupGlyphOrder(names)
    fb.setupCharacterMap(cmap)
    fb.setupGlyf(glyphs)
    fb.setupHorizontalMetrics(metrics)
    fb.setupHorizontalHeader(ascent=ASCENT, descent=DESCENT, lineGap=LINE_GAP)
    fb.setupNameTable({"familyName": "CentralBaselineTest", "styleName": "Regular"})
    fb.setupOS2(
        sTypoAscender=ASCENT,
        sTypoDescender=DESCENT,
        sTypoLineGap=LINE_GAP,
        usWinAscent=ASCENT,
        usWinDescent=-DESCENT,
        fsSelection=0x40 | 0x80,
        achVendID="NONE",
        version=4,
    )
    fb.setupPost()
    fb.font["head"].created = fb.font["head"].modified = 0
    fb.save(OUT)
    print(OUT)


if __name__ == "__main__":
    main()
