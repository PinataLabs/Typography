#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes HarfBuzzMarkOrder.txt, the order in which HarfBuzz sorts combining marks.

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

The Unicode trie generator reads the file. The output is deterministic for one HarfBuzz version
and one UnicodeData.txt. The file records the HarfBuzz version that wrote it.
"""

import io
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
RULES = os.path.join(ROOT, "src", "PinataLabs.Typography.UnicodeTrieGenerator", "Rules")
OUTPUT = os.path.join(RULES, "HarfBuzzMarkOrder.txt")

BASE = ord("x")
BEH, FATHA, KASRA = 0x0628, 0x064E, 0x0650


def read_unicode_data():
    classes, decomposed = {}, set()
    with open(os.path.join(RULES, "UnicodeData.txt"), encoding="utf-8") as file:
        for line in file:
            fields = line.split(";")
            code_point = int(fields[0], 16)
            if fields[3] != "0":
                classes[code_point] = int(fields[3])
            if fields[5] and not fields[5].startswith("<"):
                decomposed.add(code_point)
    return classes, decomposed


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
    classes, decomposed = read_unicode_data()
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
        file.write("\n")
        for _, _, text in sorted(lines):
            file.write(text + "\n")
        file.write("\n")
        for mark in modifiers:
            file.write("modifier %04X\n" % mark)
    print(os.path.relpath(OUTPUT, ROOT))


if __name__ == "__main__":
    main()
