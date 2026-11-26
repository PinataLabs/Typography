#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes GposVariations.harfbuzz.json, the HarfBuzz reference for the GPOS VariationIndex deltas of the two variable
test fonts (issue #129).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_gpos_variations_reference.py

Teko-Variable.ttf and SourceSans3VF-Upright.otf keep their kerning and mark anchors in GPOS value records and anchors that
point to VariationIndex device tables. The script finds the glyph sequences that those tables position, in these kinds of row:

* `pair`: two glyphs that a PairPos value record with a VariationIndex device moves, from lookups of format 1 and format 2.
* `mark`: a base and a mark that a MarkBasePos anchor with a VariationIndex device attaches.
* `chain`: a base and two marks, where the second mark attaches to the first, which a MarkBasePos anchor with a
  VariationIndex device attaches to the base. The second mark only moves because the first one does.
* `static`: a glyph pair that a PairPos value record with no device moves. It does not change with the weight.

HarfBuzz shapes each text at the default instance and at wght 400 and 700. The script keeps the texts whose positions
differ between the default instance and one of the other two, and for the static rows the texts whose positions do not.
Every glyph holds [glyph ID, x of its origin from the start of the line, y offset, final advance, advance before
positioning, mark]. HarfBuzz gives the advance before positioning as the advance of the glyph at the location, which
includes HVAR. The mark flag is the GDEF glyph class. `GposVariationTests` subtracts the advance before positioning of the
library from its positions, so it compares what GPOS adds, and the test holds whether or not the library varies advances.

The rows come from a fixed stride through the candidates in glyph ID order, so the output is deterministic for one fontTools
and HarfBuzz version. The file records the HarfBuzz version.
"""

import json
import os

import harfbuzz_reference as ref
import uharfbuzz as hb
from fontTools.ttLib import TTFont

FONT_PATHS = [
    os.path.join(ref.FONTS, "Teko", "Teko-Variable.ttf"),
    os.path.join(ref.FONTS, "SourceSans3", "SourceSans3VF-Upright.otf"),
]
REFERENCE = os.path.join(ref.FONTS, "Variations", "GposVariations.harfbuzz.json")

# The default instance first, which is None.
WEIGHTS = [None, 400, 700]
MARK_CLASS = 3

# How many rows of each kind a font gets, at most.
ROW_LIMITS = {"pair": 90, "static": 24, "mark": 90, "chain": 24}


class FontData:
    def __init__(self, path):
        self.path = path
        self.font = TTFont(path)
        self.glyph_id = {name: i for i, name in enumerate(self.font.getGlyphOrder())}
        self.gpos = self.font["GPOS"].table
        self.default_weight = int({axis.axisTag: axis.defaultValue for axis in self.font["fvar"].axes}["wght"])
        self.code_point = {}
        for code, glyph in sorted(self.font.getBestCmap().items()):
            self.code_point.setdefault(glyph, code)
        class_defs = self.font["GDEF"].table.GlyphClassDef.classDefs
        self.marks = {glyph for glyph, glyph_class in class_defs.items() if glyph_class == MARK_CLASS}
        self.hb_fonts = []
        for weight in WEIGHTS:
            hb_font = ref.load_font(path)
            if weight is not None:
                hb_font.set_variations({"wght": weight})
            self.hb_fonts.append(hb_font)

    def text_of(self, glyphs):
        """Gives the text of the glyphs, or None when a glyph has no code point."""
        if any(glyph not in self.code_point for glyph in glyphs):
            return None
        return "".join(chr(self.code_point[glyph]) for glyph in glyphs)

    def shape(self, text):
        """Shapes the text at each weight and gives the glyphs of each, or None when the weights disagree on the glyph IDs."""
        locations = []
        for hb_font in self.hb_fonts:
            buffer = hb.Buffer()
            buffer.add_str(text)
            buffer.guess_segment_properties()
            hb.shape(hb_font, buffer)
            glyphs = []
            pen = 0
            for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
                glyphs.append(
                    [
                        info.codepoint,
                        pen + position.x_offset,
                        position.y_offset,
                        position.x_advance,
                        hb_font.get_glyph_h_advance(info.codepoint),
                        1 if self.font.getGlyphName(info.codepoint) in self.marks else 0,
                    ]
                )
                pen += position.x_advance
            locations.append(glyphs)
        if len({tuple(glyph[0] for glyph in glyphs) for glyphs in locations}) != 1:
            return None
        return locations

    def subtables(self, lookup_type):
        """Gives the subtables of the lookups of a type, with the extension subtables unwrapped."""
        for lookup in self.gpos.LookupList.Lookup:
            for subtable in lookup.SubTable:
                subtable_type = lookup.LookupType
                if subtable_type == 9:
                    subtable_type = subtable.ExtensionLookupType
                    subtable = subtable.ExtSubTable
                if subtable_type == lookup_type:
                    yield subtable

    def pair_candidates(self, wanted):
        """Gives the glyph pairs of the PairPos subtables that have a value record which `wanted` accepts, in glyph ID order.
        A pair of format 2 stands for its class pair, as the lowest glyph of each class."""
        candidates = set()
        for subtable in self.subtables(2):
            coverage = subtable.Coverage.glyphs
            if subtable.Format == 1:
                for first, pair_set in zip(coverage, subtable.PairSet):
                    for record in pair_set.PairValueRecord:
                        if wanted(record.Value1) or wanted(record.Value2):
                            candidates.add((first, record.SecondGlyph))
                continue

            by_class1 = {}
            for glyph in coverage:
                by_class1.setdefault(subtable.ClassDef1.classDefs.get(glyph, 0), []).append(glyph)
            by_class2 = {}
            for glyph, glyph_class in subtable.ClassDef2.classDefs.items():
                by_class2.setdefault(glyph_class, []).append(glyph)
            for class1, class1_record in enumerate(subtable.Class1Record):
                for class2, class2_record in enumerate(class1_record.Class2Record):
                    if (wanted(class2_record.Value1) or wanted(class2_record.Value2)) and class1 in by_class1 and class2 in by_class2:
                        candidates.add((min(by_class1[class1], key=self.glyph_id.get), min(by_class2[class2], key=self.glyph_id.get)))
        return sorted(candidates, key=lambda pair: (self.glyph_id[pair[0]], self.glyph_id[pair[1]]))

    def mark_candidates(self):
        """Gives the (base, mark) pairs of the MarkBasePos subtables whose anchors have a VariationIndex, in glyph ID order."""
        candidates = set()
        for subtable in self.subtables(4):
            for base, base_record in zip(subtable.BaseCoverage.glyphs, subtable.BaseArray.BaseRecord):
                for mark, mark_record in zip(subtable.MarkCoverage.glyphs, subtable.MarkArray.MarkRecord):
                    base_anchor = base_record.BaseAnchor[mark_record.Class]
                    if base_anchor is not None and (has_variation_anchor(base_anchor) or has_variation_anchor(mark_record.MarkAnchor)):
                        candidates.add((base, mark))
        return sorted(candidates, key=lambda pair: (self.glyph_id[pair[0]], self.glyph_id[pair[1]]))

    def mark_follower(self, mark):
        """Gives the lowest glyph with a code point that a MarkMarkPos subtable attaches to the mark, or None."""
        followers = set()
        for subtable in self.subtables(6):
            if mark in subtable.Mark1Coverage.glyphs:
                followers.update(glyph for glyph in subtable.Mark2Coverage.glyphs if glyph in self.code_point)
        return min(followers, key=self.glyph_id.get) if followers else None


DEVICES = ("XPlaDevice", "YPlaDevice", "XAdvDevice", "YAdvDevice")


def has_device(value_record):
    return value_record is not None and any(getattr(value_record, name, None) is not None for name in DEVICES)


def has_variation(value_record):
    return value_record is not None and any(
        getattr(value_record, name, None) is not None and getattr(value_record, name).DeltaFormat == 0x8000 for name in DEVICES
    )


def has_variation_anchor(anchor):
    return anchor.Format == 3 and any(
        device is not None and device.DeltaFormat == 0x8000 for device in (anchor.XDeviceTable, anchor.YDeviceTable)
    )


def gpos_only(glyphs):
    """Gives what GPOS adds at one weight: for each glyph its ID, its position from the pen that the advances before
    positioning would give, its y offset and the change of its advance. A mark has no advance, because HarfBuzz zeroes it, and
    a mark takes its position from the pen of the glyph it attaches to, which is the closest glyph before it that is no mark."""
    unpositioned = [0 if mark else advance for _, _, _, _, advance, mark in glyphs]
    result = []
    base = 0
    for i, (glyph_id, x, y, advance, _, mark) in enumerate(glyphs):
        if not mark:
            base = i
        result.append((glyph_id, x - sum(unpositioned[:base]), y, advance - unpositioned[i]))
    return result


def varies(locations):
    return any(gpos_only(glyphs) != gpos_only(locations[0]) for glyphs in locations[1:])


def pick(items, limit):
    """Takes `limit` items at an even stride, or all of them when there are fewer."""
    if len(items) <= limit:
        return items
    stride = len(items) / limit
    return [items[int(i * stride)] for i in range(limit)]


def take(data, kind, glyph_lists, must_vary):
    """Shapes each list of glyphs and gives rows for an even stride of those that vary with the weight, or of those that do
    not. A text whose shaping substitutes a glyph is out, because the rows are about positioning."""
    passing = []
    for glyphs in glyph_lists:
        text = data.text_of(glyphs)
        locations = None if text is None else data.shape(text)
        if locations is None or [glyph[0] for glyph in locations[0]] != [data.glyph_id[glyph] for glyph in glyphs]:
            continue
        if varies(locations) == must_vary:
            passing.append([kind, text, locations])
    return pick(passing, ROW_LIMITS[kind])


def build_rows(data):
    marks = data.mark_candidates()
    chains = []
    for base, mark in marks:
        follower = data.mark_follower(mark)
        if follower is not None:
            chains.append([base, mark, follower])

    static = data.pair_candidates(lambda record: record is not None and not has_device(record))
    return (
        take(data, "pair", data.pair_candidates(has_variation), True)
        + take(data, "static", static, False)
        + take(data, "mark", marks, True)
        + take(data, "chain", chains, True)
    )


def write_reference(fonts):
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "fonts": [\n')
        for i, font in enumerate(fonts):
            file.write("    {\n")
            file.write('      "font": %s,\n' % json.dumps(font["font"]))
            file.write('      "weights": %s,\n' % json.dumps(font["weights"]))
            file.write('      "rows": [\n')
            file.write(",\n".join("        " + json.dumps(row, separators=(",", ":")) for row in font["rows"]))
            file.write("\n      ]\n")
            file.write("    }" + ("," if i < len(fonts) - 1 else "") + "\n")
        file.write("  ]\n}\n")
    print(os.path.relpath(REFERENCE, ref.ROOT))


def main():
    fonts = []
    for path in FONT_PATHS:
        data = FontData(path)
        rows = build_rows(data)
        print(os.path.basename(path), {kind: sum(1 for row in rows if row[0] == kind) for kind in ROW_LIMITS})
        fonts.append(
            {
                "font": os.path.relpath(path, ref.FONTS).replace(os.sep, "/"),
                "weights": [data.default_weight if weight is None else weight for weight in WEIGHTS],
                "rows": rows,
            }
        )
    write_reference(fonts)


if __name__ == "__main__":
    main()
