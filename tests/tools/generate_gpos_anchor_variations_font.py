#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds GposAnchorVariations.ttf, a variable font whose GPOS anchors and value records point to VariationIndex device
tables of every kind that Teko-Variable.ttf and SourceSans3VF-Upright.otf lack (issue #129).

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_gpos_anchor_variations_font.py

generate_gpos_variations_reference.py calls it, so one run of that script rebuilds the font and its HarfBuzz reference.

The two variable test fonts have deltas in mark-to-base, mark-to-mark and pair positioning, but none in mark-to-ligature
or cursive attachment, and their deltas come out as they come out. This font puts a delta of a known size in each place
and holds it at fractional scales, where HarfBuzz and the library round and add differently if they differ at all. It has
the axis wght (100 to 900, default 400), empty glyphs 600 wide, no HVAR, gvar or avar, and a GDEF Item Variation Store of
one region, wght 0 to 1, so that the scalar of a row is (wght - 400) / 500. At wght 650 it is 0.5, and a row of 1, 3 or 5
gives a delta of 0.5, 1.5 or 2.5, which are the cases where rounding the sum and rounding each part differ.

Each kind of anchor gets glyphs that cover every pair of ROWS, so that each delta of one anchor meets each delta of the
other. A row is the number of design units that the delta has at the peak, and 0 is an anchor with no device table:

* `single`: S0 to S6 each have a SinglePos value record with an x placement, a y placement and an x advance, each with a
  device table.
* `mark`: B0 to B6 are bases and M0 to M6 are marks. A base has an anchor with deltas for the class TOP, and a mark has one too.
* `markmark`: N0 to N6 are marks that A, a base, carries. M0 to M6 attach to them with a MarkMarkPos anchor.
* `ligature`: L0 to L6 are ligatures of X and Y0 to Y6, and each of its two components has anchors with deltas. M0 to M6
  come after the ligature and attach to its last component.
* `ligature0`: the same ligatures, but the mark comes between X and Y, so that it belongs to the first component.
* `cursive`: C0 to C6 have an entry and an exit anchor with deltas, and every pair of them is a text.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import harfbuzz_reference as ref
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib import newTable
from fontTools.ttLib.tables import otTables as ot
from fontTools.ttLib.tables._g_l_y_f import Glyph
from fontTools.varLib import builder as var_builder
from fontTools.varLib.varStore import OnlineVarStoreBuilder

PATH = os.path.join(ref.FONTS, "Variations", "GposAnchorVariations.ttf")

# The delta of the device table at the peak, in design units. 0 gives an anchor or value with no device table.
ROWS = [0, 1, -1, 3, -3, 5, -5]
COUNT = len(ROWS)

AXES = [("wght", 100, 400, 900, "Weight")]

# The glyph names that the sections below make, in the order of the glyph IDs.
SINGLES = ["S%d" % i for i in range(COUNT)]
BASES = ["B%d" % i for i in range(COUNT)]
MARKS = ["M%d" % i for i in range(COUNT)]
CARRIED = ["N%d" % i for i in range(COUNT)]
LIGATURES = ["L%d" % i for i in range(COUNT)]
SECONDS = ["Y%d" % i for i in range(COUNT)]
CURSIVES = ["C%d" % i for i in range(COUNT)]
GLYPHS = [".notdef", "A", "X"] + SINGLES + BASES + MARKS + CARRIED + LIGATURES + SECONDS + CURSIVES
CODE_POINT = {name: 0xE000 + i for i, name in enumerate(GLYPHS[1:])}


def row(index, shift):
    """Gives the row that a glyph of an index uses for one of its coordinates, so that the four coordinates of an anchor
    differ and every row meets every other."""
    return ROWS[(index + shift) % COUNT]


def text(*names):
    return "".join(chr(CODE_POINT[name]) for name in names)


def texts():
    """Gives the texts of each kind that the reference holds."""
    pairs = [(i, j) for i in range(COUNT) for j in range(COUNT)]
    return {
        "single": [text(SINGLES[i]) for i in range(COUNT)],
        "mark": [text(BASES[i], MARKS[j]) for i, j in pairs],
        "markmark": [text("A", CARRIED[i], MARKS[j]) for i, j in pairs],
        "ligature": [text("X", SECONDS[i], MARKS[j]) for i, j in pairs],
        "ligature0": [text("X", MARKS[j], SECONDS[i]) for i, j in pairs],
        "cursive": [text(CURSIVES[i], CURSIVES[j]) for i, j in pairs],
    }


def features():
    lines = ["languagesystem DFLT dflt;", "languagesystem latn dflt;", ""]
    lines.append("table GDEF {")
    lines.append("    GlyphClassDef [A X %s %s %s], [%s], [%s %s], ;" % (" ".join(SINGLES), " ".join(BASES), " ".join(SECONDS + CURSIVES), " ".join(LIGATURES), " ".join(MARKS), " ".join(CARRIED)))
    lines.append("} GDEF;")
    lines.append("")
    for i, mark in enumerate(MARKS):
        lines.append("markClass %s <anchor 0 %d> @TOP;" % (mark, 500 + 10 * i))
    for mark in CARRIED:
        lines.append("markClass %s <anchor 0 0> @CARRIED;" % mark)
    lines.append("")

    lines.append("lookup LIGA {")
    lines.append("    lookupflag IgnoreMarks;")
    for second, ligature in zip(SECONDS, LIGATURES):
        lines.append("    sub X %s by %s;" % (second, ligature))
    lines.append("} LIGA;")
    lines.append("feature liga { lookup LIGA; } liga;")
    lines.append("")

    lines.append("feature mark {")
    lines.append("    pos base A <anchor 300 700> mark @CARRIED;")
    for i, base in enumerate(BASES):
        lines.append("    pos base %s <anchor %d %d> mark @TOP;" % (base, 300 + 10 * i, 700 + 10 * i))
    for i, ligature in enumerate(LIGATURES):
        lines.append("    pos ligature %s <anchor %d 700> mark @TOP ligComponent <anchor %d 710> mark @TOP;" % (ligature, 100 + 10 * i, 400 + 10 * i))
    lines.append("} mark;")
    lines.append("")

    lines.append("feature mkmk {")
    for i, carried in enumerate(CARRIED):
        lines.append("    pos mark %s <anchor %d %d> mark @TOP;" % (carried, 20 + 10 * i, 120 + 10 * i))
    lines.append("} mkmk;")
    lines.append("")

    lines.append("feature curs {")
    for i, cursive in enumerate(CURSIVES):
        lines.append("    pos cursive %s <anchor %d %d> <anchor %d %d>;" % (cursive, 10 * i, 50 + 10 * i, 560 + 10 * i, 100 + 10 * i))
    lines.append("} curs;")
    lines.append("")

    lines.append("feature kern {")
    for i, single in enumerate(SINGLES):
        lines.append("    pos %s <%d %d %d 0>;" % (single, 10 + i, 20 + i, 30 + i))
    lines.append("} kern;")
    return "\n".join(lines) + "\n"


class Devices:
    """Hands out the VariationIndex device tables of the rows, which the Item Variation Store of the font holds."""

    def __init__(self, font):
        builder = OnlineVarStoreBuilder([axis[0] for axis in AXES])
        builder.setSupports([{"wght": (0.0, 1.0, 1.0)}])
        self.index = {value: builder.storeDeltas([value]) for value in ROWS if value != 0}
        gdef = font["GDEF"].table
        gdef.Version = 0x00010003
        gdef.VarStore = builder.finish()
        for field in ("AttachList", "LigCaretList", "MarkAttachClassDef", "MarkGlyphSetsDef"):
            if not hasattr(gdef, field):
                setattr(gdef, field, None)

    def device(self, value):
        return None if value == 0 else var_builder.buildVarDevTable(self.index[value])

    def anchor(self, anchor, x_row, y_row):
        """Gives the anchor a format 3 version with the devices of the rows, or the anchor when both rows are 0."""
        if x_row == 0 and y_row == 0:
            return anchor
        varied = ot.Anchor()
        varied.Format = 3
        varied.XCoordinate = anchor.XCoordinate
        varied.YCoordinate = anchor.YCoordinate
        varied.XDeviceTable = self.device(x_row)
        varied.YDeviceTable = self.device(y_row)
        return varied


def index_of(name):
    return int(name[1:])


def anchor_rows(name, component=0):
    """Gives the rows of the x and y coordinates of the anchor of a glyph."""
    index = index_of(name) + component
    return row(index, 0), row(index, 3)


def vary_anchors(font, devices):
    gpos = font["GPOS"].table
    for lookup in gpos.LookupList.Lookup:
        for subtable in lookup.SubTable:
            kind = lookup.LookupType
            if kind == 9:
                kind = subtable.ExtensionLookupType
                subtable = subtable.ExtSubTable
            if kind == 1:
                vary_values(subtable, devices)
            elif kind == 3:
                for glyph, record in zip(subtable.Coverage.glyphs, subtable.EntryExitRecord):
                    record.EntryAnchor = devices.anchor(record.EntryAnchor, row(index_of(glyph), 0), row(index_of(glyph), 3))
                    record.ExitAnchor = devices.anchor(record.ExitAnchor, row(index_of(glyph), 4), row(index_of(glyph), 6))
            elif kind in (4, 5, 6):
                for glyph, record in zip(mark_coverage(subtable, kind), subtable.MarkArray.MarkRecord if kind != 6 else subtable.Mark1Array.MarkRecord):
                    record.MarkAnchor = devices.anchor(record.MarkAnchor, *anchor_rows(glyph))
                if kind == 4:
                    for glyph, record in zip(subtable.BaseCoverage.glyphs, subtable.BaseArray.BaseRecord):
                        if glyph != "A":
                            record.BaseAnchor = [devices.anchor(a, *anchor_rows(glyph)) if a is not None else None for a in record.BaseAnchor]
                elif kind == 5:
                    for glyph, attach in zip(subtable.LigatureCoverage.glyphs, subtable.LigatureArray.LigatureAttach):
                        for component, record in enumerate(attach.ComponentRecord):
                            record.LigatureAnchor = [devices.anchor(a, *anchor_rows(glyph, component)) if a is not None else None for a in record.LigatureAnchor]
                else:
                    for glyph, record in zip(subtable.Mark2Coverage.glyphs, subtable.Mark2Array.Mark2Record):
                        record.Mark2Anchor = [devices.anchor(a, *anchor_rows(glyph)) if a is not None else None for a in record.Mark2Anchor]


def mark_coverage(subtable, kind):
    return subtable.Mark1Coverage.glyphs if kind == 6 else subtable.MarkCoverage.glyphs


def vary_values(subtable, devices):
    """Gives each value record of a SinglePos subtable devices for its x placement, y placement and x advance."""
    assert subtable.Format == 2, "the values differ, so that feaLib writes one record for each glyph"
    for glyph, record in zip(subtable.Coverage.glyphs, subtable.Value):
        index = index_of(glyph)
        record.XPlaDevice = devices.device(row(index, 0))
        record.YPlaDevice = devices.device(row(index, 2))
        record.XAdvDevice = devices.device(row(index, 5))
    subtable.ValueFormat |= 0x10 | 0x20 | 0x40


def build():
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap({code: name for name, code in CODE_POINT.items()})
    builder.setupGlyf({name: Glyph() for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (600, 0) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "GposAnchorVariations", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    builder.setupFvar(AXES, [])
    font = builder.font
    addOpenTypeFeaturesFromString(font, features())
    vary_anchors(font, Devices(font))
    return font


def main():
    build().save(PATH)
    print(os.path.relpath(PATH, ref.ROOT))


if __name__ == "__main__":
    main()
