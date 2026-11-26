#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes Cff2Outlines.fonttools.json, the fontTools reference for the outlines of CFF2 variation instances (issue #126).

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_cff2_outline_reference.py

SourceSans3VF-Upright.otf has CFF2 outlines with a variation store of two ItemVariationData subtables, so a glyph
blends with the regions of vsindex 0, or with those of vsindex 1 after a vsindex operator. The script records some of
its glyphs at the default instance (wght 200), at the named instances Regular and Bold, at the axis maximum (wght 900),
and at two user locations between them (wght 350 and 850). The glyphs are listed in GLYPHS. The script checks that they
blend in the charstring, blend only in a subroutine, switch to vsindex 1, and do not blend at all.

For each glyph at each location the file holds what fontTools gives with TTFont.getGlyphSet(location=...,
normalized=True), whose CFF2 charstrings blend with the region scalars of the location:

* the outline, which a RecordingPen records, as the outline text of the other fontTools fixtures: M, L, C and Z
  commands in glyph space, with y up;
* the bounds that a BoundsPen finds, which are exact for curves, where the library samples them;
* the advance width, which HVAR varies. It is the sum before the library rounds it.

The locations are given to fontTools as the F2Dot14 coordinates that the Font Variations Overview, "Coordinate Scales and
Normalization", gives, so that fontTools and the library compute with the same coordinates. The file holds them.

The output is deterministic for one fontTools version. The reference records the version that wrote it.
"""

import os

import fontTools
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.recordingPen import RecordingPen
from fontTools.ttLib import TTFont

from variation_reference import FONTS, VARIATIONS, clean, get_axis_location, get_locations, write_reference

FILE = "SourceSans3/SourceSans3VF-Upright.otf"
REFERENCE = os.path.join(VARIATIONS, "Cff2Outlines.fonttools.json")

NAMES = ["ExtraLight", "Regular", "Bold", "Black"]
EXTRA = [{"wght": 350.0}, {"wght": 850.0}]

GLYPHS = [
    ".notdef", "space", "A", "H", "I", "M", "O", "Q", "T", "a", "c", "e", "g", "i", "j", "m", "n", "o", "q", "s", "t",
    "u", "w", "ampersand", "at", "percent", "zero", "two", "Aring", "Agrave", "Aogonek", "Dcroat", "Eogonek",
    "uni0190", "Eng", "Oslashacute", "Hbar", "Lslash",
]

# Digits of a coordinate. The library computes in single precision, so a finer digit would not be compared.
DIGITS = 4


def number(value):
    value = clean(value, DIGITS)
    return str(value)


class OutlinePen(RecordingPen):
    """Records an outline as the outline text of the fontTools fixtures."""

    def text(self):
        parts = []
        for operator, points in self.value:
            if operator == "moveTo":
                parts.append("M")
            elif operator == "lineTo":
                parts.append("L")
            elif operator == "curveTo":
                parts.append("C")
            elif operator == "qCurveTo":
                parts.append("Q")
            elif operator in ("closePath", "endPath"):
                parts.append("Z")
            else:
                raise ValueError(operator)
            for x, y in points:
                parts += [number(x), number(y)]
        return " ".join(parts)


def get_kinds(font, name, flat):
    """Tells how a glyph blends: in its charstring, only in a subroutine, with vsindex 1, or not at all.

    `flat` is the CFF table with its subroutines inlined, which shows a blend that only a subroutine holds.
    """
    charstring = font["CFF2"].cff.topDictIndex[0].CharStrings[name]
    charstring.decompile()
    inlined = flat.topDictIndex[0].CharStrings[name]
    inlined.decompile()
    kinds = set()
    if "blend" in charstring.program:
        kinds.add("blend")
    elif "blend" in inlined.program:
        kinds.add("subroutine")
    if "vsindex" in charstring.program and charstring.program[charstring.program.index("vsindex") - 1] == 1:
        kinds.add("vsindex1")
    return kinds or {"none"}


def check_coverage(font):
    flat = TTFont(os.path.join(FONTS, FILE))["CFF2"].cff
    flat.desubroutinize()
    kinds = set()
    for name in GLYPHS:
        kinds |= get_kinds(font, name, flat)
    missing = {"blend", "subroutine", "vsindex1", "none"} - kinds
    if missing:
        raise SystemExit("GLYPHS has no glyph of the kinds: " + ", ".join(sorted(missing)))


def main():
    font = TTFont(os.path.join(FONTS, FILE))
    order = font.getGlyphOrder()
    missing = [name for name in GLYPHS if name not in order]
    if missing:
        raise SystemExit("Glyphs missing from the font: " + ", ".join(missing))

    check_coverage(font)
    locations = get_locations(font, NAMES, EXTRA)
    glyph_sets = [
        font.getGlyphSet(location=get_axis_location(font, location["coordinates"]), normalized=True)
        for location in locations
    ]

    glyphs = []
    for name in GLYPHS:
        outlines = []
        bounds = []
        advances = []
        for glyph_set in glyph_sets:
            pen = OutlinePen()
            glyph_set[name].draw(pen)
            outlines.append(pen.text())
            bounds_pen = BoundsPen(glyph_set)
            glyph_set[name].draw(bounds_pen)
            bounds.append([clean(value, DIGITS) for value in bounds_pen.bounds] if bounds_pen.bounds else [])
            advances.append(clean(glyph_set[name].width, DIGITS))
        glyphs.append({"name": name, "id": font.getGlyphID(name), "outlines": outlines, "bounds": bounds, "advances": advances})

    reference = {
        "fontTools": fontTools.version,
        "file": FILE,
        "unitsPerEm": font["head"].unitsPerEm,
        "locations": locations,
        "glyphs": glyphs,
    }
    write_reference(REFERENCE, reference)


if __name__ == "__main__":
    main()
