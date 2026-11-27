#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Checks the font subsets of FontSubsetter with fontTools.

Usage (from the repository root, fontTools 4.68 or later):

    EXPORT_FONT_SUBSETS=<directory> dotnet test --project tests/PinataLabs.Typography.Tests -c Release -f net8.0 \
        --treenode-filter "/*/*/FontSubsetter*Tests/*"
    python tests/tools/verify_font_subsets.py <directory> [--selftest]

The tests write each subset into the directory, with a JSON file that names the font that it came from and the glyph IDs that
the test asked it to keep. This script loads the font and the subset with fontTools, and checks that

* the subset loads, and for an sfnt file every table checksum and the 'head' checkSumAdjustment are right,
* the subset has the glyph count and the glyph names of the font,
* every glyph that the test asked for has the outline and the advance that it had, at the same glyph ID, and
* every other glyph is either as it was, because a kept glyph needs it (a seac accent, a TrueType component), or empty.

Of a font with more than 1,500 glyphs the script compares every glyph that the test asked for and one glyph in every few of the others.

With --selftest it also runs two checks that must fail, to show that the comparison can fail: the subset against the font with the
roles swapped, so that the dropped glyphs of the subset count as outlines that were added, and the subset with every glyph claimed as kept.
The exit code is 0 when every subset passes and every control fails as it should.
"""

import io
import json
import os
import struct
import sys

from fontTools import cffLib
from fontTools.pens.basePen import BasePen
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables import _g_l_y_f as glyf_module


class OutlinePen(BasePen):
    """Records an outline, with the components of a seac glyph or a composite glyph drawn in place."""

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


MAX_CHECKED = 1500


def outline_text(commands):
    parts = []
    for command in commands:
        parts.append(command[0])
        for x, y in command[1:]:
            parts.extend(str(round(float(value), 4) + 0.0) for value in (x, y))
    return " ".join(parts)


class Program:
    """A font program as fontTools reads it: glyph order, outline text and advance by glyph ID."""

    def __init__(self, path):
        self.path = path
        with open(path, "rb") as stream:
            self.data = stream.read()
        self.size = len(self.data)
        self.outlines = {}
        self.font = None
        if path.endswith(".cff"):
            self.font_set = cffLib.CFFFontSet()
            self.font_set.decompile(io.BytesIO(self.data), None)
            self.order = list(self.font_set.topDictIndex[0].charset)
            self.kind = "bare CFF"
        else:
            # checkChecksums=2 makes fontTools raise for a table whose checksum is wrong.
            self.font = TTFont(path, checkChecksums=2)
            self.order = self.font.getGlyphOrder()
            self.kind = "TrueType" if "glyf" in self.font else "OpenType CFF"
            self.font_set = self.font["CFF "].cff if "CFF " in self.font else None

    def outline(self, glyph_id):
        if glyph_id not in self.outlines:
            self.outlines[glyph_id] = self.draw(glyph_id)
        return self.outlines[glyph_id]

    def draw(self, glyph_id):
        name = self.order[glyph_id]
        if self.font_set is not None:
            char_strings = self.font_set.topDictIndex[0].CharStrings
            pen = OutlinePen(char_strings)
            char_strings.getItemAndSelector(name)[0].draw(pen)
        else:
            # Glyph.getCoordinates applies component transforms, scaled offsets and point matching, and the points then draw
            # as one simple glyph. The glyph pen of fontTools cannot draw a component that is placed by matching points.
            table = self.font["glyf"]
            coordinates, end_points, flags = table[name].getCoordinates(table)
            flat = glyf_module.Glyph()
            flat.numberOfContours = len(end_points)
            flat.coordinates = coordinates
            flat.endPtsOfContours = end_points
            flat.flags = flags
            pen = OutlinePen()
            if end_points:
                flat.draw(pen, table)
        return outline_text(pen.commands)

    def advance(self, glyph_id):
        name = self.order[glyph_id]
        if self.font is not None and "hmtx" in self.font:
            return self.font["hmtx"][name][0]
        if self.font_set is not None:
            return self.font_set.topDictIndex[0].CharStrings.getItemAndSelector(name)[0].width
        return None

    def file_problems(self):
        """The rules of the sfnt structure that fontTools does not raise for."""
        problems = []
        if self.font is None:
            return problems
        data = self.data
        count = struct.unpack(">H", data[4:6])[0]
        if len(data) % 4:
            problems.append("the file length %d is not a multiple of 4" % len(data))
        previous = b""
        head_offset = None
        for i in range(count):
            tag, _, offset, length = struct.unpack(">4sIII", data[12 + 16 * i:12 + 16 * i + 16])
            if tag <= previous:
                problems.append("the directory is not in the order of the tags at %r" % tag)
            previous = tag
            if offset % 4:
                problems.append("%r starts at %d, which is not a multiple of 4" % (tag, offset))
            if tag == b"head":
                head_offset = offset
        if head_offset is not None:
            total = sum(struct.unpack(">%dI" % (len(data) // 4), data[:len(data) // 4 * 4])) & 0xFFFFFFFF
            if total != 0xB1B0AFBA:
                problems.append("the words of the file sum to %08X, and 'head' should make it B1B0AFBA" % total)
        return problems

    def tags(self):
        return sorted(self.font.reader.tables) if self.font is not None else []


def check(original, subset, kept, label):
    """Returns (problems, needed, emptied, checked): the problems, the glyphs that were not asked for and keep their outline because
    a kept glyph needs them, the glyphs that had an outline and are empty, and the number of glyphs that it compared."""
    problems = []
    if len(subset.order) != len(original.order):
        problems.append("%s: %d glyphs, and the font has %d" % (label, len(subset.order), len(original.order)))
        return problems, 0, 0, 0
    if subset.order != original.order:
        problems.append("%s: the glyph names are not the glyph names of the font" % label)
    problems.extend("%s: %s" % (label, problem) for problem in subset.file_problems())

    wanted = set(kept) | {0}
    closure = 0
    dropped = 0
    checked = 0
    # Of a font with more than MAX_CHECKED glyphs, every glyph that was asked for and a stride of the others.
    stride = max(1, len(original.order) // MAX_CHECKED)
    for glyph_id in range(len(original.order)):
        if glyph_id not in wanted and glyph_id % stride:
            continue
        checked += 1
        before = original.outline(glyph_id)
        after = subset.outline(glyph_id)
        if glyph_id in wanted:
            if before != after:
                problems.append("%s: kept glyph %d draws '%s', and drew '%s'" % (label, glyph_id, after, before))
            if original.advance(glyph_id) != subset.advance(glyph_id):
                problems.append("%s: kept glyph %d has advance %s, and had %s" % (label, glyph_id, subset.advance(glyph_id), original.advance(glyph_id)))
        elif before == after:
            # A glyph with no outline gives no sign of whether it was kept.
            closure += before != ""
        elif after == "":
            dropped += 1
        else:
            problems.append("%s: glyph %d draws '%s', which is neither '%s' nor empty" % (label, glyph_id, after, before))
        if len(problems) >= 10:
            break
    return problems, closure, dropped, checked


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    directory = argv[1]
    selftest = "--selftest" in argv[2:]
    manifests = sorted(name for name in os.listdir(directory) if name.endswith(".json"))
    if not manifests:
        print("no subsets in %s: run the tests with EXPORT_FONT_SUBSETS set" % directory)
        return 2

    originals = {}
    failed = 0
    controls_missed = 0
    totals = {"subsets": 0, "glyphs": 0, "original": 0, "subset": 0}
    for name in manifests:
        with open(os.path.join(directory, name), encoding="utf-8") as stream:
            manifest = json.load(stream)
        if manifest["original"] not in originals:
            originals[manifest["original"]] = Program(manifest["original"])
        original = originals[manifest["original"]]
        subset = Program(os.path.join(directory, manifest["subset"]))
        kept = manifest["glyphs"]

        problems, closure, dropped, checked = check(original, subset, kept, name)
        totals["subsets"] += 1
        totals["glyphs"] += checked
        totals["original"] += original.size
        totals["subset"] += subset.size
        status = "FAIL" if problems else "ok"
        print("%-4s %-62s %-12s %5d glyphs, %5d asked, %3d needed by them, %5d emptied; %8d -> %8d bytes" % (
            status, name[:-5], original.kind, len(original.order), len(set(kept) - {0}), closure, dropped, original.size, subset.size))
        for problem in problems:
            print("     " + problem)
        failed += bool(problems)

        if selftest and dropped:
            stride = max(1, len(original.order) // MAX_CHECKED)
            swapped, _, _, _ = check(subset, original, kept, name + " (roles swapped)")
            claimed, _, _, _ = check(original, subset, range(0, len(original.order), stride), name + " (all glyphs claimed)")
            for control, result in (("roles swapped", swapped), ("all glyphs claimed", claimed)):
                if not result:
                    print("     CONTROL MISSED: %s did not fail for %s" % (control, name))
                    controls_missed += 1

    print()
    print("%d subsets, %d failed; %d glyph comparisons; %d -> %d bytes in total" % (
        totals["subsets"], failed, totals["glyphs"], totals["original"], totals["subset"]))
    if selftest:
        print("controls: %s" % ("every seeded error was caught" if not controls_missed else "%d missed" % controls_missed))
    return 1 if failed or controls_missed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
