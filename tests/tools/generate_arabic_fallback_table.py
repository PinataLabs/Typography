#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes the table that the library uses to shape Arabic in a font without the GSUB features of Arabic.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_arabic_fallback_table.py

When the GSUB table of a font has none of the `isol`, `fina`, `medi` and `init` features for Arabic, HarfBuzz gives
each letter the presentation form that the font has for its place in the word, and then ligates some of those forms.
The forms of each letter come from the compatibility decompositions of UnicodeData.txt. HarfBuzz uses only some of
the ligatures there. The script finds them by shaping each one in a probe font that has every Arabic letter, mark,
presentation form and ligature, and keeps the ligatures that HarfBuzz gives.

The script writes src/PinataLabs.Typography/Tables/AdvancedTypographic/Shapers/ArabicFallbackTable.Generated.cs.
The output is deterministic for one HarfBuzz version and one UnicodeData.txt. NormalizationTests checks the library
against HarfBuzz with the fonts and cases of generate_normalization_reference.py.
"""

import os
import tempfile

import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
RULES = os.path.join(ROOT, "src", "PinataLabs.Typography.UnicodeTrieGenerator", "Rules")
OUTPUT = os.path.join(ROOT, "src", "PinataLabs.Typography", "Tables", "AdvancedTypographic", "Shapers", "ArabicFallbackTable.Generated.cs")
FORMS = ("isolated", "final", "initial", "medial")
TATWEEL = 0x0640
SHADDA = 0x0651

# The text that puts a run of letters in each form, with tatweel to join it on the side that the form joins.
CONTEXTS = {"isolated": ("", ""), "final": ("ـ", ""), "initial": ("", "ـ"), "medial": ("ـ", "ـ")}


def read_unicode_data():
    rows = {}
    with open(os.path.join(RULES, "UnicodeData.txt"), encoding="utf-8") as file:
        for line in file:
            fields = line.split(";")
            rows[int(fields[0], 16)] = fields
    return rows


def presentation_forms(rows):
    """Gives each character with an Arabic form decomposition its form and parts."""
    forms = {}
    for code_point, fields in rows.items():
        decomposition = fields[5]
        if decomposition.startswith("<") and decomposition[1:decomposition.index(">")] in FORMS:
            tag, *parts = decomposition.split()
            forms[code_point] = (tag[1:-1], [int(part, 16) for part in parts])
    return forms


def probe_font(code_points):
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({name: (600, 0) for name in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "ArabicFallbackProbe", "styleName": "Regular"})
    builder.setupOS2()
    builder.setupPost(keepGlyphNames=False)
    with tempfile.TemporaryDirectory() as directory:
        path = os.path.join(directory, "probe.ttf")
        builder.font.save(path)
        with open(path, "rb") as file:
            return hb.Font(hb.Face(file.read()))


def main():
    rows = read_unicode_data()
    forms = presentation_forms(rows)
    combining = {c: int(fields[3]) for c, fields in rows.items()}
    letters = {}
    for code_point, (form, parts) in forms.items():
        if len(parts) == 1 and 0x0600 <= parts[0] <= 0x06FF:
            letters.setdefault(parts[0], {}).setdefault(form, code_point)
    first, last = min(letters), max(letters)

    arabic = [c for c, fields in rows.items() if 0x0600 <= c <= 0x06FF and fields[2] in ("Lo", "Lm", "Mn")]
    probe_code_points = sorted(set(arabic) | set(forms) | {0x20, TATWEEL})
    font = probe_font(probe_code_points)
    code_point_of = {i + 1: c for i, c in enumerate(probe_code_points)}

    def shape(text):
        buffer = hb.Buffer()
        buffer.add_str(text)
        buffer.guess_segment_properties()
        hb.shape(font, buffer)
        return [code_point_of.get(info.codepoint, 0) for info in buffer.glyph_infos]

    letter_ligatures, mark_ligatures = [], []
    for code_point, (form, parts) in sorted(forms.items()):
        if len(parts) < 2 or any(part not in probe_code_points for part in parts):
            continue
        if parts[0] in (0x20, TATWEEL):
            # HarfBuzz sorts shadda before the vowel marks, so a mark ligature starts with shadda.
            marks = sorted(parts[1:], key=lambda c: (c != SHADDA, combining.get(c, 0)))
            if code_point in shape("ب" + "".join(map(chr, marks))):
                mark_ligatures.append((code_point, marks))
            continue
        before, after = CONTEXTS[form]
        if code_point in shape(before + "".join(map(chr, parts)) + after):
            letter_ligatures.append((code_point, FORMS.index(form), parts))

    # A longer ligature is tried before a shorter one with the same first letter.
    letter_ligatures.sort(key=lambda ligature: (ligature[2][0], -len(ligature[2]), ligature[0]))

    lines = [
        "// Copyright (c) PinataLabs.",
        "// Licensed under the Apache License, Version 2.0.",
        "",
        "// <auto-generated />",
        "// Written by tests/tools/generate_arabic_fallback_table.py from UnicodeData.txt and HarfBuzz %s." % hb.version_string(),
        "using System;",
        "",
        "namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;",
        "",
        "internal static class ArabicFallbackTable",
        "{",
        "    public const int FirstLetter = 0x%04X;" % first,
        "",
        "    public const int LastLetter = 0x%04X;" % last,
        "",
        "    // The isolated, final, initial and medial form of each letter from FirstLetter, or 0 when it has none.",
        "    public static ReadOnlySpan<ushort> Forms => new ushort[]",
        "    {",
    ]
    for letter in range(first, last + 1):
        values = ", ".join("0x%04X" % letters.get(letter, {}).get(form, 0) for form in FORMS)
        lines.append("        %s, // %04X" % (values, letter))
    lines += [
        "    };",
        "",
        "    // Each ligature of letters: the ligature, its form as an index of the forms above, the number of letters,",
        "    // and the letters.",
        "    public static ReadOnlySpan<ushort> LetterLigatures => new ushort[]",
        "    {",
    ]
    for code_point, form, parts in letter_ligatures:
        lines.append("        0x%04X, %d, %d, %s," % (code_point, form, len(parts), ", ".join("0x%04X" % p for p in parts)))
    lines += [
        "    };",
        "",
        "    // Each ligature of marks: the ligature, the number of marks, and the marks in the order that they sort in.",
        "    public static ReadOnlySpan<ushort> MarkLigatures => new ushort[]",
        "    {",
    ]
    for code_point, marks in mark_ligatures:
        lines.append("        0x%04X, %d, %s," % (code_point, len(marks), ", ".join("0x%04X" % m for m in marks)))
    lines += ["    };", "}", ""]
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as file:
        file.write("\n".join(lines))
    print(os.path.relpath(OUTPUT, ROOT), last - first + 1, "letters", len(letter_ligatures), "letter ligatures", len(mark_ligatures), "mark ligatures")


if __name__ == "__main__":
    main()
