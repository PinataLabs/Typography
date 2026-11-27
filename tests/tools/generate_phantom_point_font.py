#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds PhantomPoints.ttf, a small hinted TrueType font whose hinted outlines go to the phantom points, and whose GPOS table
kerns them (issue #170).

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_phantom_point_font.py

FreeType places the phantom points of a glyph from its advances in the 'hmtx' and 'vmtx' tables, so kerning in a layout
does not move them. The glyphs are those of HintedVariableMetricsTables.ttf, which the FreeType reference of that font
checks:

* R is a composite of the box .notdef whose right edge goes to the second phantom point, so its hinted right edge is at
  the end of the advance width, 736.
* T is a box whose top goes to the third phantom point and whose bottom goes to the fourth, so its hinted bottom is the
  advance height, 752, below its top.

The GPOS table kerns R R by -100 on x with `kern` and T T by -100 on y with `vkrn`.

The output is deterministic for one fontTools version.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib.tables import ttProgram
from fontTools.ttLib.tables._g_l_y_f import Glyph, GlyphComponent

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONT = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts", "PhantomPoints.ttf")

UNITS_PER_EM = 1024
ASCENT = 800
DESCENT = -200
BOX = [(64, 0), (64, 640), (576, 640), (576, 0)]

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;

feature kern {
    pos R R <0 0 -100 0>;
} kern;

feature vkrn {
    pos T T <0 0 0 -100>;
} vkrn;
"""


def box(program=None):
    pen = TTGlyphPen(None)
    pen.moveTo(BOX[0])
    for point in BOX[1:]:
        pen.lineTo(point)
    pen.closePath()
    glyph = pen.glyph()
    if program:
        glyph.program = ttProgram.Program()
        glyph.program.fromAssembly(program)
    return glyph


def glyphs():
    # R is a composite because the library, like FreeType with its version 40 interpreter, leaves a move on the x axis out of a
    # simple glyph. The phantom points follow the four points of the box, as points 4 to 7.
    component = GlyphComponent()
    component.glyphName = ".notdef"
    component.flags = 0
    component.x, component.y = 0, 0
    r = Glyph()
    r.numberOfContours = -1
    r.components = [component]
    r.program = ttProgram.Program()
    r.program.fromAssembly(["SVTCA[1]", "PUSHB[ ] 2 5", "GC[0]", "SCFS[ ]", "PUSHB[ ] 3 5", "GC[0]", "SCFS[ ]"])
    t = box(
        [
            "SVTCA[0]",
            "PUSHB[ ] 1 6", "GC[0]", "SCFS[ ]", "PUSHB[ ] 2 6", "GC[0]", "SCFS[ ]",
            "PUSHB[ ] 0 7", "GC[0]", "SCFS[ ]", "PUSHB[ ] 3 7", "GC[0]", "SCFS[ ]",
        ]
    )
    return {".notdef": box(), "R": r, "T": t}


def main():
    order = [".notdef", "R", "T"]
    builder = FontBuilder(UNITS_PER_EM, isTTF=True)
    builder.setupGlyphOrder(order)
    builder.setupCharacterMap({0x52: "R", 0x54: "T"})
    builder.setupGlyf(glyphs())
    builder.setupHorizontalMetrics({".notdef": (512, 64), "R": (736, 64), "T": (640, 64)})
    builder.setupHorizontalHeader(ascent=ASCENT, descent=DESCENT)
    builder.setupVerticalMetrics({".notdef": (1024, 160), "R": (1024, 160), "T": (752, 96)})
    builder.setupVerticalHeader(ascent=ASCENT, descent=DESCENT)
    builder.setupNameTable({"familyName": "PhantomPoints", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=ASCENT, sTypoDescender=DESCENT, usWinAscent=ASCENT, usWinDescent=-DESCENT)
    builder.setupPost(keepGlyphNames=False)
    addOpenTypeFeaturesFromString(builder.font, FEATURES)

    maxp = builder.font["maxp"]
    maxp.maxStackElements = 32
    maxp.maxStorage = 0
    maxp.maxFunctionDefs = 0
    maxp.maxInstructionDefs = 0
    maxp.maxTwilightPoints = 0
    maxp.maxSizeOfInstructions = 64
    maxp.maxZones = 2
    builder.font.save(FONT)
    print(os.path.relpath(FONT, ROOT))


if __name__ == "__main__":
    main()
