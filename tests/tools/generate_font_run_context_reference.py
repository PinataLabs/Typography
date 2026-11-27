#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds FontRunContext.ttf and FontRunContextFallback.ttf and writes FontRunContext.harfbuzz.json, which GPOS
lookups HarfBuzz applies when a fallback font draws some of the characters of a line (issue #198).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_font_run_context_reference.py

A caller of HarfBuzz shapes the characters that each font draws in a buffer of that font, so a lookup of one font
never sees a glyph of another. FontRunContext.ttf is the font of the text and has a and b, glyphs 1 and 2.
FontRunContextFallback.ttf has x, y, z and w, glyphs 1 to 4, so a and x, and b and y, have the same glyph IDs. Its
GDEF table makes w a mark. Each case has a lookup that would apply if it took the glyph of the other font for its own:

* FallbackPair: the fallback font moves z after x, and a comes before z.
* FallbackContext: a chained context lookup of the fallback font moves y after x, and a comes before y.
* FallbackMark: the fallback font attaches w to x, and w comes after a. The mark is not a mark in Unicode, so the
  library does not place it across fonts as it places a combining mark whose base another font draws (issue #191).
* PrimaryPair: the font of the text moves a after b, and y comes before a, after another a.

Each text is shaped as the runs of a caller, in a buffer for each run of characters that one font draws, and, as a
control, in one buffer of the font whose lookup the case tests, where the lookup applies. Each row holds the case, the
text, how the text is shaped ("runs", or the font of the one buffer, "primary" or "fallback"), and the glyph ID, x and
y of each glyph. FontRunContextTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

REFERENCE = os.path.join(FONTS, "FontRunContext.harfbuzz.json")

PRIMARY = [0x0061, 0x0062]
FALLBACK = [0x0078, 0x0079, 0x007A, 0x0077]

LANGUAGES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
"""

PRIMARY_FEATURES = LANGUAGES + """
feature kern {
    pos u0062 <0 0 0 0> u0061 <-100 0 0 0>;
} kern;
"""

FALLBACK_FEATURES = LANGUAGES + """
table GDEF {
    GlyphClassDef [u0078 u0079 u007A], , [u0077], ;
} GDEF;

feature kern {
    lookup PAIR {
        pos u0078 <0 0 0 0> u007A <-100 0 0 0>;
    } PAIR;

    lookup CONTEXT {
        pos u0078 u0079' <0 100 0 0>;
    } CONTEXT;
} kern;

markClass u0077 <anchor 0 500> @TOP;

feature mark {
    pos base u0078 <anchor 300 700> mark @TOP;
} mark;
"""

# The case, its text, and the font of the one buffer in which the lookup applies, with the text of that buffer.
CASES = [
    ("FallbackPair", [0x0061, 0x007A], "fallback", [0x0078, 0x007A]),
    ("FallbackContext", [0x0061, 0x0079], "fallback", [0x0078, 0x0079]),
    ("FallbackMark", [0x0061, 0x0077], "fallback", [0x0078, 0x0077]),
    ("PrimaryPair", [0x0061, 0x0079, 0x0061], "primary", [0x0061, 0x0062, 0x0061]),
]


def font_runs(code_points):
    """Splits the text into runs of the characters that one font draws: the font of the text if it has the character,
    or else the fallback font. Gives the start, the end and the font of each run."""
    runs = []
    for i, code_point in enumerate(code_points):
        font = "primary" if code_point in PRIMARY else "fallback"
        if runs and runs[-1][2] == font:
            runs[-1][1] = i + 1
        else:
            runs.append([i, i + 1, font])
    return runs


def shape(fonts, code_points, runs):
    """Shapes each run in a buffer of its font, with the rest of the text as context, and gives the glyph ID, x and y
    of each glyph, in font units from the start of the line."""
    glyphs = []
    x = 0
    for start, end, font in runs:
        buffer = hb.Buffer()
        buffer.add_codepoints(code_points, start, end - start)
        buffer.guess_segment_properties()
        assert buffer.direction == "ltr", buffer.direction
        hb.shape(fonts[font], buffer)
        for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
            glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
            x += position.x_advance
    return glyphs


def main():
    fonts = {
        "primary": load_font(build_font("FontRunContext.ttf", PRIMARY, PRIMARY_FEATURES)),
        "fallback": load_font(build_font("FontRunContextFallback.ttf", FALLBACK, FALLBACK_FEATURES)),
    }

    rows = []
    for case, text, font, control in CASES:
        rows.append([case, "".join(map(chr, text)), "runs", shape(fonts, text, font_runs(text))])
        rows.append([case, "".join(map(chr, control)), font, shape(fonts, control, [(0, len(control), font)])])

    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
