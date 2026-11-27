#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes Syllables.harfbuzz.json, how HarfBuzz forms syllables around joiners and invalid vowel sequences.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_use_syllable_reference.py

The script does not read the HarfBuzz source. It builds fonts that have no GDEF table, so HarfBuzz takes the class of
each glyph from the Unicode properties of its character. Only SyllablesCircles.ttf and SyllablesCircleProperties.ttf
have a GSUB table, so in the other fonts only the shaper changes the order and the number of the glyphs:

* SyllablesBalinese.ttf has a Balinese letter, a vowel sign above it and one in front of it, the dotted circle,
  the space, and characters that the universal shaper of HarfBuzz leaves out when it finds syllables: the
  combining grapheme joiner, ZWJ and ZWNJ. Those three have no advance. The `joiners` rows
  hold each of them between the letter and each sign, before a letter, and before a sign with no letter.
* SyllablesInvalid.ttf has the characters of ms-use/IndicShapingInvalidCluster.txt and the dotted circle. The
  `invalid` rows hold each sequence of that file, which HarfBuzz breaks with a dotted circle. It shapes the text in
  the script of the first character: Devanagari and the other Indic scripts with its Indic shaper, and Brahmi,
  Khojki, Khudawadi, Tirhuta and Takri with its universal shaper.
* SyllablesCircles.ttf and SyllablesNoCircle.ttf show what the dotted circle of such a sequence is. The `circles`
  rows hold texts where it matters. HarfBuzz copies the character after the circle to make it, so the circle has
  the Unicode properties of that character. Before the Sinhala virama, which has combining class 9, the circle
  sorts after U+0334, which has class 1. Before the Brahmi vowel sign AA, a nonspacing mark, a lookup that ignores
  marks skips the circle: the calt lookup of SyllablesCircles.ttf, under the scripts brah and sinh, changes KA to
  the vowel sign AA after the letter A and the marks after it. SyllablesNoCircle.ttf has no dotted circle, and
  HarfBuzz inserts .notdef in its place.
* SyllablesCircleProperties.ttf shows two more of those properties, each beside a dotted circle in the text, which
  has its own. Its init lookup, under the script gur2, changes the Gurmukhi vowel sign I. That sign goes before
  its base, and takes init at the start of a word: after a character that is not a letter, a mark or a format
  character. After the circle of the invalid sequence U+0A72 U+0A3F, which has the properties of the sign, it
  takes no init. After a dotted circle in the text, a symbol, it does. Its mark lookup, under the script sinh,
  places the Sinhala virama on the letter before it and on the dotted circle at different anchors. The circle of
  the invalid sequence U+0D91 U+0DCA is a mark, so the virama goes on the letter. A dotted circle in the text is a
  base, and takes the virama.

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
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
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


def build(file_name, code_points, zero_advance=(), features=None, extra_glyphs=()):
    names = [".notdef"] + ["u%04X" % c for c in code_points] + list(extra_glyphs)
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({"u%04X" % c: (0 if c in zero_advance else 600, 0) for c in code_points} | {name: (600, 0) for name in [".notdef", *extra_glyphs]})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    if features:
        addOpenTypeFeaturesFromString(builder.font, features)
        # A mark class makes feaLib build a GDEF table. Without one, HarfBuzz classes glyphs by their characters.
        if "GDEF" in builder.font:
            del builder.font["GDEF"]
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

    circles = []
    file_name = "SyllablesCircles.ttf"
    features = (
        "languagesystem DFLT dflt; languagesystem brah dflt; languagesystem sinh dflt;"
        " lookup Ka { lookupflag IgnoreMarks; sub u11005 u11013' by u11038; } Ka;"
        " feature calt { lookup Ka; } calt;")
    code_points = [DOTTED_CIRCLE, 0x0334, 0x0D91, 0x0D9A, 0x0DCA, 0x11005, 0x11013, 0x11038]
    font = build(file_name, code_points, features=features)
    for script, text in (("Sinh", [0x0D91, 0x0DCA, 0x0334]), ("Sinh", [0x0D9A, 0x0DCA, 0x0334]),
                         ("Brah", [0x11005, 0x11038, 0x11013]), ("Brah", [0x11005, 0x11013])):
        text = "".join(map(chr, text))
        circles.append([file_name, script, text, shape(font, script, text)])
    file_name = "SyllablesCircleProperties.ttf"
    features = (
        "languagesystem DFLT dflt; languagesystem gur2 dflt; languagesystem sinh dflt;"
        " feature init { sub u0A3F by u0A3F.init; } init;"
        " markClass u0DCA <anchor 0 0> @Virama;"
        " feature mark { pos base u0D91 <anchor 100 0> mark @Virama; pos base u25CC <anchor 300 0> mark @Virama; } mark;")
    code_points = [DOTTED_CIRCLE, 0x0A15, 0x0A3F, 0x0A72, 0x0D91, 0x0DCA]
    font = build(file_name, code_points, features=features, extra_glyphs=["u0A3F.init"])
    for script, text in (("Guru", [0x0A72, 0x0A3F, 0x0A15, 0x0A3F]), ("Guru", [DOTTED_CIRCLE, 0x0A3F, 0x0A15, 0x0A3F]),
                         ("Sinh", [0x0D91, 0x0DCA]), ("Sinh", [0x0D91, DOTTED_CIRCLE, 0x0DCA])):
        text = "".join(map(chr, text))
        circles.append([file_name, script, text, shape(font, script, text)])
    file_name = "SyllablesNoCircle.ttf"
    font = build(file_name, [0x0905, 0x0946])
    text = "\u0905\u0946"
    circles.append([file_name, "Deva", text, shape(font, "Deva", text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        for name, rows in (("joiners", joiners), ("invalid", invalid), ("circles", circles)):
            file.write('  "%s": [\n' % name)
            file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
            file.write("\n  ]%s\n" % ("" if name == "circles" else ","))
        file.write("}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(joiners), "joiner rows,", len(invalid), "invalid rows,",
          len(circles), "circle rows")


if __name__ == "__main__":
    main()
