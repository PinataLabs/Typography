#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds HintedVariable.ttf, a small hinted variable TrueType font, and writes HintedVariable.fonttools.json, the reference for
what a variation instance of it draws (issues #127 and #130).

Usage (from the repository root, fontTools 4.68 or later, freetype-py for the hinted outlines, and uharfbuzz):

    python tests/tools/generate_hinted_variable_font.py

Neither variable test font has a 'cvar' table or hinting instructions that depend on the control values, so this font has them.
It has two axes, wght and wdth, whose ranges are powers of two on both sides of the default, so that every location below
normalises to an exact F2Dot14 value and the library, fontTools and FreeType agree on the coordinates. It has no avar table.
It has 1024 units to the em, so at 16 and 32 pixels to the em a font unit is a whole number of 1/64 pixel, and the hinting of
FreeType and of the library start from the same points.

The glyphs:

* A has a counter, curves, and 'gvar' tuples that leave points out (interpolation of untouched points, with one touched point in
  the counter), cover every point, span an intermediate region, and use two axes. It also moves the advance with a phantom point.
* B has two components of A at offsets, and 'gvar' deltas of the offsets.
* C has a component of A that is scaled, and placed by matching a point of the first component to a point of the second.
* D has a scaled component of A at an offset that the scale also applies to, which a 'gvar' delta moves.
* space is empty, with phantom point deltas for the advance and for the vertical advance.
* H is a box whose top points go to control value 0 with MIAP, so its hinted height depends on the 'cvar' deltas for control value 0.
* P is a box that calls a function of the 'fpgm' table and goes to control value 3, which the 'prep' program sets from control value 0.
* V is a box whose points go to the coordinates that GETVARIATION gives for each axis, and to a position that GETINFO gives.
* L is a box whose first phantom point and second phantom point move right together, so that the outline stays where it is and
  the origin moves: FreeType and HarfBuzz shift the outline left by the delta of the first phantom point. FreeType hints the
  outline before that shift and moves it by the rounded phantom point after, and a program that rounds the first point on the x axis
  shows whether the library does the same.
* M is a composite of A and L, whose component L has the USE_MY_METRICS flag. The composite and L have different deltas for the
  phantom points, so the advance of M at an instance shows which of the two sets of deltas a font program uses.
* N is a composite of L alone, with the USE_MY_METRICS flag, whose advance in the 'hmtx' table is not the advance of L. At an
  instance a font program takes the advance of L, with its deltas, and not the advance of N.

The 'cvar' table has tuples on the weight, on the width, and on an intermediate region of the weight, and it moves control values 0, 1
and 2. The font has a 'vmtx' table, so a glyph with phantom point deltas on y changes its vertical advance.

The reference holds, for the locations that LOCATIONS lists:

* the control values that fontTools gives the instance, from `fontTools.varLib.instancer`, which sums the 'cvar' deltas and rounds once.
* the varied outline, the bounds, the advance width, the left side bearing, the advance height and the top side bearing of every glyph
  that fontTools can draw with `getGlyphSet(location=..., normalized=True)`: all but C, whose components are placed by points, and D,
  whose component has a scaled offset.
* the advances and the extents that HarfBuzz gives every glyph, which are those of the phantom points: the advances and the left
  side bearing (the x of the extents) of a glyph are measured from the first phantom point, which moves the outline, and a composite
  takes the phantom points of its component that has the USE_MY_METRICS flag. fontTools does neither.
* the outline of every glyph, drawn by FreeType without hinting at 16384 pixels to the em, which keeps the outline to 1/1024 of a font
  unit. It is the reference for C and D.
* the hinted outline of H, P, V and L at 16 and 32 pixels to the em, which FreeType gives with its version 35 interpreter in monochrome
  rendering. The library hints with an interpreter that follows the version 35 one.

It also builds HintedVariableMetricsTables.ttf and writes HintedVariableMetricsTables.fonttools.json, for the glyphs of a font that has
'HVAR' and 'VVAR' as well as 'gvar'. FreeType leaves the deltas of the phantom points out of a glyph when the font has the table of the
direction, and takes the advance from the table: it neither moves the outline by the first phantom point nor takes the advance from the
phantom points of 'gvar', and the vertical phantom points are those of 'vmtx' with the 'VVAR' advance. The font is the one above with two more
glyphs and the two tables:

* R is a composite of the box .notdef whose right edge goes to the second phantom point, so that its hinted outline shows where FreeType puts the end of the advance
  and, with the origin, where it puts the first phantom point. Its 'gvar' deltas for the phantom points differ from its 'HVAR' advance.
* T is a box whose top goes to the third phantom point and whose bottom goes to the fourth, so that its hinted outline shows the vertical phantom
  points. Its 'gvar' deltas differ from its 'VVAR' advance.

The reference for it holds the hinted outlines of L, R, T and H at 16 and 32 pixels to the em that FreeType gives, and the advances and extents
that HarfBuzz gives, which still move the outline by the first phantom point, as HarfBuzz does when it has 'HVAR'.

The output is deterministic for one fontTools and FreeType version. The reference records the versions that wrote it.
"""

import array
import ctypes
import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import fontTools
import freetype
import uharfbuzz
from fontTools.fontBuilder import FontBuilder
from fontTools.misc.roundTools import otRound
from fontTools.pens.basePen import BasePen
from fontTools.pens.boundsPen import ControlBoundsPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable
from fontTools.ttLib.tables import ttProgram
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent
from fontTools.ttLib.tables.TupleVariation import TupleVariation
from fontTools.varLib import instancer
from fontTools.varLib import builder as var_builder
from fontTools.varLib.models import supportScalar
from fontTools.ttLib.tables import otTables as ot

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts", "Variations")
FONT = os.path.join(OUT, "HintedVariable.ttf")
REFERENCE = os.path.join(OUT, "HintedVariable.fonttools.json")
TABLES_FONT = os.path.join(OUT, "HintedVariableMetricsTables.ttf")
TABLES_REFERENCE = os.path.join(OUT, "HintedVariableMetricsTables.fonttools.json")

UNITS_PER_EM = 1024
ASCENT = 800
DESCENT = -200

# The ranges are 256 and 64 units on each side of the default, so 528 is 0.5 and 592 is 0.75 of the weight axis.
AXES = [("wght", 144, 400, 656, "Weight"), ("wdth", 64, 128, 192, "Width")]

# The locations of the reference, in user coordinates. Every one is an exact F2Dot14 coordinate.
LOCATIONS = [
    ("Default", {"wght": 400, "wdth": 128}),
    ("Half weight", {"wght": 528, "wdth": 128}),
    ("Maximum", {"wght": 656, "wdth": 192}),
    ("Light and half width", {"wght": 272, "wdth": 160}),
    ("Three quarters weight", {"wght": 592, "wdth": 128}),
]

# The pixels to the em at which FreeType hints the glyphs.
HINTED_SIZES = [16, 32]
HINTED_GLYPHS = ["H", "P", "V", "L"]

# The scale at which FreeType draws the outlines that it does not hint: 16 pixels to a font unit.
UNHINTED_PPEM = UNITS_PER_EM * 16

CVT = [656, 128, 0, 0, 0, 0, 0, 0]

# The fpgm table defines function 0, which moves a point to a control value. The prep table sets control value 3 to control
# value 0 and a pixel.
FPGM = ["PUSHB[ ] 0", "FDEF[ ]", "MIAP[0]", "ENDF[ ]"]
PREP = ["PUSHB[ ] 3 0", "RCVT[ ]", "PUSHB[ ] 64", "ADD[ ]", "WCVTP[ ]"]

BOX = [(64, 0), (64, 640), (576, 640), (576, 0)]


def draw_simple(points, ends):
    """Makes a simple glyph from (x, y, on curve) points. Each contour starts with an on-curve point and ends with one."""
    pen = TTGlyphPen(None)
    start = 0
    for end in ends:
        contour = points[start : end + 1]
        pen.moveTo(contour[0][:2])
        off = []
        for x, y, on in contour[1:]:
            if on and off:
                pen.qCurveTo(*off, (x, y))
                off = []
            elif on:
                pen.lineTo((x, y))
            else:
                off.append((x, y))
        pen.closePath()
        start = end + 1
    return pen.glyph()


def box(program=None):
    glyph = draw_simple([(x, y, True) for x, y in BOX], [3])
    if program:
        glyph.program = ttProgram.Program()
        glyph.program.fromAssembly(program)
    return glyph


def component(name, x=0, y=0, transform=None, flags=0, points=None):
    comp = GlyphComponent()
    comp.glyphName = name
    comp.flags = flags
    if points is None:
        comp.x, comp.y = x, y
    else:
        comp.firstPt, comp.secondPt = points
    if transform is not None:
        comp.transform = transform
    return comp


def composite(components):
    glyph = Glyph()
    glyph.numberOfContours = -1
    glyph.components = components
    return glyph


# The outline of A: an outer contour of seven points with a curve on top, and a counter of four.
A_POINTS = [
    (64, 0, True), (64, 512, True), (192, 640, False), (320, 640, True), (448, 640, False), (448, 512, True), (448, 0, True),
    (192, 128, True), (320, 128, True), (320, 384, True), (192, 384, True),
]
A_ENDS = [6, 10]
SCALED_COMPONENT_OFFSET = 0x0800
USE_MY_METRICS = 0x0200
HALF = [[0.5, 0], [0, 0.5]]


def glyphs():
    return {
        ".notdef": box(),
        "space": Glyph(),
        "A": draw_simple(A_POINTS, A_ENDS),
        "B": composite([component("A"), component("A", 320, 64)]),
        # The second component is half the size, and its first point goes onto the seventh point of the first component.
        "C": composite([component("A"), component("A", transform=HALF, points=(6, 0))]),
        "D": composite([component("A"), component("A", 256, 128, HALF, SCALED_COMPONENT_OFFSET)]),
        "H": box(
            [
                "SVTCA[0]",
                "PUSHB[ ] 0", "MDAP[0]",
                "PUSHB[ ] 3", "MDAP[0]",
                "PUSHB[ ] 1 0", "MIAP[0]",
                "PUSHB[ ] 2 0", "MIAP[0]",
                "IUP[0]",
            ]
        ),
        "P": box(
            [
                "SVTCA[0]",
                "PUSHB[ ] 0", "MDAP[0]",
                "PUSHB[ ] 3", "MDAP[0]",
                "PUSHB[ ] 1 3 0", "CALL[ ]",
                "PUSHB[ ] 2 3 0", "CALL[ ]",
                "IUP[0]",
            ]
        ),
        "L": box(["SVTCA[1]", "PUSHB[ ] 0", "MDAP[1]", "IUP[1]"]),
        "M": composite([component("A"), component("L", 320, 64, flags=USE_MY_METRICS)]),
        "N": composite([component("L", flags=USE_MY_METRICS)]),
        "V": box(
            [
                "SVTCA[0]",
                # Point 1 goes to the weight coordinate, in eighths of a pixel per 1024 units, and point 2 to the width coordinate.
                "PUSHB[ ] 1", "GETVARIATION[ ]", "POP[ ]", "PUSHW[ ] 8192", "DIV[ ]", "SCFS[ ]",
                "PUSHB[ ] 2", "GETVARIATION[ ]", "SWAP[ ]", "POP[ ]", "PUSHW[ ] 8192", "DIV[ ]", "SCFS[ ]",
                # Point 3 goes to what GETINFO says about variations.
                "PUSHB[ ] 3 8", "GETINFO[ ]", "PUSHW[ ] 512", "DIV[ ]", "SCFS[ ]",
            ]
        ),
    }


# No two glyphs at the end share an advance, so the font has a horizontal metric for every glyph.
ADVANCES = {".notdef": 512, "space": 256, "A": 512, "B": 832, "C": 928, "D": 640, "H": 640, "P": 640, "V": 656, "L": 720, "M": 720, "N": 800}
CMAP = {0x20: "space", 0x41: "A", 0x42: "B", 0x43: "C", 0x44: "D", 0x48: "H", 0x4C: "L", 0x4D: "M", 0x4E: "N", 0x50: "P", 0x56: "V"}


# The glyphs that only the font with 'HVAR' and 'VVAR' has. R goes to the second phantom point on the x axis, and T to the third and the
# fourth on the y axis. The phantom points follow the four points of the box, as points 4 to 7.
TABLES_ADVANCES = {**ADVANCES, "R": 736, "T": 752}
TABLES_CMAP = {**CMAP, 0x52: "R", 0x54: "T"}
TABLES_HINTED_GLYPHS = ["L", "R", "T", "H"]


def tables_glyphs():
    # R is a composite because the library, like FreeType with its version 40 interpreter, leaves a move on the x axis out of a simple glyph.
    r = composite([component(".notdef")])
    r.program = ttProgram.Program()
    r.program.fromAssembly(["SVTCA[1]", "PUSHB[ ] 2 5", "GC[0]", "SCFS[ ]", "PUSHB[ ] 3 5", "GC[0]", "SCFS[ ]"])
    extra = {
        "R": r,
        "T": box(
            [
                "SVTCA[0]",
                "PUSHB[ ] 1 6", "GC[0]", "SCFS[ ]", "PUSHB[ ] 2 6", "GC[0]", "SCFS[ ]",
                "PUSHB[ ] 0 7", "GC[0]", "SCFS[ ]", "PUSHB[ ] 3 7", "GC[0]", "SCFS[ ]",
            ]
        ),
    }
    return {**glyphs(), **extra}


def tables_gvar_variations():
    """The 'gvar' tuples of the font with 'HVAR' and 'VVAR': those of the other font, and for R and T deltas of the phantom points that
    are not what the tables give."""
    result = gvar_variations()
    zero = [(0, 0)] * 4
    result["R"] = [
        variation({"wght": (0, 1, 1)}, [(0, 0)] + [(24, 0), (60, 0), (0, 0), (0, 0)]),
        variation({"wdth": (0, 1, 1)}, [(0, 0)] + [(8, 0), (-16, 0), (0, 0), (0, 0)]),
    ]
    result["T"] = [
        variation({"wght": (0, 1, 1)}, zero + [(0, 0), (0, 0), (0, 48), (0, -32)]),
        variation({"wdth": (0, 1, 1)}, zero + [(0, 0), (0, 0), (0, -16), (0, 8)]),
    ]
    return result


def variation(axes, coordinates):
    return TupleVariation({tag: tuple(value) for tag, value in axes.items()}, coordinates)


def gvar_variations():
    """The 'gvar' tuples of each glyph. A point that a tuple leaves out is None. The last four are the phantom points."""
    none = None

    def a_phantoms(right=0, top=0, bottom=0):
        return [(0, 0), (right, 0), (0, top), (0, bottom)]

    sparse_a = [none] * 11 + a_phantoms()
    sparse_a[0] = (0, 0)
    sparse_a[3] = (16, 64)
    sparse_a[6] = (32, 0)
    sparse_a[9] = (8, 24)
    sparse_a[12] = (48, 0)

    full_a = [(-4 - i, 2 * i) for i in range(11)] + a_phantoms(-16)

    width_a = [none] * 11 + a_phantoms()
    width_a[1] = (24, 0)
    width_a[5] = (-24, 8)
    width_a[7] = (10, 0)
    width_a[9] = (10, 16)
    width_a[12] = (64, 0)

    middle_a = [none] * 11 + [none] * 4
    middle_a[3] = (40, -16)
    middle_a[9] = (0, 32)

    box_weight = [(0, 0), (0, 64), (0, 64), (0, 0)] + a_phantoms(32)
    box_width = [none, none, (32, 0), (32, 0)] + a_phantoms(64)

    return {
        "space": [
            variation({"wght": (0, 1, 1)}, [(0, 0), (64, 0), (0, 16), (0, -16)]),
            variation({"wdth": (0, 1, 1)}, [(0, 0), (-32, 0), (0, 0), (0, 0)]),
        ],
        "A": [
            variation({"wght": (0, 1, 1)}, sparse_a),
            variation({"wght": (-1, -1, 0)}, full_a),
            variation({"wdth": (0, 1, 1)}, width_a),
            variation({"wght": (0.25, 0.5, 0.75), "wdth": (0, 1, 1)}, middle_a),
        ],
        "B": [
            variation({"wght": (0, 1, 1)}, [(0, 0), (48, 16)] + a_phantoms(80, 8, -8)),
            variation({"wdth": (0, 1, 1)}, [none, (16, -8)] + a_phantoms(32)),
        ],
        "C": [variation({"wght": (0, 1, 1)}, [(24, 8), none] + a_phantoms(96))],
        "D": [
            variation({"wght": (0, 1, 1)}, [none, (32, 16)] + a_phantoms(48)),
            variation({"wdth": (-1, -1, 0)}, [(-8, 0), (-16, 8)] + a_phantoms(-16)),
        ],
        "L": [
            variation({"wght": (0, 1, 1)}, [(0, 0)] * 4 + [(20, 0), (20, 0), (0, 0), (0, 0)]),
            variation({"wdth": (0, 1, 1)}, [(0, 0)] * 4 + [(16, 0), (0, 0), (0, 0), (0, 0)]),
        ],
        "M": [
            variation({"wght": (0, 1, 1)}, [(0, 0), (8, 0)] + [(0, 0), (100, 0), (0, 12), (0, -12)]),
            variation({"wdth": (0, 1, 1)}, [none, none] + [(-24, 0), (40, 0), (0, 0), (0, 0)]),
        ],
        "N": [variation({"wght": (0, 1, 1)}, [(0, 0), (0, 0), (64, 0), (0, 0), (0, 0)])],
        "H": [variation({"wght": (0, 1, 1)}, box_weight), variation({"wdth": (0, 1, 1)}, box_width)],
        "P": [variation({"wght": (0, 1, 1)}, box_weight)],
        "V": [variation({"wght": (0, 1, 1)}, box_weight)],
    }


def cvar_variations():
    """The 'cvar' tuples: a value that a tuple leaves out is None."""
    def values(**changes):
        result = [None] * len(CVT)
        for index, delta in changes.items():
            result[int(index[1:])] = delta
        return result

    return [
        variation({"wght": (0, 1, 1)}, values(i0=96, i1=16)),
        variation({"wght": (-1, -1, 0)}, values(i0=-64)),
        variation({"wdth": (0, 1, 1)}, values(i0=32, i2=8)),
        variation({"wght": (0.25, 0.5, 0.75)}, values(i1=10)),
    ]


def build(path=FONT, with_tables=False):
    advances = TABLES_ADVANCES if with_tables else ADVANCES
    order = list(advances)
    builder = FontBuilder(UNITS_PER_EM, isTTF=True)
    builder.setupGlyphOrder(order)
    builder.setupCharacterMap(TABLES_CMAP if with_tables else CMAP)
    builder.setupGlyf(tables_glyphs() if with_tables else glyphs())
    table = builder.font["glyf"]
    metrics = {}
    for name in order:
        glyph = table[name]
        glyph.recalcBounds(table)
        metrics[name] = (advances[name], getattr(glyph, "xMin", 0) if glyph.numberOfContours else 0)
    builder.setupHorizontalMetrics(metrics)
    builder.setupHorizontalHeader(ascent=ASCENT, descent=DESCENT)

    # The vertical origin is at the ascent, so the top of the first phantom point row is 800 for every glyph.
    vertical = {}
    for name in order:
        glyph = table[name]
        top = glyph.yMax if glyph.numberOfContours else 0
        vertical[name] = (UNITS_PER_EM, ASCENT - top)
    builder.setupVerticalMetrics(vertical)
    builder.setupVerticalHeader(ascent=ASCENT, descent=DESCENT)

    family = "Hinted Variable Metrics Tables" if with_tables else "Hinted Variable"
    builder.setupNameTable(
        {
            "familyName": family,
            "styleName": "Regular",
            "uniqueFontIdentifier": family.replace(" ", ""),
            "fullName": family + " Regular",
            "version": "Version 1.000",
            "psName": family.replace(" ", "") + "-Regular",
        },
        mac=False,
    )
    builder.setupOS2(
        version=4,
        usWeightClass=400,
        usWidthClass=5,
        fsSelection=(1 << 6) | (1 << 7),
        sTypoAscender=ASCENT,
        sTypoDescender=DESCENT,
        usWinAscent=ASCENT,
        usWinDescent=-DESCENT,
    )
    builder.setupPost(keepGlyphNames=False)
    builder.setupFvar(
        [(tag, low, default, high, name) for tag, low, default, high, name in AXES],
        [{"location": {"wght": 400, "wdth": 128}, "stylename": "Regular"}],
    )

    font = builder.font
    font["cvt "] = newTable("cvt ")
    font["cvt "].values = array.array("h", CVT)
    font["fpgm"] = newTable("fpgm")
    font["fpgm"].program = ttProgram.Program()
    font["fpgm"].program.fromAssembly(FPGM)
    font["prep"] = newTable("prep")
    font["prep"].program = ttProgram.Program()
    font["prep"].program.fromAssembly(PREP)
    font["gvar"] = newTable("gvar")
    font["gvar"].version = 1
    font["gvar"].reserved = 0
    font["gvar"].variations = tables_gvar_variations() if with_tables else gvar_variations()
    font["cvar"] = newTable("cvar")
    font["cvar"].version = 1
    font["cvar"].variations = cvar_variations()

    maxp = font["maxp"]
    maxp.maxStackElements = 32
    maxp.maxStorage = 4
    maxp.maxFunctionDefs = 4
    maxp.maxInstructionDefs = 0
    maxp.maxTwilightPoints = 4
    maxp.maxSizeOfInstructions = 128
    maxp.maxZones = 2
    if with_tables:
        add_metrics_tables(font, order)
    font.save(path)


def add_metrics_tables(font, order):
    """Adds 'HVAR' and 'VVAR'. The advance of each glyph takes the row of its glyph index in the first subtable of the store, and the side bearing
    the row in the second. For each step of the index the advance has the deltas 8 on the weight, -8 on the lower half of the weight and -4 on the
    width, and the bearing 6, -6 and 3. They are not the deltas of the phantom points of the glyph."""
    regions = var_builder.buildVarRegionList([{"wght": (0, 1, 1)}, {"wght": (-1, -1, 0)}, {"wdth": (0, 1, 1)}], ["wght", "wdth"])

    def store(advance_scale, bearing_scale):
        advances = [[advance_scale * 8 * (i + 1), -advance_scale * 8 * (i + 1), -advance_scale * 4 * (i + 1)] for i in range(len(order))]
        bearings = [[bearing_scale * 6 * (i + 1), -bearing_scale * 6 * (i + 1), bearing_scale * 3 * (i + 1)] for i in range(len(order))]
        return var_builder.buildVarStore(
            regions,
            [var_builder.buildVarData([0, 1, 2], rows, optimize=False) for rows in (advances, bearings)],
        )

    advance_map = var_builder.buildVarIdxMap(list(range(len(order))), order)
    bearing_map = var_builder.buildVarIdxMap([(1 << 16) | i for i in range(len(order))], order)

    hvar = newTable("HVAR")
    hvar.table = ot.HVAR()
    hvar.table.Version = 0x00010000
    hvar.table.VarStore = store(1, 1)
    hvar.table.AdvWidthMap = advance_map
    hvar.table.LsbMap = bearing_map
    hvar.table.RsbMap = None
    font["HVAR"] = hvar

    vvar = newTable("VVAR")
    vvar.table = ot.VVAR()
    vvar.table.Version = 0x00010000
    vvar.table.VarStore = store(1, 1)
    vvar.table.AdvHeightMap = advance_map
    vvar.table.TsbMap = bearing_map
    vvar.table.BsbMap = None
    vvar.table.VOrgMap = None
    font["VVAR"] = vvar


class OutlinePen(BasePen):
    """Records an outline as the commands of IGlyphOutlineSink."""

    def __init__(self, glyph_set=None):
        super().__init__(glyph_set)
        self.commands = []

    def _moveTo(self, pt):
        self.commands.append(("M", pt))

    def _lineTo(self, pt):
        self.commands.append(("L", pt))

    def _curveToOne(self, pt1, pt2, pt3):
        self.commands.append(("C", pt1, pt2, pt3))

    def _qCurveToOne(self, pt1, pt2):
        self.commands.append(("Q", pt1, pt2))

    def _closePath(self):
        self.commands.append(("Z",))

    def _endPath(self):
        self.commands.append(("Z",))


def number(value, digits=4):
    value = round(float(value), digits) + 0.0
    return int(value) if value == int(value) else value


def outline_text(commands, digits=4):
    parts = []
    for command in commands:
        parts.append(command[0])
        for point in command[1:]:
            parts.append(str(number(point[0], digits)))
            parts.append(str(number(point[1], digits)))
    return " ".join(parts)


def normalised(font, user):
    axes = {a.axisTag: (a.minValue, a.defaultValue, a.maxValue) for a in font["fvar"].axes}
    result = {}
    for tag, value in user.items():
        low, default, high = axes[tag]
        result[tag] = (value - default) / (default - low if value < default else high - default)
    return result


def freetype_outline(face, glyph_id, scale, flags):
    """Draws a glyph with FreeType, as the commands of the contours. Each contour starts at its first point that is on the curve."""
    face.load_glyph(glyph_id, flags)
    outline = face.glyph.outline
    commands = []
    start = 0
    for end in outline.contours:
        contour = [
            (x / scale, y / scale, bool(outline.tags[i] & 1)) for i, (x, y) in enumerate(outline.points[start : end + 1], start)
        ]
        commands.extend(contour_commands(contour))
        start = end + 1
    return commands


def contour_commands(contour):
    first = next(i for i, point in enumerate(contour) if point[2])
    ordered = contour[first:] + contour[:first]
    commands = [("M", ordered[0][:2])]
    control = None
    for x, y, on in ordered[1:] + [ordered[0]]:
        if on:
            commands.append(("Q", control, (x, y)) if control else ("L", (x, y)))
            control = None
        else:
            if control:
                commands.append(("Q", control, ((control[0] + x) / 2, (control[1] + y) / 2)))
            control = (x, y)

    # The line back to the first point is the end of the contour.
    if commands[-1][0] == "L":
        commands.pop()
    commands.append(("Z",))
    return commands


def freetype_face(path=FONT):
    """Opens the font with the version 35 interpreter, which the interpreter of the library follows."""
    library = freetype.get_handle()
    version = ctypes.c_uint(35)
    if freetype.FT_Property_Set(library, b"truetype", b"interpreter-version", ctypes.byref(version)):
        raise SystemExit("This FreeType has no version 35 interpreter.")
    return freetype.Face(path)


def get_phantom(font, name, location, index_from_end):
    """Adds up the 'gvar' deltas of one of the four phantom points of a glyph at a location."""
    glyph = font["glyf"][name]
    count = len(glyph.components) if glyph.isComposite() else len(glyph.coordinates)
    total = (0, 0)
    for tuple_variation in font["gvar"].variations.get(name, []):
        scalar = supportScalar(location, tuple_variation.axes)
        delta = tuple_variation.coordinates[count + index_from_end] if len(tuple_variation.coordinates) > count else None
        if scalar and delta:
            total = (total[0] + scalar * delta[0], total[1] + scalar * delta[1])
    return total


def get_glyph_reference(font, glyph_set, face, hb_font, glyph_id, name, location):
    glyph = {"unhinted": outline_text(freetype_outline(face, glyph_id, 64 * UNHINTED_PPEM / UNITS_PER_EM, freetype.FT_LOAD_NO_HINTING | freetype.FT_LOAD_NO_BITMAP), 3)}
    is_composite = font["glyf"][name].isComposite()
    # fontTools places the components of C by points as if they had no offsets, and it does not scale the offset of a component
    # of D that the glyph asks to be scaled, so FreeType is the only reference for the two. It does not move the outline of L by the
    # first phantom point, and M and N take the phantom points of L, so FreeType and HarfBuzz are the only reference for those two.
    # A glyph object computes the metrics of the instance when it is asked for its advance width, so the metrics come from one object.
    drawn = glyph_set[name]
    glyph["advanceWidth"] = drawn.width
    extents = hb_font.get_glyph_extents(glyph_id)
    glyph["harfbuzz"] = {
        "advanceWidth": hb_font.get_glyph_h_advance(glyph_id),
        "advanceHeight": -hb_font.get_glyph_v_advance(glyph_id),
        "leftSideBearing": extents.x_bearing,
        "yMax": extents.y_bearing,
        "width": extents.width,
        "height": -extents.height,
        "topSideBearing": hb_font.get_glyph_v_origin(glyph_id)[1] - extents.y_bearing,
    }
    if name not in ("C", "D", "L", "M", "N"):
        pen = OutlinePen(glyph_set)
        drawn.draw(pen)
        bounds = ControlBoundsPen(glyph_set)
        drawn.draw(bounds)
        glyph["outline"] = outline_text(pen.commands)
        glyph["bounds"] = None if bounds.bounds is None else [number(v) for v in bounds.bounds]

        # fontTools takes the box of a composite glyph from its unvaried components, so the side bearings of a composite are left out.
        glyph["leftSideBearing"] = None if is_composite else drawn.lsb
        glyph["topSideBearing"] = None if is_composite else drawn.tsb
    glyph["advanceHeight"] = drawn.height
    return glyph


def main():
    build()
    font = TTFont(FONT)
    order = font.getGlyphOrder()
    reference = {
        "fontTools": fontTools.version,
        "freetype": ".".join(str(v) for v in freetype.version()),
        "harfbuzz": uharfbuzz.version_string(),
        "controlValues": CVT,
        "hintedSizes": HINTED_SIZES,
        "locations": [],
    }

    face = freetype_face()
    for name, user in LOCATIONS:
        location = normalised(font, user)
        coordinates = [otRound(location[a.axisTag] * 16384) for a in font["fvar"].axes]
        if any(c / 16384 != location[a.axisTag] for c, a in zip(coordinates, font["fvar"].axes)):
            raise SystemExit("%s is not an exact F2Dot14 coordinate" % name)
        instance = instancer.instantiateVariableFont(TTFont(FONT), user)
        glyph_set = font.getGlyphSet(location=location, normalized=True)
        hb_font = uharfbuzz.Font(uharfbuzz.Face(open(FONT, "rb").read()))
        hb_font.set_variations(user)
        face.set_var_blend_coords([location[a.axisTag] for a in font["fvar"].axes])
        entry = {
            "name": name,
            "user": user,
            "coordinates": coordinates,
            "controlValues": list(instance["cvt "].values),
            "glyphs": {},
            "hinted": {},
        }
        face.set_pixel_sizes(0, UNHINTED_PPEM)
        for glyph_id, glyph_name in enumerate(order):
            entry["glyphs"][glyph_name] = get_glyph_reference(font, glyph_set, face, hb_font, glyph_id, glyph_name, location)
        for size in HINTED_SIZES:
            face.set_pixel_sizes(0, size)
            flags = freetype.FT_LOAD_TARGET_MONO | freetype.FT_LOAD_NO_AUTOHINT | freetype.FT_LOAD_NO_BITMAP
            entry["hinted"][str(size)] = {
                glyph_name: outline_text(freetype_outline(face, order.index(glyph_name), 64, flags), 4) for glyph_name in HINTED_GLYPHS
            }
        reference["locations"].append(entry)

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write(json.dumps(reference, indent=1) + "\n")
    print(os.path.relpath(FONT, ROOT), os.path.getsize(FONT), "bytes;", os.path.relpath(REFERENCE, ROOT), os.path.getsize(REFERENCE), "bytes")


def tables_reference():
    build(TABLES_FONT, with_tables=True)
    font = TTFont(TABLES_FONT)
    order = font.getGlyphOrder()
    reference = {
        "freetype": ".".join(str(v) for v in freetype.version()),
        "harfbuzz": uharfbuzz.version_string(),
        "hintedSizes": HINTED_SIZES,
        "glyphs": order,
        "locations": [],
    }
    face = freetype_face(TABLES_FONT)
    with open(TABLES_FONT, "rb") as file:
        data = file.read()
    for name, user in LOCATIONS:
        location = normalised(font, user)
        coordinates = [otRound(location[a.axisTag] * 16384) for a in font["fvar"].axes]
        hb_font = uharfbuzz.Font(uharfbuzz.Face(data))
        hb_font.set_variations(user)
        face.set_var_blend_coords([location[a.axisTag] for a in font["fvar"].axes])
        entry = {"name": name, "user": user, "coordinates": coordinates, "harfbuzz": {}, "hinted": {}}
        for glyph_id, glyph_name in enumerate(order):
            extents = hb_font.get_glyph_extents(glyph_id)
            entry["harfbuzz"][glyph_name] = {
                "advanceWidth": hb_font.get_glyph_h_advance(glyph_id),
                "advanceHeight": -hb_font.get_glyph_v_advance(glyph_id),
                "leftSideBearing": extents.x_bearing,
                "yMax": extents.y_bearing,
                "width": extents.width,
                "height": -extents.height,
                "topSideBearing": hb_font.get_glyph_v_origin(glyph_id)[1] - extents.y_bearing,
            }
        for size in HINTED_SIZES:
            face.set_pixel_sizes(0, size)
            flags = freetype.FT_LOAD_TARGET_MONO | freetype.FT_LOAD_NO_AUTOHINT | freetype.FT_LOAD_NO_BITMAP
            entry["hinted"][str(size)] = {
                glyph_name: outline_text(freetype_outline(face, order.index(glyph_name), 64, flags), 4) for glyph_name in TABLES_HINTED_GLYPHS
            }
        reference["locations"].append(entry)

    with open(TABLES_REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write(json.dumps(reference, indent=1) + "\n")
    print(os.path.relpath(TABLES_FONT, ROOT), os.path.getsize(TABLES_FONT), "bytes;", os.path.relpath(TABLES_REFERENCE, ROOT), os.path.getsize(TABLES_REFERENCE), "bytes")


if __name__ == "__main__":
    main()
    tables_reference()
