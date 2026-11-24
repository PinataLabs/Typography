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
* lookup 3, `liga` and `ss01`: c copyright -> c_copyright
* lookup 4, no feature: d copyright -> d_copyright
* lookup 5, `liga` and `ss01`: d followed by copyright -> lookup 4
* lookup 6, `ccmp`: beh.rtlm -> beh.ccmp
* lookup 7, `rtlm`: beh -> beh.rtlm

A shaper that applies lookups in LookupList order gives f_i.vrtr for a rotated "fi" and
a_b for "ab". Lookups 3 and 5 belong to two features, so they apply to a glyph that has
either one. Lookups 6 and 7 are in the reverse of the order in which the Arabic shaper
needs them, so beh.ccmp shows that `rtlm` ran in a stage before `ccmp`.
The output is deterministic, so a second run changes no file.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts", "GsubLookupOrder.ttf")

# The glyph order is the glyph ID order that GsubLookupOrderTests relies on.
GLYPHS = [
    ".notdef", "f", "i", "a", "b", "f.vrtr", "i.vrtr", "f_i", "f_i.vrtr", "a.alt", "a_b",
    "c", "d", "copyright", "c_copyright", "d_copyright", "beh", "beh.rtlm", "beh.ccmp",
]
CMAP = {
    ord("f"): "f", ord("i"): "i", ord("a"): "a", ord("b"): "b",
    ord("c"): "c", ord("d"): "d", 0x00A9: "copyright", 0x0628: "beh",
}

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem arab dflt;

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

feature ss01 {
    lookup SHARED_LIGATURE {
        sub c copyright by c_copyright;
    } SHARED_LIGATURE;
} ss01;

lookup NESTED_LIGATURE {
    sub d copyright by d_copyright;
} NESTED_LIGATURE;

feature ss01 {
    lookup SHARED_CONTEXT {
        sub d' lookup NESTED_LIGATURE copyright';
    } SHARED_CONTEXT;
} ss01;

feature liga {
    lookup SHARED_LIGATURE;
    lookup SHARED_CONTEXT;
} liga;

feature ccmp {
    sub beh.rtlm by beh.ccmp;
} ccmp;

feature rtlm {
    sub beh by beh.rtlm;
} rtlm;
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
    expected = {"vrtr": [0], "liga": [1, 3, 5], "calt": [2], "ss01": [3, 5], "ccmp": [6], "rtlm": [7]}
    assert features == expected, features

    builder.font.save(OUT)
    print(os.path.relpath(OUT, ROOT))


if __name__ == "__main__":
    main()
