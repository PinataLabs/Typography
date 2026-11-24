#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Generates GposLookupOrder.ttf and its HarfBuzz reference, the fixtures for GPOS lookup ordering.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_gpos_lookup_order_font.py

The font's LookupList order differs from the order in which a shaper plans the features:

* lookup 0, `kern`: gravecomb is moved right by 120
* lookup 1, `mkmk`: a top mark is attached to acutecomb
* lookup 2, `mark`: a top mark is attached to o, fatha to beh, anusvara to ka
* lookup 3, `kern` and `mark`: A V is kerned by -100
* lookup 4, no feature: T is narrowed by 70
* lookup 5, `kern` and `mark`: T followed by o -> lookup 4
* lookup 6, `abvm`: anusvara is attached to a higher anchor of ka
* lookup 7, `calt`: fatha is attached to a higher anchor of beh

An attachment replaces the offset of the mark, so the last lookup to position a mark wins.
A shaper that applies lookups in LookupList order drops the `kern` shift of gravecomb,
attaches a second mark to the base instead of to the first mark, and uses the higher
anchors of lookups 6 and 7. Lookups 3 and 5 belong to two features and still apply once.

GposLookupOrder.harfbuzz.json holds the glyph positions that HarfBuzz gives for each test
text, in font units from the origin of the line. GposLookupOrderTests reads it.
The output is deterministic, so a second run changes no file.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
OUT = os.path.join(FONTS, "GposLookupOrder.ttf")
REFERENCE = os.path.join(FONTS, "GposLookupOrder.harfbuzz.json")

BASES = ["o", "A", "V", "T", "beh", "ka"]
MARKS = ["gravecomb", "acutecomb", "circumflexcomb", "fatha", "anusvara"]
GLYPHS = [".notdef"] + BASES + MARKS
CMAP = {
    ord("o"): "o", ord("A"): "A", ord("V"): "V", ord("T"): "T", 0x0628: "beh", 0x0915: "ka",
    0x0300: "gravecomb", 0x0301: "acutecomb", 0x0302: "circumflexcomb", 0x064E: "fatha", 0x0902: "anusvara",
}

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem arab dflt;
languagesystem dev2 dflt;

markClass [gravecomb acutecomb circumflexcomb] <anchor 50 450> @TOP;
markClass fatha <anchor 50 450> @ARABIC_TOP;
markClass anusvara <anchor 50 450> @DEVANAGARI_TOP;

feature kern {
    pos gravecomb <120 0 0 0>;
} kern;

feature mkmk {
    pos mark acutecomb <anchor 50 700> mark @TOP;
} mkmk;

feature mark {
    pos base o <anchor 300 500> mark @TOP;
    pos base beh <anchor 300 500> mark @ARABIC_TOP;
    pos base ka <anchor 300 500> mark @DEVANAGARI_TOP;
} mark;

lookup SHARED_PAIR {
    pos A V -100;
} SHARED_PAIR;

lookup NESTED_SINGLE {
    pos T <0 0 -70 0>;
} NESTED_SINGLE;

lookup SHARED_CONTEXT {
    pos T' lookup NESTED_SINGLE o;
} SHARED_CONTEXT;

feature kern {
    lookup SHARED_PAIR;
    lookup SHARED_CONTEXT;
} kern;

feature mark {
    lookup SHARED_PAIR;
    lookup SHARED_CONTEXT;
} mark;

feature abvm {
    pos base ka <anchor 300 650> mark @DEVANAGARI_TOP;
} abvm;

feature calt {
    pos base beh <anchor 300 650> mark @ARABIC_TOP;
} calt;
"""

# Each text has every glyph once, so the tests can find a glyph by its ID.
TEXTS = {
    "MarkAlone": "ó",
    "KernBeforeMark": "ò",
    "MkmkBeforeMark": "ó̂",
    "PairOfTwoFeatures": "AV",
    "ContextOfTwoFeatures": "To",
    "MarkBeforeAbvm": "कं",
    "MarkBeforeCalt": "بَ",
}


def box():
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((450, 700))
    pen.lineTo((450, 0))
    pen.closePath()
    return pen.glyph()


def build():
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    builder.setupGlyf({name: box() for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (0 if name in MARKS else 600, 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "GposLookupOrder", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    addOpenTypeFeaturesFromString(builder.font, FEATURES)

    gpos = builder.font["GPOS"].table
    features = {}
    for record in gpos.FeatureList.FeatureRecord:
        features.setdefault(record.FeatureTag, set()).update(record.Feature.LookupListIndex)
    expected = {"kern": {0, 3, 5}, "mkmk": {1}, "mark": {2, 3, 5}, "abvm": {6}, "calt": {7}}
    assert features == expected, features

    classes = builder.font["GDEF"].table.GlyphClassDef.classDefs
    assert all(classes.get(name) == 3 for name in MARKS), classes

    builder.font.save(OUT)


def shape(font, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)

    glyphs = []
    x = y = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append({"id": info.codepoint, "x": x + position.x_offset, "y": y + position.y_offset})
        x += position.x_advance
        y += position.y_advance
    return glyphs


def write_reference():
    with open(OUT, "rb") as file:
        font = hb.Font(hb.Face(file.read()))

    cases = [{"name": name, "text": text, "glyphs": shape(font, text)} for name, text in TEXTS.items()]
    reference = {"harfbuzz": hb.version_string(), "cases": cases}
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        json.dump(reference, file, indent=2)
        file.write("\n")


def main():
    build()
    write_reference()
    print(os.path.relpath(OUT, ROOT))
    print(os.path.relpath(REFERENCE, ROOT))


if __name__ == "__main__":
    main()
