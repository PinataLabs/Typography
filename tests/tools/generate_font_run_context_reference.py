#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds FontRunContext.ttf and FontRunContextFallback.ttf and writes FontRunContext.harfbuzz.json, which GPOS
lookups HarfBuzz applies when a fallback font draws some of the characters of a line (issue #198).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_font_run_context_reference.py

A caller of HarfBuzz shapes the characters that each font draws in a buffer of that font, so a lookup of one font
never sees a glyph of another. FontRunContext.ttf is the font of the text and has a, b, e, ZWJ, U+5B57 and the word
joiner U+2060, glyphs 1 to 6. FontRunContextFallback.ttf has x, y, the combining grapheme joiner U+034F, z, w, the
combining acute, U+5B58, U+5B57, ZWJ and the man and woman emoji, glyphs 1 to 11, and their ZWJ ligature, so a and x,
b and y, and e and U+034F have the same glyph IDs. Its GDEF table makes w and the acute marks. Neither font has the tag
character U+E0061. Each case has a lookup that would apply if it took the glyph of the other font for its own, or that
applies only if the characters of a cluster are in one buffer:

* FallbackPair: the fallback font moves z after x, and a comes before z.
* FallbackContext: a chained context lookup of the fallback font moves y after x, and a comes before y.
* FallbackMark: the fallback font attaches w to x, and w comes after a. The mark is not a mark in Unicode, so the
  library does not place it across fonts as it places a combining mark whose base another font draws (issue #191).
* PrimaryPair: the font of the text moves a after b, and y comes before a, after another a.
* FallbackIgnorable: the font of the text moves a after e, and the U+034F that it lacks comes before a. HarfBuzz hides a
  default-ignorable character that the font lacks, so it leaves no missing glyph for its caller to take from another
  font, and the U+034F stays with the a before it, in the buffer of the font of the text.
* PrimaryIgnorable: the fallback font moves z after x, and a ZWJ comes between them. The font of the text has the
  ZWJ, but the ZWJ is in the cluster of the x that it lacks, so the caller shapes the whole cluster with the fallback
  font, as it takes another font for each cluster with a missing glyph. x, ZWJ and z are then in one buffer.
* PrimaryWordJoiner: the fallback font moves z after x, and the word joiner of the font of the text, which is a
  cluster of its own, comes between them. The caller shapes it in a buffer of the font of the text, so x and z are in
  two buffers.
* MissingIgnorable: the fallback font attaches the acute to x, and the tag character U+E0061 that neither font has
  comes between them, in their cluster. The caller shapes the cluster with the fallback font, where HarfBuzz hides the
  tag character, so the acute attaches.
* ZwjLigature: the fallback font ligates the man, ZWJ and woman emoji. The font of the text has the ZWJ, but the
  caller shapes the whole cluster with the fallback font.

A default-ignorable character takes the font of the rest of its cluster: the fallback font when another character of
the cluster takes it, also when the font of the text has the default-ignorable character, and else the font of the
text, which hides it when it lacks it.

* MarkOrientation, in vertical mixed text: the fallback font moves U+5B58 up after the acute. The acute after U+5B57
  is sideways on its own, but it takes the upright orientation of its grapheme cluster, so the run of the fallback font
  that starts at it is one upright run with U+5B58. The vertical rows give the offset of each glyph from its vertical
  origin.

Each text is shaped as the runs of a caller, in a buffer for each run of characters that one font draws, and, as a
control, in one buffer of the font whose lookup the case tests, where the lookup applies. Each row holds the case, the
text, how the text is shaped ("runs", or the font of the one buffer, "primary" or "fallback", after "ttb " for vertical
text), and the glyph ID, x and y of each glyph. FontRunContextTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os
import unicodedata

import uharfbuzz as hb

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

REFERENCE = os.path.join(FONTS, "FontRunContext.harfbuzz.json")

PRIMARY = [0x0061, 0x0062, 0x0065, 0x200D, 0x5B57, 0x2060]
FALLBACK = [0x0078, 0x0079, 0x034F, 0x007A, 0x0077, 0x0301, 0x5B58, 0x5B57, 0x200D, 0x1F468, 0x1F469]
FALLBACK_LIGATURES = ["u1F468_u200D_u1F469"]

# The default-ignorable characters of the cases. Neither font has the tag character U+E0061.
IGNORABLE = [0x034F, 0x200D, 0xE0061, 0x2060]

LANGUAGES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
"""

PRIMARY_FEATURES = LANGUAGES + """
feature kern {
    pos u0062 <0 0 0 0> u0061 <-100 0 0 0>;
    pos u0065 <0 0 0 0> u0061 <-100 0 0 0>;
} kern;
"""

FALLBACK_FEATURES = LANGUAGES + """
table GDEF {
    GlyphClassDef [u0078 u0079 u007A u1F468 u1F469 u1F468_u200D_u1F469], , [u0077 u0301], ;
} GDEF;

feature ccmp {
    sub u1F468 u200D u1F469 by u1F468_u200D_u1F469;
} ccmp;

feature kern {
    lookup PAIR {
        pos u0078 <0 0 0 0> u007A <-100 0 0 0>;
    } PAIR;

    lookup CONTEXT {
        pos u0078 u0079' <0 100 0 0>;
    } CONTEXT;
} kern;

markClass u0077 <anchor 0 500> @TOP;
markClass u0301 <anchor 0 500> @ACUTE;

feature mark {
    lookup MARK_TO_BASE {
        pos base u0078 <anchor 300 700> mark @TOP <anchor 200 800> mark @ACUTE;
    } MARK_TO_BASE;

    lookup AFTER_ACUTE {
        pos u0301 u5B58' <0 100 0 0>;
    } AFTER_ACUTE;
} mark;
"""

# The case, its text, and the font of the one buffer in which the lookup applies, with the text of that buffer.
CASES = [
    ("FallbackPair", [0x0061, 0x007A], "fallback", [0x0078, 0x007A]),
    ("FallbackContext", [0x0061, 0x0079], "fallback", [0x0078, 0x0079]),
    ("FallbackMark", [0x0061, 0x0077], "fallback", [0x0078, 0x0077]),
    ("PrimaryPair", [0x0061, 0x0079, 0x0061], "primary", [0x0061, 0x0062, 0x0061]),
    ("FallbackIgnorable", [0x0061, 0x034F, 0x0061], "primary", [0x0065, 0x0061]),
    ("PrimaryIgnorable", [0x0078, 0x200D, 0x007A], "fallback", [0x0078, 0x200D, 0x007A]),
    ("PrimaryWordJoiner", [0x0078, 0x2060, 0x007A], "fallback", [0x0078, 0x007A]),
    ("MissingIgnorable", [0x0078, 0xE0061, 0x0301], "fallback", [0x0078, 0xE0061, 0x0301]),
    ("ZwjLigature", [0x1F468, 0x200D, 0x1F469], "fallback", [0x1F468, 0x200D, 0x1F469]),
]

# The cases of vertical text, as CASES.
VERTICAL_CASES = [
    ("MarkOrientation", [0x5B57, 0x0301, 0x5B58], "fallback", [0x5B57, 0x0301, 0x5B58]),
]


def is_extended_pictographic(code_point):
    """Whether the character is Extended_Pictographic, which is enough for the texts of the cases."""
    return 0x1F000 <= code_point <= 0x1FAFF


def clusters(code_points):
    """Gives the cluster of each character, as the library forms it from the grapheme cluster classes: a mark, a tag
    character, ZWJ and an Extended_Pictographic character after ZWJ continue the cluster before them, which is enough
    for the texts of the cases."""
    cluster = []
    for i, code_point in enumerate(code_points):
        continues = i > 0 and (
            unicodedata.category(chr(code_point)).startswith("M")
            or 0xE0020 <= code_point <= 0xE007F
            or code_point == 0x200D
            or (code_points[i - 1] == 0x200D and is_extended_pictographic(code_point)))
        cluster.append(cluster[-1] if continues else i)
    return cluster


def font_runs(code_points):
    """Splits the text into runs of the characters that one font draws: the font of the text if it has the character,
    or else the fallback font. A default-ignorable character takes the font of the rest of its cluster: the fallback
    font when another character of its cluster does, whether the font of the text has it or not, and else the font of
    the text. Gives the start, the end and the font of each run."""
    cluster = clusters(code_points)
    missing = {cluster[i] for i, c in enumerate(code_points) if c not in PRIMARY and c not in IGNORABLE}
    runs = []
    for i, code_point in enumerate(code_points):
        primary = cluster[i] not in missing if code_point in IGNORABLE else code_point in PRIMARY
        font = "primary" if primary else "fallback"
        if runs and runs[-1][2] == font:
            runs[-1][1] = i + 1
        else:
            runs.append([i, i + 1, font])
    return runs


def shape(fonts, code_points, runs, direction="ltr"):
    """Shapes each run in a buffer of its font, with the rest of the text as context, and gives the glyph ID, x and y
    of each glyph, in font units from the start of the line."""
    glyphs = []
    x = 0
    for start, end, font in runs:
        buffer = hb.Buffer()
        buffer.add_codepoints(code_points, start, end - start)
        buffer.guess_segment_properties()
        if direction == "ttb":
            buffer.direction = direction
        assert buffer.direction == direction, buffer.direction
        hb.shape(fonts[font], buffer)
        for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
            if direction == "ttb":
                # HarfBuzz gives the offset from the horizontal origin; the offset that GPOS gave is from the vertical one.
                origin_x, origin_y = fonts[font].get_glyph_v_origin(info.codepoint)
                glyphs.append([info.codepoint, position.x_offset + origin_x, position.y_offset + origin_y])
                continue
            glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
            x += position.x_advance
    return glyphs


def main():
    fonts = {
        "primary": load_font(build_font("FontRunContext.ttf", PRIMARY, PRIMARY_FEATURES)),
        "fallback": load_font(build_font("FontRunContextFallback.ttf", FALLBACK, FALLBACK_FEATURES, FALLBACK_LIGATURES)),
    }

    rows = []
    for case, text, font, control in CASES:
        rows.append([case, "".join(map(chr, text)), "runs", shape(fonts, text, font_runs(text))])
        rows.append([case, "".join(map(chr, control)), font, shape(fonts, control, [(0, len(control), font)])])

    for case, text, font, control in VERTICAL_CASES:
        rows.append([case, "".join(map(chr, text)), "ttb runs", shape(fonts, text, font_runs(text), "ttb")])
        rows.append([case, "".join(map(chr, control)), "ttb " + font, shape(fonts, control, [(0, len(control), font)], "ttb")])

    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
