#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds TextShaper.ttf and writes TextShaper.harfbuzz.json, the glyphs that HarfBuzz gives for a run of text: the glyph
ID, the cluster, the advance, and the x and y offset of each glyph in visual order, in font units.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_text_shaper_reference.py

The script does not read the HarfBuzz source. It shapes each case with the script, language, direction and features
that the case lists, and writes what HarfBuzz returns. A cluster is the UTF-16 index of the first character of the
glyph, as HarfBuzz gives it at its default cluster level. uharfbuzz counts code points, so the script converts them.

TextShaper.ttf has empty glyphs 600 wide. Its features:

* `liga`: f i, f f and f f i.
* `dlig`: a b, with IgnoreMarks, so that a mark between the two letters does not stop the ligature.
* `rlig` in the script `arab` only: a b. A caller that names the script Arabic gets it for Latin letters.
* `locl` in the language `TRK `: i becomes a glyph of its own.
* `smcp`: a and b become small capitals. It is off unless the caller turns it on.
* `kern`: A V is 80 units closer, and o l is 700 units closer, which leaves o with a negative advance. The language
  system `TRK ` of `latn` has no `kern` feature.
* `mark`: the marks U+0301 and U+0323 attach above and below e and a.
* `mkmk`: U+0301 attaches above U+0323, to stack the marks.

The font has no precomposed e acute, so the shaper decomposes it, and it has i diaeresis, so the shaper composes i and
U+0308. It also has a `kern` table, which HarfBuzz reads only when the GPOS table has no `kern` feature for the script
and the language: T o is 100 units closer, and A V is 50 units closer, where GPOS makes it 80.

The cases of real faces take the letters and marks of a block from the cmap table of the face. Each case is a base with
one mark, a base with two marks, and a word of bases with marks, so that the marks of the face are on show. Each face
also has words of its script, with the conjuncts, the reordered vowel signs, the ligatures and the vowel points that
the script uses. The faces are those of the shaping tests: Latin, Greek and Cyrillic, Hebrew, Arabic, the Indic scripts,
Khmer, Myanmar, Balinese and Hangul.

Other cases:

* TextShaperLarge.ttf, a font of 16384 units to the em with wide glyphs, so that an advance is above 32767 or below zero,
  and a mark that moves by more than 32767 units.
* Variable fonts at three weights each, with the advances and the GPOS deltas that the weight gives.
* Emoji with a variation selector, a skin tone, a joiner, a tag sequence and regional indicators.
* Characters that a face has no glyph for, to show which of them HarfBuzz keeps in the cluster before them.

The cases leave out a run in a direction that is not the one of its script, such as Latin text from right to left. HarfBuzz
shapes such a run in the native direction and then reverses it, so a lookup that matches "f i" does not match it. The
library shapes the characters in the order of the text, as the layout shapes the runs of the Unicode Bidirectional
Algorithm, whose direction always follows its script.

TextShaperTests and TextShaperPositionTests read the references.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import unicodedata

import harfbuzz_reference as ref
import uharfbuzz as hb
from fontTools.ttLib import TTFont, newTable
from fontTools.ttLib.tables._k_e_r_n import KernTable_format_0

REFERENCE = os.path.join(ref.FONTS, "TextShaper.harfbuzz.json")
FONT = "TextShaper.ttf"


def U(*groups):
    """Gives the string of the code points that the hexadecimal groups list, as in U("0061 0301")."""
    return "".join(chr(int(c, 16)) for g in groups for c in g.split())


# The glyphs of TextShaper.ttf, by code point. The glyph of a code point is named u and its hexadecimal value.
CODE_POINTS = [0x20, 0x41, 0x56, 0x54, 0x61, 0x62, 0x63, 0x65, 0x66, 0x69, 0x6C, 0x6F, 0xEF, 0x0301, 0x0308, 0x0323,
               0x200C, 0x200D]
EXTRA_GLYPHS = ["f_i", "f_f", "f_f_i", "a_b", "i.trk", "a.sc", "b.sc"]

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem latn TRK;
languagesystem arab dflt;

markClass u0301 <anchor 0 600> @ABOVE;
markClass u0323 <anchor 0 -100> @BELOW;
markClass u0301 <anchor 0 800> @ABOVE_MARK;

feature liga {
    sub u0066 u0066 u0069 by f_f_i;
    sub u0066 u0066 by f_f;
    sub u0066 u0069 by f_i;
} liga;

feature dlig {
    lookupflag IgnoreMarks;
    sub u0061 u0062 by a_b;
} dlig;

feature rlig {
    script arab;
    sub u0061 u0062 by a_b;
} rlig;

feature locl {
    script latn;
    language TRK;
    sub u0069 by i.trk;
} locl;

feature smcp {
    sub u0061 by a.sc;
    sub u0062 by b.sc;
} smcp;

lookup KERN_PAIRS {
    pos u0041 u0056 -80;
    pos u006F u006C -700;
} KERN_PAIRS;

feature kern {
    script DFLT;
    lookup KERN_PAIRS;
    script latn;
    lookup KERN_PAIRS;
    script arab;
    lookup KERN_PAIRS;
} kern;

feature mark {
    pos base u0065 <anchor 300 700> mark @ABOVE;
    pos base u0061 <anchor 300 700> mark @ABOVE;
    pos base u0065 <anchor 300 0> mark @BELOW;
    pos base u0061 <anchor 300 0> mark @BELOW;
} mark;

feature mkmk {
    pos mark u0323 <anchor 0 600> mark @ABOVE_MARK;
} mkmk;

table GDEF {
    GlyphClassDef [u0041 u0056 u0054 u0061 u0062 u0063 u0065 u0066 u0069 u006C u006F u00EF a_b i.trk a.sc b.sc],
                  [f_i f_f f_f_i],
                  [u0301 u0308 u0323],
                  ;
} GDEF;
"""

# TextShaperLarge.ttf has W 40960 wide, o 20000 wide and i 8192 wide in an em of 16384 units. A pair kern moves the advance of
# the first glyph by up to 32000 units, so the advances come to 8960 and 50000, below zero, and above 65535. A mark on
# W moves back by the advance of W.
LARGE_FONT = "TextShaperLarge.ttf"
LARGE_CODE_POINTS = [0x20, 0x57, 0x69, 0x6F, 0x0301]
LARGE_ADVANCES = {0x57: 40960, 0x6F: 20000, 0x69: 8192, 0x20: 8192, 0x0301: 0}
LARGE_FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;

markClass u0301 <anchor 0 0> @ABOVE;

feature kern {
    pos u0057 u0069 -32000;
    pos u0069 u006F -12000;
    pos u006F u0069 30000;
    pos u0057 u0057 32000;
} kern;

feature mark {
    pos base u0057 <anchor 0 20000> mark @ABOVE;
    pos base u006F <anchor 30000 20000> mark @ABOVE;
} mark;

table GDEF {
    GlyphClassDef [u0057 u0069 u006F], , [u0301], ;
} GDEF;
"""
LARGE_TEXTS = ["Wi", "io", "oi", "WW", "Wio", "WiW", "o" + U("0301"), "W" + U("0301"), "W" + U("0301 0301"), "W o"]

# Each case is (name, font, text, options). The options can have `script`, `language`, `direction` and `features`.
SYNTHETIC_CASES = [
    ("Ligature", "fi", {}),
    ("LigatureThree", "ffi", {}),
    ("LigatureInWord", "office", {}),
    ("LigatureOff", "ffi", {"features": {"liga": False}}),
    ("LigatureOffAndKernOn", "fi", {"features": {"liga": False, "kern": True}}),
    ("LigatureStoppedByZwnj", "f" + U("200C") + "i", {}),
    ("LigatureThroughZwj", "f" + U("200D") + "i", {}),
    ("LigatureWithMarkBetween", "f" + U("0301") + "i", {}),
    ("LigatureIgnoringMarks", "a" + U("0301") + "b", {"features": {"dlig": True}}),
    ("LigatureIgnoringMarksWithTwoMarks", "a" + U("0301 0323") + "b", {"features": {"dlig": True}}),
    ("LigatureThenMark", "fi" + U("0301"), {}),
    ("ScriptOnlyFeatureNotAppliedToLatin", "ab", {}),
    ("ScriptOnlyFeatureAppliedWhenScriptIsArabic", "ab", {"script": "Arab", "direction": "rtl"}),
    ("Language", "ii", {}),
    ("LanguageTurkish", "ii", {"language": ("tr", "TRK ")}),
    ("LanguageWithoutLookups", "ii", {"language": ("ro", "ROM ")}),
    ("SmallCapsOff", "ab", {}),
    ("SmallCapsOn", "ab", {"features": {"smcp": True}}),
    ("Kerning", "AVA", {}),
    ("KerningOff", "AVA", {"features": {"kern": False}}),
    ("NegativeAdvance", "ole", {}),
    ("MarkAbove", "e" + U("0301"), {}),
    ("MarkAboveAndBelow", "e" + U("0301 0323"), {}),
    ("MarkAboveAndBelowReversed", "e" + U("0323 0301"), {}),
    ("MarkAboveOnSecondBase", "ae" + U("0301"), {}),
    ("MarksOnTwoBases", "a" + U("0301") + "e" + U("0323"), {}),
    ("MarkStack", "a" + U("0323 0301"), {}),
    ("MarkOff", "e" + U("0301"), {"features": {"mark": False}}),
    ("MarkWithoutBase", U("0301") + "e", {}),
    ("Decomposition", U("00E9"), {}),
    ("DecompositionInWord", "c" + U("00E9") + "c", {}),
    ("Composition", "i" + U("0308"), {}),
    ("CompositionInWord", "ci" + U("0308") + "c", {}),
    ("AstralBeforeMark", U("1F600") + "e" + U("0301"), {}),
    ("AstralAfterLigature", "fi" + U("1F600") + "e", {}),
    ("MissingGlyph", "e" + U("4E00") + "e", {}),
    ("ZeroWidthJoiner", "e" + U("200D") + "e", {}),
    ("Space", "e e", {}),
    ("RightToLeft", "fie", {"script": "Arab", "direction": "rtl"}),
    ("RightToLeftMark", "e" + U("0301") + "a", {"script": "Arab", "direction": "rtl"}),
    ("RightToLeftLigature", "fi", {"script": "Arab", "direction": "rtl"}),
    ("RightToLeftLigatureThenMark", "fi" + U("0301") + "e", {"script": "Arab", "direction": "rtl"}),
    ("KernTableAfterGposKern", "AVA To", {}),
    ("KernTableForLanguageWithoutGposKern", "AVA To", {"language": ("tr", "TRK ")}),
    ("KernTableForLanguageWithoutGposKernOff", "AVA To", {"language": ("tr", "TRK "), "features": {"kern": False}}),
    ("KernTableForLanguageWithLigature", "fiAVA", {"language": ("tr", "TRK ")}),
]

# Letters and marks of a block of a real face are listed as (font, ISO 15924 code, direction, blocks of the letters,
# blocks of the marks, extra characters).
REAL_FACES = [
    ("EBGaramond-Regular.ttf", "Latn", "ltr", [(0x41, 0x7A)], [(0x0300, 0x036F)]),
    ("DejaVuSans.ttf", "Latn", "ltr", [(0x61, 0x7A)], [(0x0300, 0x036F)]),
    ("DejaVuSans.ttf", "Grek", "ltr", [(0x03B1, 0x03C9)], [(0x0300, 0x0345)]),
    ("DejaVuSans.ttf", "Cyrl", "ltr", [(0x0430, 0x044F)], [(0x0300, 0x0489)]),
    ("DejaVuSans.ttf", "Hebr", "rtl", [(0x05D0, 0x05EA)], [(0x0591, 0x05C7)]),
    ("DejaVuSans.ttf", "Arab", "rtl", [(0x0628, 0x064A)], [(0x064B, 0x0655), (0x0670, 0x0670)]),
    ("Dubai-Regular.ttf", "Arab", "rtl", [(0x0627, 0x064A)], [(0x064B, 0x0655), (0x0670, 0x0670)]),
    ("me_quran_volt_newmet.ttf", "Arab", "rtl", [(0x0627, 0x064A)], [(0x0610, 0x061A), (0x064B, 0x065F),
                                                                  (0x0670, 0x0670), (0x06D6, 0x06ED)]),
    ("TimesNewRoman.ttf", "Hebr", "rtl", [(0x05D0, 0x05EA)], [(0x0591, 0x05C7)]),
    ("Segoeui.ttf", "Hebr", "rtl", [(0x05D0, 0x05EA)], [(0x0591, 0x05C7)]),
    ("Segoeui.ttf", "Arab", "rtl", [(0x0627, 0x064A)], [(0x064B, 0x0655), (0x0670, 0x0670)]),
    ("TimesNewRoman.ttf", "Arab", "rtl", [(0x0627, 0x064A)], [(0x064B, 0x0655), (0x0670, 0x0670)]),
    ("OpenSans-Regular.ttf", "Latn", "ltr", [(0x41, 0x7A)], [(0x0300, 0x036F)]),
    ("OpenSans-Regular.ttf", "Grek", "ltr", [(0x03B1, 0x03C9)], [(0x0300, 0x0345)]),
    ("SourceSansPro-Regular.otf", "Latn", "ltr", [(0x41, 0x7A)], [(0x0300, 0x036F)]),
    ("NotoSansDevanagari-Regular.ttf", "Deva", "ltr", [(0x0915, 0x0939)], [(0x0900, 0x0903), (0x093A, 0x094F)]),
    ("Sumana-Regular.ttf", "Deva", "ltr", [(0x0915, 0x0939)], [(0x0900, 0x0903), (0x093A, 0x094F)]),
    ("NotoSansBengali-Regular.ttf", "Beng", "ltr", [(0x0995, 0x09B9)], [(0x0981, 0x0983), (0x09BC, 0x09D7)]),
    ("NotoSansGujarati-Regular.ttf", "Gujr", "ltr", [(0x0A95, 0x0AB9)], [(0x0A81, 0x0A83), (0x0ABC, 0x0ACD)]),
    ("NotoSansGurmukhi-Regular.ttf", "Guru", "ltr", [(0x0A15, 0x0A39)], [(0x0A01, 0x0A03), (0x0A3C, 0x0A4D)]),
    ("NotoSansKannada-Regular.ttf", "Knda", "ltr", [(0x0C95, 0x0CB9)], [(0x0C81, 0x0C83), (0x0CBC, 0x0CD6)]),
    ("NotoSansMalayalam-Regular.ttf", "Mlym", "ltr", [(0x0D15, 0x0D3A)], [(0x0D01, 0x0D03), (0x0D3B, 0x0D4E)]),
    ("NotoSansOriya-Regular.ttf", "Orya", "ltr", [(0x0B15, 0x0B39)], [(0x0B01, 0x0B03), (0x0B3C, 0x0B57)]),
    ("NotoSansTamil-Regular.ttf", "Taml", "ltr", [(0x0B95, 0x0BB9)], [(0x0B82, 0x0B83), (0x0BBE, 0x0BCD)]),
    ("NotoSansTelugu-Regular.ttf", "Telu", "ltr", [(0x0C15, 0x0C39)], [(0x0C00, 0x0C04), (0x0C3C, 0x0C56)]),
    ("NotoSansKhmer-Regular.ttf", "Khmr", "ltr", [(0x1780, 0x17A2)], [(0x17B6, 0x17D3)]),
    ("NotoSansMyanmar/NotoSansMyanmar-Regular.ttf", "Mymr", "ltr", [(0x1000, 0x1021)], [(0x102B, 0x103E)]),
    ("NotoSansBalinese-Regular.ttf", "Bali", "ltr", [(0x1B05, 0x1B33)], [(0x1B00, 0x1B04), (0x1B34, 0x1B44)]),
]

# Hangul syllables are not marks on a base, so a face lists the jamo that make them: a leading consonant, a vowel and a
# trailing consonant, as (font, leading, vowels, trailing).
HANGUL_FACES = [
    ("NotoSansKR-Regular.otf", [0x1100, 0x1102, 0x1109, 0x110B, 0x1112], [0x1161, 0x1165, 0x1169, 0x116E, 0x1175],
     [0x11A8, 0x11AB, 0x11BC]),
]

# Words of each script with the marks that are in use: vowel points, conjuncts, reordered vowel signs, ligatures.
ARABIC_WORDS = ["بِسْمِ اللَّهِ الرَّحْمَنِ الرَّحِيمِ", "مُحَمَّد", "السَّلَامُ عَلَيْكُمْ", "لا إله إلا الله", "لَأَ لِإ"]
HEBREW_WORDS = ["בְּרֵאשִׁית בָּרָא אֱלֹהִים", "שָׁלוֹם", "אֲנִי", "וַיְהִי"]
WORDS = {
    ("EBGaramond-Regular.ttf", "Latn"): ["office", "affluent fifty", "ffl ffi fl fi", "caf" + U("00E9 0020 006E 0061 00EF 0076 0065 0020 00C5") + "str" + U("00F6") + "m", "cafe" + U("0301")],
    ("DejaVuSans.ttf", "Latn"): ["office", "cafe" + U("0301") + " na" + U("0308") + "ve", "a" + U("0323 0301")],
    ("DejaVuSans.ttf", "Arab"): ARABIC_WORDS,
    ("DejaVuSans.ttf", "Hebr"): HEBREW_WORDS,
    ("Dubai-Regular.ttf", "Arab"): ARABIC_WORDS,
    ("me_quran_volt_newmet.ttf", "Arab"): ARABIC_WORDS,
    ("TimesNewRoman.ttf", "Hebr"): HEBREW_WORDS,
    ("Segoeui.ttf", "Hebr"): HEBREW_WORDS,
    ("Segoeui.ttf", "Arab"): ARABIC_WORDS,
    ("TimesNewRoman.ttf", "Arab"): ARABIC_WORDS,
    ("OpenSans-Regular.ttf", "Latn"): ["office", "cafe" + U("0301") + " na" + U("0308") + "ve", "a" + U("0323 0301")],
    ("SourceSansPro-Regular.otf", "Latn"): ["office", "affluent fifty", "cafe" + U("0301") + " na" + U("0308") + "ve", "a" + U("0323 0301")],
    ("NotoSansDevanagari-Regular.ttf", "Deva"): ["संस्कृतम्", "हिन्दी", "क्षत्रिय", "कर्म", "दुःख", "ज्ञान", "श्री", "किताब कुत्ता"],
    ("Sumana-Regular.ttf", "Deva"): ["संस्कृतम्", "हिन्दी", "क्षत्रिय", "कर्म", "दुःख", "ज्ञान", "श्री"],
    ("NotoSansBengali-Regular.ttf", "Beng"): ["বাংলা", "ভাষা", "ক্ষমা", "কোথায়", "সংস্কৃত", "কৌশল"],
    ("NotoSansGujarati-Regular.ttf", "Gujr"): ["ગુજરાતી", "ક્ષેત્ર", "કર્મ"],
    ("NotoSansGurmukhi-Regular.ttf", "Guru"): ["ਪੰਜਾਬੀ", "ਸਿੱਖ", "ਕਿਤਾਬ"],
    ("NotoSansKannada-Regular.ttf", "Knda"): ["ಕನ್ನಡ", "ಕ್ಷಮೆ", "ಕೊಡು"],
    ("NotoSansMalayalam-Regular.ttf", "Mlym"): ["മലയാളം", "ക്ഷമ", "കൊടു"],
    ("NotoSansOriya-Regular.ttf", "Orya"): ["ଓଡ଼ିଆ", "କ୍ଷମା", "କୌ"],
    ("NotoSansTamil-Regular.ttf", "Taml"): ["தமிழ்", "கொடு", "கௌ"],
    ("NotoSansTelugu-Regular.ttf", "Telu"): ["తెలుగు", "క్ష", "కొ"],
    ("NotoSansKhmer-Regular.ttf", "Khmr"): ["ខ្មែរ", "ភាសាខ្មែរ", "ស្រុក", "កើត"],
    ("NotoSansMyanmar/NotoSansMyanmar-Regular.ttf", "Mymr"): ["မြန်မာ", "ဘာသာစကား", "ကျွန်ုပ်"],
    ("NotoSansBalinese-Regular.ttf", "Bali"): ["ᬩᬮᬶ", "ᬩᬲᬩᬮᬶ", "ᬓ᭄ᬱ"],
}

# Emoji with a variation selector, a skin tone, a joiner or a pair of regional indicators, which the font draws as one glyph
# or as several, with the glyph IDs and the clusters of HarfBuzz either way.
EMOJI_FACES = [
    ("Twemoji Mozilla.ttf", [
        U("2764 FE0F"), U("2764 FE0F 0061"), "a" + U("2764 FE0F 2764"), U("1F44D 1F3FD"), U("1F44D 1F3FD 1F44D"),
        U("1F468 200D 1F469 200D 1F467"), U("1F468 200D 1F469") + "a", U("1F1E9 1F1EA"), U("1F1E9 1F1EA 1F1EB 1F1F7"),
        "1" + U("FE0F 20E3"), "1" + U("FE0F 20E3 0032"), U("1F3F4 E0067 E0062 E0065 E006E E0067 E007F"),
        U("1F600") + "a" + U("1F601"),
    ]),
]

# Characters that the face has no glyph for are still in clusters, as HarfBuzz joins them: after a zero width joiner, a
# pictograph joins its cluster, and the second of a pair of regional indicators does.
CLUSTER_FACES = [
    ("EBGaramond-Regular.ttf", "Latn", [
        U("1F468 200D 1F469 200D 1F467"), "a" + U("200D 1F468") + "b", U("200D 1F468"), "a" + U("200D") + "b",
        U("1F1E9 1F1EA"), U("1F1E9 1F1EA 1F1EB 1F1F7"), U("1F1E9 1F1EA 1F1EB"), "a" + U("1F1E9") + "b",
        U("1F1E9 1F1E9 1F1E9 1F1E9 1F1E9"), U("2764 FE0F 200D 1F525"), U("1F44D 1F3FD") + "a" + U("1F44D 1F3FD"),
        "1" + U("FE0F 20E3") + "a", U("1F3F4 E0067 E0062") + "a", "a" + U("200C") + "b", "a" + U("034F") + "b",
    ]),
]

# Variable fonts at the default and at other values of the weight axis: HVAR changes the advances, and the GPOS deltas move
# the pairs and the marks. The advance and the offsets are whole font units, as HarfBuzz rounds them.
VARIABLE_FACES = [
    ("Teko/Teko-Variable.ttf", [300, 450, 700], [
        "AVATAR To", "Wa Yo", "fi ffl", U("0939 093F 0928 094D 0926 0940"), U("0915 094D 0937 0924 094D 0930 093F 092F"),
        U("0915 093C 094D 0937 093F 0902"),
    ]),
    ("SourceSans3/SourceSans3VF-Upright.otf", [200, 400, 900], [
        "AVATAR To", "Wa Yo", "fi ffl office", "e" + U("0301"), "a" + U("0323 0301"), "o" + U("0302 0303 0301"),
        "A" + U("030A") + "ngstr" + U("00F6") + "m", "f" + U("0301") + "i",
    ]),
]

# Hebrew points with a letter, shaped with and without the mark feature. Without it, HarfBuzz composes the letter and the
# point to a presentation form when the font has one.
HEBREW_POINTS = ["שׁ", "שּׁ", "בּ", "וֹ", "בֿ", "יִ"]

# Latin words in a font with a GPOS kern feature and a kern table, in a language whose language system has no kern feature.
KERN_WORDS = ["AVATAR To", "Wa Yo", "Te AV", "To", "LT"]

# Devanagari with the rphf feature on and off. Without it, a ra and a virama before a consonant do not form a reph.
REPH_FACES = ["NotoSansDevanagari-Regular.ttf", "Sumana-Regular.ttf"]
REPH_WORDS = [U("0930 094D 0915"), U("0915 0930 094D 092E"), U("0938 0930 094D 0935 0924")]

# Sumana has no Indic features in the language systems of Marathi and Nepali, so the shaper finds no reph, half form or
# conjunct there, and HarfBuzz takes the default language system for any other language. The words leave out a vowel
# sign that goes before its consonant after a virama, as in ta, virama, ra, i: with no feature to join the glyphs, the
# library gives the glyphs that the sign passes the lowest cluster of them, and HarfBuzz joins the clusters from the
# sign to the base only.
LANGUAGE_FACES = [("Sumana-Regular.ttf", "Deva", [("mr", "MAR "), ("ne", "NEP "), ("hi", "HIN ")],
                   [U("0938 0902 0938 094D 0915 0943 0924 092E 094D"), U("0939 093F 0928 094D 0926 0940"), U("0915 0930 094D 092E"),
                    U("0926 0941 0903 0916"), U("0936 094D 0930 0940")])]

# Fonts with the Arabic presentation forms and no GSUB table, or a GSUB table with other features. HarfBuzz joins the
# letters with the forms of the font when the font has none of isol, fina, medi and init, and only if all four are planned.
ARABIC_FALLBACK_FONTS = ["NormalizationArabicFallback.ttf", "NormalizationArabicFallbackWithoutMedial.ttf",
                         "NormalizationArabicFallbackInit.ttf"]
ARABIC_FALLBACK_WORDS = [U("0622 0627 0628"), U("0628 0628 0628"), U("0644 0627 0020 0633 0644 0645")]
JOINING_FEATURES = ["isol", "fina", "medi", "init"]

BASES_PER_CASE = 8
MARKS_PER_CASE = 10


def add_kern_table(path):
    """Adds the legacy `kern` table to the font at the path."""
    subtable = KernTable_format_0()
    subtable.version = 0
    subtable.format = 0
    subtable.coverage = 1
    font = TTFont(path)
    names = font.getBestCmap()
    subtable.kernTable = {(names[0x54], names[0x6F]): -100, (names[0x41], names[0x56]): -50}
    table = newTable("kern")
    table.version = 0
    table.kernTables = [subtable]
    font["kern"] = table
    font.save(path)


def utf16_indexes(text):
    """Gives, for each code point of the text, the UTF-16 index of its first code unit."""
    indexes = [0]
    for c in text:
        indexes.append(indexes[-1] + (2 if ord(c) > 0xFFFF else 1))
    return indexes


def shape(font, text, script=None, language=None, direction=None, features=None, **_):
    """Gives the glyphs in visual order as [glyph ID, cluster, x advance, x offset, y offset], and the direction that
    HarfBuzz shaped with. A language is the BCP 47 tag that HarfBuzz takes and the OpenType tag of the language system
    that it maps to."""
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    if script is not None:
        buffer.script = script
    if direction is not None:
        buffer.direction = direction
    if language is not None:
        buffer.language = language[0]
    hb.shape(font, buffer, features or {})
    indexes = utf16_indexes(text)
    glyphs = [[i.codepoint, indexes[i.cluster], p.x_advance, p.x_offset, p.y_offset]
              for i, p in zip(buffer.glyph_infos, buffer.glyph_positions)]
    return glyphs, buffer.direction


def row(name, font_name, hb_font, text, **options):
    """Gives the row of a case: the inputs that the case set, and the direction that HarfBuzz shaped with."""
    glyphs, direction = shape(hb_font, text, **options)
    result = {"name": name, "font": font_name, "text": text}
    if "script" in options:
        result["script"] = options["script"]
    if "language" in options:
        result["language"] = options["language"][1]
    result["direction"] = direction
    if "features" in options:
        result["features"] = options["features"]
    if "variations" in options:
        result["variations"] = options["variations"]
    result["glyphs"] = glyphs
    return result


def block_chars(cmap, blocks, categories):
    """Gives the characters of the blocks that the face has and whose general category is in the categories."""
    return [chr(c) for lo, hi in blocks for c in range(lo, hi + 1)
            if c in cmap and unicodedata.category(chr(c)) in categories]


def spread(items, count):
    """Gives count items spread over the list, the first and the last of them included."""
    if len(items) <= count:
        return items
    return [items[round(i * (len(items) - 1) / (count - 1))] for i in range(count)]


def real_face_texts(path, letters, marks):
    cmap = TTFont(path).getBestCmap()
    bases = spread(block_chars(cmap, letters, {"Lu", "Ll", "Lo", "Lm"}), BASES_PER_CASE)
    marks = spread(block_chars(cmap, marks, {"Mn", "Mc", "Me"}), MARKS_PER_CASE)
    texts = []
    for i, mark in enumerate(marks):
        texts.append(bases[i % len(bases)] + mark)
    for i, mark in enumerate(marks):
        other = marks[(i + 3) % len(marks)]
        texts.append(bases[(i + 1) % len(bases)] + mark + other)
    for i in range(0, len(marks) - 2, 2):
        texts.append("".join(bases[(i + j) % len(bases)] + marks[i + j] for j in range(3)))
        texts.append("".join(bases[(i + j) % len(bases)] + marks[i + j] + marks[(i + j + 5) % len(marks)]
                             for j in range(3)))
    # A mark at the start of the text, between a base and a space, and after a space.
    texts.append(marks[0] + bases[0])
    texts.append(bases[0] + " " + marks[1] + bases[1])
    return list(dict.fromkeys(texts))


def hangul_texts(leading, vowels, trailing):
    """Gives syllables as jamo: a leading consonant and a vowel, with and without a trailing consonant, and words of
    them, so that the shaper composes some of them and leaves others."""
    texts = []
    for i, lead in enumerate(leading):
        vowel = vowels[i % len(vowels)]
        texts.append(chr(lead) + chr(vowel))
        texts.append(chr(lead) + chr(vowel) + chr(trailing[i % len(trailing)]))
        texts.append(chr(lead) + chr(vowels[(i + 2) % len(vowels)]) + chr(vowel))
        texts.append(chr(lead) + chr(trailing[i % len(trailing)]))
    texts.append("".join(chr(leading[i]) + chr(vowels[i]) for i in range(len(leading))))
    texts.append("".join(chr(leading[i]) + chr(vowels[i]) + chr(trailing[i % len(trailing)]) for i in range(len(leading))))
    return list(dict.fromkeys(texts))


def main():
    rows = []

    path = ref.build_font(FONT, CODE_POINTS, FEATURES, EXTRA_GLYPHS)
    add_kern_table(path)
    hb_font = ref.load_font(path)
    for name, text, options in SYNTHETIC_CASES:
        rows.append(row(name, FONT, hb_font, text, **options))

    for font_name, script, direction, letters, marks in REAL_FACES:
        path = os.path.join(ref.FONTS, font_name)
        hb_font = ref.load_font(path)
        for text in real_face_texts(path, letters, marks) + WORDS.get((font_name, script), []):
            rows.append(row(script, font_name, hb_font, text, script=script, direction=direction))

    for font_name, script, texts in CLUSTER_FACES:
        hb_font = ref.load_font(os.path.join(ref.FONTS, font_name))
        for text in texts:
            rows.append(row("Clusters", font_name, hb_font, text, script=script, direction="ltr"))

    for font_name, weights, texts in VARIABLE_FACES:
        for weight in weights:
            hb_font = ref.load_font(os.path.join(ref.FONTS, font_name))
            hb_font.set_variations({"wght": weight})
            for text in texts:
                rows.append(row("Variable", font_name, hb_font, text, direction="ltr", variations={"wght": weight}))

    for font_name, texts in EMOJI_FACES:
        hb_font = ref.load_font(os.path.join(ref.FONTS, font_name))
        for text in texts:
            rows.append(row("Emoji", font_name, hb_font, text, direction="ltr"))

    path = ref.build_font(LARGE_FONT, LARGE_CODE_POINTS, LARGE_FEATURES, units_per_em=16384, advances=LARGE_ADVANCES)
    hb_font = ref.load_font(path)
    for text in LARGE_TEXTS:
        rows.append(row("LargeUnitsPerEm", LARGE_FONT, hb_font, text, direction="ltr"))

    for text in HEBREW_POINTS:
        hb_font = ref.load_font(os.path.join(ref.FONTS, "DejaVuSans.ttf"))
        rows.append(row("HebrewPoints", "DejaVuSans.ttf", hb_font, text, script="Hebr", direction="rtl"))
        rows.append(row("HebrewPointsWithoutMark", "DejaVuSans.ttf", hb_font, text, script="Hebr", direction="rtl", features={"mark": False}))

    hb_font = ref.load_font(os.path.join(ref.FONTS, "Segoeui.ttf"))
    for text in KERN_WORDS:
        rows.append(row("KernTable", "Segoeui.ttf", hb_font, text, script="Latn", direction="ltr"))
        rows.append(row("KernTableLanguageWithoutKern", "Segoeui.ttf", hb_font, text, script="Latn", direction="ltr", language=("tr", "TRK ")))

    for font_name in REPH_FACES:
        hb_font = ref.load_font(os.path.join(ref.FONTS, font_name))
        for text in REPH_WORDS:
            rows.append(row("Reph", font_name, hb_font, text, script="Deva", direction="ltr"))
            rows.append(row("RephOff", font_name, hb_font, text, script="Deva", direction="ltr", features={"rphf": False}))

    for font_name, script, languages, texts in LANGUAGE_FACES:
        hb_font = ref.load_font(os.path.join(ref.FONTS, font_name))
        for language in languages:
            for text in texts:
                rows.append(row("IndicLanguage", font_name, hb_font, text, script=script, direction="ltr", language=language))

    for font_name in ARABIC_FALLBACK_FONTS:
        hb_font = ref.load_font(os.path.join(ref.FONTS, font_name))
        for text in ARABIC_FALLBACK_WORDS:
            rows.append(row("ArabicFallback", font_name, hb_font, text, script="Arab", direction="rtl"))
            for feature in JOINING_FEATURES:
                rows.append(row("ArabicFallbackOff", font_name, hb_font, text, script="Arab", direction="rtl", features={feature: False}))

    for font_name, leading, vowels, trailing in HANGUL_FACES:
        hb_font = ref.load_font(os.path.join(ref.FONTS, font_name))
        for text in hangul_texts(leading, vowels, trailing) + ["한국어 조선글", "각 힣"]:
            rows.append(row("Hang", font_name, hb_font, text, script="Hang", direction="ltr"))

    ref.write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
