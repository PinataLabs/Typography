#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds ChainBacktrack.ttf and writes ChainBacktrack.harfbuzz.json, how HarfBuzz matches a backtrack of two glyphs.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_chain_backtrack_reference.py

A chained context or reverse chaining subtable lists its backtrack glyphs from the glyph nearest to the input to the
farthest one. The `calt` feature of the font has one lookup of each kind, each with the backtrack "a b" and its own
input glyph:

* p: a chained context subtable of format 1 (glyphs).
* q: a chained context subtable of format 2 (classes), with every pair of the classes [a c], [b d] and [g h] except
  "[b d] [a c]".
* r: a chained context subtable of format 3 (coverages).
* s: a reverse chaining subtable.

Each input glyph is replaced with its alternate after "a b" and stays after "b a". A last reverse chaining subtable
covers t and u but has a substitute only for t, so HarfBuzz leaves u as it is.

The reference holds the glyph IDs that HarfBuzz gives for each text. ChainBacktrackTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
OUT = os.path.join(FONTS, "ChainBacktrack.ttf")
REFERENCE = os.path.join(FONTS, "ChainBacktrack.harfbuzz.json")

LETTERS = list("abcdghpqrstu")
GLYPHS = [".notdef"] + LETTERS + ["p.alt", "q.alt", "r.alt", "s.alt", "t.alt", "u.alt"]

CLASSES = ["[a c]", "[b d]", "[g h]"]
FORMAT2_RULES = "\n".join(
    "        sub %s %s q' lookup ALTERNATE;" % (far, near)
    for far in CLASSES
    for near in CLASSES
    if (far, near) != ("[b d]", "[a c]"))

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;

lookup ALTERNATE {
    sub p by p.alt;
    sub q by q.alt;
    sub r by r.alt;
} ALTERNATE;

feature calt {
    lookup FORMAT1 {
        sub a b p' lookup ALTERNATE;
    } FORMAT1;

    lookup FORMAT2 {
%s
    } FORMAT2;

    lookup FORMAT3 {
        sub [a c] [b d] r' lookup ALTERNATE;
    } FORMAT3;

    lookup REVERSE {
        rsub a b s' by s.alt;
    } REVERSE;

    lookup SHORT_REVERSE {
        rsub [t u]' by [t.alt u.alt];
    } SHORT_REVERSE;
} calt;
""" % FORMAT2_RULES

TEXTS = ["abp", "bap", "abq", "baq", "abr", "bar", "abs", "bas", "t", "u", "tu"]


def build():
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap({ord(name): name for name in LETTERS})
    builder.setupGlyf({name: Glyph() for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (600, 0) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "ChainBacktrack", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    addOpenTypeFeaturesFromString(builder.font, FEATURES)

    # Drop the substitute of u, the second glyph of the coverage.
    short_reverse = builder.font["GSUB"].table.LookupList.Lookup[-1].SubTable[0]
    short_reverse.Substitute = short_reverse.Substitute[:1]
    builder.font.save(OUT)

    # Check that each lookup after ALTERNATE has the subtable format that the font is built to test.
    lookups = TTFont(OUT)["GSUB"].table.LookupList.Lookup
    formats = [(lookup.LookupType, [subtable.Format for subtable in lookup.SubTable]) for lookup in lookups[1:]]
    assert formats == [(6, [1]), (6, [2]), (6, [3]), (8, [1]), (8, [1])], formats
    assert len(lookups[-1].SubTable[0].Substitute) == 1


def shape(font, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    return [info.codepoint for info in buffer.glyph_infos]


def main():
    build()
    with open(OUT, "rb") as file:
        font = hb.Font(hb.Face(file.read()))

    rows = [[text, shape(font, text)] for text in TEXTS]
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(OUT, ROOT))
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
