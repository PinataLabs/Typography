#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds IndicOldSpec.ttf and writes IndicOldSpec.harfbuzz.json, how the Indic shaper of HarfBuzz reorders a syllable
for an old-spec script tag, such as deva, where the new-spec tag is dev2.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_indic_old_spec_reference.py

The font has empty glyphs for ka, ya and the virama of the nine Indic scripts, and for Devanagari ta, ra and ZWJ. Its
GSUB table has only the old-spec tags, so HarfBuzz takes each script as old spec. It has three features:

* pstf: ya, virama ligate to a post-base form, in the old-spec order of consonant and virama. In ka, virama, ya,
  virama the base is ka, and old spec moves the virama after ka to after ya, unless the script disallows two viramas
  after the last consonant. HarfBuzz disallows them in Kannada only. The ligature shows where the virama went.
* blwf: Devanagari ra, virama ligate to a below-base form. Old-spec Devanagari also gives this feature to a ra,
  virama before the base, unless a ZWJ follows them and asks for the eyelash ra.
* half: Devanagari ta, virama and ra, virama ligate to half forms. The eyelash ra takes the half form.

The reference holds the glyph IDs that HarfBuzz gives for each text. IndicOldSpecTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

FONT = "IndicOldSpec.ttf"
REFERENCE = os.path.join(FONTS, "IndicOldSpec.harfbuzz.json")

# The old-spec tag of each script, with its ka, ya and virama.
SCRIPTS = {
    "deva": (0x0915, 0x092F, 0x094D),
    "beng": (0x0995, 0x09AF, 0x09CD),
    "guru": (0x0A15, 0x0A2F, 0x0A4D),
    "gujr": (0x0A95, 0x0AAF, 0x0ACD),
    "orya": (0x0B15, 0x0B2F, 0x0B4D),
    "taml": (0x0B95, 0x0BAF, 0x0BCD),
    "telu": (0x0C15, 0x0C2F, 0x0C4D),
    "knda": (0x0C95, 0x0CAF, 0x0CCD),
    "mlym": (0x0D15, 0x0D2F, 0x0D4D),
}

# Devanagari ta, ra and ZWJ.
TA, RA, VIRAMA, ZWJ = 0x0924, 0x0930, 0x094D, 0x200D

CODE_POINTS = sorted({c for script in SCRIPTS.values() for c in script} | {TA, RA, ZWJ})

FEATURES = (
    "languagesystem DFLT dflt;\n"
    + "".join("languagesystem %s dflt;\n" % tag for tag in SCRIPTS)
    + "feature pstf {\n"
    + "".join("  sub u%04X u%04X by u%04X.pstf;\n" % (ya, virama, ya) for _, ya, virama in SCRIPTS.values())
    + "} pstf;\n"
    + "feature blwf { sub u0930 u094D by ra.blwf; } blwf;\n"
    + "feature half { sub u0930 u094D by ra.half; sub u0924 u094D by ta.half; } half;\n")

EXTRA_GLYPHS = ["u%04X.pstf" % ya for _, ya, _ in SCRIPTS.values()] + ["ra.blwf", "ra.half", "ta.half"]

TEXTS = [
    # ka, virama, ya, virama in each script.
    *([ka, virama, ya, virama] for ka, ya, virama in SCRIPTS.values()),
    # ta, virama, ra, virama, ka: ra, virama take blwf. With ZWJ after them they take half, the eyelash ra.
    [TA, VIRAMA, RA, VIRAMA, 0x0915],
    [TA, VIRAMA, RA, VIRAMA, ZWJ, 0x0915],
]


def shape(font, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    return [info.codepoint for info in buffer.glyph_infos]


def main():
    path = build_font(FONT, CODE_POINTS, FEATURES, extra_glyphs=EXTRA_GLYPHS)
    font = load_font(path)
    rows = [[text, shape(font, text)] for text in ("".join(map(chr, text)) for text in TEXTS)]
    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
