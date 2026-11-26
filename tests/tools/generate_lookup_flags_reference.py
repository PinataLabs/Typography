#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds LookupFlags.ttf and writes LookupFlags.harfbuzz.json, how HarfBuzz applies lookups at the glyphs that their
flags skip, nested lookups, and cursive chains.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_lookup_flags_reference.py

Bases are empty glyphs 600 wide and marks are empty glyphs with no advance. Each case has glyphs of its own:

* `ChainFormat1` to `ChainFormat3` (#118): a GPOS chained context lookup of each format, with IgnoreMarks, matches
  "a b" and applies MOVE, which raises b by 100, at sequence index 1. The mark m between a and b is skipped, so b moves.
* `MarkToMarkPastFilteredMark` (#119): a mark-to-mark lookup with the mark filtering set [k1 k2] attaches k2 to k1
  past k3, which is outside the set. k3 has a mark2 anchor of its own.
* `SubstitutionAfterLeadingIgnoredMark` and `PositioningAfterLeadingIgnoredMark` (#120): a lookup with IgnoreMarks
  covers the mark that starts the text, and does not apply to it.
* `ReverseFromContext` (#121): a chained context substitution calls a reverse chaining lookup, which does not apply.
* `Cursive*` (#139): chains of three cursive glyphs, in a lookup with and without the RightToLeft flag, in left to
  right Latin text and right to left Hebrew text. The exit anchor is 100 above the entry anchor.

The reference holds, for each case, its name, its text and the glyph ID and place of each glyph in font units from
the start of the line, as HarfBuzz gives them. LookupFlagsTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import harfbuzz_reference as ref
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.otlLib.builder import ChainContextualBuilder
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables._g_l_y_f import Glyph

OUT = os.path.join(ref.FONTS, "LookupFlags.ttf")
REFERENCE = os.path.join(ref.FONTS, "LookupFlags.harfbuzz.json")

BASES = ["a1", "b1", "a2", "b2", "a3", "b3", "base", "x", "s", "t", "c", "r"]
MARKS = ["m", "k1", "k2", "k3", "n", "p"]
RTL = {"e": 0x05D0, "er": 0x05D1}
UNMAPPED = ["n.alt", "s.alt"]
GLYPHS = [".notdef"] + BASES + MARKS + list(RTL) + UNMAPPED
CMAP = {name: 0xE000 + i for i, name in enumerate(BASES + MARKS)} | RTL

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem hebr dflt;

table GDEF {
    GlyphClassDef [%s e er s.alt], , [%s n.alt], ;
} GDEF;

lookup MOVE {
    pos [b1 b2 b3] <0 100 0 0>;
} MOVE;

feature kern {
    lookup CHAIN1 {
        lookupflag IgnoreMarks;
        pos a1' b1' lookup MOVE;
    } CHAIN1;

    lookup CHAIN2 {
        lookupflag IgnoreMarks;
        pos a2' b2' lookup MOVE;
    } CHAIN2;

    lookup CHAIN3 {
        lookupflag IgnoreMarks;
        pos a3' b3' lookup MOVE;
    } CHAIN3;

    lookup LEADING_MARK {
        lookupflag IgnoreMarks;
        pos [p x] <0 50 0 0>;
    } LEADING_MARK;
} kern;

markClass k2 <anchor 0 0> @MKMK;

feature mkmk {
    lookup FILTERED {
        lookupflag UseMarkFilteringSet [k1 k2];
        pos mark k1 <anchor 0 300> mark @MKMK;
        pos mark k3 <anchor 0 500> mark @MKMK;
    } FILTERED;
} mkmk;

lookup REVERSE {
    rsub s' by s.alt;
} REVERSE;

feature liga {
    lookup LEADING_MARK_SUB {
        lookupflag IgnoreMarks;
        sub n by n.alt;
    } LEADING_MARK_SUB;

    lookup CALLS_REVERSE {
        sub s' lookup REVERSE t;
    } CALLS_REVERSE;
} liga;

feature curs {
    lookup CURSIVE {
        pos cursive c <anchor 0 0> <anchor 600 100>;
        pos cursive e <anchor 600 0> <anchor 0 100>;
    } CURSIVE;

    lookup CURSIVE_RTL_FLAG {
        lookupflag RightToLeft;
        pos cursive r <anchor 0 0> <anchor 600 100>;
        pos cursive er <anchor 600 0> <anchor 0 100>;
    } CURSIVE_RTL_FLAG;
} curs;
""" % (" ".join(BASES), " ".join(MARKS))

# The chained context lookup whose first input glyph is the key is built in the subtable format of the value.
CHAIN_FORMAT = {"a1": 1, "a2": 2, "a3": 3}

CASES = {
    "ChainFormat1": ["a1", "m", "b1"],
    "ChainFormat2": ["a2", "m", "b2"],
    "ChainFormat3": ["a3", "m", "b3"],
    "MarkToMarkPastFilteredMark": ["base", "k1", "k3", "k2"],
    "SubstitutionAfterLeadingIgnoredMark": ["n", "x"],
    "PositioningAfterLeadingIgnoredMark": ["p", "x"],
    "ReverseFromContext": ["s", "t"],
    "Cursive": ["c", "c", "c"],
    "CursiveRightToLeftFlag": ["r", "r", "r"],
    "CursiveRightToLeftText": ["e", "e", "e"],
    "CursiveRightToLeftTextAndFlag": ["er", "er", "er"],
}


def first_input_glyph(subtable):
    coverage = subtable.InputCoverage[0] if subtable.Format == 3 else subtable.Coverage
    return coverage.glyphs[0]


def build():
    """Builds the font. fontTools picks the smallest format of a chained context subtable, so the size it compares
    is made to favor the format that CHAIN_FORMAT names."""
    compiled_size = ChainContextualBuilder.getCompiledSize_

    def sized_by_format(self, subtables):
        wanted = CHAIN_FORMAT.get(first_input_glyph(subtables[0]))
        if wanted is None:
            return compiled_size(self, subtables)
        return 0 if subtables[0].Format == wanted else 1 << 20

    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap({code: name for name, code in CMAP.items()})
    builder.setupGlyf({name: Glyph() for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (0 if name in MARKS or name == "n.alt" else 600, 0) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "LookupFlags", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    ChainContextualBuilder.getCompiledSize_ = sized_by_format
    try:
        addOpenTypeFeaturesFromString(builder.font, FEATURES)
    finally:
        ChainContextualBuilder.getCompiledSize_ = compiled_size
    builder.font.save(OUT)

    font = TTFont(OUT)
    chains = [lookup for lookup in font["GPOS"].table.LookupList.Lookup if lookup.LookupType == 8]
    formats = {first_input_glyph(lookup.SubTable[0]): lookup.SubTable[0].Format for lookup in chains}
    assert formats == CHAIN_FORMAT, formats
    gsub_types = [lookup.LookupType for lookup in font["GSUB"].table.LookupList.Lookup]
    assert gsub_types == [8, 1, 6], gsub_types


def main():
    build()
    hb_font = ref.load_font(OUT)
    rows = []
    for name, glyphs in CASES.items():
        text = "".join(chr(CMAP[glyph]) for glyph in glyphs)
        rows.append([name, text, ref.shape_line(hb_font, text)[0]])
    ref.write_reference(REFERENCE, rows)
    print(os.path.relpath(OUT, ref.ROOT))


if __name__ == "__main__":
    main()
