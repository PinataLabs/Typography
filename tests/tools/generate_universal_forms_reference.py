#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes UniversalForms.harfbuzz.json, the isol, init, medi and fina forms that HarfBuzz gives the syllables of a
script whose letters do not join.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_universal_forms_reference.py

The universal shaper of HarfBuzz applies `isol`, `init`, `medi` and `fina` to every script. For the scripts whose
letters do not join, it gives them by syllable: a run of syllables that join takes `init` on the first, `medi` on
the middle ones and `fina` on the last, and a syllable alone takes `isol`. Characters outside every syllable, such
as WORD JOINER, end the run.

The script builds UniversalForms.ttf. It has Balinese, Tibetan and Javanese letters and signs, the space, digits,
ZWJ, ZWNJ, WORD JOINER and the dotted circle. Its GSUB table has the four features for `bali`, `tibt`, `java` and
`DFLT`, and each feature gives every glyph a glyph of its own. The reference holds the glyph IDs and where HarfBuzz
draws each glyph for texts of one to four syllables. UniversalShaperTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import itertools
import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
FONT = "UniversalForms.ttf"
REFERENCE = os.path.join(FONTS, "UniversalForms.harfbuzz.json")

ZWNJ, ZWJ, WJ, DOTTED_CIRCLE = 0x200C, 0x200D, 0x2060, 0x25CC
FORMS = ["isol", "init", "medi", "fina"]

# Syllables of each script: a letter, a letter with a vowel sign, a conjunct, a sign alone (a broken cluster) and
# a digit, and characters between syllables.
SCRIPTS = {
    "bali": [[0x1B13], [0x1B13, 0x1B36], [0x1B13, 0x1B3E], [0x1B13, 0x1B44, 0x1B13], [0x1B36], [0x1B51], [0x1B13, 0x1B03]],
    "tibt": [[0x0F40], [0x0F40, 0x0F72], [0x0F40, 0x0F90], [0x0F72], [0x0F21], [0x0F0B]],
    "java": [[0xA98F], [0xA98F, 0xA9B6], [0xA98F, 0xA9C0, 0xA98F], [0xA9B6], [0xA9D1]],
}
BETWEEN = [[0x20], [ZWNJ], [ZWJ], [WJ], [0x30]]


def code_points():
    characters = {DOTTED_CIRCLE, ZWNJ, ZWJ, WJ, 0x20, 0x30}
    for syllables in SCRIPTS.values():
        for syllable in syllables:
            characters.update(syllable)
    return sorted(characters)


def build(characters):
    names = [".notdef"] + ["u%04X" % c for c in characters]
    forms = ["u%04X.%s" % (c, form) for form in FORMS for c in characters]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names + forms)
    builder.setupCharacterMap({c: "u%04X" % c for c in characters})
    builder.setupGlyf({name: Glyph() for name in names + forms})
    builder.setupHorizontalMetrics({name: (600, 0) for name in names + forms})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "UniversalForms", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    feature_code = ["languagesystem DFLT dflt;"] + ["languagesystem %s dflt;" % script for script in SCRIPTS]
    for form in FORMS:
        feature_code.append("feature %s {" % form)
        feature_code += ["  sub u%04X by u%04X.%s;" % (c, c, form) for c in characters]
        feature_code.append("} %s;" % form)
    addOpenTypeFeaturesFromString(builder.font, "\n".join(feature_code))
    path = os.path.join(FONTS, FONT)
    builder.font.save(path)
    with open(path, "rb") as file:
        return hb.Font(hb.Face(file.read()))


def shape(font, text):
    """Gives the glyph ID and the place of each glyph, in font units from the start of the line."""
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
        x += position.x_advance
    return glyphs


def main():
    font = build(code_points())
    texts = []
    for syllables in SCRIPTS.values():
        letter = syllables[0]
        for count in range(1, 4):
            texts += [sum(combination, []) for combination in itertools.product(syllables, repeat=count) if count < 3 or combination[0] == letter]
        for between in BETWEEN:
            texts += [letter + between + letter, letter + letter + between + letter + letter]
        texts.append(letter * 4)

    rows = []
    for text in dict.fromkeys("".join(map(chr, text)) for text in texts):
        rows.append([text, shape(font, text)])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "font": %s,\n' % json.dumps(FONT))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
