#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes Gvar.fonttools.json, the fontTools reference for the outlines, bounds and advances of a TrueType variation instance.

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_gvar_reference.py

Teko-Variable.ttf is the one variable test font with TrueType outlines. The script draws it at the locations that CONFIG
lists with fontTools `getGlyphSet(location=...)`, which applies the 'gvar' deltas with interpolation of untouched points,
the deltas of the component offsets of composite glyphs, and the deltas of the phantom points. The locations are the default
instance, the "SemiBold" named instance, the maximum, and a user location between them. Each is normalised as the Font
Variations Overview says, with the avar segment map and a round to F2Dot14, and drawn at those F2Dot14 values as a normalised
location.

For every glyph of the font the reference holds at each location:

* the control bounds of the varied outline, which fontTools computes from the points of the decomposed glyph. The bounds that
  fontTools keeps in a composite glyph come from the unvaried components, so the script does not use them.
* the advance width that the phantom points give. Teko-Variable.ttf has an HVAR table, which makes fontTools use it, so the
  script reads the advances from a copy of the font that has none.
* the left side bearing, which is the rounded left edge of the bounds less the first phantom point.

For a sample of the glyphs it also holds the varied outline: the Basic Latin glyphs, the composites that the sample picks,
and the glyphs that are empty. An outline is the text of path commands that the FontProgram fixtures use.

The script checks the assumption that the first phantom point of every glyph is the origin, which the left side bearing relies
on: the glyphs have a left side bearing equal to xMin, and no 'gvar' delta moves the first phantom point.

The output is deterministic for one fontTools version. The reference records the version that wrote it.
"""

import json
import os

import fontTools
from fontTools.misc.fixedTools import fixedToFloat
from fontTools.misc.roundTools import otRound
from fontTools.pens.basePen import BasePen
from fontTools.pens.boundsPen import ControlBoundsPen
from fontTools.ttLib import TTFont

from generate_font_variations_reference import normalise

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "Variations", "Gvar.fonttools.json")

# The font file, the named instances to record, and the user locations to record besides them. The default instance is
# recorded too, because the library draws it from the font data with no variation at all.
CONFIG = {
    "Teko-Variable.ttf": ("Teko/Teko-Variable.ttf", ["Light", "SemiBold", "Bold"], [{"wght": 450.0}]),
}

# Every fourth composite glyph, and the Basic Latin glyphs, get an outline.
COMPOSITE_STEP = 4
DIGITS = 4


class OutlinePen(BasePen):
    """Records an outline as the commands of IGlyphOutlineSink."""

    def __init__(self, glyph_set):
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


def number(value):
    value = round(float(value), DIGITS) + 0.0
    return int(value) if value == int(value) else value


def outline_text(commands):
    parts = []
    for command in commands:
        parts.append(command[0])
        for point in command[1:]:
            parts.append(str(number(point[0])))
            parts.append(str(number(point[1])))
    return " ".join(parts)


def get_locations(font, names, extra):
    instances = {font["name"].getDebugName(i.subfamilyNameID): i.coordinates for i in font["fvar"].instances}
    default = {a.axisTag: a.defaultValue for a in font["fvar"].axes}
    locations = [("Default", dict(default))]
    locations += [(name, dict(instances[name])) for name in names if name != "Light"]
    locations += [("User " + " ".join("%s=%g" % item for item in user.items()), user) for user in extra]
    result = []
    for name, user in locations:
        coordinates = normalise(font, user)
        axes = [a.axisTag for a in font["fvar"].axes]
        result.append(
            {
                "name": name,
                "user": user,
                "coordinates": coordinates,
                "location": {tag: fixedToFloat(value, 14) for tag, value in zip(axes, coordinates)},
            }
        )
    return result


def check_origin_phantom_point(font):
    """The left side bearing of every glyph is its xMin, and no delta moves the first phantom point."""
    glyf = font["glyf"]
    for name in font.getGlyphOrder():
        glyph = glyf[name]
        if not hasattr(glyph, "xMin"):
            glyph.recalcBounds(glyf)
        if glyph.numberOfContours and font["hmtx"].metrics[name][1] != glyph.xMin:
            raise SystemExit("%s has a left side bearing that is not its xMin" % name)
        count = len(glyph.components) if glyph.isComposite() else len(getattr(glyph, "coordinates", []))
        for variation in font["gvar"].variations.get(name, []):
            delta = variation.coordinates[count] if len(variation.coordinates) > count else None
            if delta not in (None, (0, 0)):
                raise SystemExit("%s moves the first phantom point" % name)


def choose_outline_glyphs(font):
    glyf = font["glyf"]
    order = font.getGlyphOrder()
    cmap = font.getBestCmap()
    chosen = {order.index(".notdef")}
    chosen.update(order.index(name) for code, name in cmap.items() if 0x20 <= code <= 0x7E)
    composites = [i for i, name in enumerate(order) if glyf[name].isComposite()]
    chosen.update(composites[::COMPOSITE_STEP])
    chosen.update(i for i, name in enumerate(order) if glyf[name].numberOfContours == 0)
    return sorted(chosen)


def get_instance_reference(font, without_hvar, location, outline_glyphs):
    glyph_set = font.getGlyphSet(location=location["location"], normalized=True)
    phantom_set = without_hvar.getGlyphSet(location=location["location"], normalized=True)
    order = font.getGlyphOrder()
    bounds = []
    advances = []
    side_bearings = []
    for name in order:
        pen = ControlBoundsPen(glyph_set)
        glyph_set[name].draw(pen)
        box = pen.bounds
        bounds.append(None if box is None else [number(v) for v in box])
        advances.append(otRound(phantom_set[name].width))
        side_bearings.append(0 if box is None else otRound(box[0]))
    outlines = {}
    for index in outline_glyphs:
        pen = OutlinePen(glyph_set)
        glyph_set[order[index]].draw(pen)
        outlines[str(index)] = outline_text(pen.commands)
    return {
        "name": location["name"],
        "user": location["user"],
        "coordinates": location["coordinates"],
        "bounds": bounds,
        "advances": advances,
        "leftSideBearings": side_bearings,
        "outlines": outlines,
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
    reference = {"fontTools": fontTools.version, "fonts": {}}
    for name, (path, names, extra) in CONFIG.items():
        font = TTFont(os.path.join(FONTS, path))
        without_hvar = TTFont(os.path.join(FONTS, path))
        del without_hvar["HVAR"]
        check_origin_phantom_point(font)
        outline_glyphs = choose_outline_glyphs(font)
        reference["fonts"][name] = {
            "file": path,
            "glyphCount": len(font.getGlyphOrder()),
            "outlineGlyphs": outline_glyphs,
            "instances": [
                get_instance_reference(font, without_hvar, location, outline_glyphs)
                for location in get_locations(font, names, extra)
            ],
        }
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write(dump(reference) + "\n")
    print(os.path.relpath(REFERENCE, ROOT), os.path.getsize(REFERENCE), "bytes")


if __name__ == "__main__":
    main()
