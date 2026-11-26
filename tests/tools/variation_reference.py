# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""What the variation reference scripts share: the locations they record, the F2Dot14 coordinates of each,
and the JSON layout.

A script imports it from its own folder, so each one still runs as `python tests/tools/<name>.py` from the
repository root.
"""

import json
import math
import os

from fontTools.misc.fixedTools import fixedToFloat, floatToFixed
from fontTools.varLib.models import normalizeValue, piecewiseLinearMap

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
VARIATIONS = os.path.join(FONTS, "Variations")


def normalise(font, user):
    """Gives the F2Dot14 coordinate of each fvar axis, in fvar order, for a user location.

    It follows the Font Variations Overview, "Coordinate Scales and Normalization": clamp and normalise, map
    with the avar segments, and round to F2Dot14.
    """
    avar = font["avar"].segments if "avar" in font else {}
    coordinates = []
    for axis in font["fvar"].axes:
        value = normalizeValue(user[axis.axisTag], (axis.minValue, axis.defaultValue, axis.maxValue))
        if axis.axisTag in avar:
            value = piecewiseLinearMap(value, avar[axis.axisTag])
        coordinates.append(floatToFixed(value, 14))
    return coordinates


def get_locations(font, names, extra):
    """Gives the named instances that `names` lists and the user locations of `extra`, each with its F2Dot14 coordinates.

    A location that `extra` gives sets the axes it names and leaves the others at their default.
    """
    defaults = {a.axisTag: a.defaultValue for a in font["fvar"].axes}
    instances = {font["name"].getDebugName(i.subfamilyNameID): i.coordinates for i in font["fvar"].instances}
    locations = [(name, dict(instances[name])) for name in names]
    locations += [("User " + " ".join("%s=%g" % item for item in user.items()), dict(defaults, **user)) for user in extra]
    return [{"name": name, "user": user, "coordinates": normalise(font, user)} for name, user in locations]


def get_axis_location(font, coordinates):
    """Gives the normalised location, as fontTools takes it with normalized=True, of F2Dot14 coordinates.

    The library computes with the F2Dot14 values, so the reference does too. A request between two F2Dot14
    values then does not give fontTools a coordinate that the library cannot have.
    """
    return {axis.axisTag: fixedToFloat(value, 14) for axis, value in zip(font["fvar"].axes, coordinates)}


def ot_round(value):
    """Rounds as fontTools otRound does, which is the rounding that the library uses: half rounds up."""
    return int(math.floor(value + 0.5))


def clean(value, digits):
    """Rounds to some digits, and gives a whole number without a fraction. Adding 0.0 turns -0.0 into 0.0."""
    rounded = round(value, digits) + 0.0
    return int(rounded) if rounded == int(rounded) else rounded


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


def write_reference(path, reference):
    with open(path, "w", encoding="utf-8", newline="\n") as file:
        file.write(dump(reference) + "\n")
    print(os.path.relpath(path, ROOT))
