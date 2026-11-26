#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds the fonts of the metrics variation tests and writes MetricsVariations.fonttools.json, the fontTools reference
for the advances, side bearings and global metrics of variation instances (issue #128).

Usage (from the repository root, fontTools 4.68 or later, and uharfbuzz for the cross-check):

    python tests/tools/generate_metrics_variation_reference.py

Neither test font has HVAR side bearing mappings, VVAR, or an MVAR record for anything but the strikeout position and
the heights, so the script builds three small TrueType fonts that have them all, next to the reference:

* VariationMetrics.ttf has the axes wght and wdth and six glyphs. Its HVAR has an advance width mapping that shares one
  row between glyphs, a left side bearing mapping that leaves a glyph out, and a right side bearing mapping. It has
  vhea, vmtx and a VVAR with the matching mappings, and an MVAR that varies every metric that the library reads: the
  ascender, descender and line gap of the hhea table, the typo and win metrics of OS/2, the vhea metrics, the subscript,
  superscript, strikeout and underline values. The values of hhea, typo and win differ, and its USE_TYPO_METRICS bit is
  clear, so the library takes the hhea values.
* VariationMetricsTypo.ttf is the same font with the USE_TYPO_METRICS bit set, so the library takes the typo values.
* VariationMetricsGvar.ttf is the first font with a gvar table, whose tuples move the points of each glyph and its four phantom
  points (issue #127). The deltas of the phantom points are not those of HVAR and VVAR, so the font tells which of the two the
  library takes. HVAR and VVAR win, as they do in HarfBuzz: the advances, and the left and top side bearings that the
  mappings give, are those of the tables. The reference records them like those of the other fonts, and it records under
  "phantom" what the phantom points give: the advances, the left side bearing (the left edge of the varied outline less the
  first phantom point) and the top side bearing, from fontTools on a copy of the font that has no HVAR and VVAR. The script
  checks the advances of that copy against HarfBuzz. A test that removes or breaks the tables, or the mapping of a bearing,
  expects those values.

The glyphs of the first two fonts have no gvar variation, so their outlines do not change, and only their metrics do.

A CFF2 outline does change, so the script also builds two CFF2 fonts for the vertical origin of a variation instance, and
writes Cff2VerticalOrigins.fonttools.json for them (see build_cff2_vertical):

* VariationCff2Vertical.otf has the axis wght, vhea, vmtx, and a VVAR that varies the advance heights and maps no top side
  bearing. The top of its glyphs moves with wght. The vertical origin stays where the default outline puts it, the top of
  that outline plus the top side bearing of vmtx, so the top side bearing of an instance is that origin minus the top of
  the varied outline. The left side bearing of a varied CFF2 glyph has the same rule, with the origin at x 0.
* VariationCff2VerticalTsb.otf is the same font with a VVAR top side bearing map, which then gives the top side bearing.

fontTools gives the top of a varied outline, which the reference records, and the script derives the top side bearing. HarfBuzz
is no reference for the origin of a CFF2 glyph: it ignores the vmtx bearing of a glyph that is not in glyf and centres the glyph
in its advance height. The script checks the tops and the advance heights against it.

For Teko-Variable.ttf, SourceSans3VF-Upright.otf and the two built fonts, the reference holds, at the default instance, the
axis minimum, the axis maximum, named instances and user locations between them:

* the advance width of each glyph that hmtx lists, and the advance height of each glyph that vmtx lists, which fontTools
  gives with TTFont.getGlyphSet(location=..., normalized=True) and rounds as otRound does. The sum of HVAR delta and
  advance must not be within 0.0001 of a half, so that the single precision of the library cannot round it the other way;
* for the built fonts, the left side bearing and the top side bearing that the mappings give, which no fontTools API gives
  for a glyph: the script adds the delta of VarStoreInstancer to the value of hmtx or vmtx, and rounds;
* the global metrics, which are the value of the table plus the rounded MVAR delta of its tag, as the fontTools
  instancer adds it. fontTools has no function that chooses between hhea, typo and win, so the script follows the
  order of the library and HarfBuzz;
* the rounded MVAR delta of every tag, which the test of the win metrics uses.

The F2Dot14 coordinates that the library normalises to are given to fontTools, so that both compute with the same
coordinates. HarfBuzz must give the same advances and global metrics for them, which the script checks for the
built fonts and for the advances of the shipped fonts, and stops when it does not.

The output is deterministic for one fontTools version. The reference records the version that wrote it.
"""

import io
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import fontTools
import uharfbuzz as hb
from fontTools import varLib
from fontTools.designspaceLib import AxisDescriptor, DesignSpaceDocument, SourceDescriptor
from fontTools.fontBuilder import FontBuilder
from fontTools.misc.fixedTools import fixedToFloat
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.t2CharStringPen import T2CharStringPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable
from fontTools.ttLib.tables import otTables as ot
from fontTools.ttLib.tables.TupleVariation import TupleVariation
from fontTools.varLib import builder
from fontTools.varLib.varStore import VarStoreInstancer

from variation_reference import FONTS, VARIATIONS, get_axis_location, get_locations, ot_round, write_reference

REFERENCE = os.path.join(VARIATIONS, "MetricsVariations.fonttools.json")
VERTICAL_REFERENCE = os.path.join(VARIATIONS, "Cff2VerticalOrigins.fonttools.json")

NO_VARIATION_INDEX = 0xFFFFFFFF

# The test fonts: file under Fonts, the named instances to record and the user locations between them.
SHIPPED_FONTS = {
    "Teko-Variable.ttf": ("Teko/Teko-Variable.ttf", ["Light", "Regular", "Bold"], [{"wght": 350.0}, {"wght": 650.0}]),
    "SourceSans3VF-Upright.otf": (
        "SourceSans3/SourceSans3VF-Upright.otf",
        ["ExtraLight", "Semibold", "Bold", "Black"],
        [{"wght": 350.0}, {"wght": 850.0}],
    ),
}

# The built fonts. The F2Dot14 coordinates of these locations are exact: 650 and 250 of wght are 0.5 and -0.5 of its
# range, 112.5 and 87.5 of wdth are 0.5 and -0.5 of its range.
# For each font: whether it has USE_TYPO_METRICS set, and whether it has a gvar table.
BUILT_FONTS = {
    "VariationMetrics.ttf": (False, False),
    "VariationMetricsTypo.ttf": (True, False),
    "VariationMetricsGvar.ttf": (False, True),
}
BUILT_NAMES = ["Regular", "Bold", "Light", "Condensed", "Extended"]
BUILT_EXTRA = [
    {"wght": 650.0},
    {"wght": 250.0},
    {"wdth": 112.5},
    {"wdth": 87.5},
    {"wght": 650.0, "wdth": 112.5},
    {"wght": 250.0, "wdth": 87.5},
    {"wght": 900.0, "wdth": 125.0},
    {"wght": 100.0, "wdth": 75.0},
]

AXES = [("wght", 100, 400, 900, "Weight"), ("wdth", 75, 100, 125, "Width")]
INSTANCES = [
    ("Regular", {"wght": 400, "wdth": 100}),
    ("Bold", {"wght": 900, "wdth": 100}),
    ("Light", {"wght": 100, "wdth": 100}),
    ("Condensed", {"wght": 400, "wdth": 75}),
    ("Extended", {"wght": 400, "wdth": 125}),
]

GLYPHS = [".notdef", "space", "A", "B", "C", "D"]
CMAP = {0x20: "space", 0x41: "A", 0x42: "B", 0x43: "C", 0x44: "D"}

# For each glyph: advance width, left side bearing, advance height and top side bearing at the default instance.
# The last two glyphs have different advances, so that hmtx lists every glyph.
METRICS = {
    ".notdef": (500, 50, 1000, 100),
    "space": (250, 0, 1000, 0),
    "A": (600, 50, 900, 150),
    "B": (620, 60, 950, 120),
    "C": (640, 40, 1000, 90),
    "D": (700, 30, 1100, 60),
}

# The regions of every store: the wght peak at 1, the wght peak at -1, the wdth peak at 1, the wdth peak at -1, and
# both peaks at 1, where the scalar is the product of the two.
REGIONS = [
    {"wght": (0, 1, 1)},
    {"wght": (-1, -1, 0)},
    {"wdth": (0, 1, 1)},
    {"wdth": (-1, -1, 0)},
    {"wght": (0, 1, 1), "wdth": (0, 1, 1)},
]
AXIS_TAGS = [axis[0] for axis in AXES]

# The gvar tuples of VariationMetricsGvar.ttf, for the peak of wght at 1 and the peak of wdth at 1. Each has the deltas of the four
# points of a box and then of the four phantom points, in the order of the font program: the origin, the end of the advance, the top and
# the bottom of the vertical advance. They are even, so that a half of one is a whole number and no value is a tie. The deltas of
# the phantom points are not those of HVAR and VVAR below, so each metric that both could give differs between the two.
GVAR_WGHT = {
    ".notdef": [(4, 0), (4, 10), (20, 10), (20, 0), (2, 0), (30, 0), (0, 16), (0, -24)],
    "space": [(0, 0), (0, 8), (16, 8), (16, 0), (0, 0), (-10, 0), (0, 20), (0, -40)],
    "A": [(10, 0), (10, 30), (30, 30), (30, 0), (4, 0), (90, 0), (0, 40), (0, -70)],
    "B": [(-8, 0), (-8, 20), (12, 20), (12, 0), (-2, 0), (-50, 0), (0, -30), (0, 60)],
    "C": [(6, -4), (6, 16), (26, 16), (26, -4), (6, 0), (70, 0), (0, 26), (0, -34)],
    "D": [(0, 0), (0, 12), (40, 12), (40, 0), (0, 0), (-20, 0), (0, -14), (0, 80)],
}
GVAR_WDTH = {
    ".notdef": [(-2, 0), (-2, 6), (8, 6), (8, 0), (-4, 0), (16, 0), (0, 10), (0, 12)],
    "space": [(0, 0), (0, 4), (6, 4), (6, 0), (2, 0), (24, 0), (0, -8), (0, 14)],
    "A": [(4, 0), (4, 8), (-6, 8), (-6, 0), (-2, 0), (-36, 0), (0, 18), (0, 22)],
    "B": [(2, -2), (2, 14), (10, 14), (10, -2), (8, 0), (44, 0), (0, 6), (0, -16)],
    "C": [(0, 0), (0, -6), (-12, -6), (-12, 0), (-6, 0), (20, 0), (0, -12), (0, 30)],
    "D": [(6, 2), (6, 22), (22, 22), (22, 2), (4, 0), (-30, 0), (0, 24), (0, -48)],
}

# The rows of the HVAR store. Subtable 0 has the deltas of the four single axis regions, and subtable 1 the delta of
# the region of both axes. A delta of 5 at the scalar 0.5 gives 2.5, which must round up.
HVAR_ROWS = [[40, -30, 0, 0], [10, -10, 5, -5], [-24, 24, 30, -30], [7, -7, 100, -100]]
HVAR_LSB_ROWS = [[12], [-9]]
HVAR_ADVANCE_MAP = {".notdef": (0, 1), "space": (0, 2), "A": (0, 0), "B": (0, 1), "C": (0, 2), "D": (0, 3)}
HVAR_LSB_MAP = {".notdef": (1, 1), "space": None, "A": (1, 0), "B": (1, 1), "C": (1, 0), "D": None}
HVAR_RSB_MAP = {".notdef": (1, 0), "space": (1, 0), "A": (1, 1), "B": (1, 1), "C": (1, 0), "D": (1, 0)}

VVAR_ROWS = [[-30, 20, 6, -6], [16, -16, -9, 9], [50, 0, 0, 25]]
VVAR_ADVANCE_MAP = {".notdef": (0, 0), "space": (0, 1), "A": (0, 2), "B": (0, 0), "C": (0, 1), "D": (0, 2)}
VVAR_TSB_MAP = {".notdef": (0, 1), "space": None, "A": (0, 0), "B": (0, 2), "C": (0, 1), "D": (0, 0)}
VVAR_BSB_MAP = {glyph: (0, 0) for glyph in GLYPHS}

# The MVAR tags that the library reads, and the deltas of the four single axis regions for each.
# hasc, hdsc and hlgp vary whichever of the hhea and the typo values the library takes.
MVAR_ROWS = {
    "hasc": [30, -20, 12, -8],
    "hdsc": [-10, 10, -5, 5],
    "hlgp": [6, -6, 3, -3],
    "hcla": [25, -15, 9, -9],
    "hcld": [14, -14, 4, -4],
    "vasc": [8, -8, 20, -20],
    "vdsc": [-12, 12, -6, 6],
    "vlgp": [2, -2, 1, -1],
    "sbxs": [20, -20, 8, -8],
    "sbys": [-16, 16, 4, -4],
    "sbxo": [10, -10, 3, -3],
    "sbyo": [12, -12, 6, -6],
    "spxs": [18, -18, 7, -7],
    "spys": [-14, 14, 5, -5],
    "spxo": [9, -9, 2, -2],
    "spyo": [22, -22, 11, -11],
    "strs": [6, -6, 2, -2],
    "stro": [35, -35, 15, -15],
    "unds": [4, -4, 1, -1],
    "undo": [-18, 18, -7, 7],
}

def box(left):
    pen = TTGlyphPen(None)
    pen.moveTo((left, 0))
    pen.lineTo((left, 700))
    pen.lineTo((left + 400, 700))
    pen.lineTo((left + 400, 0))
    pen.closePath()
    return pen.glyph()


def build_store(rows_by_data):
    """Builds an Item Variation Store. `rows_by_data` has the (region indexes, rows) of each subtable."""
    regions = builder.buildVarRegionList(REGIONS, AXIS_TAGS)
    return builder.buildVarStore(
        regions,
        [builder.buildVarData(indexes, rows, optimize=False) for indexes, rows in rows_by_data],
    )


def pack(mapping):
    """Gives each glyph its delta set index, or NO_VARIATION_INDEX for a glyph that the mapping leaves out."""
    values = [NO_VARIATION_INDEX if mapping[glyph] is None else (mapping[glyph][0] << 16) | mapping[glyph][1] for glyph in GLYPHS]
    return builder.buildVarIdxMap(values, GLYPHS)


def build_hvar():
    table = newTable("HVAR")
    table.table = ot.HVAR()
    table.table.Version = 0x00010000
    table.table.VarStore = build_store([([0, 1, 2, 3], HVAR_ROWS), ([4], HVAR_LSB_ROWS)])
    table.table.AdvWidthMap = pack(HVAR_ADVANCE_MAP)
    table.table.LsbMap = pack(HVAR_LSB_MAP)
    table.table.RsbMap = pack(HVAR_RSB_MAP)
    return table


def build_vvar():
    table = newTable("VVAR")
    table.table = ot.VVAR()
    table.table.Version = 0x00010000
    table.table.VarStore = build_store([([0, 1, 2, 3], VVAR_ROWS)])
    table.table.AdvHeightMap = pack(VVAR_ADVANCE_MAP)
    table.table.TsbMap = pack(VVAR_TSB_MAP)
    table.table.BsbMap = pack(VVAR_BSB_MAP)
    table.table.VOrgMap = None
    return table


def build_mvar():
    tags = sorted(MVAR_ROWS)
    table = newTable("MVAR")
    table.table = ot.MVAR()
    table.table.Version = 0x00010000
    table.table.Reserved = 0
    table.table.VarStore = build_store([([0, 1, 2, 3], [MVAR_ROWS[tag] for tag in tags])])
    table.table.ValueRecord = []
    for index, tag in enumerate(tags):
        record = ot.MetricsValueRecord()
        record.ValueTag = tag
        record.VarIdx = index
        table.table.ValueRecord.append(record)
    table.table.ValueRecordSize = 8
    table.table.ValueRecordCount = len(tags)
    return table


def build_gvar():
    table = newTable("gvar")
    table.version = 1
    table.reserved = 0
    table.variations = {
        name: [
            TupleVariation({"wght": (0.0, 1.0, 1.0)}, GVAR_WGHT[name]),
            TupleVariation({"wdth": (0.0, 1.0, 1.0)}, GVAR_WDTH[name]),
        ]
        for name in GLYPHS
    }
    return table


def build(file_name, use_typo, with_gvar):
    path = os.path.join(VARIATIONS, file_name)
    family = file_name.replace(".ttf", "")
    builder_ = FontBuilder(1000, isTTF=True)
    builder_.setupGlyphOrder(GLYPHS)
    builder_.setupCharacterMap(CMAP)
    builder_.setupGlyf({name: box(METRICS[name][1]) for name in GLYPHS})
    builder_.setupHorizontalMetrics({name: (METRICS[name][0], METRICS[name][1]) for name in GLYPHS})
    builder_.setupHorizontalHeader(ascent=800, descent=-250, lineGap=50)
    builder_.setupVerticalMetrics({name: (METRICS[name][2], METRICS[name][3]) for name in GLYPHS})
    builder_.setupVerticalHeader(ascent=500, descent=-500, lineGap=10)
    builder_.setupNameTable(
        {
            "familyName": family,
            "styleName": "Regular",
            "uniqueFontIdentifier": family,
            "fullName": family + " Regular",
            "version": "Version 1.000",
            "psName": family + "-Regular",
        },
        mac=False,
    )
    builder_.setupOS2(
        version=4,
        usWeightClass=400,
        usWidthClass=5,
        fsSelection=(1 << 6) | ((1 << 7) if use_typo else 0),
        sTypoAscender=780,
        sTypoDescender=-230,
        sTypoLineGap=70,
        usWinAscent=900,
        usWinDescent=300,
        ySubscriptXSize=650,
        ySubscriptYSize=600,
        ySubscriptXOffset=0,
        ySubscriptYOffset=75,
        ySuperscriptXSize=650,
        ySuperscriptYSize=600,
        ySuperscriptXOffset=0,
        ySuperscriptYOffset=350,
        yStrikeoutSize=50,
        yStrikeoutPosition=290,
        sxHeight=500,
        sCapHeight=700,
    )
    builder_.setupPost(keepGlyphNames=False, underlinePosition=-100, underlineThickness=50)
    builder_.font["maxp"].maxStackElements = 8
    builder_.setupFvar(
        [(tag, low, default, high, name) for tag, low, default, high, name in AXES],
        [{"location": location, "stylename": name} for name, location in INSTANCES],
    )
    builder_.font["HVAR"] = build_hvar()
    builder_.font["VVAR"] = build_vvar()
    builder_.font["MVAR"] = build_mvar()
    if with_gvar:
        builder_.font["gvar"] = build_gvar()
    builder_.font.save(path)
    print(os.path.relpath(path, os.path.join(FONTS, "..", "..", "..")))
    return path


def get_deltas(font, locations):
    """Gives the rounded delta of each MVAR tag at each location, which the fontTools instancer adds to the value."""
    if "MVAR" not in font:
        return [{} for _ in locations]
    table = font["MVAR"].table
    deltas = []
    for location in locations:
        instancer = VarStoreInstancer(table.VarStore, font["fvar"].axes, get_axis_location(font, location["coordinates"]))
        deltas.append({record.ValueTag: ot_round(instancer[record.VarIdx]) for record in table.ValueRecord})
    return deltas


def get_horizontal(font, delta):
    """Gives the ascender, descender and line gap that the library takes: the hhea values, or the typo values when the
    USE_TYPO_METRICS bit is set, then the typo values when the hhea ascender or descender is 0, then the win values.
    The deltas of hasc, hdsc and hlgp vary the hhea and typo values, those of hcla and hcld the win values."""
    os2 = font["OS/2"]
    hhea = font["hhea"]
    if os2.fsSelection & (1 << 7):
        ascender, descender, gap = os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap
    else:
        ascender, descender, gap = hhea.ascent, hhea.descent, hhea.lineGap
    line_height_gap = True
    win = False
    if ascender == 0 or descender == 0:
        if os2.sTypoAscender != 0 or os2.sTypoDescender != 0:
            ascender, descender, gap = os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap
        else:
            win = True
            line_height_gap = False
    gap = gap + delta.get("hlgp", 0)
    if win:
        ascender = os2.usWinAscent + delta.get("hcla", 0)
        descender = -(os2.usWinDescent + delta.get("hcld", 0))
    else:
        ascender += delta.get("hasc", 0)
        descender += delta.get("hdsc", 0)
    return ascender, descender, gap, ascender - descender + (gap if line_height_gap else 0)


def get_vertical(font, delta, horizontal):
    """Gives the vertical metrics: the vhea values, or the horizontal metrics when the font has no vhea."""
    if "vhea" not in font:
        return horizontal
    vhea = font["vhea"]
    ascender = vhea.ascent + delta.get("vasc", 0)
    descender = vhea.descent + delta.get("vdsc", 0)
    descender = -descender if descender > 0 else descender
    gap = vhea.lineGap + delta.get("vlgp", 0)
    return ascender, descender, gap, ascender - descender + gap


def get_metrics(font, delta):
    os2 = font["OS/2"]
    post = font["post"]
    horizontal = get_horizontal(font, delta)
    vertical = get_vertical(font, delta, horizontal)
    values = {
        "ascender": horizontal[0],
        "descender": horizontal[1],
        "lineGap": horizontal[2],
        "lineHeight": horizontal[3],
        "verticalAscender": vertical[0],
        "verticalDescender": vertical[1],
        "verticalLineGap": vertical[2],
        "verticalLineHeight": vertical[3],
        "subscriptXSize": os2.ySubscriptXSize + delta.get("sbxs", 0),
        "subscriptYSize": os2.ySubscriptYSize + delta.get("sbys", 0),
        "subscriptXOffset": os2.ySubscriptXOffset + delta.get("sbxo", 0),
        "subscriptYOffset": os2.ySubscriptYOffset + delta.get("sbyo", 0),
        "superscriptXSize": os2.ySuperscriptXSize + delta.get("spxs", 0),
        "superscriptYSize": os2.ySuperscriptYSize + delta.get("spys", 0),
        "superscriptXOffset": os2.ySuperscriptXOffset + delta.get("spxo", 0),
        "superscriptYOffset": os2.ySuperscriptYOffset + delta.get("spyo", 0),
        "strikeoutSize": os2.yStrikeoutSize + delta.get("strs", 0),
        "strikeoutPosition": os2.yStrikeoutPosition + delta.get("stro", 0),
        "underlinePosition": post.underlinePosition + delta.get("undo", 0),
        "underlineThickness": post.underlineThickness + delta.get("unds", 0),
    }
    return values


def check_not_a_tie(value, what):
    """Stops when a value is within 0.0001 of a half but not on it, where the single precision of the library could round the other way."""
    fraction = abs(value - int(value))
    if 0 < abs(fraction - 0.5) < 0.0001:
        raise SystemExit("%s is %r, too close to a half" % (what, value))


def get_advances(font, glyph_set, glyph_names, attribute):
    values = []
    for name in glyph_names:
        value = getattr(glyph_set[name], attribute)
        check_not_a_tie(value, "%s of %s" % (attribute, name))
        values.append(ot_round(value))
    return values


def get_bearings(font, table_tag, map_name, metrics_tag, location):
    """Gives the side bearing that a mapping of HVAR or VVAR sets for each glyph, or None when the table has no such mapping.
    A glyph that the mapping leaves out keeps the value of hmtx or vmtx."""
    if table_tag not in font or getattr(font[table_tag].table, map_name, None) is None:
        return None
    table = font[table_tag].table
    instancer = VarStoreInstancer(table.VarStore, font["fvar"].axes, location)
    result = []
    for name in font.getGlyphOrder():
        base = font[metrics_tag].metrics[name][1]
        index = getattr(table, map_name).mapping[name]
        delta = 0 if index == NO_VARIATION_INDEX else instancer[index]
        check_not_a_tie(base + delta, "%s of %s" % (map_name, name))
        result.append(ot_round(base + delta))
    return result


def check_with_harfbuzz(path, font, locations, advances, heights, metrics):
    """Stops when HarfBuzz gives other advances or global metrics for the same F2Dot14 coordinates."""
    with open(path, "rb") as file:
        data = file.read()
    face = hb.Face(data)
    hb_font = hb.Font(face)
    mismatches = []
    for index, location in enumerate(locations):
        hb_font.set_var_coords_normalized([fixedToFloat(value, 14) for value in location["coordinates"]])
        for gid, expected in enumerate(advances[index]):
            actual = hb_font.get_glyph_h_advance(gid)
            if actual != expected:
                mismatches.append("%s glyph %d advance %d, HarfBuzz %d" % (location["name"], gid, expected, actual))
        if heights is not None:
            for gid, expected in enumerate(heights[index]):
                actual = -hb_font.get_glyph_v_advance(gid)
                if actual != expected:
                    mismatches.append("%s glyph %d height %d, HarfBuzz %d" % (location["name"], gid, expected, actual))
    if metrics is not None:
        for index, location in enumerate(locations):
            hb_font.set_var_coords_normalized([fixedToFloat(value, 14) for value in location["coordinates"]])
            expected = metrics[index]
            for tag, hb_tag, name in (
                ("hasc", "HORIZONTAL_ASCENDER", "ascender"),
                ("hdsc", "HORIZONTAL_DESCENDER", "descender"),
                ("hlgp", "HORIZONTAL_LINE_GAP", "lineGap"),
                ("vasc", "VERTICAL_ASCENDER", "verticalAscender"),
                ("vdsc", "VERTICAL_DESCENDER", "verticalDescender"),
                ("vlgp", "VERTICAL_LINE_GAP", "verticalLineGap"),
                ("sbxs", "SUBSCRIPT_EM_X_SIZE", "subscriptXSize"),
                ("sbys", "SUBSCRIPT_EM_Y_SIZE", "subscriptYSize"),
                ("sbxo", "SUBSCRIPT_EM_X_OFFSET", "subscriptXOffset"),
                ("sbyo", "SUBSCRIPT_EM_Y_OFFSET", "subscriptYOffset"),
                ("spxs", "SUPERSCRIPT_EM_X_SIZE", "superscriptXSize"),
                ("spys", "SUPERSCRIPT_EM_Y_SIZE", "superscriptYSize"),
                ("spxo", "SUPERSCRIPT_EM_X_OFFSET", "superscriptXOffset"),
                ("spyo", "SUPERSCRIPT_EM_Y_OFFSET", "superscriptYOffset"),
                ("strs", "STRIKEOUT_SIZE", "strikeoutSize"),
                ("stro", "STRIKEOUT_OFFSET", "strikeoutPosition"),
                ("unds", "UNDERLINE_SIZE", "underlineThickness"),
                ("undo", "UNDERLINE_OFFSET", "underlinePosition"),
            ):
                actual = hb_font.get_metric_position(getattr(hb.OTMetricsTag, hb_tag))
                if actual is not None and actual != expected[name]:
                    mismatches.append("%s %s %d, HarfBuzz %s" % (location["name"], name, expected[name], actual))
    if mismatches:
        raise SystemExit("HarfBuzz differs from the reference:\n  " + "\n  ".join(mismatches[:20]))


def get_phantom_reference(path, locations):
    """Gives, for a font that has gvar, the advances and side bearings that the phantom points give: those that fontTools gives on a
    copy of the font without HVAR and VVAR, which then reads them from the phantom points. HarfBuzz must give the same advances."""
    font = TTFont(path)
    del font["HVAR"]
    del font["VVAR"]
    buffer = io.BytesIO()
    font.save(buffer)
    data = buffer.getvalue()
    font = TTFont(io.BytesIO(data))
    hb_font = hb.Font(hb.Face(data))
    glyph_order = font.getGlyphOrder()
    reference = {"advanceWidths": [], "advanceHeights": [], "leftSideBearings": [], "topSideBearings": []}
    mismatches = []
    for location in locations:
        glyph_set = font.getGlyphSet(location=get_axis_location(font, location["coordinates"]), normalized=True)
        hb_font.set_var_coords_normalized([fixedToFloat(value, 14) for value in location["coordinates"]])
        widths, heights, left, top = [], [], [], []
        for gid, name in enumerate(glyph_order):
            glyph = glyph_set[name]
            widths.append(ot_round(glyph.width))
            heights.append(ot_round(glyph.height))
            left.append(ot_round(glyph.lsb))
            top.append(ot_round(glyph.tsb))
            if widths[-1] != hb_font.get_glyph_h_advance(gid):
                mismatches.append("%s glyph %d advance %d, HarfBuzz %d" % (location["name"], gid, widths[-1], hb_font.get_glyph_h_advance(gid)))
            if heights[-1] != -hb_font.get_glyph_v_advance(gid):
                mismatches.append("%s glyph %d height %d, HarfBuzz %d" % (location["name"], gid, heights[-1], -hb_font.get_glyph_v_advance(gid)))
        reference["advanceWidths"].append(widths)
        reference["advanceHeights"].append(heights)
        reference["leftSideBearings"].append(left)
        reference["topSideBearings"].append(top)
    if mismatches:
        raise SystemExit("HarfBuzz differs from the phantom point reference:\n  " + "\n  ".join(mismatches[:20]))
    return reference


def get_reference(path, names, extra, harfbuzz_metrics, with_phantom=False):
    font = TTFont(path)
    locations = get_locations(font, names, extra)
    glyph_order = font.getGlyphOrder()
    horizontal_count = font["hhea"].numberOfHMetrics
    vertical_count = font["vhea"].numberOfVMetrics if "vhea" in font else 0
    deltas = get_deltas(font, locations)
    advances = []
    heights = []
    left = []
    top = []
    for location in locations:
        normalized = get_axis_location(font, location["coordinates"])
        glyph_set = font.getGlyphSet(location=normalized, normalized=True)
        advances.append(get_advances(font, glyph_set, glyph_order[:horizontal_count], "width"))
        if vertical_count:
            heights.append(get_advances(font, glyph_set, glyph_order[:vertical_count], "height"))
        left.append(get_bearings(font, "HVAR", "LsbMap", "hmtx", normalized))
        top.append(get_bearings(font, "VVAR", "TsbMap", "vmtx", normalized) if "VVAR" in font else None)
    metrics = [get_metrics(font, delta) for delta in deltas]
    check_with_harfbuzz(path, font, locations, advances, heights or None, metrics if harfbuzz_metrics else None)
    reference = {
        "numberOfHMetrics": horizontal_count,
        "locations": locations,
        "advanceWidths": advances,
        "advanceHeights": heights or None,
        "leftSideBearings": left if left[0] is not None else None,
        "topSideBearings": top if top[0] is not None else None,
        "metrics": metrics,
        "mvar": [{tag: delta[tag] for tag in sorted(delta)} for delta in deltas],
    }
    if with_phantom:
        reference["phantom"] = get_phantom_reference(path, locations)
    return reference


# The CFF2 fonts of the vertical origin. For each glyph: the top of its box at wght 100 and at wght 900, the advance height at
# wght 100 and at wght 900, the top side bearing of vmtx, and the delta of the VVAR top side bearing map at wght 900.
# A grows, B shrinks, C does not move. Every delta is a multiple of 4, so that 0.25 and 0.75 of it have no half.
VERTICAL_GLYPHS = [".notdef", "A", "B", "C"]
VERTICAL = {
    "A": ((700, 800), (1000, 1100), 100, -20),
    "B": ((600, 500), (1000, 960), 150, 32),
    "C": ((650, 650), (1000, 1000), 90, 8),
}
VERTICAL_EXTRA = [{"wght": 100.0}, {"wght": 300.0}, {"wght": 500.0}, {"wght": 900.0}]


def build_vertical_master(top, height):
    """Builds a master of the CFF2 font: boxes of the given tops and advance heights."""
    order = VERTICAL_GLYPHS
    master = FontBuilder(1000, isTTF=False)
    master.setupGlyphOrder(order)
    master.setupCharacterMap({0x41: "A", 0x42: "B", 0x43: "C"})
    strings = {}
    for name in order:
        pen = T2CharStringPen(600, None)
        if name != ".notdef":
            pen.moveTo((50, 0))
            pen.lineTo((50, top[name]))
            pen.lineTo((450, top[name]))
            pen.lineTo((450, 0))
            pen.closePath()
        strings[name] = pen.getCharString()
    master.setupCFF("VariationCff2Vertical-Regular", {"FullName": "VariationCff2Vertical Regular"}, strings, {})
    master.setupHorizontalMetrics({name: (600, 0 if name == ".notdef" else 50) for name in order})
    master.setupHorizontalHeader(ascent=800, descent=-200)
    master.setupVerticalMetrics(
        {name: (height.get(name, 1000), 0 if name == ".notdef" else VERTICAL[name][2]) for name in order}
    )
    master.setupVerticalHeader(ascent=500, descent=-500)
    master.setupNameTable(
        {"familyName": "VariationCff2Vertical", "styleName": "Regular", "psName": "VariationCff2Vertical-Regular"},
        mac=False,
    )
    master.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=900, usWinDescent=300)
    master.setupPost()
    return master.font


def build_cff2_vertical():
    """Builds VariationCff2Vertical.otf, with no top side bearing map, and VariationCff2VerticalTsb.otf, with one."""
    document = DesignSpaceDocument()
    axis = AxisDescriptor()
    axis.name = "Weight"
    axis.tag = "wght"
    axis.minimum, axis.default, axis.maximum = 100, 100, 900
    document.addAxis(axis)
    for index, (value, tops, heights) in enumerate(
        [(100, {n: VERTICAL[n][0][0] for n in VERTICAL}, {n: VERTICAL[n][1][0] for n in VERTICAL}),
         (900, {n: VERTICAL[n][0][1] for n in VERTICAL}, {n: VERTICAL[n][1][1] for n in VERTICAL})]
    ):
        source = SourceDescriptor()
        source.name = "master%d" % index
        source.styleName = source.name
        source.location = {"Weight": value}
        source.font = build_vertical_master(tops, heights)
        document.addSource(source)
    font, _, _ = varLib.build(document)
    paths = []
    for file_name, with_map in (("VariationCff2Vertical.otf", False), ("VariationCff2VerticalTsb.otf", True)):
        if with_map:
            add_top_side_bearing_map(font["VVAR"].table, font.getGlyphOrder())
        path = os.path.join(VARIATIONS, file_name)
        font.save(path)
        print(os.path.relpath(path, os.path.join(FONTS, "..", "..", "..")))
        paths.append(path)
    return paths


def add_top_side_bearing_map(vvar, glyph_order):
    """Gives a VVAR the top side bearing map of VERTICAL: a store subtable with a row per glyph, and the map into it."""
    store = vvar.VarStore
    outer = len(store.VarData)
    rows = [[VERTICAL[name][3] if name in VERTICAL else 0] for name in glyph_order]
    store.VarData.append(builder.buildVarData([0], rows, optimize=False))
    store.VarDataCount = len(store.VarData)
    vvar.TsbMap = builder.buildVarIdxMap([(outer << 16) | inner for inner in range(len(glyph_order))], glyph_order)


def get_vertical_reference(path, with_map):
    """Gives, for each location, the top of every glyph at fontTools, the advance height, the vertical origin of the default
    outline, and the top side bearing: the origin minus the top of the varied outline, or the vmtx value plus the map delta."""
    font = TTFont(path)
    locations = get_locations(font, [], VERTICAL_EXTRA)
    glyphs = []
    for name in VERTICAL_GLYPHS[1:]:
        tops = []
        heights = []
        bearings = []
        top_side_bearing = font["vmtx"].metrics[name][1]
        default_top = VERTICAL[name][0][0]
        origin = default_top + top_side_bearing
        for location in locations:
            normalized = get_axis_location(font, location["coordinates"])
            glyph_set = font.getGlyphSet(location=normalized, normalized=True)
            pen = BoundsPen(glyph_set)
            glyph_set[name].draw(pen)
            top = ot_round(pen.bounds[3])
            tops.append(top)
            check_not_a_tie(glyph_set[name].height, "height of " + name)
            heights.append(ot_round(glyph_set[name].height))
            if with_map:
                instancer = VarStoreInstancer(font["VVAR"].table.VarStore, font["fvar"].axes, normalized)
                delta = instancer[font["VVAR"].table.TsbMap.mapping[name]]
                check_not_a_tie(top_side_bearing + delta, "top side bearing of " + name)
                bearings.append(ot_round(top_side_bearing + delta))
            else:
                bearings.append(origin - top)
        glyphs.append(
            {
                "name": name,
                "id": font.getGlyphID(name),
                "origin": origin,
                "tops": tops,
                "advanceHeights": heights,
                "topSideBearings": bearings,
            }
        )
    check_vertical_with_harfbuzz(path, locations, glyphs)
    return {"file": "Variations/" + os.path.basename(path), "locations": locations, "glyphs": glyphs}


def check_vertical_with_harfbuzz(path, locations, glyphs):
    """Stops when HarfBuzz gives another top or advance height for the same F2Dot14 coordinates."""
    with open(path, "rb") as file:
        hb_font = hb.Font(hb.Face(file.read()))
    mismatches = []
    for index, location in enumerate(locations):
        hb_font.set_var_coords_normalized([fixedToFloat(value, 14) for value in location["coordinates"]])
        for glyph in glyphs:
            top = hb_font.get_glyph_extents(glyph["id"]).y_bearing
            height = -hb_font.get_glyph_v_advance(glyph["id"])
            if top != glyph["tops"][index]:
                mismatches.append("%s %s top %d, HarfBuzz %d" % (location["name"], glyph["name"], glyph["tops"][index], top))
            if height != glyph["advanceHeights"][index]:
                mismatches.append(
                    "%s %s height %d, HarfBuzz %d" % (location["name"], glyph["name"], glyph["advanceHeights"][index], height)
                )
    if mismatches:
        raise SystemExit("HarfBuzz differs from the reference:\n  " + "\n  ".join(mismatches[:20]))


def check_vertical_descender_with_harfbuzz(path):
    """Stops when HarfBuzz does not give the vertical descender that the library gives for a vhea descent stored as a positive
    number: the stored value plus the MVAR delta, negated when that is positive. The sign is not changed before the delta."""
    font = TTFont(path)
    for stored in (500, 5, -5, 0):
        font["vhea"].descent = stored
        font.save(path + ".tmp")
        with open(path + ".tmp", "rb") as file:
            hb_font = hb.Font(hb.Face(file.read()))
        os.remove(path + ".tmp")
        for wght, delta in ((100, 12), (400, 0), (900, -12)):
            hb_font.set_var_coords_normalized([(wght - 400) / (500 if wght > 400 else 300), 0])
            expected = stored + delta
            expected = -expected if expected > 0 else expected
            actual = hb_font.get_metric_position(hb.OTMetricsTag.VERTICAL_DESCENDER)
            if actual != expected:
                raise SystemExit("vdsc of stored %d at wght %d is %d, HarfBuzz %d" % (stored, wght, expected, actual))


def main():
    os.makedirs(VARIATIONS, exist_ok=True)
    reference = {"fontTools": fontTools.version, "fonts": {}}
    for file_name, (use_typo, with_gvar) in BUILT_FONTS.items():
        path = build(file_name, use_typo, with_gvar)
        reference["fonts"][file_name] = dict(
            file="Variations/" + file_name,
            **get_reference(path, BUILT_NAMES, BUILT_EXTRA, harfbuzz_metrics=True, with_phantom=with_gvar),
        )
    for file_name, (path, names, extra) in SHIPPED_FONTS.items():
        reference["fonts"][file_name] = dict(
            file=path,
            **get_reference(os.path.join(FONTS, path), names, extra, harfbuzz_metrics=True),
        )
    write_reference(REFERENCE, reference)

    check_vertical_descender_with_harfbuzz(os.path.join(VARIATIONS, "VariationMetrics.ttf"))
    vertical = {"fontTools": fontTools.version, "fonts": {}}
    for path, with_map in zip(build_cff2_vertical(), (False, True)):
        vertical["fonts"][os.path.basename(path)] = get_vertical_reference(path, with_map)
    write_reference(VERTICAL_REFERENCE, vertical)


if __name__ == "__main__":
    main()
