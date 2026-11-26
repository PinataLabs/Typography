#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes Khmer.harfbuzz.json, how HarfBuzz shapes Khmer syllables in NotoSansKhmer-Regular.ttf.

Usage (from the repository root, uharfbuzz):

    python tests/tools/generate_khmer_reference.py

HarfBuzz has a shaper of its own for Khmer. It applies the features of each stage in its order, moves a COENG RO
and a vowel that goes in front of the base before the base, and splits a two-part vowel. The reference holds the
glyph IDs and where HarfBuzz draws each glyph for a consonant with a vowel, with one or two subscript consonants
before the vowel, with a register shifter, with ROBAT and with a sign after the vowel. KhmerTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

from harfbuzz_reference import FONTS, load_font, write_unique_rows

FONT = "NotoSansKhmer-Regular.ttf"
REFERENCE = os.path.join(FONTS, "Khmer.harfbuzz.json")

COENG = 0x17D2

# KA, BA, RO, SA, NYO.
CONSONANTS = [0x1780, 0x1794, 0x179A, 0x179F, 0x1789]

# Subscripts: KA, RO (which goes in front of the base), TA, SA, NYO.
SUBSCRIPTS = [0x1780, 0x179A, 0x178F, 0x179F, 0x1789]

# AA, I, II, U, UU, the two-part vowels OE, YA, IE, OO, AU, and E, AE, AI (in front of the base), and AM, AAM.
VOWELS = [0x17B6, 0x17B7, 0x17B8, 0x17BB, 0x17BC, 0x17BE, 0x17BF, 0x17C0, 0x17C4, 0x17C5, 0x17C1, 0x17C2, 0x17C3, 0x17C6, 0x17C7]

# MUUSIKATOAN and TRIISAP (register shifters), ROBAT, TOANDAKHIAT, BANTOC.
SIGNS = [0x17C9, 0x17CA, 0x17CC, 0x17CD, 0x17CB]


def main():
    font = load_font(os.path.join(FONTS, FONT))

    texts = []
    for consonant in CONSONANTS:
        texts += [[consonant, vowel] for vowel in VOWELS]
        texts += [[consonant, sign] for sign in SIGNS]
        for subscript in SUBSCRIPTS:
            texts.append([consonant, COENG, subscript])
            texts += [[consonant, COENG, subscript, vowel] for vowel in VOWELS]
            texts += [[consonant, COENG, subscript, COENG, second, 0x17C1] for second in SUBSCRIPTS if second != subscript]
        for sign in SIGNS:
            texts += [[consonant, sign, vowel] for vowel in VOWELS]
            texts += [[consonant, vowel, sign] for vowel in VOWELS]

    write_unique_rows(REFERENCE, texts, font, font=FONT)


if __name__ == "__main__":
    main()
