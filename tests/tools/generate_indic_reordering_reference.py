#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds IndicReordering.ttf and writes IndicReordering.harfbuzz.json, how the Indic shaper of HarfBuzz reorders a
pre-base-reordering consonant and finds the base of a syllable that has none after the sort.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_indic_reordering_reference.py

The font has empty glyphs for some Devanagari and Telugu letters and four features:

* pref: virama, ra ligate to a pre-base-reordering consonant. HarfBuzz moves it after the last matra or halant before
  the base. With a left matra before the base, that is between the matra and the base, even when a below-base matra
  follows the base.
* rphf: Telugu ra, virama ligate to a reph. Telugu forms a reph from ra, virama, ZWJ. In the syllable ra, virama, ZWJ the
  ra is the base and the reph, so the sort leaves no base, and HarfBuzz takes every glyph as one before the base.
* half and blwf: each gives ZWJ a glyph of its own, so the reference shows whether HarfBuzz took ZWJ as before or after
  the base.

The reference holds the glyph IDs that HarfBuzz gives for each text. IndicReorderingTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

FONT = "IndicReordering.ttf"
REFERENCE = os.path.join(FONTS, "IndicReordering.harfbuzz.json")

# Devanagari ka, ra, virama, u, uu, i, e; Telugu ra, virama, ka; ZWJ; dotted circle.
CODE_POINTS = [0x0915, 0x0930, 0x094D, 0x0941, 0x0942, 0x093F, 0x0947, 0x0C30, 0x0C4D, 0x0C15, 0x200D, 0x25CC]

FEATURES = """
languagesystem DFLT dflt;
languagesystem dev2 dflt;
languagesystem tel2 dflt;

feature pref { sub u094D u0930 by rapref; } pref;
feature rphf { sub u0C30 u0C4D by reph; } rphf;
feature half { sub u200D by zwj.half; } half;
feature blwf { sub u200D by zwj.blwf; } blwf;
"""

TEXTS = [
    # ka, virama, ra with no matra, a left matra i, a below matra u or uu, and both.
    [0x0915, 0x094D, 0x0930],
    [0x0915, 0x094D, 0x0930, 0x093F],
    [0x0915, 0x094D, 0x0930, 0x0941],
    [0x0915, 0x094D, 0x0930, 0x0941, 0x093F],
    [0x0915, 0x094D, 0x0930, 0x093F, 0x0941],
    [0x0915, 0x094D, 0x0930, 0x0942, 0x093F],
    [0x0915, 0x094D, 0x0930, 0x0947, 0x0941, 0x093F],
    # Telugu ra, virama, ZWJ alone and before ka, and the syllables with no reph.
    [0x0C30, 0x0C4D, 0x200D],
    [0x0C30, 0x0C4D, 0x200D, 0x0C15],
    [0x0C30, 0x0C4D, 0x0C15],
    [0x0C15, 0x0C4D, 0x200D],
]


def shape(font, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    return [info.codepoint for info in buffer.glyph_infos]


def main():
    path = build_font(FONT, CODE_POINTS, FEATURES, extra_glyphs=["rapref", "reph", "zwj.half", "zwj.blwf"])
    font = load_font(path)
    rows = [[text, shape(font, text)] for text in ("".join(map(chr, text)) for text in TEXTS)]
    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
