#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes MongolianLookups.ttf and MongolianLookups.harfbuzz.json, how HarfBuzz shapes the GSUB lookups that Mongolian
Baiti uses (issue #111).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_mongolian_lookups_reference.py

Mongolian Baiti cannot ship in the repository, so the font copies the four things in it that HarfBuzz shapes and that
the library shaped differently. Each row of the reference names one:

* joining: ArabicShaping.txt gives the Mongolian vowel separator U+180E and the Arabic number sign U+0600 joining
  type U, so the letters on either side of them take their isolated forms.
* context-match: a chaining subtable that matches and has no substitution records ends its lookup, as an `ignore sub`
  rule in its own subtable does. The `rclt` lookup keeps GA final after A initial and substitutes it elsewhere.
* mark-filtering-set: a lookup with the UseMarkFilteringSet flag skips each mark that is not in its GDEF mark glyph
  set and stops at each mark that is.
* reverse-chaining: a reverse chaining lookup runs from the last glyph to the first, skips the glyphs that its flags
  ignore, and substitutes a glyph with the substitute at its Coverage index. The second rule matches only the glyph
  that the first rule substituted.

The reference holds the glyph ID and the place of each glyph that HarfBuzz gives, in font units from the start of the
line, and the advance of the text. MongolianLookupsTests reads it.
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
FONT = os.path.join(FONTS, "MongolianLookups.ttf")
REFERENCE = os.path.join(FONTS, "MongolianLookups.harfbuzz.json")

A, GA, MVS, DAGALGA, BEH, NUMBER_SIGN = 0x1820, 0x182D, 0x180E, 0x18A9, 0x0628, 0x0600
CMAP = {0x20: "space", A: "a", GA: "ga", MVS: "mvs", DAGALGA: "dagalga", BEH: "beh", NUMBER_SIGN: "numbersign"}
FORMS = ["isol", "init", "medi", "fina"]
LETTERS = ["a", "ga", "beh"]
MARKS = ["dagalga", "filtered"]
GLYPHS = (
    [".notdef", "space", "mvs", "numbersign"]
    + [f"{letter}.{form}" for letter in LETTERS for form in FORMS]
    + ["a", "ga", "beh", "a.rev", "ga.rev", "ga.alt"]
    + MARKS
)

FEATURES = """
languagesystem DFLT dflt;
languagesystem arab dflt;
languagesystem mong dflt;

table GDEF {
  GlyphClassDef [%(bases)s], , [dagalga filtered], ;
} GDEF;

%(forms)s

@NOT_DAGALGA = [filtered];
@DAGALGA = [dagalga];

feature rclt {
  lookup contextMatch {
    ignore sub a.init ga.fina';
    subtable;
    sub ga.fina' by ga.alt;
  } contextMatch;

  lookup markFilteringSet {
    lookupflag UseMarkFilteringSet @NOT_DAGALGA;
    sub ga.init' a.fina by ga.alt;
  } markFilteringSet;

  lookup markInFilteringSet {
    lookupflag UseMarkFilteringSet @DAGALGA;
    sub a.init' a.fina by a.rev;
  } markInFilteringSet;

  lookup reverseChaining {
    lookupflag IgnoreMarks;
    rsub [a.medi ga.medi]' a.fina by [a.rev ga.rev];
    rsub a.medi' ga.rev by a.rev;
  } reverseChaining;
} rclt;
""" % {
    "bases": " ".join(g for g in GLYPHS[1:] if g not in MARKS),
    "forms": "\n".join(
        "feature %s { %s } %s;" % (form, " ".join(f"sub {letter} by {letter}.{form};" for letter in LETTERS), form)
        for form in FORMS
    ),
}

# (cause, text)
TEXTS = [
    ("joining", [A, MVS, A]),
    ("joining", [A, A, MVS, A]),
    ("joining", [BEH, NUMBER_SIGN, BEH]),
    ("context-match", [A, GA]),
    ("context-match", [GA, GA]),
    ("mark-filtering-set", [GA, DAGALGA, A]),
    ("mark-filtering-set", [A, DAGALGA, A]),
    ("reverse-chaining", [A, A, A]),
    ("reverse-chaining", [A, GA, A]),
    ("reverse-chaining", [A, A, GA, A]),
    ("reverse-chaining", [A, A, GA, DAGALGA, A]),
]


def build():
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    builder.setupGlyf({name: Glyph() for name in GLYPHS})

    # Every base glyph has its own advance, so that a different glyph moves the glyphs after it. Marks have none.
    builder.setupHorizontalMetrics({name: (0 if name in MARKS else 300 + 10 * i, 0) for i, name in enumerate(GLYPHS)})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "MongolianLookups", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    addOpenTypeFeaturesFromString(builder.font, FEATURES)
    builder.font.save(FONT)


def shape(text):
    """Gives the glyph ID and place of each glyph, in font units from the start of the line, and the advance."""
    with open(FONT, "rb") as file:
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
    build()
    rows = []
    for cause, text in TEXTS:
        text = "".join(map(chr, text))
        rows.append([cause, text, *shape(text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
