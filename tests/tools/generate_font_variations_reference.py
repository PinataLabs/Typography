#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds the small variable fonts of the variation tests and writes FontVariations.fonttools.json,
the fontTools reference for fvar, avar and the normalised coordinates of an instance (issue #125).

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_font_variations_reference.py

The fonts go to tests/PinataLabs.Typography.Tests/Fonts/Variations. They have fvar and avar and no outline
variation data, because the library draws the default outlines until the gvar and CFF2 issues land:

* VariationAxes.ttf has the axes wght, wdth, ital, opsz and a hidden GRAD axis, with an avar segment map for wght
  and opsz. Neither Teko-Variable.ttf nor SourceSans3VF-Upright.otf has an opsz axis, so the opsz tests use this font.
* VariationSlant.ttf has the axes wght and slnt and no ital axis, so an italic request has to use slnt.

The reference covers those two fonts and the two variable fonts of the test project. For each font it holds the axes,
the named instances and a list of locations. A location is the user coordinates of every axis, and the F2Dot14
coordinates that the Font Variations Overview, "Coordinate Scales and Normalization", gives for them:

1. fontTools normalizeLocation clamps each user coordinate to the axis range and maps it to -1, 0 or 1 at the minimum,
   the default and the maximum.
2. The avar segment map of the axis maps the result, with fontTools piecewiseLinearMap.
3. floatToFixed rounds the result to F2Dot14: it adds 0.5 and takes the floor, as the specification says.

The locations are the default, the minimum and the maximum of each axis with the other axes at their default, every axis at
its minimum and at its maximum, each named instance, and user locations between them. A location that two names share is
listed twice. FontVariationTests reads the file.

The output is deterministic for one fontTools version. The reference records the version that wrote it.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import fontTools
from fontTools.fontBuilder import FontBuilder
from fontTools.misc.fixedTools import floatToFixed
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable
from fontTools.varLib.models import normalizeLocation, piecewiseLinearMap

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
OUT = os.path.join(FONTS, "Variations")
REFERENCE = os.path.join(OUT, "FontVariations.fonttools.json")

GLYPHS = [".notdef", "space", "A"]
CMAP = {0x20: "space", 0x41: "A"}
ADVANCES = {".notdef": 500, "space": 250, "A": 600}

# The built fonts: file name, family name, axes as (tag, min, default, max, name, hidden), the avar segment maps
# as {tag: {user-free normalised from: to}}, and the named instances as (style name, location).
# A segment map has -1, 0 and 1 mapped to themselves, as the specification asks.
BUILT_FONTS = {
    "VariationAxes.ttf": (
        "Variation Axes",
        [
            ("wght", 100, 400, 900, "Weight", False),
            ("wdth", 75, 100, 125, "Width", False),
            ("opsz", 8, 14, 144, "Optical size", False),
            ("ital", 0, 0, 1, "Italic", False),
            ("GRAD", -100, 0, 100, "Grade", True),
        ],
        {
            "wght": {-1.0: -1.0, -0.5: -0.25, 0.0: 0.0, 0.5: 0.625, 1.0: 1.0},
            "opsz": {-1.0: -1.0, 0.0: 0.0, 0.25: 0.5, 1.0: 1.0},
        },
        [
            ("Regular", {"wght": 400, "wdth": 100, "opsz": 14, "ital": 0, "GRAD": 0}),
            ("Bold", {"wght": 700, "wdth": 100, "opsz": 14, "ital": 0, "GRAD": 0}),
            ("Condensed Bold", {"wght": 700, "wdth": 75, "opsz": 14, "ital": 0, "GRAD": 0}),
            ("Italic", {"wght": 400, "wdth": 100, "opsz": 14, "ital": 1, "GRAD": 0}),
            ("Display Thin", {"wght": 100, "wdth": 100, "opsz": 144, "ital": 0, "GRAD": 0}),
        ],
        [{"wght": 250}, {"wght": 650, "wdth": 90}, {"opsz": 24}, {"opsz": 100, "ital": 0.5}, {"GRAD": -50}],
    ),
    "VariationSlant.ttf": (
        "Variation Slant",
        [
            ("wght", 300, 400, 800, "Weight", False),
            ("slnt", -12, 0, 0, "Slant", False),
        ],
        {},
        [
            ("Regular", {"wght": 400, "slnt": 0}),
            ("Oblique", {"wght": 400, "slnt": -12}),
        ],
        [{"wght": 600, "slnt": -6}],
    ),
}

# The variable fonts of the test project: file, and the user locations to record besides the generated ones.
SHIPPED_FONTS = {
    "Teko-Variable.ttf": ("Teko/Teko-Variable.ttf", [{"wght": 350}, {"wght": 450}, {"wght": 650}]),
    "SourceSans3VF-Upright.otf": ("SourceSans3/SourceSans3VF-Upright.otf", [{"wght": 350}, {"wght": 450}, {"wght": 850}]),
}


def box():
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((550, 700))
    pen.lineTo((550, 0))
    pen.closePath()
    return pen.glyph()


def build(file_name, family, axes, segments, instances):
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    builder.setupGlyf({name: box() for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (ADVANCES[name], 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
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
        sTypoAscender=800,
        sTypoDescender=-200,
        usWinAscent=800,
        usWinDescent=200,
    )
    builder.setupPost(keepGlyphNames=False)
    builder.setupFvar(
        [(tag, low, default, high, name) for tag, low, default, high, name, _ in axes],
        [{"location": location, "stylename": name} for name, location in instances],
    )
    for axis in builder.font["fvar"].axes:
        if [a for a in axes if a[0] == axis.axisTag][0][5]:
            axis.flags = 1

    if segments:
        avar = newTable("avar")
        avar.segments = {
            tag: segments.get(tag, {-1.0: -1.0, 0.0: 0.0, 1.0: 1.0}) for tag, *_ in axes
        }
        builder.font["avar"] = avar

    path = os.path.join(OUT, file_name)
    builder.font.save(path)
    return path


def normalise(font, user):
    """Gives the F2Dot14 coordinate of each fvar axis, in fvar order, for a user location."""
    axes = {a.axisTag: (a.minValue, a.defaultValue, a.maxValue) for a in font["fvar"].axes}
    normalised = normalizeLocation(user, axes)
    segments = font["avar"].segments if "avar" in font else {}
    coordinates = []
    for axis in font["fvar"].axes:
        value = normalised[axis.axisTag]
        if axis.axisTag in segments:
            value = piecewiseLinearMap(value, segments[axis.axisTag])
        coordinates.append(floatToFixed(value, 14))
    return coordinates


def get_locations(font, extra):
    axes = font["fvar"].axes
    defaults = {a.axisTag: a.defaultValue for a in axes}
    named = [("Default", dict(defaults))]
    for axis in axes:
        named.append(("%s minimum" % axis.axisTag, dict(defaults, **{axis.axisTag: axis.minValue})))
        named.append(("%s maximum" % axis.axisTag, dict(defaults, **{axis.axisTag: axis.maxValue})))
    named.append(("Minimum", {a.axisTag: a.minValue for a in axes}))
    named.append(("Maximum", {a.axisTag: a.maxValue for a in axes}))
    for instance in font["fvar"].instances:
        named.append((font["name"].getDebugName(instance.subfamilyNameID), dict(instance.coordinates)))
    for user in extra:
        named.append(("User " + " ".join("%s=%g" % item for item in user.items()), dict(defaults, **user)))

    return [{"name": name, "user": user, "coordinates": normalise(font, user)} for name, user in named]


def get_reference(path, extra):
    font = TTFont(path)
    names = font["name"]
    return {
        "axes": [
            {
                "tag": a.axisTag,
                "min": a.minValue,
                "default": a.defaultValue,
                "max": a.maxValue,
                "hidden": bool(a.flags & 1),
                "name": names.getDebugName(a.axisNameID),
            }
            for a in font["fvar"].axes
        ],
        "instances": [
            {"name": names.getDebugName(i.subfamilyNameID), "coordinates": dict(i.coordinates)}
            for i in font["fvar"].instances
        ],
        "avar": {tag: [[k, v] for k, v in sorted(m.items())] for tag, m in font["avar"].segments.items()}
        if "avar" in font
        else {},
        "locations": get_locations(font, extra),
    }


def dump(value, level=0):
    """Formats JSON with short values on one line and long lists in rows of 20, so a diff stays readable."""
    inline = json.dumps(value, separators=(", ", ": "))
    pad = "  " * (level + 1)
    end = "  " * level
    if len(inline) <= 100 or not isinstance(value, (dict, list)):
        return inline
    if isinstance(value, dict):
        lines = ["%s%s: %s" % (pad, json.dumps(key), dump(item, level + 1)) for key, item in value.items()]
        return "{\n" + ",\n".join(lines) + "\n" + end + "}"
    if not any(isinstance(item, (dict, list)) for item in value):
        lines = [pad + ", ".join(json.dumps(item) for item in value[i : i + 20]) for i in range(0, len(value), 20)]
    else:
        lines = [pad + dump(item, level + 1) for item in value]
    return "[\n" + ",\n".join(lines) + "\n" + end + "]"


def main():
    os.makedirs(OUT, exist_ok=True)
    reference = {"fontTools": fontTools.version, "fonts": {}}
    for file_name, (family, axes, segments, instances, extra) in BUILT_FONTS.items():
        path = build(file_name, family, axes, segments, instances)
        print(os.path.relpath(path, ROOT))
        reference["fonts"][file_name] = dict(file="Variations/" + file_name, **get_reference(path, extra))
    for file_name, (path, extra) in SHIPPED_FONTS.items():
        reference["fonts"][file_name] = dict(file=path, **get_reference(os.path.join(FONTS, path), extra))

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write(dump(reference) + "\n")
    print(os.path.relpath(REFERENCE, ROOT), sum(len(f["locations"]) for f in reference["fonts"].values()), "locations")


if __name__ == "__main__":
    main()
