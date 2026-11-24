#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes UniversalShaper.harfbuzz.json, how HarfBuzz shapes text with its universal shaper.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_universal_shaper_reference.py

The script does not read the HarfBuzz source. It builds UniversalShaperJoining.ttf, which has every character of
the eleven scripts whose letters the universal shaper of HarfBuzz joins. Each character has a glyph for each of
the `isol`, `init`, `medi` and `fina` features, and for `fin2`, `fin3` and `med2`, which the universal shaper does
not apply. The reference holds the glyph IDs that HarfBuzz gives for:

* each character alone, after a letter that joins on both sides, before that letter, and between two of them.
  These four texts show the joining type that HarfBuzz gives the character.
* every text of two or three characters, from one character of each joining type and general category of the
  script.
* a Mongolian letter and a free variation selector, alone and between letters. Each of the four features has
  a ligature of the two, which HarfBuzz forms only when the selector has the feature of the letter.

The font has no other character that is Default_Ignorable_Code_Point. HarfBuzz takes the glyph of such a character
out of its output, and the library keeps it and does not draw it.
A text has only letters and marks of one direction. HarfBuzz takes the direction of a text from its script, and
the library takes it from the bidirectional class of each character, so the two can differ for a digit or a symbol.

UniversalShaperTests reads the reference.
The output is deterministic for one HarfBuzz version and one set of Unicode data files.
The reference records the HarfBuzz version that wrote it.
"""

import collections
import itertools
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
REFERENCE = os.path.join(FONTS, "UniversalShaper.harfbuzz.json")
JOINING_FONT = "UniversalShaperJoining.ttf"

# The scripts whose letters the universal shaper of HarfBuzz joins, with the name of their script in feature code.
# ShaperChoice.harfbuzz.json shows that these are the ones: a lone letter of them takes `isol`.
JOINING_SCRIPTS = {
    "Adlam": "adlm",
    "Chorasmian": "chrs",
    "Hanifi_Rohingya": "rohg",
    "Mandaic": "mand",
    "Manichaean": "mani",
    "Mongolian": "mong",
    "Nko": "nko",
    "Old_Uyghur": "ougr",
    "Phags_Pa": "phag",
    "Psalter_Pahlavi": "phlp",
    "Sogdian": "sogd",
}
FORMS = ["isol", "init", "medi", "fina", "fin2", "fin3", "med2"]
LIGATURE_FORMS = FORMS[:4]

MONGOLIAN_LETTER = 0x1820
MONGOLIAN_SELECTORS = [0x180B, 0x180C, 0x180D, 0x180F]


def read_joining_types():
    types = {}
    with open(os.path.join(RULES, "ArabicShaping.txt"), encoding="utf-8") as file:
        for line in file:
            match = re.match(r"([0-9A-F]+);[^;]+;\s*(\w);", line)
            if match:
                types[int(match.group(1), 16)] = match.group(2)
    return types


def read_bidi_classes():
    with open(os.path.join(RULES, "UnicodeData.txt"), encoding="utf-8") as file:
        return {int(fields[0], 16): fields[4] for fields in (line.split(";") for line in file)}


def read_default_ignorable():
    ignorable = set()
    with open(os.path.join(RULES, "DerivedCoreProperties.txt"), encoding="utf-8") as file:
        for line in file:
            match = re.match(r"([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*Default_Ignorable_Code_Point ", line)
            if match:
                ignorable.update(range(int(match.group(1), 16), int(match.group(2) or match.group(1), 16) + 1))
    return ignorable


def read_script_characters(ignorable):
    """Gives the characters of each joining script that HarfBuzz keeps in its output, each with its general category."""
    characters = collections.defaultdict(list)
    with open(os.path.join(RULES, "Scripts.txt"), encoding="utf-8") as file:
        for line in file:
            match = re.match(r"([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(\w+) # (\S\S)", line)
            if match and match.group(3) in JOINING_SCRIPTS:
                for code_point in range(int(match.group(1), 16), int(match.group(2) or match.group(1), 16) + 1):
                    if code_point not in ignorable:
                        characters[match.group(3)].append((code_point, match.group(4)))
    return characters


def build_joining_font(code_points):
    # The glyphs of each form are in the order of their characters, so each feature is one substitution by a delta.
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    names += ["u%04X.%s" % (c, form) for form in FORMS for c in code_points]
    names += ["u%04X" % c for c in MONGOLIAN_SELECTORS]
    names += ["u%04X_u%04X.%s" % (MONGOLIAN_LETTER, c, form) for form in LIGATURE_FORMS for c in MONGOLIAN_SELECTORS]
    features = "languagesystem DFLT dflt;\n"
    features += "".join(f"languagesystem {script} dflt;\n" for script in JOINING_SCRIPTS.values())
    for form in FORMS:
        features += f"feature {form} {{\n"
        if form in LIGATURE_FORMS:
            # The ligatures come first, so that their lookup is applied before the letter takes its form.
            features += "".join(
                "    sub u%04X u%04X by u%04X_u%04X.%s;\n" % (MONGOLIAN_LETTER, c, MONGOLIAN_LETTER, c, form)
                for c in MONGOLIAN_SELECTORS)
        features += "".join("    sub u%04X by u%04X.%s;\n" % (c, c, form) for c in code_points)
        features += f"}} {form};\n"

    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in [*code_points, *MONGOLIAN_SELECTORS]})
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({name: (600, 0) for name in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": os.path.splitext(JOINING_FONT)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    addOpenTypeFeaturesFromString(builder.font, features)
    path = os.path.join(FONTS, JOINING_FONT)
    builder.font.save(path)
    with open(path, "rb") as file:
        return hb.Font(hb.Face(file.read()))


def shape(font, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    return [info.codepoint for info in buffer.glyph_infos]


def joining_texts(characters, types, bidi):
    """Gives the texts of one script."""
    def joining_type(code_point, category):
        return types.get(code_point, "T" if category in ("Mn", "Me", "Cf") else "U")

    dual = next(c for c, category in characters if joining_type(c, category) == "D")
    texts = []
    for code_point, _ in characters:
        texts += [[code_point], [dual, code_point], [code_point, dual], [dual, code_point, dual]]

    samples = {}
    for code_point, category in characters:
        samples.setdefault((joining_type(code_point, category), category), code_point)
    for length in (2, 3):
        texts += [list(text) for text in itertools.product(samples.values(), repeat=length)]

    def has_one_direction(text):
        classes = {bidi[c] for c in text}
        return classes <= {"L", "NSM"} and "L" in classes or classes <= {"R", "AL", "NSM"} and classes != {"NSM"}

    return list(dict.fromkeys("".join(map(chr, text)) for text in texts if has_one_direction(text)))


def selector_texts():
    letter = MONGOLIAN_LETTER
    texts = []
    for selector in MONGOLIAN_SELECTORS:
        pair = [letter, selector]
        texts += [pair, [letter, *pair], [*pair, letter], [letter, *pair, letter], [*pair, *pair]]
    return ["".join(map(chr, text)) for text in texts]


def main():
    types = read_joining_types()
    bidi = read_bidi_classes()
    characters = read_script_characters(read_default_ignorable())
    font = build_joining_font(sorted(c for script in characters.values() for c, _ in script))
    texts = [text for script in sorted(characters) for text in joining_texts(characters[script], types, bidi)]
    joining = [[text, shape(font, text)] for text in texts + selector_texts()]

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "joiningFont": %s,\n' % json.dumps(JOINING_FONT))
        file.write('  "joining": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in joining))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(joining), "joining rows")


if __name__ == "__main__":
    main()
