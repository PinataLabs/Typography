#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes the fonts and the HarfBuzz reference for the normalization tests.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_normalization_reference.py

Before GSUB, HarfBuzz maps the characters of the text to the glyphs that the font has. It replaces a
base and a mark with the precomposed character when the font has it, it replaces a character that the
font lacks with its canonical decomposition when the font has the parts, and it sorts each run of marks.

The script builds four fonts in tests/PinataLabs.Typography.Tests/Fonts:

* Normalization.ttf has a small character set chosen for the named cases below. For example it has
  o, U+0301 and U+00F3, and it has T and U+0327 but not U+0162. Its GSUB table has only a `hang` script
  with a `liga` lookup that no case can match.
* NormalizationScan.ttf has every character of UnicodeData.txt that has a canonical decomposition, and
  every part of those decompositions. It has no Hangul syllables, and only the first CJK compatibility
  ideographs of each block.
* NormalizationParts.ttf has the same characters, except those that have a canonical decomposition.
* NormalizationBase.ttf has a, e, o, q and U+0323 only. The tests use it as a primary font that lacks marks,
  with Normalization.ttf as its fallback font. HarfBuzz has no font fallback, so the reference has no case for it.

The glyphs of the two scan fonts have no outline. The scan shapes each character alone in both fonts,
the two parts of each decomposition and the full decomposition in NormalizationScan.ttf, and each pair of
combining classes after a base. The reference holds the glyph IDs that HarfBuzz gives, in the order of
its output. NormalizationTests reads it.

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
RULES = os.path.join(ROOT, "src", "PinataLabs.Typography.UnicodeTrieGenerator", "Rules")
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "Normalization.harfbuzz.json")
CASE_FONT = "Normalization.ttf"
SCAN_FONT = "NormalizationScan.ttf"
PARTS_FONT = "NormalizationParts.ttf"
BASE_FONT = "NormalizationBase.ttf"

CASE_CHARACTERS = (
    " ATaeoqu"
    "̧̣̀́̈̊"
    "Åáóüạ"
    "कनऩरऱ़ाक़"
    "각가"
    "ابآأَّٓٔ"
)

BASE_CHARACTERS = " aeoq̣"

CASE_FEATURES = """
languagesystem DFLT dflt;
languagesystem hang dflt;
feature liga { script hang; sub u1100 u1100 u1100 by u1161; } liga;
"""

CASES = [
    ("ComposesBaseAndMark", "ó"),
    ("DecomposesCharacterTheFontLacks", "Ţ"),
    ("DecomposesToThePartsTheFontHas", "ë"),
    ("KeepsPartsWithoutPrecomposedGlyph", "ë"),
    ("KeepsPrecomposedCharacter", "á"),
    ("KeepsCharacterWithoutAllParts", "ç"),
    ("SortsStackedMarks", "q̣́"),
    ("SortsMarksThenComposes", "ạ́"),
    ("RecomposesPrecomposedCharacterWithLowerMark", "ạ́"),
    ("DoesNotComposeBlockedMark", "à́"),
    ("DecomposesToTheShortestParts", "ǘ"),
    ("ReplacesSingleton", "Å"),
    ("ComposesEachBaseInAWord", "Tó Ţá qü"),
    ("DevanagariComposesNukta", "ऩ"),
    ("DevanagariDoesNotComposeExcludedNukta", "क़"),
    ("DevanagariDecomposesExcludedNukta", "क़"),
    ("DevanagariKeepsRra", "ऱ"),
    ("DevanagariRecomposesNuktaInTextWithMarks", "ऩका"),
    ("DevanagariDecomposesNuktaInTextWithoutMarks", "ऩ"),
    ("HangulDecomposesSyllableTheFontLacks", "각"),
    ("HangulComposesJamo", "가"),
    ("HangulKeepsJamoWithoutSyllableGlyph", "각"),
    ("ArabicComposesMadda", "آ"),
    ("ArabicSortsShaddaBeforeFatha", "بَّ"),
    ("ArabicMovesHamzaAboveBeforeFatha", "بَٔ"),
    ("ArabicComposesHamzaAboveAcrossFatha", "أَ"),
]

# A space or a hyphen that the font lacks becomes the space or the hyphen that the font has. Each text ends with
# a letter, so that the place of the letter shows the advance of the space.
SPACE_FONT = "NormalizationSpace.ttf"
SPACE_BARE_FONT = "NormalizationSpaceBare.ttf"
SPACE_CHARACTERS = " a0. ‐"
SPACE_ADVANCES = {0x20: 250, ord("0"): 555, ord("."): 222, 0x2002: 480}
SPACE_BARE_CHARACTERS = " a,-"
SPACE_BARE_ADVANCES = {0x20: 250, ord(","): 333}

SPACE_CASES = [
    ("NoBreakSpaceBecomesSpace", "a a"),
    ("EnQuadDecomposesToEnSpace", "a a"),
    ("EmQuadIsOneEm", "a a"),
    ("EmSpaceIsOneEm", "a a"),
    ("ThreePerEmSpace", "a a"),
    ("FourPerEmSpace", "a a"),
    ("SixPerEmSpace", "a a"),
    ("FigureSpaceIsAsWideAsADigit", "a a"),
    ("PunctuationSpaceIsAsWideAsAFullStop", "a a"),
    ("ThinSpace", "a a"),
    ("HairSpace", "a a"),
    ("NarrowNoBreakSpaceIsHalfASpace", "a a"),
    ("MediumMathematicalSpace", "a a"),
    ("IdeographicSpaceIsOneEm", "a　a"),
    ("NoBreakHyphenBecomesHyphen", "a‑a"),
    ("OghamSpaceMarkStaysMissing", "a a"),
    ("FigureDashStaysMissing", "a‒a"),
    ("SpacesInAWord", "a  a‑ a"),
]

SPACE_BARE_CASES = [
    ("EnQuadWithoutEnSpaceIsHalfAnEm", "a a"),
    ("FigureSpaceWithoutDigitsKeepsTheSpaceAdvance", "a a"),
    ("PunctuationSpaceIsAsWideAsAComma", "a a"),
    ("NoBreakHyphenDoesNotBecomeHyphenMinus", "a‑a"),
]

# Hangul syllables decompose by arithmetic and the Hangul shaper owns them.
HANGUL = range(0xAC00, 0xD7A4)
CJK_COMPATIBILITY = (range(0xF900, 0xFB00), range(0x2F800, 0x2FA20))
CJK_SAMPLE = 8
BASE = ord("x")


def read_unicode_data():
    classes, decompositions = {}, {}
    with open(os.path.join(RULES, "UnicodeData.txt"), encoding="utf-8") as file:
        for line in file:
            fields = line.split(";")
            code_point = int(fields[0], 16)
            if fields[3] != "0":
                classes[code_point] = int(fields[3])
            if fields[5] and not fields[5].startswith("<"):
                decompositions[code_point] = [int(part, 16) for part in fields[5].split()]
    return classes, decompositions


def box():
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((450, 700))
    pen.lineTo((450, 0))
    pen.closePath()
    return pen.glyph()


def build(file_name, code_points, outlines, features=None, advances=None):
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: box() if outlines else Glyph() for name in names})
    metrics = {name: (600, 50 if outlines else 0) for name in names}
    for code_point, advance in (advances or {}).items():
        metrics["u%04X" % code_point] = (advance, 50)
    builder.setupHorizontalMetrics(metrics)
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    if features:
        addOpenTypeFeaturesFromString(builder.font, features)
    path = os.path.join(FONTS, file_name)
    builder.font.save(path)
    with open(path, "rb") as file:
        return hb.Font(hb.Face(file.read()))


def shape(font, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    return [info.codepoint for info in buffer.glyph_infos]


def shape_positions(font, text):
    """Gives the glyph ID of each glyph, and where HarfBuzz draws it, in font units from the start of the line."""
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append({"id": info.codepoint, "x": x + position.x_offset, "y": position.y_offset})
        x += position.x_advance
    return glyphs


def positioned_cases(font_name, font, cases):
    return [{"name": name, "font": font_name, "text": text, "glyphs": shape_positions(font, text)} for name, text in cases]


def full_decomposition(code_point, decompositions):
    parts = decompositions.get(code_point)
    if parts is None:
        return [code_point]
    return [leaf for part in parts for leaf in full_decomposition(part, decompositions)]


def scanned(decompositions):
    for code_point in sorted(decompositions):
        if code_point in HANGUL:
            continue
        block = next((b for b in CJK_COMPATIBILITY if code_point in b), None)
        if block is not None and code_point >= block.start + CJK_SAMPLE:
            continue
        yield code_point


def text_of(code_points):
    return "".join(chr(c) for c in code_points)


def main():
    classes, decompositions = read_unicode_data()
    case_font = build(CASE_FONT, sorted(set(map(ord, CASE_CHARACTERS))), True, CASE_FEATURES)
    cases = [{"name": name, "text": text, "glyphs": shape(case_font, text)} for name, text in CASES]
    build(BASE_FONT, sorted(set(map(ord, BASE_CHARACTERS))), True)

    space_font = build(SPACE_FONT, sorted(set(map(ord, SPACE_CHARACTERS))), True, advances=SPACE_ADVANCES)
    space_bare_font = build(SPACE_BARE_FONT, sorted(set(map(ord, SPACE_BARE_CHARACTERS))), True, advances=SPACE_BARE_ADVANCES)
    positioned = positioned_cases(SPACE_FONT, space_font, SPACE_CASES)
    positioned += positioned_cases(SPACE_BARE_FONT, space_bare_font, SPACE_BARE_CASES)

    composed = list(scanned(decompositions))
    values = sorted(set(classes.values()))
    probes = [min(c for c in classes if classes[c] == value and c not in decompositions) for value in values]
    parts = {BASE, *probes}
    for code_point in composed:
        parts.update(full_decomposition(code_point, decompositions))
    parts -= set(decompositions)
    intermediate = {part for c in composed for part in decompositions[c] if part in decompositions}

    scan_font = build(SCAN_FONT, sorted(parts | set(composed) | intermediate), False)
    parts_font = build(PARTS_FONT, sorted(parts), False)

    rows = []

    def add(font_name, font, code_points):
        text = text_of(code_points)
        rows.append([font_name, text, shape(font, text)])

    for code_point in composed:
        pair = decompositions[code_point]
        full = full_decomposition(code_point, decompositions)
        add("parts", parts_font, [code_point])
        add("scan", scan_font, [code_point])
        if len(pair) == 2:
            add("scan", scan_font, pair)
        if full != pair:
            add("scan", scan_font, full)
    for first in probes:
        for second in probes:
            if first != second:
                add("scan", scan_font, [BASE, first, second])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "font": %s,\n' % json.dumps(CASE_FONT))
        file.write('  "cases": [\n')
        file.write(",\n".join("    " + json.dumps(case) for case in cases))
        file.write("\n  ],\n")
        file.write('  "positioned": [\n')
        file.write(",\n".join("    " + json.dumps(case) for case in positioned))
        file.write("\n  ],\n")
        file.write('  "scanFonts": %s,\n' % json.dumps({"scan": SCAN_FONT, "parts": PARTS_FONT}))
        file.write('  "scan": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(cases), "cases", len(positioned), "positioned cases", len(rows), "scan rows")


if __name__ == "__main__":
    main()
