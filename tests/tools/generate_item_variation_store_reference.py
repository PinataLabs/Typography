#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes ItemVariationStore.fonttools.json, the fontTools reference for the Item Variation Store reader.

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_item_variation_store_reference.py

The two variable test fonts have Item Variation Stores in these tables:

* Teko-Variable.ttf: HVAR, MVAR and the GDEF 1.3 store.
* SourceSans3VF-Upright.otf: the CFF2 store, HVAR, MVAR and the GDEF 1.3 store.

For each font the script picks a few locations on its axes: the named instances that CONFIG lists,
which include the axis minimum and the axis maximum, and some user locations between them. It
normalises each location the way the OpenType Font Variations Overview says, with the avar segment
map and a round to F2Dot14, and records the F2Dot14 values. The library does not read fvar and avar
yet, so its test feeds these values to the reader as they are.

For each store at each location the reference holds the scalar of every region and the delta of
every row, which fontTools computes with VarStoreInstancer from the F2Dot14 coordinates. A
HVAR store also has the VarIdx that its advance width mapping gives to every glyph, and a MVAR store
has the VarIdx of each of its value records. ItemVariationStoreTests reads the reference.

The output is deterministic for one fontTools version. The reference records the version that wrote it.
"""

import json
import os

import fontTools
from fontTools.misc.fixedTools import fixedToFloat, floatToFixed
from fontTools.ttLib import TTFont
from fontTools.varLib.models import normalizeValue, piecewiseLinearMap, supportScalar
from fontTools.varLib.varStore import VarStoreInstancer

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "ItemVariationStore.fonttools.json")

# The font file, the named instances to record, and the user locations to record besides them.
# Both fonts have one axis, wght. The extra locations lie inside the regions of the stores,
# so their scalars are neither 0 nor 1.
CONFIG = {
    "Teko-Variable.ttf": (
        "Teko/Teko-Variable.ttf",
        ["Light", "Regular", "Bold"],
        [{"wght": 350.0}],
    ),
    "SourceSans3VF-Upright.otf": (
        "SourceSans3/SourceSans3VF-Upright.otf",
        ["ExtraLight", "Semibold", "Bold", "Black"],
        [{"wght": 350.0}],
    ),
}

# Every digit that the C# test compares is far above these.
SCALAR_DIGITS = 8
DELTA_DIGITS = 6


def normalise(font, user):
    """Gives the F2Dot14 coordinate of each fvar axis, in fvar order, for a user location."""
    avar = font["avar"].segments if "avar" in font else {}
    coordinates = []
    for axis in font["fvar"].axes:
        value = normalizeValue(user[axis.axisTag], (axis.minValue, axis.defaultValue, axis.maxValue))
        if axis.axisTag in avar:
            value = piecewiseLinearMap(value, avar[axis.axisTag])
        coordinates.append(floatToFixed(value, 14))
    return coordinates


def get_locations(font, names, extra):
    instances = {font["name"].getDebugName(i.subfamilyNameID): i.coordinates for i in font["fvar"].instances}
    locations = [(name, dict(instances[name])) for name in names]
    locations += [("User " + " ".join("%s=%g" % item for item in user.items()), user) for user in extra]
    return [
        {"name": name, "user": user, "coordinates": normalise(font, user)}
        for name, user in locations
    ]


def get_stores(font):
    """Gives each Item Variation Store of the font by the name of its table, with what else the table maps."""
    stores = {}
    if "HVAR" in font:
        hvar = font["HVAR"].table
        extra = {}
        if hvar.AdvWidthMap is not None:
            extra["advanceWidthMap"] = [hvar.AdvWidthMap.mapping[name] for name in font.getGlyphOrder()]
        stores["HVAR"] = (hvar.VarStore, extra)
    if "MVAR" in font:
        mvar = font["MVAR"].table
        stores["MVAR"] = (mvar.VarStore, {"valueRecords": [[r.ValueTag, r.VarIdx] for r in mvar.ValueRecord]})
    if "GDEF" in font and getattr(font["GDEF"].table, "VarStore", None) is not None:
        stores["GDEF"] = (font["GDEF"].table.VarStore, {})
    if "CFF2" in font:
        stores["CFF2"] = (font["CFF2"].cff.topDictIndex[0].VarStore.otVarStore, {})
    return stores


def clean(value, digits):
    # A whole number is written without a fraction, which keeps the many zero deltas short.
    # Adding 0.0 turns -0.0 into 0.0.
    rounded = round(value, digits) + 0.0
    return int(rounded) if rounded == int(rounded) else rounded


def get_store_reference(store, axes, locations):
    regions = store.VarRegionList.Region
    reference = {
        "axisCount": store.VarRegionList.RegionAxisCount,
        "regionCount": len(regions),
        "data": [{"itemCount": data.ItemCount, "regionIndexes": list(data.VarRegionIndex)} for data in store.VarData],
        "scalars": [],
        "deltas": [],
    }
    for location in locations:
        coordinates = {axis.axisTag: fixedToFloat(value, 14) for axis, value in zip(axes, location["coordinates"])}
        instancer = VarStoreInstancer(store, axes, coordinates)
        reference["scalars"].append(
            [clean(supportScalar(coordinates, region.get_support(axes)), SCALAR_DIGITS) for region in regions]
        )
        reference["deltas"].append(
            [
                [clean(instancer[(outer << 16) | inner], DELTA_DIGITS) for inner in range(data.ItemCount)]
                for outer, data in enumerate(store.VarData)
            ]
        )
    return reference


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
        axes = font["fvar"].axes
        locations = get_locations(font, names, extra)
        reference["fonts"][name] = {
            "file": path,
            "axes": [{"tag": a.axisTag, "min": a.minValue, "default": a.defaultValue, "max": a.maxValue} for a in axes],
            "locations": locations,
            "stores": {
                table: dict(get_store_reference(store, axes, locations), **extra_data)
                for table, (store, extra_data) in get_stores(font).items()
            },
        }
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write(dump(reference) + "\n")
    print(os.path.relpath(REFERENCE, ROOT), sum(len(f["stores"]) for f in reference["fonts"].values()), "stores")


if __name__ == "__main__":
    main()
