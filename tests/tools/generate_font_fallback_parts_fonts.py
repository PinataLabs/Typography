#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds the five fonts of the fallback rows of FontFallbackTests that draw a character in parts, and prints the
glyphs that HarfBuzz gives each font alone.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_font_fallback_parts_fonts.py

FontFallbackParts.ttf is the font of the text. It has f, h, Bengali ka and the vowel sign e, and U+25CC. It lacks g, i,
the Bengali vowel signs aa and o, U+0941 and Khmer.

FontFallbackPartsWhole.ttf is a fallback font. It has f, g, h, i, Bengali ka, e and o, U+0941, Khmer ka and U+17BE, and
U+25CC, and lacks Bengali aa and Khmer U+17C1. So HarfBuzz keeps the two-part vowel signs o and U+17BE whole. liga makes
ligatures of f and i, g and h, and i and g, and blws makes one glyph of the dotted circle and U+0941.

FontFallbackPartsSplit.ttf is a fallback font that has every part: Bengali ka, e, aa and o, Khmer ka, U+17BE and U+17C1,
and U+25CC. So HarfBuzz splits o into e and aa, and U+17BE into U+17C1 and U+17BE, and moves the first part before ka.

FontFallbackPartsCircle.ttf is a font of the text that has Bengali ka and U+25CC, and lacks Bengali e, aa and o. So
HarfBuzz keeps o whole, one .notdef, and gives a lone o a dotted circle.

FontFallbackPartsNoCircle.ttf is a fallback font that has Bengali ka and o, and lacks Bengali e and aa, and U+25CC. So
HarfBuzz keeps o whole and gives a lone o no dotted circle.

The rows of FontFallbackTests give for each font alone the glyph IDs that this script prints.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from harfbuzz_reference import build_font, load_font, shape_line

LANGUAGES = "languagesystem DFLT dflt; languagesystem latn dflt; languagesystem dev2 dflt; " \
            "languagesystem bng2 dflt; languagesystem khmr dflt;"

FONTS = [
    ("FontFallbackParts.ttf", [0x66, 0x68, 0x0995, 0x09C7, 0x25CC], [], LANGUAGES),
    ("FontFallbackPartsWhole.ttf",
     [0x66, 0x67, 0x68, 0x69, 0x0941, 0x0995, 0x09C7, 0x09CB, 0x1780, 0x17BE, 0x25CC],
     ["f_i", "g_h", "i_g", "u25CC_u0941"],
     LANGUAGES + """
feature liga { sub u0066 u0069 by f_i; sub u0067 u0068 by g_h; sub u0069 u0067 by i_g; } liga;
feature blws { sub u25CC u0941 by u25CC_u0941; } blws;
"""),
    ("FontFallbackPartsSplit.ttf", [0x0995, 0x09BE, 0x09C7, 0x09CB, 0x1780, 0x17BE, 0x17C1, 0x25CC], [], LANGUAGES),
    ("FontFallbackPartsCircle.ttf", [0x0995, 0x25CC], [], LANGUAGES),
    ("FontFallbackPartsNoCircle.ttf", [0x0995, 0x09CB], [], LANGUAGES),
]

TEXTS = ["fi", "gh", "ig", "ु", "কো", "ো", "កើ"]


def main():
    for file_name, code_points, extra_glyphs, features in FONTS:
        font = load_font(build_font(file_name, code_points, features, extra_glyphs=extra_glyphs))
        for text in TEXTS:
            glyphs = " ".join(str(glyph[0]) for glyph in shape_line(font, text)[0])
            print(file_name, " ".join("%04X" % ord(c) for c in text), glyphs, sep="\t")


if __name__ == "__main__":
    main()
