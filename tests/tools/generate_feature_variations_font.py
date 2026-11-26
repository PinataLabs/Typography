#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds FeatureVariations.ttf, a variable font with GSUB and GPOS FeatureVariations, and writes
FeatureVariations.harfbuzz.json, the HarfBuzz reference for it (issue #131).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_feature_variations_font.py

Neither Teko-Variable.ttf nor SourceSans3VF-Upright.otf has a FeatureVariations table, so this font stands in. It has
the axes wght (100 to 900, default 400) and wdth (75 to 125, default 100) and empty glyphs, so that the position and the
advance of each glyph come from its glyph ID and its lookups alone. It has no HVAR, gvar or avar.

GSUB has `rvrn`, which fontTools varLib.featureVars.addFeatureVariations adds. Its regions are in normalised
coordinates, and fontTools splits them into boxes that do not overlap:

* wght 0.5 to 1 substitutes C.rvrn for C.
* wdth 0.5 to 1 substitutes B.rvrn for B.

The glyphs that rvrn substitutes are not the ones that GPOS kerns, so that a substitution does not hide a kerning pair.

GPOS has the feature `kern` with a lookup that kerns A V by -100 and V A by -40. Its FeatureVariations records, which the
script builds with addFeatureVariationsRaw and edits, come in this order, and the first one that holds decides:

1. wght 0.5 to 1: the feature takes the lookup that kerns A V by -300 and V A by -150 in place of its own.
2. wght 0.2 to 1 and wdth 0.5 to 1, two conditions in one set: the feature takes its own lookup and then the lookup
   that kerns A V by -500, so A V is kerned by -600.
3. wght -1 to -0.5: the feature takes the lookup that kerns A V by -20 and V A by -10.

Four more records use the condition formats of OpenType 1.9. Each also needs wght -0.25 to 0.125, which the first three
records do not cover, and each takes a lookup that kerns A V by a different amount in place of the feature's own:

4. Format 2, a value that the GDEF Item Variation Store varies: -50 plus 100 times the normalised wdth, when it is
   above 0. HarfBuzz truncates the sum, so this holds from wdth 0.51 on. The store has one region, wdth 0 to 1, and one
   row. A V is kerned by -410.
5. Format 3, AND, of wdth 0.25 to 1 and wght -0.25 to 0.125: -420.
6. Format 4, OR, of wdth -1 to -0.75 and wdth -0.25 to 0.125: -430.
7. Format 5, NOT, of wdth -0.5 to 0.5: -440.

Every boundary is a power of 2 fraction, which has one F2Dot14 value. HarfBuzz and fontTools round a value such as 0.1
differently in the last bit, and a boundary there would test their rounding and not the table.

The reference holds the glyph IDs, positions and advance that HarfBuzz gives each text of TEXTS at each location of
LOCATIONS, and each text of FORMAT_TEXTS at each location of FORMAT_LOCATIONS. The locations are on both sides of the
boundary of each condition, and at the boundary, where a range includes its ends. Each row is a text, the user wght and wdth, the glyph ID, x of the origin and y offset of
each glyph, and the advance of the text, in font units.

The output is deterministic for one fontTools and HarfBuzz version. The reference records the HarfBuzz version.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import harfbuzz_reference as ref
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib import newTable
from fontTools.ttLib.tables import otTables as ot
from fontTools.ttLib.tables._g_l_y_f import Glyph
from fontTools.varLib.featureVars import addFeatureVariations, addFeatureVariationsRaw, buildConditionTable
from fontTools.varLib.varStore import OnlineVarStoreBuilder

OUT = os.path.join(ref.FONTS, "Variations")
FONT = os.path.join(OUT, "FeatureVariations.ttf")
REFERENCE = os.path.join(OUT, "FeatureVariations.harfbuzz.json")

GLYPHS = [".notdef", "space", "A", "B", "C", "V", "B.rvrn", "C.rvrn"]
CMAP = {0x20: "space", 0x41: "A", 0x42: "B", 0x43: "C", 0x56: "V"}
ADVANCES = {".notdef": 500, "space": 250, "A": 600, "B": 620, "C": 640, "V": 660, "B.rvrn": 720, "C.rvrn": 740}

# The axes wght and wdth: tag, minimum, default, maximum and name.
AXES = [("wght", 100, 400, 900, "Weight"), ("wdth", 75, 100, 125, "Width")]

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;

lookup KernHeavy {
    pos A V -300;
    pos V A -150;
} KernHeavy;

lookup KernWide {
    pos A V -500;
} KernWide;

lookup KernLight {
    pos A V -20;
    pos V A -10;
} KernLight;

lookup KernValue {
    pos A V -410;
} KernValue;

lookup KernAnd {
    pos A V -420;
} KernAnd;

lookup KernOr {
    pos A V -430;
} KernOr;

lookup KernNot {
    pos A V -440;
} KernNot;

feature kern {
    pos A V -100;
    pos V A -40;
} kern;
"""

# The texts show the glyphs that rvrn substitutes, the pair that the kern lookups move, and both.
TEXTS = ["A", "B", "C", "BC", "AV", "VA", "AVA", "BAV", "A V", "VAB"]

# The normalised wght is (w - 400) / 500 above 400 and (w - 400) / 300 below it. The normalised wdth is (w - 100) / 25.
# The boundaries are wght 650 (0.5), 500 (0.2) and 250 (-0.5), and wdth 112.5 (0.5). Each has a location on both sides,
# and one on it.
WEIGHTS = [100, 200, 249, 250, 251, 400, 450, 499, 500, 501, 600, 649, 650, 651, 700, 900]
WIDTHS = [75, 100, 112, 112.5, 113, 125]
LOCATIONS = [(wght, wdth) for wght in WEIGHTS for wdth in WIDTHS]

# The conditions of the records 4 to 7 need wght -0.25 to 0.125, which is 325 to 462.5, and they split the range of wdth. The
# boundaries are wdth 81.25 (-0.75), 87.5 (-0.5), 93.75 (-0.25), 103.125 (0.125), 106.25 (0.25) and 112.5 (0.5).
# The value condition holds above wdth 0.5, and 113 is 0.52.
FORMAT_TEXTS = ["AV", "VA"]
FORMAT_WEIGHTS = [324, 325, 400, 462.5, 463]
FORMAT_WIDTHS = [75, 81, 81.25, 82, 87, 87.5, 88, 93, 93.75, 94, 100, 103, 103.125, 104, 106, 106.25, 107, 112, 112.5, 113, 125]
FORMAT_LOCATIONS = [(wght, wdth) for wght in FORMAT_WEIGHTS for wdth in FORMAT_WIDTHS]


def kern_lookup(gpos, first, second, advance):
    """Gives the index of the only PairPos lookup that gives the pair an advance."""
    found = []
    for index, lookup in enumerate(gpos.LookupList.Lookup):
        for subtable in lookup.SubTable:
            if first not in subtable.Coverage.glyphs:
                continue
            pair_set = subtable.PairSet[subtable.Coverage.glyphs.index(first)]
            if any(r.SecondGlyph == second and r.Value1.XAdvance == advance for r in pair_set.PairValueRecord):
                found.append(index)
    assert len(found) == 1, (first, second, advance, found)
    return found[0]


def build():
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    builder.setupGlyf({name: Glyph() for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (ADVANCES[name], 0) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "FeatureVariations", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    builder.setupFvar(AXES, [])
    font = builder.font
    addOpenTypeFeaturesFromString(font, FEATURES)

    gpos = font["GPOS"].table
    lookups = [kern_lookup(gpos, "A", "V", advance) for advance in (-300, -500, -20, -410, -420, -430, -440)]
    heavy, wide, light, value, both, either, neither = lookups

    # The conditions of the last four records are the format 1 conditions that the placeholders give, which the script
    # replaces below. Each placeholder is different, so that fontTools does not merge the records.
    addFeatureVariationsRaw(
        font,
        gpos,
        [
            ({"wght": (0.5, 1.0)}, [heavy]),
            ({"wght": (0.2, 1.0), "wdth": (0.5, 1.0)}, [wide]),
            ({"wght": (-1.0, -0.5)}, [light]),
            ({"wght": (-0.25, 0.125), "wdth": (0.1, 0.2)}, [value]),
            ({"wght": (-0.25, 0.125), "wdth": (0.2, 0.3)}, [both]),
            ({"wght": (-0.25, 0.125), "wdth": (0.3, 0.4)}, [either]),
            ({"wght": (-0.25, 0.125), "wdth": (0.4, 0.5)}, [neither]),
        ],
        "kern",
    )

    # addFeatureVariationsRaw appends the lookup to those of the feature. The records swap it in place of them, but the second.
    records = gpos.FeatureVariations.FeatureVariationRecord
    for index, record in enumerate(records):
        if index != 1:
            substitution = only(record.FeatureTableSubstitution.SubstitutionRecord)
            substitution.Feature.LookupListIndex = [lookups[index]]
            substitution.Feature.LookupCount = 1

    weight = axis_range(0, -0.25, 0.125)
    value_condition = ot.ConditionTable()
    value_condition.Format = 2
    value_condition.DefaultValue = -50
    value_condition.VarIdx = add_store(font)
    replace_conditions(records[3], [weight, value_condition])
    replace_conditions(records[4], [condition_list(3, [axis_range(1, 0.25, 1.0), axis_range(0, -0.25, 0.125)])])
    replace_conditions(records[5], [weight, condition_list(4, [axis_range(1, -1.0, -0.75), axis_range(1, -0.25, 0.125)])])
    replace_conditions(records[6], [weight, negation(axis_range(1, -0.5, 0.5))])

    addFeatureVariations(
        font,
        [
            ([{"wght": (0.5, 1.0)}], {"C": "C.rvrn"}),
            ([{"wdth": (0.5, 1.0)}], {"B": "B.rvrn"}),
        ],
    )
    return font


def only(items):
    assert len(items) == 1, items
    return items[0]


def axis_range(axis, minimum, maximum):
    return buildConditionTable(axis, minimum, maximum)


def condition_list(format, conditions):
    """Gives an AND (format 3) or an OR (format 4) of conditions."""
    condition = ot.ConditionTable()
    condition.Format = format
    condition.ConditionTable = conditions
    condition.ConditionCount = len(conditions)
    return condition


def negation(condition):
    negated = ot.ConditionTable()
    negated.Format = 5
    negated.ConditionTable = condition
    return negated


def replace_conditions(record, conditions):
    record.ConditionSet.ConditionTable = conditions
    record.ConditionSet.ConditionCount = len(conditions)


def add_store(font):
    """Gives the font a GDEF table of version 1.3 with an Item Variation Store of one region, wdth 0 to 1, and one row
    whose delta is 100 at its peak, and gives the index of the row."""
    builder = OnlineVarStoreBuilder([axis[0] for axis in AXES])
    builder.setSupports([{"wdth": (0.0, 1.0, 1.0)}])
    index = builder.storeDeltas([100])
    gdef = newTable("GDEF")
    gdef.table = ot.GDEF()
    gdef.table.Version = 0x00010003
    for field in ("GlyphClassDef", "AttachList", "LigCaretList", "MarkAttachClassDef", "MarkGlyphSetsDef"):
        setattr(gdef.table, field, None)
    gdef.table.VarStore = builder.finish()
    font["GDEF"] = gdef
    return index


def shape(path):
    rows = []
    for texts, locations in ((TEXTS, LOCATIONS), (FORMAT_TEXTS, FORMAT_LOCATIONS)):
        for wght, wdth in locations:
            font = ref.load_font(path)
            font.set_variations({"wght": wght, "wdth": wdth})
            for text in texts:
                glyphs, advance = ref.shape_line(font, text)
                rows.append([text, wght, wdth, glyphs, advance])
    return rows


def main():
    font = build()
    font.save(FONT)
    print(os.path.relpath(FONT, ref.ROOT))
    ref.write_reference(REFERENCE, shape(FONT), texts=TEXTS + FORMAT_TEXTS)


if __name__ == "__main__":
    main()
