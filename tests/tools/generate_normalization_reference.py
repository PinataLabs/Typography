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
  NormalizationHangulLead.ttf and NormalizationBaseKern.ttf are primary fonts of the same kind.
* NormalizationHebrew.ttf, NormalizationHebrewMark.ttf, NormalizationHebrewKern.ttf and
  NormalizationHebrewRequiredMark.ttf have the Hebrew letters, points and presentation forms. They differ in the
  GPOS `mark` feature that decides whether a letter and a point compose.
* NormalizationLigature.ttf, NormalizationLigatureGdef.ttf and NormalizationLigatureGdefBase.ttf have a GSUB table that
  ligates across marks, and no GPOS table. The second and third also have GDEF glyph classes.
* NormalizationThai.ttf and NormalizationThaiWithoutParts.ttf have Thai and Lao letters, marks and SARA AM, with and
  without its parts.
* NormalizationArabicForms.ttf has every Arabic letter, mark and presentation form, and no GSUB table.
  NormalizationArabicFallback.ttf, NormalizationArabicFallbackWithoutMedial.ttf, NormalizationArabicFallbackRlig.ttf and
  NormalizationArabicFallbackInit.ttf have a few letters and their forms, and differ in the medial forms and GSUB.
* NormalizationHebrewGposDflt.ttf, NormalizationHebrewGposLatn.ttf and NormalizationHebrewGposHebr.ttf have Hebrew and
  Latin letters, marks and a `kern` table. Only the GPOS table of the third has the `hebr` script.
* NormalizationHangul.ttf, NormalizationHangulGsub.ttf and NormalizationHangulJamo.ttf have Hangul jamo, with and
  without syllables and a GSUB table.
* NormalizationSpace.ttf and NormalizationSpaceBare.ttf have U+0020 and lack the other spaces.
  NormalizationSpaceLigature.ttf and NormalizationSpaceSingle.ttf also substitute U+0020 in GSUB.
* NormalizationMarks.ttf, NormalizationMarksGdef.ttf and NormalizationMarksKern.ttf have a base and marks of
  eight scripts. They differ in their GDEF and GPOS tables. NormalizationMarksGdefKern.ttf has both tables.
  NormalizationMarksLegacyKern.ttf has a `kern` table in place of them, and NormalizationMarksBothKern.ttf has a
  `kern` table and a GPOS table.
* NormalizationDevanagari.ttf has Devanagari letters, signs and the dotted circle, and no GSUB table.
* NormalizationDivesAkuru.ttf, NormalizationDivesAkuruWithoutE.ttf and NormalizationDivesAkuruWithoutAa.ttf have a
  Dives Akuru letter and a split vowel sign, with and without its parts.
* NormalizationScripts.ttf has the first letter of every script and U+0301. NormalizationScriptsKern.ttf also has
  a `kern` table.

The comment above the cases of each group says what the group shows.

The glyphs of the two scan fonts have no outline. The scan shapes each character alone in both fonts,
the two parts of each decomposition and the full decomposition in NormalizationScan.ttf, and each pair of
combining classes after a base.

The reference holds five lists, in the order of the HarfBuzz output. `cases` and `scan` hold the glyph IDs
that HarfBuzz gives. `positioned`, `scripts` and `kernedScripts` also hold where HarfBuzz draws each glyph, in font
units from the start of the line. NormalizationTests reads the reference.

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
from fontTools.ttLib import newTable
from fontTools.ttLib.tables._g_l_y_f import Glyph
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0

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

# A `mark` feature that is only the required feature of the language system does not stop the composition.
HEBREW_REQUIRED_MARK_FONT = "NormalizationHebrewRequiredMark.ttf"

HEBREW_REQUIRED_MARK_FEATURES = """
languagesystem DFLT dflt;
languagesystem hebr dflt;
markClass [u05BC] <anchor 300 0> @DAGESH;
feature mark { script hebr; language dflt required; pos base [u05D1] <anchor 300 350> mark @DAGESH; } mark;
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

# NormalizationHangulLead.ttf has only the lead U+1100. The tests use it as a primary font with Hangul.ttf as its fallback.
HANGUL_LEAD_FONT = "NormalizationHangulLead.ttf"

# The universal shaper splits a vowel sign in two parts only when the font has both parts. DivesAkuru has the letter
# KA, the vowel sign O and its parts E and AA. DivesAkuruWithoutE lacks E, and DivesAkuruWithoutAa lacks AA.
DIVES_AKURU_KA = 0x1190C
DIVES_AKURU_O = 0x11938
DIVES_AKURU_E = 0x11935
DIVES_AKURU_AA = 0x11930
DIVES_AKURU_FONTS = [
    ("DivesAkuru", "NormalizationDivesAkuru.ttf", [DIVES_AKURU_KA, DIVES_AKURU_O, DIVES_AKURU_E, DIVES_AKURU_AA]),
    ("DivesAkuruWithoutE", "NormalizationDivesAkuruWithoutE.ttf", [DIVES_AKURU_KA, DIVES_AKURU_O, DIVES_AKURU_AA]),
    ("DivesAkuruWithoutAa", "NormalizationDivesAkuruWithoutAa.ttf", [DIVES_AKURU_KA, DIVES_AKURU_O, DIVES_AKURU_E]),
]

DIVES_AKURU_CASES = [
    ("VowelSign", "\U0001190C\U00011938"),
    ("PartsOfVowelSign", "\U0001190C\U00011935\U00011930"),
]

# The Indic shaper reorders each syllable and gives a broken syllable a dotted circle before GSUB, so it does the
# same in a font without GSUB.
DEVANAGARI_FONT = "NormalizationDevanagari.ttf"
DEVANAGARI_CHARACTERS = [0x20, 0x0915, 0x0930, 0x093C, 0x093F, 0x094D, 0x25CC]

DEVANAGARI_CASES = [
    ("MovesShortIBeforeItsConsonant", "\u0915\u093F"),
    ("MovesShortIBeforeConsonantWithNukta", "\u0915\u093C\u093F"),
    ("MovesShortIBeforeTheLastConsonant", "\u0915\u094D\u0930\u093F"),
    ("KeepsRaAndHalantFirstWithoutRephGlyph", "\u0930\u094D\u0915\u093F"),
    ("PutsDottedCircleAfterLoneShortI", "\u093F"),
    ("ReordersEachSyllableInAWord", "\u0915\u093F \u093F\u0915"),
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

# A font without GPOS is kerned by its `kern` table. HarfBuzz gives half of the kerning to each glyph of a pair,
# looks for the pair across marks and in the order of the line, and still places a mark by the advance that the
# font gives its base.
MARKS_LEGACY_KERN_FONT = "NormalizationMarksLegacyKern.ttf"
MARKS_LEGACY_KERN = {
    (ord("x"), ord("a")): -100,
    (ord("a"), ord("x")): -60,
    (0x05D0, 0x05D0): -40,
    (0x0628, 0x0628): -80,
    (0x0E01, 0x0E01): -30,
}

MARKS_LEGACY_KERN_CASES = [
    ("LegacyKernMovesTheSecondGlyphOfAPair", "xa"),
    ("LegacyKernOfThreeGlyphs", "xax"),
    ("LegacyKernSkipsAMark", "a\u0301x"),
    ("MarkOfAKernedBaseIsPlacedByTheAdvanceOfTheFont", "xa\u0301"),
    ("MarksOfTwoKernedBases", "a\u0301xa\u0323\u0301"),
    ("HebrewLegacyKern", "\u05D0\u05B7\u05D0\u05B7"),
    ("ArabicLegacyKern", "\u0628\u064E\u0628\u0650"),
    ("ThaiHasNoLegacyKern", "\u0E01\u0E38\u0E48\u0E01"),
]

# HarfBuzz leaves the `kern` table alone when the GPOS table has a `kern` feature for the script, even for a pair
# that only the `kern` table has. MarksBothKern has the GPOS table of MarksKern and the `kern` table of MarksLegacyKern.
MARKS_BOTH_KERN_FONT = "NormalizationMarksBothKern.ttf"

MARKS_BOTH_KERN_CASES = [
    ("KernTableIsIgnoredWhenGposHasAKernFeature", "xa"),
    ("GposKernsAPairThatTheKernTableAlsoHas", "ax"),
]

# MarksGdefKern has the GDEF table of MarksGdef and the GPOS table of MarksKern, so U+0323 is a base that keeps its
# advance. NormalizationBaseKern.ttf has a and x and a GPOS `kern` feature. The tests use it as a primary font that
# lacks U+0323, with MarksGdefKern as its fallback font.
MARKS_GDEF_KERN_FONT = "NormalizationMarksGdefKern.ttf"
BASE_KERN_FONT = "NormalizationBaseKern.ttf"

MARKS_GDEF_KERN_CASES = [
    ("GposKeepsTheAdvanceOfAGdefBase", "ạa"),
]

MARKS_KERN_CASES = [
    ("GposWithoutGdefTakesTheAdvanceOfAMark", "a\u0301a"),
    ("GposWithoutGdefTakesTheAdvanceOfThaiMarks", "\u0E01\u0E38\u0E48\u0E01"),
    ("GposWithoutGdefTakesTheAdvanceOfAMyanmarMark", "\u1000\u1036\u1000"),
    ("GposWithoutGdefKeepsTheAdvanceOfAHangulMark", "\uAC00\u0301\uAC00"),
    ("GposWithoutGdefKeepsTheAdvanceOfADevanagariMark", "\u0915\u093C\u0915"),
    ("GposWithoutGdefKeepsTheAdvanceOfAnEnclosingMark", "a\u20DDa"),
    ("GposKernsAPair", "axa"),
]

# The `mark` feature of HebrewMark attaches a dagesh to its letter.
HEBREW_MARK_POSITIONED_CASES = [
    ("GposAttachesAMark", "\u05D0\u05D1\u05BC\u05D0"),
]

# HarfBuzz positions Hebrew with GPOS only when the GPOS table has the `hebr` script. When the table gives Hebrew the
# language system of `DFLT` or `latn`, HarfBuzz kerns Hebrew with the `kern` table and places its marks as in a font
# without GPOS, while Latin text keeps the GPOS table. The fonts have the marks of the Marks fonts and a `kern` table
# with one pair in each order. HebrewGposDflt has GPOS `kern` and `mark` features under `DFLT` only, HebrewGposLatn
# has them under `latn` only, and HebrewGposHebr has a `mark` feature under `hebr` and no `kern` feature.
HEBREW_GPOS_CHARACTERS = [ord("a"), ord("x"), 0x05D0, 0x05D1, 0x05B7, 0x05BC, 0x0301]
HEBREW_GPOS_MARKS = [0x05B7, 0x05BC, 0x0301]
HEBREW_GPOS_KERN = {(0x05D0, 0x05D1): -100, (0x05D1, 0x05D0): -30, (ord("a"), ord("x")): -60}
HEBREW_GPOS_LOOKUPS = """
markClass [u05B7 u05BC u0301] <anchor 300 0> @MARK;
feature kern { pos u05D0 u05D1 -50; pos u0061 u0078 -50; } kern;
feature mark { pos base [u05D0 u05D1 u0061 u0078] <anchor 250 350> mark @MARK; } mark;
"""
HEBREW_GPOS_FONTS = [
    ("NormalizationHebrewGposDflt.ttf", "languagesystem DFLT dflt;\n" + HEBREW_GPOS_LOOKUPS),
    ("NormalizationHebrewGposLatn.ttf", "languagesystem latn dflt;\n" + HEBREW_GPOS_LOOKUPS),
    ("NormalizationHebrewGposHebr.ttf", """
languagesystem DFLT dflt;
languagesystem hebr dflt;
markClass [u05B7 u05BC u0301] <anchor 300 0> @MARK;
feature mark { pos base [u05D0 u05D1 u0061 u0078] <anchor 250 350> mark @MARK; } mark;
"""),
]

HEBREW_GPOS_CASES = [
    ("HebrewPair", "\u05D0\u05D1"),
    ("HebrewMarks", "\u05D1\u05B7\u05BC"),
    ("LatinPairAndMark", "ax\u0301"),
]

# A font without GPOS places a mark of a ligature on the component that the mark came after, not on the whole
# ligature. Each component takes an equal part of the advance of the ligature, counted from the right in text that
# runs backward. A mark after the last component goes on the last component. Ligature ligates f and i to U+FB01, and
# f, f and i to U+FB03, across marks. U+FB03 has three components in an advance of 1000. It ligates lam and alef in
# `rlig`, and a with U+0301 to U+FB01, which is not a ligature of components. LigatureGdef also has GDEF glyph classes.
# LigatureGdefBase calls the ligatures bases, so their marks go on the whole ligature.
# Each text starts with a letter that is not part of the ligature, so the tests can end a text run after it.
LIGATURE_CHARACTERS = [ord("a"), ord("f"), ord("i"), 0xFB01, 0xFB03, 0x0301, 0x0302, 0x0323, 0x0627, 0x0644, 0x064E, 0x0650, 0xFEFB]
LIGATURE_MARKS = [0x0301, 0x0302, 0x0323, 0x064E, 0x0650]
LIGATURE_ADVANCES = {**{mark: MARK_ADVANCES[0x0301] for mark in LIGATURE_MARKS}, 0xFB01: 1000, 0xFB03: 1000, 0xFEFB: 1000}
LIGATURE_BOXES = {**{mark: MARK_BOXES[0x0301] for mark in LIGATURE_MARKS}, 0xFB01: (0, 0, 1000, 700), 0xFB03: (0, 0, 1000, 700), 0xFEFB: (0, 0, 1000, 700)}
LIGATURE_FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem arab dflt;
feature liga { sub u0061 u0301 by uFB01; lookupflag IgnoreMarks; sub u0066 u0066 u0069 by uFB03; sub u0066 u0069 by uFB01; } liga;
feature rlig { lookupflag IgnoreMarks; sub u0644 u0627 by uFEFB; } rlig;
"""
LIGATURE_GDEF_FEATURES = LIGATURE_FEATURES + """
table GDEF { GlyphClassDef [u0061 u0066 u0069], [uFB01 uFB03], [u0301 u0302 u0323], ; } GDEF;
"""
LIGATURE_GDEF_BASE_FEATURES = LIGATURE_FEATURES + """
table GDEF { GlyphClassDef [u0061 u0066 u0069 uFB01 uFB03], , [u0301 u0302 u0323], ; } GDEF;
"""
LIGATURE_FONTS = [
    ("NormalizationLigature.ttf", LIGATURE_FEATURES),
    ("NormalizationLigatureGdef.ttf", LIGATURE_GDEF_FEATURES),
    ("NormalizationLigatureGdefBase.ttf", LIGATURE_GDEF_BASE_FEATURES),
]

LIGATURE_CASES = [
    ("MarkOnTheFirstComponent", "af\u0301i"),
    ("MarkAfterTheLastComponent", "afi\u0301"),
    ("MarksOnBothComponents", "af\u0323i\u0301"),
    ("MarksStackOnTheirComponent", "af\u0301\u0302i"),
    ("MarksOnThreeComponents", "af\u0301f\u0301i\u0301"),
    ("MarkOnTheMiddleComponent", "aff\u0301i"),
    ("RightToLeftMarksOnBothComponents", "\u0627\u0644\u064E\u0627\u0650"),
    ("MarkAfterALigatureOfABaseAndAMark", "aa\u0301\u0302"),
]

# The Thai shaper splits SARA AM into NIKHAHIT and SARA AA before it maps the text to glyphs, even when the font
# lacks the parts, and moves NIKHAHIT before the marks above the base that come before it. Lao does the same with
# its AM. Thai has Thai and Lao letters, SARA AM, its parts and the marks, and ThaiWithoutParts lacks the parts.
THAI_CHARACTERS = [
    0x20, 0x0E01, 0x0E19, 0x0E31, 0x0E32, 0x0E33, *range(0x0E34, 0x0E3B), *range(0x0E47, 0x0E4F),
    0x0E81, 0x0EB1, 0x0EB2, 0x0EB3, *range(0x0EB4, 0x0EBD), *range(0x0EC8, 0x0ECF),
]
THAI_PARTS = [0x0E4D, 0x0E32, 0x0ECD, 0x0EB2]
THAI_FONTS = [
    ("NormalizationThai.ttf", THAI_CHARACTERS),
    ("NormalizationThaiWithoutParts.ttf", [c for c in THAI_CHARACTERS if c not in THAI_PARTS]),
]

THAI_CASES = [
    ("SplitsSaraAm", "\u0E19\u0E33"),
    ("MovesNikhahitBeforeAToneMark", "\u0E19\u0E49\u0E33"),
    ("MovesNikhahitBeforeTwoToneMarks", "\u0E01\u0E48\u0E49\u0E33"),
    ("MovesNikhahitBeforeAVowelAbove", "\u0E01\u0E34\u0E48\u0E33"),
    ("DoesNotMoveNikhahitBeforeAVowelBelow", "\u0E01\u0E38\u0E48\u0E33"),
    ("MovesNikhahitOnlyBeforeTheMarksAboveNextToIt", "\u0E01\u0E48\u0E38\u0E49\u0E33"),
    ("SplitsSaraAmWithoutBase", "\u0E33"),
    ("SplitsSaraAmAfterASpace", "\u0E01 \u0E49\u0E33"),
    ("SplitsEachSaraAm", "\u0E01\u0E33\u0E33"),
    ("SplitsLaoAm", "\u0E81\u0EC8\u0EB3"),
    ("MovesLaoNiggahitaBeforeMaiKon", "\u0E81\u0EBB\u0EB3"),
    ("DoesNotMoveLaoNiggahitaBeforeASemivowelBelow", "\u0E81\u0EBC\u0EB3"),
]

# In a font whose GSUB table has none of the joining forms of Arabic, HarfBuzz gives each Arabic letter the presentation
# form that the font has for its place in the word, and then ligates some of the forms, and a shadda with a vowel.
# ArabicForms has every Arabic letter, mark and presentation form, and no outlines. The cases put each letter that has
# a form in each place, and each ligature that HarfBuzz uses in its place. ArabicFallback has a few letters, marks,
# forms and ligatures. ArabicFallbackWithoutMedial lacks the medial forms. ArabicFallbackRlig has a GSUB table with
# only `rlig`, which does not stop the forms, and ArabicFallbackInit has an `init` feature, which does.
# The library joins letters only within one text run, so each text starts with a space where the tests can end a run.
ARABIC_FORMS_FONT = "NormalizationArabicForms.ttf"
ARABIC_FORM_CONTEXTS = {"isolated": ("", ""), "final": ("\u0640", ""), "initial": ("", "\u0640"), "medial": ("\u0640", "\u0640")}
ARABIC_FALLBACK_LETTERS = [0x0622, 0x0627, 0x0628, 0x0633, 0x0644, 0x0645, 0x0647]
ARABIC_FALLBACK_MARKS = [0x064E, 0x064F, 0x0650, 0x0651]
ARABIC_FALLBACK_INIT_FEATURES = """
languagesystem DFLT dflt;
languagesystem arab dflt;
feature init { sub u0628 by u0647; } init;
"""
ARABIC_FALLBACK_RLIG_FEATURES = """
languagesystem DFLT dflt;
languagesystem arab dflt;
feature rlig { sub u0628 u0628 u0628 by u0647; } rlig;
"""

ARABIC_FALLBACK_CASES = [
    ("JoinsLetters", "\u0628\u0628\u0628"),
    ("LigatesLamAndAlef", "\u0644\u0627"),
    ("LigatesFinalLamAndAlef", "\u0628\u0644\u0627"),
    ("LigatesAcrossMarks", "\u0644\u064E\u0627\u0650"),
    ("LigatesShaddaAndFatha", "\u0628\u0651\u064E"),
    ("LigatesLettersAndMarks", "\u0644\u0651\u064E\u0627"),
    ("JoinsAWord", "\u0633\u0644\u0627\u0645"),
    ("DoesNotJoinAcrossASpace", "\u0628 \u0628"),
]

# The shaper that HarfBuzz gives a script decides what happens to a mark in a font without GPOS and GDEF: the
# mark is placed on its base, it only loses its advance, or it keeps its advance. Scripts has the first letter of
# every script of Scripts.txt and U+0301, and the reference has that letter and mark for each script.
# The same shaper decides whether the `kern` table of the font kerns the text. ScriptsKern also has a `kern` table
# with the pair of each letter and itself, and the reference has that pair for each script.
SCRIPTS_FONT = "NormalizationScripts.ttf"
SCRIPTS_KERN_FONT = "NormalizationScriptsKern.ttf"
SCRIPTS_MARK = 0x0301
SCRIPTS_KERN = -100

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

# A space that GSUB substitutes keeps the advance of the space it stands for, unless it became part of a ligature.
# SpaceLigature ligates U+0020 and a to b, and SpaceSingle substitutes b for U+0020. b has an advance of 700.
SPACE_LIGATURE_FONT = "NormalizationSpaceLigature.ttf"
SPACE_SINGLE_FONT = "NormalizationSpaceSingle.ttf"
SPACE_SUBSTITUTED_CHARACTERS = [0x20, ord("a"), ord("b")]
SPACE_SUBSTITUTED_ADVANCES = {0x20: 250, ord("b"): 700}
SPACE_LIGATURE_FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
feature liga { sub u0020 u0061 by u0062; } liga;
"""
SPACE_SINGLE_FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
feature ccmp { sub u0020 by u0062; } ccmp;
"""

SPACE_LIGATURE_CASES = [
    ("LigatureOfASpaceKeepsItsAdvance", "a aa"),
]

SPACE_SINGLE_CASES = [
    ("SubstitutedSpaceKeepsTheAdvanceOfTheSpace", "a aa"),
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


def build(file_name, code_points, outlines, features=None, advances=None, boxes=None, kern=None):
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
    if kern:
        subtable = KernTable_format_0()
        subtable.version = 0
        subtable.coverage = 1
        subtable.apple = False
        subtable.tupleIndex = None
        subtable.kernTable = {("u%04X" % left, "u%04X" % right): value for (left, right), value in kern.items()}
        builder.font["kern"] = table = newTable("kern")
        table.version = 0
        table.kernTables = [subtable]
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


def arabic_presentation_forms():
    """Gives each character that UnicodeData.txt calls an Arabic form of other characters its form and parts."""
    forms = {}
    with open(os.path.join(RULES, "UnicodeData.txt"), encoding="utf-8") as file:
        for line in file:
            fields = line.split(";")
            tag, _, parts = fields[5].partition(" ")
            if tag[1:-1] in ARABIC_FORM_CONTEXTS:
                forms[int(fields[0], 16)] = (tag[1:-1], [int(part, 16) for part in parts.split()])
    return forms


def arabic_form_cases(classes):
    forms = arabic_presentation_forms()
    letters = sorted({parts[0] for form, parts in forms.values() if len(parts) == 1 and 0x0600 <= parts[0] <= 0x06FF})
    marks = [c for c in classes if 0x0600 <= c <= 0x06FF]
    font = build(ARABIC_FORMS_FONT, sorted({0x20, 0x0640, *letters, *marks, *forms}), False)
    cases = []
    for letter in letters:
        text = " ".join(before + chr(letter) + after for before, after in ARABIC_FORM_CONTEXTS.values())
        cases.append(("ArabicFallbackFormsOf%04X" % letter, text))
    for code_point, (form, parts) in sorted(forms.items()):
        if len(parts) > 1 and parts[0] != 0x20 and all(part in letters for part in parts):
            before, after = ARABIC_FORM_CONTEXTS[form]
            cases.append(("ArabicFallbackLigature%04X" % code_point, before + text_of(parts) + after))
        elif len(parts) > 1 and parts[0] == 0x20:
            cases.append(("ArabicFallbackLigature%04X" % code_point, "\u0628" + text_of(sorted(parts[1:], key=lambda c: classes.get(c, 0)))))
    return named_cases("", ARABIC_FORMS_FONT, font, cases)


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
    cases += arabic_form_cases(classes)
    hebrew_fonts = {}
    for prefix, file_name, features in (
        ("HebrewWithoutMarkFeature", HEBREW_FONT, None),
        ("HebrewWithMarkFeature", HEBREW_MARK_FONT, HEBREW_MARK_FEATURES),
        ("HebrewWithMarkFeatureOfAnotherScript", HEBREW_KERN_FONT, HEBREW_KERN_FEATURES),
        ("HebrewWithRequiredMarkFeature", HEBREW_REQUIRED_MARK_FONT, HEBREW_REQUIRED_MARK_FEATURES),
    ):
        hebrew_fonts[file_name] = build(file_name, HEBREW_CHARACTERS, True, features)
        cases += named_cases(prefix, file_name, hebrew_fonts[file_name], HEBREW_CASES)
    for prefix, file_name, characters, features in (
        ("HangulWithoutGsub", HANGUL_FONT, HANGUL_CHARACTERS, None),
        ("HangulWithGsub", HANGUL_GSUB_FONT, HANGUL_CHARACTERS, HANGUL_FEATURES),
        ("HangulWithoutSyllables", HANGUL_JAMO_FONT, HANGUL_JAMO, None),
    ):
        cases += named_cases(prefix, file_name, build(file_name, characters, True, features), HANGUL_CASES)
    cases += named_cases("DevanagariWithoutGsub", DEVANAGARI_FONT, build(DEVANAGARI_FONT, DEVANAGARI_CHARACTERS, True), DEVANAGARI_CASES)
    build(HANGUL_LEAD_FONT, [0x1100], True)
    for prefix, file_name, characters in DIVES_AKURU_FONTS:
        cases += named_cases(prefix, file_name, build(file_name, characters, True), DIVES_AKURU_CASES)
    build(BASE_FONT, sorted(set(map(ord, BASE_CHARACTERS))), True)
    build(BASE_KERN_FONT, [ord("a"), ord("x")], True, MARKS_KERN_FEATURES)

    space_font = build(SPACE_FONT, sorted(set(map(ord, SPACE_CHARACTERS))), True, advances=SPACE_ADVANCES)
    space_bare_font = build(SPACE_BARE_FONT, sorted(set(map(ord, SPACE_BARE_CHARACTERS))), True, advances=SPACE_BARE_ADVANCES)
    positioned = positioned_cases(SPACE_FONT, space_font, SPACE_CASES)
    positioned += positioned_cases(SPACE_BARE_FONT, space_bare_font, SPACE_BARE_CASES)
    for file_name, features, space_cases in (
        (SPACE_LIGATURE_FONT, SPACE_LIGATURE_FEATURES, SPACE_LIGATURE_CASES),
        (SPACE_SINGLE_FONT, SPACE_SINGLE_FEATURES, SPACE_SINGLE_CASES),
    ):
        font = build(file_name, SPACE_SUBSTITUTED_CHARACTERS, True, features, SPACE_SUBSTITUTED_ADVANCES)
        positioned += positioned_cases(file_name, font, space_cases)
    for file_name, features, kern, mark_cases in (
        (MARKS_FONT, None, None, MARKS_CASES),
        (MARKS_GDEF_FONT, MARKS_GDEF_FEATURES, None, MARKS_GDEF_CASES),
        (MARKS_GDEF_KERN_FONT, MARKS_KERN_FEATURES + MARKS_GDEF_FEATURES, None, MARKS_GDEF_KERN_CASES),
        (MARKS_KERN_FONT, MARKS_KERN_FEATURES, None, MARKS_KERN_CASES),
        (MARKS_LEGACY_KERN_FONT, None, MARKS_LEGACY_KERN, MARKS_LEGACY_KERN_CASES),
        (MARKS_BOTH_KERN_FONT, MARKS_KERN_FEATURES, MARKS_LEGACY_KERN, MARKS_BOTH_KERN_CASES),
    ):
        marks_font = build(file_name, MARKS_CHARACTERS, True, features, MARK_ADVANCES, MARK_BOXES, kern)
        positioned += positioned_cases(file_name, marks_font, mark_cases)
    positioned += positioned_cases(HEBREW_MARK_FONT, hebrew_fonts[HEBREW_MARK_FONT], HEBREW_MARK_POSITIONED_CASES)
    for file_name, features in HEBREW_GPOS_FONTS:
        font = build(
            file_name,
            HEBREW_GPOS_CHARACTERS,
            True,
            features,
            {mark: MARK_ADVANCES[0x0301] for mark in HEBREW_GPOS_MARKS},
            {mark: MARK_BOXES[0x0301] for mark in HEBREW_GPOS_MARKS},
            HEBREW_GPOS_KERN)
        prefix = os.path.splitext(file_name)[0].replace("Normalization", "")
        positioned += positioned_cases(file_name, font, [(prefix + name, text) for name, text in HEBREW_GPOS_CASES])

    for file_name, features in LIGATURE_FONTS:
        font = build(file_name, LIGATURE_CHARACTERS, True, features, LIGATURE_ADVANCES, LIGATURE_BOXES)
        prefix = os.path.splitext(file_name)[0].replace("Normalization", "")
        positioned += positioned_cases(file_name, font, [(prefix + name, text) for name, text in LIGATURE_CASES])

    for file_name, characters in THAI_FONTS:
        font = build(file_name, characters, True)
        prefix = os.path.splitext(file_name)[0].replace("Normalization", "")
        positioned += positioned_cases(file_name, font, [(prefix + name, text) for name, text in THAI_CASES])

    forms = arabic_presentation_forms()
    fallback_forms = [c for c, (_, parts) in forms.items() if all(p in ARABIC_FALLBACK_LETTERS + ARABIC_FALLBACK_MARKS + [0x20] for p in parts)]
    fallback_characters = sorted({0x20, *ARABIC_FALLBACK_LETTERS, *ARABIC_FALLBACK_MARKS, *fallback_forms})
    fallback_advances = {mark: MARK_ADVANCES[0x0301] for mark in ARABIC_FALLBACK_MARKS}
    fallback_boxes = {mark: MARK_BOXES[0x0301] for mark in ARABIC_FALLBACK_MARKS}
    for file_name, characters, features in (
        ("NormalizationArabicFallback.ttf", fallback_characters, None),
        ("NormalizationArabicFallbackWithoutMedial.ttf", [c for c in fallback_characters if forms.get(c, ("",))[0] != "medial"], None),
        ("NormalizationArabicFallbackRlig.ttf", fallback_characters, ARABIC_FALLBACK_RLIG_FEATURES),
        ("NormalizationArabicFallbackInit.ttf", fallback_characters, ARABIC_FALLBACK_INIT_FEATURES),
    ):
        font = build(file_name, characters, True, features, fallback_advances, fallback_boxes)
        prefix = os.path.splitext(file_name)[0].replace("Normalization", "")
        positioned += positioned_cases(file_name, font, [(prefix + name, " " + text) for name, text in ARABIC_FALLBACK_CASES])

    script_letters = read_script_letters()
    scripts_font, scripts_kern_font = (
        build(
            file_name,
            sorted([SCRIPTS_MARK, *(letter for letter, _ in script_letters.values())]),
            True,
            advances={SCRIPTS_MARK: MARK_ADVANCES[0x0301]},
            boxes={SCRIPTS_MARK: MARK_BOXES[0x0301]},
            kern=kern)
        for file_name, kern in (
            (SCRIPTS_FONT, None),
            (SCRIPTS_KERN_FONT, {(letter, letter): SCRIPTS_KERN for letter, _ in script_letters.values()}),
        ))
    # HarfBuzz has no direction of its own for a script such as Old Hungarian. Its caller gives it the bidi direction.
    scripts, kerned_scripts = [], []
    for name, (letter, direction) in script_letters.items():
        text = chr(letter) + chr(SCRIPTS_MARK)
        scripts.append({"name": name, "font": SCRIPTS_FONT, "text": text, "glyphs": shape_positions(scripts_font, text, direction)})
        text = chr(letter) + chr(letter)
        kerned_scripts.append({"name": name, "font": SCRIPTS_KERN_FONT, "text": text, "glyphs": shape_positions(scripts_kern_font, text, direction)})

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
        file.write('  "kernedScripts": [\n')
        file.write(",\n".join("    " + json.dumps(case) for case in kerned_scripts))
        file.write("\n  ],\n")
        file.write('  "scanFonts": %s,\n' % json.dumps({"scan": SCAN_FONT, "parts": PARTS_FONT}))
        file.write('  "scan": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(cases), "cases", len(positioned), "positioned cases", len(scripts), "scripts", len(kerned_scripts), "kerned scripts", len(rows), "scan rows")


if __name__ == "__main__":
    main()
