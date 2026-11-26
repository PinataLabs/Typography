#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes Syllables.harfbuzz.json, how HarfBuzz forms syllables around joiners and invalid vowel sequences.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_use_syllable_reference.py

The script does not read the HarfBuzz source. It builds fonts that have no GSUB table, so only the shaper changes
the order and the number of the glyphs:

* SyllablesBalinese.ttf has a Balinese letter, a vowel sign above it and one in front of it, the dotted circle,
  the space, and characters that the universal shaper of HarfBuzz leaves out when it finds syllables: the
  combining grapheme joiner, ZWJ and ZWNJ. Those three have no advance. The `joiners` rows
  hold each of them between the letter and each sign, before a letter, and before a sign with no letter.
* SyllablesInvalid.ttf has the characters of ms-use/IndicShapingInvalidCluster.txt and the dotted circle. The
  `invalid` rows hold each sequence of that file, which HarfBuzz breaks with a dotted circle. It shapes the text in
  the script of the first character: Devanagari and the other Indic scripts with its Indic shaper, and Brahmi,
  Khojki, Khudawadi, Tirhuta and Takri with its universal shaper.

The script shapes with the default buffer flags. HarfBuzz then draws each default-ignorable character with the glyph
of the space and no advance, and so does the library (see issue #110).

Each row holds the font, the ISO 15924 code of the script, the text, and the glyph ID of each glyph that HarfBuzz
gives with where it draws it, in font units from the start of the line.

SyllableTests reads the reference.
The output is deterministic for one HarfBuzz version and one set of Unicode data files.
The reference records the HarfBuzz version that wrote it.
"""

import json
import os
import re

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
RULES = os.path.join(ROOT, "src", "PinataLabs.Typography.UnicodeTrieGenerator", "Rules")
REFERENCE = os.path.join(FONTS, "Syllables.harfbuzz.json")

DOTTED_CIRCLE = 0x25CC
SPACE = 0x20

# KA, the vowel sign ULU above the base and TALING in front of it.
BALINESE_LETTER = 0x1B13
BALINESE_SIGNS = [0x1B36, 0x1B3E]

# COMBINING GRAPHEME JOINER, ZWJ and ZWNJ. A variation selector is left out: the library takes it with the
# character before it when it maps characters to glyphs, so its glyph is not in the output.
JOINERS = [0x034F, 0x200D, 0x200C]

def read_script_codes():
    """Gives the ISO 15924 code of each script name of Scripts.txt."""
    codes = {}
    with open(os.path.join(RULES, "PropertyValueAliases.txt"), encoding="utf-8") as file:
        for line in file:
            match = re.match(r"sc\s*;\s*(\w+)\s*;\s*(\w+)", line)
            if match:
                codes[match.group(2)] = match.group(1)
    return codes


def read_scripts():
    scripts = {}
    with open(os.path.join(RULES, "Scripts.txt"), encoding="utf-8") as file:
        for line in file:
            match = re.match(r"([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(\w+)", line)
            if match:
                for code_point in range(int(match.group(1), 16), int(match.group(2) or match.group(1), 16) + 1):
                    scripts[code_point] = match.group(3)
    return scripts


def read_invalid_clusters():
    sequences = []
    with open(os.path.join(RULES, "IndicShapingInvalidCluster.txt"), encoding="utf-8") as file:
        for line in file:
            fields = line.split("#")[0].split(";")[0].split()
            if fields:
                sequences.append([int(c, 16) for c in fields])
    return sequences


def build(file_name, code_points, zero_advance=()):
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({"u%04X" % c: (0 if c in zero_advance else 600, 0) for c in code_points} | {".notdef": (600, 0)})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    path = os.path.join(FONTS, file_name)
    builder.font.save(path)
    with open(path, "rb") as file:
        return hb.Font(hb.Face(file.read()))


def shape(font, script, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.script = script
    buffer.direction = "ltr"
    hb.shape(font, buffer)
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append([info.codepoint, x + position.x_offset])
        x += position.x_advance
    return glyphs


def main():
    joiners = []
    file_name = "SyllablesBalinese.ttf"
    font = build(file_name, sorted({SPACE, DOTTED_CIRCLE, BALINESE_LETTER, *BALINESE_SIGNS, *JOINERS}), JOINERS)
    texts = []
    for joiner in JOINERS:
        texts += [[BALINESE_LETTER, joiner, sign] for sign in BALINESE_SIGNS]
        texts += [[BALINESE_LETTER, joiner, BALINESE_LETTER], [joiner, BALINESE_SIGNS[0]]]
    for text in texts:
        text = "".join(map(chr, text))
        joiners.append([file_name, "Bali", text, shape(font, "Bali", text)])

    invalid = []
    file_name = "SyllablesInvalid.ttf"
    scripts = read_scripts()
    codes = read_script_codes()
    sequences = read_invalid_clusters()
    font = build(file_name, sorted({DOTTED_CIRCLE, *(c for s in sequences for c in s)}))
    for sequence in sequences:
        script = codes[scripts[sequence[0]]]
        text = "".join(map(chr, sequence))
        invalid.append([file_name, script, text, shape(font, script, text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        for name, rows in (("joiners", joiners), ("invalid", invalid)):
            file.write('  "%s": [\n' % name)
            file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
            file.write("\n  ]%s\n" % ("," if name == "joiners" else ""))
        file.write("}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(joiners), "joiner rows,", len(invalid), "invalid rows")


if __name__ == "__main__":
    main()
