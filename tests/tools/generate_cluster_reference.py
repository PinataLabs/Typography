#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes Clusters.harfbuzz.json, how HarfBuzz shapes a sign that has no letter of its script before it.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_cluster_reference.py

The script does not read the HarfBuzz source. It builds fonts that have no GSUB table, so only the shaper
changes the order and the number of the glyphs:

* ClustersBalinese.ttf has Balinese letters and signs, the dotted circle, and the characters of the Basic Latin
  block and some others that have the script Common. HarfBuzz shapes Balinese text with its universal shaper.
* ClustersDevanagari.ttf has the same for Devanagari, which HarfBuzz shapes with its Indic shaper.

The reference holds the glyph IDs that HarfBuzz gives, and where it draws each glyph, for:

* each sign alone, before a letter, and after a letter and a space. Such a sign starts a broken cluster, and
  HarfBuzz gives it a dotted circle as its base.
* each sign after each other sign, and each sign before and after the virama. When the first sign goes in front of
  the base, the sign after it goes there too, before the dotted circle. HarfBuzz joins a virama and some of the
  signs next to it in one cluster with one dotted circle.
* each Common character before a vowel sign that goes above or after the base, and before one that goes in front
  of it. The universal shaper takes most of these characters as the base of the sign.

ClustersMalayalam.ttf and ClustersMasaramGondi.ttf have a letter, the repha that the text has before its letter,
the dotted circle and the space. The reference holds the same for a repha alone and before a letter, a space or
a second repha. A repha with no letter after it is a broken cluster of its own, and HarfBuzz puts the dotted
circle after it and leaves the cluster that follows as it is.

* in ClustersDevanagari.ttf, each sign after U+2015, U+2022 and U+25FB to U+25FE. The Indic shaper takes these
  characters as bases, as it does the dotted circle.

ClusterTests reads the reference.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb
from harfbuzz_reference import FONTS, build_font, load_font, write_reference


REFERENCE = os.path.join(FONTS, "Clusters.harfbuzz.json")

DOTTED_CIRCLE = 0x25CC
SPACE = 0x20

# Characters of the script Common. A caller of HarfBuzz gives them the script of the text around them.
# NO-BREAK SPACE and the dashes are bases in the Unicode data of both shapers. The universal shaper does not take
# a right square or curly bracket as a base.
COMMON = [c for c in range(0x20, 0x7F) if not chr(c).isalpha()] + [0xA0, 0xD7, 0x2010, 0x2013, 0x2014, 0x27E7, 0x3000]

# (font, ISO 15924 code, letter, sign above or after the base, sign in front of the base, other signs, virama,
#  characters that only the shaper of the script takes as bases)
SCRIPTS = [
    # KA, the vowel signs ULU and TALING, the signs SURANG, BISAH, REREKAN and SUKU, and ADEG ADEG.
    ("ClustersBalinese.ttf", "Bali", 0x1B13, 0x1B36, 0x1B3E, [0x1B03, 0x1B04, 0x1B34, 0x1B38], 0x1B44, []),

    # KA, the vowel signs AA and I, the signs ANUSVARA, NUKTA, II and U, and VIRAMA. HORIZONTAL BAR, BULLET and
    # the four medium squares.
    ("ClustersDevanagari.ttf", "Deva", 0x0915, 0x093E, 0x093F, [0x0902, 0x093C, 0x0940, 0x0941], 0x094D,
     [0x2015, 0x2022, 0x25FB, 0x25FC, 0x25FD, 0x25FE]),
]

# (font, ISO 15924 code, letter, repha that the text has before the letter)
REPHA_SCRIPTS = [
    # KA and DOT REPH, in the Indic shaper.
    ("ClustersMalayalam.ttf", "Mlym", 0x0D15, 0x0D4E),

    # KA and REPHA, in the universal shaper.
    ("ClustersMasaramGondi.ttf", "Gonm", 0x11D0C, 0x11D46),
]


def build(file_name, code_points):
    return load_font(build_font(file_name, code_points))


def shape(font, script, text):
    """Gives the glyph ID of each glyph and where HarfBuzz draws it, in font units from the start of the line."""
    # The text has one script, as a run that a caller of HarfBuzz itemizes. A space takes the script of the run.
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
    rows = []
    for file_name, script, letter, after, front, others, virama, bases in SCRIPTS:
        signs = sorted([after, front, *others])
        font = build(file_name, sorted({*COMMON, *bases, DOTTED_CIRCLE, letter, *signs, virama}))
        texts = []
        for sign in [*signs, virama]:
            texts += [[sign], [sign, letter], [letter, SPACE, sign]]
        for sign in [*signs, virama]:
            texts += [[other, sign] for other in [*signs, virama]]
        for character in COMMON:
            texts += [[character, after], [character, front]]
        for character in bases:
            texts += [[character, sign] for sign in [*signs, virama]]
        for text in dict.fromkeys("".join(map(chr, text)) for text in texts):
            rows.append([file_name, script, text, shape(font, script, text)])

    for file_name, script, letter, repha in REPHA_SCRIPTS:
        font = build(file_name, sorted({SPACE, DOTTED_CIRCLE, letter, repha}))
        texts = [[repha], [repha, letter], [repha, SPACE, letter], [repha, repha, letter], [letter, repha, repha, letter]]
        for text in texts:
            text = "".join(map(chr, text))
            rows.append([file_name, script, text, shape(font, script, text)])

    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
