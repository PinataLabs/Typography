#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Generates GsubLookupOrder.ttf, the test font for GSUB lookup ordering.

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_gsub_lookup_order_font.py

The font's LookupList order differs from the order in which a shaper plans the features:

* lookup 0, `vrtr`: f -> f.vrtr, i -> i.vrtr
* lookup 1, `liga`: f i -> f_i, f.vrtr i.vrtr -> f_i.vrtr, a b -> a_b
* lookup 2, `calt`: a -> a.alt

A shaper that applies lookups in LookupList order gives f_i.vrtr for a rotated "fi" and
a_b for "ab". The output is deterministic, so a second run changes no file.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts", "GsubLookupOrder.ttf")

# The glyph order is the glyph ID order that GsubLookupOrderTests relies on.
GLYPHS = [".notdef", "f", "i", "a", "b", "f.vrtr", "i.vrtr", "f_i", "f_i.vrtr", "a.alt", "a_b"]
CMAP = {ord("f"): "f", ord("i"): "i", ord("a"): "a", ord("b"): "b"}

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;

feature vrtr {
    sub f by f.vrtr;
    sub i by i.vrtr;
} vrtr;

feature liga {
    sub f i by f_i;
    sub f.vrtr i.vrtr by f_i.vrtr;
    sub a b by a_b;
} liga;

feature calt {
    sub a by a.alt;
} calt;
"""


def box(width):
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((width - 50, 700))
    pen.lineTo((width - 50, 0))
    pen.closePath()
    return pen.glyph()


def main():
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    advances = {name: 500 + 10 * index for index, name in enumerate(GLYPHS)}
    builder.setupGlyf({name: box(advances[name]) for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (advances[name], 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "GsubLookupOrder", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    addOpenTypeFeaturesFromString(builder.font, FEATURES)

    gsub = builder.font["GSUB"].table
    features = {record.FeatureTag: record.Feature.LookupListIndex for record in gsub.FeatureList.FeatureRecord}
    assert features == {"vrtr": [0], "liga": [1], "calt": [2]}, features

    builder.font.save(OUT)
    print(os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
