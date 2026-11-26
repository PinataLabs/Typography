#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds the fonts of the metrics variation tests and writes MetricsVariations.fonttools.json, the fontTools reference
for the advances, side bearings and global metrics of variation instances (issue #128).

Usage (from the repository root, fontTools 4.68 or later, and uharfbuzz for the cross-check):

    python tests/tools/generate_metrics_variation_reference.py

Neither test font has HVAR side bearing mappings, VVAR, or an MVAR record for anything but the strikeout position and
the heights, so the script builds two small TrueType fonts that have them all, next to the reference:

* VariationMetrics.ttf has the axes wght and wdth and six glyphs. Its HVAR has an advance width mapping that shares one
  row between glyphs, a left side bearing mapping that leaves a glyph out, and a right side bearing mapping. It has
  vhea, vmtx and a VVAR with the matching mappings, and an MVAR that varies every metric that the library reads: the
  ascender, descender and line gap of the hhea table, the typo and win metrics of OS/2, the vhea metrics, the subscript,
  superscript, strikeout and underline values. The values of hhea, typo and win differ, and its USE_TYPO_METRICS bit is
  clear, so the library takes the hhea values.
* VariationMetricsTypo.ttf is the same font with the USE_TYPO_METRICS bit set, so the library takes the typo values.

A glyph of either font has no gvar variation, so its outline does not change, and only its metrics do.

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

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import fontTools
import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.misc.fixedTools import fixedToFloat
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable
from fontTools.ttLib.tables import otTables as ot
from fontTools.varLib import builder
from fontTools.varLib.varStore import VarStoreInstancer

from variation_reference import FONTS, VARIATIONS, get_axis_location, get_locations, ot_round, write_reference

REFERENCE = os.path.join(VARIATIONS, "MetricsVariations.fonttools.json")

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
BUILT_FONTS = {"VariationMetrics.ttf": False, "VariationMetricsTypo.ttf": True}
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


def build(file_name, use_typo):
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


def get_reference(path, names, extra, harfbuzz_metrics):
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
    return {
        "numberOfHMetrics": horizontal_count,
        "locations": locations,
        "advanceWidths": advances,
        "advanceHeights": heights or None,
        "leftSideBearings": left if left[0] is not None else None,
        "topSideBearings": top if top[0] is not None else None,
        "metrics": metrics,
        "mvar": [{tag: delta[tag] for tag in sorted(delta)} for delta in deltas],
    }


def main():
    os.makedirs(VARIATIONS, exist_ok=True)
    reference = {"fontTools": fontTools.version, "fonts": {}}
    for file_name, use_typo in BUILT_FONTS.items():
        path = build(file_name, use_typo)
        reference["fonts"][file_name] = dict(
            file="Variations/" + file_name,
            **get_reference(path, BUILT_NAMES, BUILT_EXTRA, harfbuzz_metrics=True),
        )
    for file_name, (path, names, extra) in SHIPPED_FONTS.items():
        reference["fonts"][file_name] = dict(
            file=path,
            **get_reference(os.path.join(FONTS, path), names, extra, harfbuzz_metrics=True),
        )
    write_reference(REFERENCE, reference)


if __name__ == "__main__":
    main()
