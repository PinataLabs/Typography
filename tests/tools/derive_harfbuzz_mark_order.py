#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes HarfBuzzMarkOrder.txt, the order in which HarfBuzz sorts combining marks and where it places them.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/derive_harfbuzz_mark_order.py

Before it applies GSUB, HarfBuzz sorts each run of combining marks. It does not sort by
Canonical_Combining_Class itself. It moves some classes, and a few characters, to another place
in the order. For example it sorts the Hebrew dagesh (class 21) before the vowel points (10 to 20).

The script does not read the HarfBuzz source. It builds a font that has every character with a
non-zero Canonical_Combining_Class in UnicodeData.txt, and shapes each of them after a base with one
mark of each class, in both orders. Where HarfBuzz gives the same order for both inputs, that is the
sorted order. From these results the script writes the classes, and the characters that HarfBuzz
sorts apart from their class, from the first sorted to the last.

The Arabic shaper also moves the modifier combining marks, such as U+0654 HAMZA ABOVE, to the
start of the run. The script finds them in the same way and lists them in the file.

In a font without a GPOS table, HarfBuzz places each mark on its base from the boxes of the two glyphs.
The side of the base depends on the class of the mark, and on the character for a few Thai and Lao marks.
The script shapes each mark after a base in a second font, whose base and mark boxes make each side
give a different offset, and lists the side of each class and of each character that differs from its class.

The Unicode trie generator reads the file. The output is deterministic for one HarfBuzz version
and one UnicodeData.txt. The file records the HarfBuzz version that wrote it.
"""

import io
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
RULES = os.path.join(ROOT, "src", "PinataLabs.Typography.UnicodeTrieGenerator", "Rules")
OUTPUT = os.path.join(RULES, "HarfBuzzMarkOrder.txt")

BASE = ord("x")
BEH, FATHA, KASRA = 0x0628, 0x064E, 0x0650

# The placement font. The base is 600 wide and 700 high. The box of a mark is 200 wide, 100 from its origin,
# and lies inside the height of the base, so HarfBuzz must move it up or down by a different amount for each side.
UNITS_PER_EM = 1000
ADVANCE = 600
BASE_BOX = (0, 0, 600, 700)
MARK_BOX = (100, 300, 300, 400)
GAP = UNITS_PER_EM // 16
HORIZONTAL = {
    (ADVANCE - 200) // 2 - 100: "center",
    -100: "left",
    ADVANCE - 200 - 100: "right",
    ADVANCE - 200 // 2 - 100: "double",
}
VERTICAL = {
    0: "none",
    -GAP - MARK_BOX[3]: "below",
    -MARK_BOX[3]: "attached-below",
    BASE_BOX[3] + GAP - MARK_BOX[1]: "above",
    BASE_BOX[3] - MARK_BOX[1]: "attached-above",
}


def read_unicode_data():
    classes, decomposed, unclassed_marks = {}, set(), []
    with open(os.path.join(RULES, "UnicodeData.txt"), encoding="utf-8") as file:
        for line in file:
            fields = line.split(";")
            code_point = int(fields[0], 16)
            if fields[3] != "0":
                classes[code_point] = int(fields[3])
            elif fields[2].startswith("M") and not fields[1].endswith(", First>") and not fields[1].endswith(", Last>"):
                unclassed_marks.append(code_point)
            if fields[5] and not fields[5].startswith("<"):
                decomposed.add(code_point)
    return classes, decomposed, unclassed_marks


def box(x_min, y_min, x_max, y_max):
    pen = TTGlyphPen(None)
    pen.moveTo((x_min, y_min))
    pen.lineTo((x_min, y_max))
    pen.lineTo((x_max, y_max))
    pen.lineTo((x_max, y_min))
    pen.closePath()
    return pen.glyph()


def build_placement_font(marks):
    code_points = [BASE] + marks
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    boxes = {name: MARK_BOX for name in names}
    boxes["u%04X" % BASE] = BASE_BOX
    builder = FontBuilder(UNITS_PER_EM, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: box(*boxes[name]) for name in names})
    builder.setupHorizontalMetrics({name: (ADVANCE, boxes[name][0]) for name in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "MarkPlacement", "styleName": "Regular"})
    builder.setupOS2()
    builder.setupPost()
    data = io.BytesIO()
    builder.font.save(data)
    return hb.Font(hb.Face(data.getvalue())), {c: i + 1 for i, c in enumerate(code_points)}


def placement(font, glyph_ids, mark):
    """Gives the side of the base that HarfBuzz places the mark on, or None when it does not place the mark."""
    buffer = hb.Buffer()
    buffer.add_codepoints([BASE, mark])
    buffer.script = "Latn"
    buffer.direction = "ltr"
    buffer.language = "en"
    hb.shape(font, buffer)
    positions = [p for i, p in zip(buffer.glyph_infos, buffer.glyph_positions) if i.codepoint == glyph_ids[mark]]
    if len(positions) != 1 or positions[0].x_advance != 0:
        return None
    x = positions[0].x_offset + ADVANCE
    y = positions[0].y_offset
    if (x, y) == (0, 0):
        # HarfBuzz only took the advance of the mark away.
        return None
    if x not in HORIZONTAL or y not in VERTICAL:
        raise SystemExit("U+%04X: HarfBuzz gave an offset (%d, %d) that no side explains" % (mark, x, y))
    return HORIZONTAL[x], VERTICAL[y]


def build_font(code_points):
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({name: (600, 0) for name in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "MarkOrder", "styleName": "Regular"})
    builder.setupOS2()
    builder.setupPost()
    data = io.BytesIO()
    builder.font.save(data)
    return hb.Font(hb.Face(data.getvalue())), {c: i + 1 for i, c in enumerate(code_points)}


def shape(font, code_points, script, direction):
    buffer = hb.Buffer()
    buffer.add_codepoints(code_points)
    buffer.script = script
    buffer.direction = direction
    buffer.language = "en"
    hb.shape(font, buffer)
    glyphs = [info.codepoint for info in buffer.glyph_infos]
    return glyphs[::-1] if direction == "rtl" else glyphs


def main():
    classes, decomposed, unclassed_marks = read_unicode_data()
    # A character with a canonical decomposition is decomposed before the sort, so it has no place of its own.
    marks = sorted(c for c in classes if c not in decomposed)
    values = sorted(set(classes.values()))
    probes = {value: min(c for c in marks if classes[c] == value) for value in values}
    font, glyph_ids = build_font(sorted(set(marks) | {BASE, BEH}))

    def sorts_after(mark, probe):
        """True or False when HarfBuzz gives one order for both inputs, None when it keeps each input order."""
        if mark == probe:
            return None
        first = shape(font, [BASE, mark, probe], "Latn", "ltr")
        second = shape(font, [BASE, probe, mark], "Latn", "ltr")
        if first != second:
            if first != [glyph_ids[BASE], glyph_ids[mark], glyph_ids[probe]]:
                raise SystemExit("U+%04X and U+%04X: HarfBuzz gave an order that is not a stable sort" % (mark, probe))
            return None
        return first[2] == glyph_ids[mark]

    # The place of a mark is the number of classes that sort before it. A mark ties with its own class,
    # or with no class when HarfBuzz sorts it apart from its class.
    place, apart = {}, []
    for mark in marks:
        results = [sorts_after(mark, probes[value]) for value in values]
        ties = [value for value, result in zip(values, results) if result is None]
        place[mark] = sum(1 for result in results if result)
        if not ties:
            apart.append(mark)
        elif ties != [classes[mark]]:
            raise SystemExit("U+%04X ties with classes %s; the file format has no line for that" % (mark, ties))

    class_place = {value: place[probes[value]] for value in values}
    if sorted(class_place.values()) != list(range(len(values))):
        raise SystemExit("Two classes share a place in the order; the file format has no line for that")
    for mark in marks:
        if mark not in apart and place[mark] != class_place[classes[mark]]:
            raise SystemExit("U+%04X ties with its class but has another place" % mark)

    # A character apart from its class sorts after the classes that sort before it, so it follows the last of them.
    lines = [(class_place[value], 0, "ccc %d" % value) for value in values]
    lines += [(place[mark] - 1, mark, "char %04X" % mark) for mark in apart]

    modifiers = []
    for mark in marks:
        vowel = {220: KASRA, 230: FATHA}.get(classes[mark])
        if vowel is None or mark == vowel:
            continue
        glyphs = shape(font, [BEH, vowel, mark], "Arab", "rtl")
        if glyphs.index(glyph_ids[mark]) < glyphs.index(glyph_ids[vowel]):
            modifiers.append(mark)

    placement_font, placement_ids = build_placement_font(marks + unclassed_marks)
    placements = {mark: placement(placement_font, placement_ids, mark) for mark in marks + unclassed_marks}
    class_placement = {value: placements[probes[value]] for value in values}
    if None in class_placement.values():
        raise SystemExit("HarfBuzz does not place the marks of a class; the file format has no line for that")
    placed_apart = [mark for mark in marks if placements[mark] != class_placement[classes[mark]]]
    placed_apart += [mark for mark in unclassed_marks if placements[mark] is not None]
    if any(placements[mark] is None for mark in placed_apart):
        raise SystemExit("HarfBuzz does not place a mark of a class that it places; the file format has no line for that")

    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as file:
        file.write("# HarfBuzzMarkOrder.txt\n")
        file.write("# Written by tests/tools/derive_harfbuzz_mark_order.py from HarfBuzz %s. Do not edit.\n" % hb.version_string())
        file.write("#\n")
        file.write("# The order in which HarfBuzz sorts a run of combining marks, from the first sorted to the last.\n")
        file.write("#   ccc <value>            every character with that Canonical_Combining_Class\n")
        file.write("#   char <code point>      one character that HarfBuzz sorts apart from its class\n")
        file.write("#\n")
        file.write("# The marks that the Arabic shaper of HarfBuzz moves to the start of a run, after the sort.\n")
        file.write("#   modifier <code point>\n")
        file.write("#\n")
        file.write("# Where HarfBuzz places a mark on its base in a font without a GPOS table: how it aligns the mark\n")
        file.write("# with the base (center, left, right, or double for a mark over two bases), and the side of the base\n")
        file.write("# (none, below, attached-below, above, attached-above). An attached mark touches the base.\n")
        file.write("#   place ccc <value> <alignment> <side>         every character with that Canonical_Combining_Class\n")
        file.write("#   place char <code point> <alignment> <side>   one character that HarfBuzz places apart from its class\n")
        file.write("\n")
        for _, _, text in sorted(lines):
            file.write(text + "\n")
        file.write("\n")
        for mark in modifiers:
            file.write("modifier %04X\n" % mark)
        file.write("\n")
        for value in values:
            file.write("place ccc %d %s %s\n" % (value, *class_placement[value]))
        for mark in sorted(placed_apart):
            file.write("place char %04X %s %s\n" % (mark, *placements[mark]))
    print(os.path.relpath(OUTPUT, ROOT))


if __name__ == "__main__":
    main()
