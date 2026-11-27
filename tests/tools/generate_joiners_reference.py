#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds Joiners.ttf and writes Joiners.harfbuzz.json, how HarfBuzz matches lookups across ZWJ and ZWNJ (#117).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_joiners_reference.py

HarfBuzz skips a default-ignorable character between the glyphs that a lookup matches, unless the glyph matches
there itself. ZWJ and ZWNJ are skipped only in some cases:

* GSUB skips ZWNJ in the backtrack and the lookahead, unless the feature asks for manual ZWNJ, and never in the input.
* GPOS always skips ZWNJ.
* Both tables skip ZWJ in the backtrack and the lookahead, and in the input unless the feature asks for manual ZWJ.
  The Arabic shaper asks for it in its forms, rlig, calt, liga and clig. The Indic and Khmer shapers ask for manual
  joiners in their basic and presentation features. Every shaper asks for them in mark and mkmk.

Joiners.ttf has Latin and Arabic letters, a combining acute, ZWJ and ZWNJ, all empty glyphs 600 wide. Its lookups
each match two glyphs:

* calt: a and b as input, c before d, e before f, the Arabic beh and heh as input, and lam before alef.
* liga: x and y, and two lams.
* kern: p and q as a pair, and g and h as the input of a chained context lookup, which raises each by 100.
* mark: the acute on b.

The reference also has text in test fonts of the repository: Dubai for Arabic, and Noto Sans Devanagari and Noto
Sans Bengali for Indic scripts.

The reference holds, for each text, the font, the text and the glyph ID and place of each glyph in font units from
the start of the line, as HarfBuzz gives them. JoinerTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import harfbuzz_reference as ref

ZWNJ, ZWJ = 0x200C, 0x200D
JOINERS = [ZWJ, ZWNJ]

ACUTE = 0x0301
BEH, HEH, LAM, ALEF = 0x0628, 0x0647, 0x0644, 0x0627
CODE_POINTS = sorted([ord(c) for c in "abcdefghpqxy"] + [ACUTE, BEH, HEH, LAM, ALEF, ZWNJ, ZWJ])


def name(c):
    """The name that harfbuzz_reference.build_font gives the glyph of a character."""
    return "u%04X" % code(c)


def code(c):
    return ord(c) if isinstance(c, str) else c


ALTERNATES = [name(c) for c in "abde"] + [name(BEH), name(HEH), name(ALEF)]

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem arab dflt;

table GDEF {
    GlyphClassDef , , [u0301], ;
} GDEF;

%s

lookup RAISE {
    pos [u0067 u0068] <0 100 0 0>;
} RAISE;

feature calt {
    sub u0061' lookup ALT_u0061 u0062' lookup ALT_u0062;
    sub u0063 u0064' lookup ALT_u0064;
    sub u0065' lookup ALT_u0065 u0066;
    sub u0628' lookup ALT_u0628 u0647' lookup ALT_u0647;
    sub u0644 u0627' lookup ALT_u0627;
} calt;

feature liga {
    sub u0078 u0079 by x_y;
    sub u0644 u0644 by lam_lam;
} liga;

feature kern {
    pos u0070 u0071 -100;
    pos u0067' lookup RAISE u0068' lookup RAISE;
} kern;

markClass u0301 <anchor 0 0> @TOP;

feature mark {
    pos base u0062 <anchor 300 700> mark @TOP;
} mark;
""" % "\n".join("lookup ALT_%s { sub %s by %s.alt; } ALT_%s;" % (g, g, g, g) for g in ALTERNATES)

# Each pair of letters that a lookup matches, with a joiner between them in the texts.
PAIRS = [("a", "b"), ("c", "d"), ("e", "f"), ("x", "y"), ("p", "q"), ("g", "h"), (BEH, HEH), (LAM, ALEF), (LAM, LAM)]

# Arabic letters of Dubai: beh, teh, jeem, seen, ain, feh, qaf, kaf, lam, meem, noon, heh, yeh, alef, dal, reh, waw.
DUBAI = [0x0628, 0x062A, 0x062C, 0x0633, 0x0639, 0x0641, 0x0642, 0x0643, 0x0644, 0x0645, 0x0646, 0x0647, 0x064A,
         0x0627, 0x062F, 0x0631, 0x0648]

# Consonants, the virama and some vowel signs of Devanagari and Bengali.
DEVANAGARI = ([0x0915, 0x0916, 0x0917, 0x0924, 0x0928, 0x092A, 0x092E, 0x092F, 0x0930, 0x0932, 0x0935, 0x0937, 0x0938,
               0x0939], 0x094D, [0x093F, 0x0940, 0x0947, 0x094B])
BENGALI = ([0x0995, 0x0996, 0x0997, 0x09A4, 0x09A8, 0x09AA, 0x09AE, 0x09AF, 0x09B0, 0x09B2, 0x09B7, 0x09B8, 0x09B9],
           0x09CD, [0x09BF, 0x09C0, 0x09C7, 0x09CB])


def built_texts():
    texts = []
    for first, second in PAIRS:
        texts.append([code(first), code(second)])
        for joiner in JOINERS:
            texts.append([code(first), joiner, code(second)])
            texts.append([code(first), joiner, joiner, code(second)])
    for joiner in JOINERS:
        texts.append([ord("b"), joiner, ACUTE])
    return texts


def arabic_texts():
    texts = []
    for first in DUBAI:
        for second in DUBAI:
            texts.append([first, second])
            for joiner in JOINERS:
                texts.append([first, joiner, second])
    for joiner in JOINERS:
        for letter in DUBAI:
            texts.append([letter, joiner])
            texts.append([joiner, letter])
            texts.append([letter, letter, joiner, letter])
            texts.append([letter, joiner, letter, letter])
    return texts


def indic_texts(script):
    consonants, virama, vowels = script
    texts = []
    for first in consonants:
        for second in consonants:
            texts.append([first, virama, second])
            for joiner in JOINERS:
                texts.append([first, virama, joiner, second])
                texts.append([first, joiner, virama, second])
        for joiner in JOINERS:
            texts.append([first, virama, joiner])
            for vowel in vowels:
                texts.append([first, joiner, vowel])
                texts.append([first, virama, joiner, consonants[0], vowel])
    return texts


def main():
    path = ref.build_font("Joiners.ttf", CODE_POINTS, FEATURES, ["%s.alt" % g for g in ALTERNATES] + ["x_y", "lam_lam"])
    fonts = [
        (os.path.basename(path), built_texts()),
        ("Dubai-Regular.ttf", arabic_texts()),
        ("NotoSansDevanagari-Regular.ttf", indic_texts(DEVANAGARI)),
        ("NotoSansBengali-Regular.ttf", indic_texts(BENGALI)),
    ]

    rows = []
    for file_name, texts in fonts:
        hb_font = ref.load_font(os.path.join(ref.FONTS, file_name))
        for text in dict.fromkeys("".join(map(chr, text)) for text in texts):
            rows.append([file_name, text, ref.shape_line(hb_font, text)[0]])
    ref.write_reference(os.path.join(ref.FONTS, "Joiners.harfbuzz.json"), rows)


if __name__ == "__main__":
    main()
