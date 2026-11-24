#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes the fonts and the HarfBuzz reference for the normalization tests.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_normalization_reference.py

Before GSUB, HarfBuzz maps the characters of the text to the glyphs that the font has. It replaces a
base and a mark with the precomposed character when the font has it, it replaces a character that the
font lacks with its canonical decomposition when the font has the parts, and it sorts each run of marks.

The same step draws a space that the font lacks with the glyph of U+0020. And when the font has no GPOS
table, HarfBuzz takes the advance of each mark away and places the mark on its base.

The script builds these fonts in tests/PinataLabs.Typography.Tests/Fonts:

* Normalization.ttf has a small character set chosen for the named cases below. For example it has
  o, U+0301 and U+00F3, and it has T and U+0327 but not U+0162. Its GSUB table has only a `hang` script
  with a `liga` lookup that no case can match.
* NormalizationScan.ttf has every character of UnicodeData.txt that has a canonical decomposition, and
  every part of those decompositions. It has no Hangul syllables, and only the first CJK compatibility
  ideographs of each block.
* NormalizationParts.ttf has the same characters, except those that have a canonical decomposition.
* NormalizationBase.ttf has a, e, o, q and U+0323 only. The tests use it as a primary font that lacks marks,
  with Normalization.ttf as its fallback font. HarfBuzz has no font fallback, so the reference has no case for it.
* NormalizationHebrew.ttf, NormalizationHebrewMark.ttf and NormalizationHebrewKern.ttf have the Hebrew letters,
  points and presentation forms. They differ in the GPOS `mark` feature that decides whether a letter and a
  point compose.
* NormalizationHangul.ttf, NormalizationHangulGsub.ttf and NormalizationHangulJamo.ttf have Hangul jamo, with and
  without syllables and a GSUB table.
* NormalizationSpace.ttf and NormalizationSpaceBare.ttf have U+0020 and lack the other spaces.
* NormalizationMarks.ttf, NormalizationMarksGdef.ttf and NormalizationMarksKern.ttf have a base and marks of
  eight scripts. They differ in their GDEF and GPOS tables.
* NormalizationScripts.ttf has the first letter of every script and U+0301.

The comment above the cases of each group says what the group shows.

The glyphs of the two scan fonts have no outline. The scan shapes each character alone in both fonts,
the two parts of each decomposition and the full decomposition in NormalizationScan.ttf, and each pair of
combining classes after a base.

The reference holds four lists, in the order of the HarfBuzz output. `cases` and `scan` hold the glyph IDs
that HarfBuzz gives. `positioned` and `scripts` also hold where HarfBuzz draws each glyph, in font units from
the start of the line. NormalizationTests reads the reference.

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os
import re

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

# HarfBuzz composes a Hebrew letter and point to the presentation form when the chosen GPOS language system has no
# `mark` feature. The three fonts have the same glyphs. HebrewMark has a `mark` feature for `hebr`. HebrewKern has
# a `kern` feature for `hebr`, and a `mark` feature that only `latn` lists.
HEBREW_FONT = "NormalizationHebrew.ttf"
HEBREW_MARK_FONT = "NormalizationHebrewMark.ttf"
HEBREW_KERN_FONT = "NormalizationHebrewKern.ttf"
HEBREW_CHARACTERS = [*range(0x05D0, 0x05EB), 0x05F2, 0x05A5, *range(0x05B0, 0x05C8), *range(0xFB1D, 0xFB4F)]

HEBREW_MARK_FEATURES = """
languagesystem DFLT dflt;
languagesystem hebr dflt;
markClass [u05BC] <anchor 300 0> @DAGESH;
feature mark { pos base [u05D1] <anchor 300 350> mark @DAGESH; } mark;
"""

HEBREW_KERN_FEATURES = """
languagesystem DFLT dflt;
languagesystem hebr dflt;
languagesystem latn dflt;
markClass [u05BC] <anchor 300 0> @DAGESH;
feature kern { pos u05D0 u05D1 -50; } kern;
feature mark { script latn; pos base [u05D1] <anchor 300 350> mark @DAGESH; } mark;
"""

HEBREW_CASES = [
    ("ComposesDagesh", "בּ"),
    ("ComposesShinDotThenDagesh", "שּׁ"),
    ("ComposesDageshOntoShinWithShinDot", "שּׁ"),
    ("ComposesRafe", "פֿ"),
    ("KeepsPresentationForm", "בּ"),
    ("ComposesEachLetterInAWord", "שָׁלוֹם בּ"),
    ("MovesMetegBeforeSheva", "אְַֽ"),
    ("MovesMarkBelowBeforeHiriq", "אִָ֥"),
    ("KeepsMetegAfterShevaWithoutPatah", "אְֽ"),
]

# The Hangul shaper composes and decomposes syllables before GSUB, so it does the same in a font without GSUB.
# Hangul and HangulGsub have the jamo, the syllables U+AC00 and U+AC01, and a tone mark. HangulJamo has no syllable.
HANGUL_FONT = "NormalizationHangul.ttf"
HANGUL_GSUB_FONT = "NormalizationHangulGsub.ttf"
HANGUL_JAMO_FONT = "NormalizationHangulJamo.ttf"
HANGUL_JAMO = [ord("a"), 0x1100, 0x1102, 0x1161, 0x11A8, 0x11AB, 0x25CC, 0x302E]
HANGUL_CHARACTERS = [*HANGUL_JAMO, 0xAC00, 0xAC01]

HANGUL_FEATURES = """
languagesystem DFLT dflt;
languagesystem hang dflt;
feature liga { sub u0061 u0061 u0061 by u25CC; } liga;
"""

HANGUL_CASES = [
    ("ComposesLeadAndVowel", "\u1100\u1161"),
    ("ComposesLeadVowelAndTail", "\u1100\u1161\u11A8"),
    ("ComposesSyllableAndTail", "\uAC00\u11A8"),
    ("KeepsSyllable", "\uAC01"),
    ("KeepsJamoWithoutSyllableGlyph", "\u1100\u1161\u11AB"),
    ("DecomposesSyllableWithoutGlyph", "\uAC04"),
    ("DecomposesSyllableWithoutTail", "\uB098"),
    ("DecomposesSyllableBeforeTailWithoutGlyph", "\uAC00\u11AB"),
    ("ComposesSecondLeadWithVowel", "\u1100\u1100\u1161"),
    ("KeepsSecondVowel", "\u1100\u1161\u1161"),
    ("MovesToneMarkBeforeSyllable", "\uAC00\u302E"),
    ("MovesToneMarkBeforeComposedJamo", "\u1100\u1161\u11A8\u302E"),
    ("MovesToneMarkBeforeJamo", "\u1100\u1161\u11AB\u302E"),
    ("PutsDottedCircleAfterLoneToneMark", "\u302E"),
    ("ComposesEachSyllableInAWord", "\u1100\u1161\u11A8\u1102\u1161 \u1100\u1161"),
    ("ComposesAfterLatinText", ("aa", "\u1100\u1161\u11AB\u302E\uAC00\u11A8")),
]

# A font without a GPOS table cannot attach a mark. HarfBuzz then takes the advance of the mark away, and its
# default, Arabic, Hebrew and Hangul shapers place the mark on its base from the boxes of the two glyphs.
# Each mark of the fonts is 200 wide and has an advance of 300. Marks has no GPOS and no GDEF table. MarksGdef has
# a GDEF table that calls U+0301 a mark and U+0323 a base. MarksKern has a GPOS table with only a `kern` feature.
MARKS_FONT = "NormalizationMarks.ttf"
MARKS_GDEF_FONT = "NormalizationMarksGdef.ttf"
MARKS_KERN_FONT = "NormalizationMarksKern.ttf"
MARKS = [
    0x0301, 0x0302, 0x0315, 0x031B, 0x0323, 0x0327, 0x035D, 0x20DD,
    0x05B7, 0x05B9, 0x05BC, 0x064E, 0x0650, 0x0E38, 0x0E48, 0x093C, 0x1036, 0x1B34,
]
MARKS_CHARACTERS = sorted([ord("a"), ord("x"), 0x05D0, 0x0628, 0x0E01, 0x0915, 0x1000, 0x1B05, 0xAC00, *MARKS])
MARK_BOXES = {mark: (100, 300, 300, 400) for mark in MARKS}
MARK_ADVANCES = {mark: 300 for mark in MARKS}

MARKS_GDEF_FEATURES = """
table GDEF { GlyphClassDef [u0061 u0323], , [u0301], ; } GDEF;
"""

MARKS_KERN_FEATURES = """
languagesystem DFLT dflt;
feature kern { pos u0061 u0078 -50; } kern;
"""

MARKS_CASES = [
    ("MarkAboveIsCentredOverItsBase", "a\u0301a"),
    ("MarkBelowIsCentredUnderItsBase", "a\u0323a"),
    ("TwoMarksAboveStack", "a\u0301\u0302a"),
    ("MarksAboveAndBelow", "a\u0323\u0301a"),
    ("AttachedMarksTouchTheBase", "a\u0327\u031Ba"),
    ("MarkAboveRight", "a\u0315a"),
    ("DoubleMarkSitsOverTheEndOfItsBase", "a\u035Dx"),
    ("MarkWithoutBaseOnlyLosesItsAdvance", "\u0301a"),
    ("EnclosingMarkKeepsItsAdvance", "a\u20DDa"),
    ("HebrewPoints", "\u05D0\u05B7\u05BC\u05D0"),
    ("HebrewHolamIsLeftAligned", "\u05D0\u05B9"),
    ("ArabicMarks", "\u0628\u064E\u0628\u0650"),
    ("ArabicMarkWithoutBase", "\u064E\u0628"),
    ("ArabicDoubleMark", "\u0628\u035D\u0628"),
    ("HangulMark", "\uAC00\u0301\uAC00"),
    ("ThaiMarksOnlyLoseTheirAdvance", "\u0E01\u0E38\u0E48\u0E01"),
    ("MyanmarMarkOnlyLosesItsAdvance", "\u1000\u1036\u1000"),
    ("BalineseMarkOnlyLosesItsAdvance", "\u1B05\u1B34\u1B05"),
    ("DevanagariMarkKeepsItsAdvance", "\u0915\u093C\u0915"),
]

MARKS_GDEF_CASES = [
    ("GdefMarkIsPlaced", "a\u0301a"),
    ("GdefBaseThatIsAMarkInUnicodeIsPlaced", "a\u0323a"),
    ("ThaiMarksWithoutGdefClassKeepTheirAdvance", "\u0E01\u0E38\u0E48\u0E01"),
]

MARKS_KERN_CASES = [
    ("GposWithoutGdefTakesTheAdvanceOfAMark", "a\u0301a"),
    ("GposWithoutGdefTakesTheAdvanceOfThaiMarks", "\u0E01\u0E38\u0E48\u0E01"),
    ("GposWithoutGdefTakesTheAdvanceOfAMyanmarMark", "\u1000\u1036\u1000"),
    ("GposWithoutGdefKeepsTheAdvanceOfAHangulMark", "\uAC00\u0301\uAC00"),
    ("GposWithoutGdefKeepsTheAdvanceOfADevanagariMark", "\u0915\u093C\u0915"),
    ("GposWithoutGdefKeepsTheAdvanceOfAnEnclosingMark", "a\u20DDa"),
]

# The shaper that HarfBuzz gives a script decides what happens to a mark in a font without GPOS and GDEF: the
# mark is placed on its base, it only loses its advance, or it keeps its advance. Scripts has the first letter of
# every script of Scripts.txt and U+0301, and the reference has that letter and mark for each script.
SCRIPTS_FONT = "NormalizationScripts.ttf"
SCRIPTS_MARK = 0x0301

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


def read_script_letters():
    """Gives the first letter of each script and the direction of its text, by the name of the script."""
    letters = {}
    with open(os.path.join(RULES, "UnicodeData.txt"), encoding="utf-8") as file:
        for line in file:
            fields = line.split(";")
            if fields[2] in ("Lu", "Ll", "Lo") and not fields[1].startswith("<"):
                letters[int(fields[0], 16)] = "rtl" if fields[4] in ("R", "AL") else "ltr"
    first = {}
    with open(os.path.join(RULES, "Scripts.txt"), encoding="utf-8") as file:
        for line in file:
            match = re.match(r"([0-9A-F]+)(?:\.\.([0-9A-F]+))?\s*;\s*(\w+)", line)
            if not match or match.group(3) in first:
                continue
            start = int(match.group(1), 16)
            for code_point in range(start, int(match.group(2) or match.group(1), 16) + 1):
                if code_point in letters:
                    first[match.group(3)] = (code_point, letters[code_point])
                    break
    return dict(sorted(first.items()))


def box(x_min=50, y_min=0, x_max=450, y_max=700):
    pen = TTGlyphPen(None)
    pen.moveTo((x_min, y_min))
    pen.lineTo((x_min, y_max))
    pen.lineTo((x_max, y_max))
    pen.lineTo((x_max, y_min))
    pen.closePath()
    return pen.glyph()


def build(file_name, code_points, outlines, features=None, advances=None, boxes=None):
    names = [".notdef"] + ["u%04X" % c for c in code_points]
    boxes = {"u%04X" % c: b for c, b in (boxes or {}).items()}
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: box(*boxes.get(name, ())) if outlines else Glyph() for name in names})
    metrics = {name: (600, boxes[name][0] if name in boxes else 50 if outlines else 0) for name in names}
    for code_point, advance in (advances or {}).items():
        name = "u%04X" % code_point
        metrics[name] = (advance, metrics[name][1])
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


def named_cases(prefix, font_name, font, cases):
    """Shapes each text. A caller of HarfBuzz shapes each script alone, so a text of two scripts is a tuple of runs."""
    result = []
    for name, text in cases:
        runs = (text,) if isinstance(text, str) else text
        glyphs = [glyph for run in runs for glyph in shape(font, run)]
        result.append({"name": prefix + name, "font": font_name, "text": "".join(runs), "glyphs": glyphs})
    return result


def shape_positions(font, text, direction=None):
    """Gives the glyph ID of each glyph, and where HarfBuzz draws it, in font units from the start of the line."""
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    if direction:
        buffer.direction = direction
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
    cases = named_cases("", CASE_FONT, case_font, CASES)
    for prefix, file_name, features in (
        ("HebrewWithoutMarkFeature", HEBREW_FONT, None),
        ("HebrewWithMarkFeature", HEBREW_MARK_FONT, HEBREW_MARK_FEATURES),
        ("HebrewWithMarkFeatureOfAnotherScript", HEBREW_KERN_FONT, HEBREW_KERN_FEATURES),
    ):
        cases += named_cases(prefix, file_name, build(file_name, HEBREW_CHARACTERS, True, features), HEBREW_CASES)
    for prefix, file_name, characters, features in (
        ("HangulWithoutGsub", HANGUL_FONT, HANGUL_CHARACTERS, None),
        ("HangulWithGsub", HANGUL_GSUB_FONT, HANGUL_CHARACTERS, HANGUL_FEATURES),
        ("HangulWithoutSyllables", HANGUL_JAMO_FONT, HANGUL_JAMO, None),
    ):
        cases += named_cases(prefix, file_name, build(file_name, characters, True, features), HANGUL_CASES)
    build(BASE_FONT, sorted(set(map(ord, BASE_CHARACTERS))), True)

    space_font = build(SPACE_FONT, sorted(set(map(ord, SPACE_CHARACTERS))), True, advances=SPACE_ADVANCES)
    space_bare_font = build(SPACE_BARE_FONT, sorted(set(map(ord, SPACE_BARE_CHARACTERS))), True, advances=SPACE_BARE_ADVANCES)
    positioned = positioned_cases(SPACE_FONT, space_font, SPACE_CASES)
    positioned += positioned_cases(SPACE_BARE_FONT, space_bare_font, SPACE_BARE_CASES)
    for file_name, features, mark_cases in (
        (MARKS_FONT, None, MARKS_CASES),
        (MARKS_GDEF_FONT, MARKS_GDEF_FEATURES, MARKS_GDEF_CASES),
        (MARKS_KERN_FONT, MARKS_KERN_FEATURES, MARKS_KERN_CASES),
    ):
        marks_font = build(file_name, MARKS_CHARACTERS, True, features, MARK_ADVANCES, MARK_BOXES)
        positioned += positioned_cases(file_name, marks_font, mark_cases)

    script_letters = read_script_letters()
    scripts_font = build(
        SCRIPTS_FONT,
        sorted([SCRIPTS_MARK, *(letter for letter, _ in script_letters.values())]),
        True,
        advances={SCRIPTS_MARK: MARK_ADVANCES[0x0301]},
        boxes={SCRIPTS_MARK: MARK_BOXES[0x0301]})
    # HarfBuzz has no direction of its own for a script such as Old Hungarian. Its caller gives it the bidi direction.
    scripts = []
    for name, (letter, direction) in script_letters.items():
        text = chr(letter) + chr(SCRIPTS_MARK)
        scripts.append({"name": name, "font": SCRIPTS_FONT, "text": text, "glyphs": shape_positions(scripts_font, text, direction)})

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
        file.write('  "scripts": [\n')
        file.write(",\n".join("    " + json.dumps(case) for case in scripts))
        file.write("\n  ],\n")
        file.write('  "scanFonts": %s,\n' % json.dumps({"scan": SCAN_FONT, "parts": PARTS_FONT}))
        file.write('  "scan": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(cases), "cases", len(positioned), "positioned cases", len(scripts), "scripts", len(rows), "scan rows")


if __name__ == "__main__":
    main()
