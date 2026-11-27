#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds MarksWithoutGlyphClasses.ttf and writes MarksWithoutGlyphClasses.harfbuzz.json, how HarfBuzz attaches marks
in a font that has no GDEF table.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_marks_without_glyph_classes_reference.py

Without glyph classes, HarfBuzz gives each glyph the class of its character: a nonspacing mark that is not default
ignorable is a mark, and any other character is a base. A ligature of components that are not all marks is a ligature,
whatever its first component is. The font has the letters a to g, the combining acute and grave accents, the grave
accent below U+0316, the combining grapheme joiner U+034F, the spacing mark U+1D165 and the enclosing mark U+20DD, as
empty glyphs 600 wide. Its mark lookup attaches each accent to a, b, d, e, the ligature of acute and a, and the two
components of the ligature of a and b, and its mkmk lookup attaches each accent to the acute. ccmp makes c d and e and f
d and g. liga makes a ligature of acute and a, and, skipping marks, one of a and b and one of b and b. feaLib builds a
GDEF table for the mark class, and the script removes it.

* a, acute, grave: mkmk attaches the grave to the acute.
* a, grave, acute: mark attaches the acute to a past the grave, which is a mark.
* acute, a, grave and acute, a, b: the ligature of acute and a is not a mark, so the grave attaches to it and it keeps
  its advance. The acute alone, before b, is a mark and has no advance.
* b, U+1D165, b: a spacing mark is a base, so the ligature that skips marks does not skip it. Past the acute, it does.
* acute, U+034F, a and b, U+034F, b: the joiner is a base, which blocks no reordering of marks there, so the ligatures
  skip it as a default-ignorable character. b, acute, U+034F, U+0316, b: there it keeps U+0316 after the acute, and
  the ligature that skips marks does not skip it.
* a, U+034F, acute and a, acute, U+034F, grave: mark positioning skips the joiner.
* a, U+1D165, acute: the acute does not attach past the spacing mark.
* c, acute: mark-to-base attaches the acute to e, the second glyph that c makes, which the lookup covers. f, acute: g is
  not covered, so the acute attaches to d, the first.
* a, acute, b, grave: the acute and the grave attach to the components of the ligature of a and b.
* b, U+20DD, b and a, U+20DD, acute: an enclosing mark is a base too, as the spacing mark is.

MarksWithoutGlyphClassesFallback.ttf has a, b, the two accents, U+1D165, U+20DD and the ligature of acute and a, and no
GPOS table, so HarfBuzz places marks around their base itself. A ligature of components that are not all marks takes
the general category Lo in place of Mn, so in acute, a, grave the grave goes over the ligature, as over b in b, grave.
The spacing mark and the enclosing mark after b keep their advance, as they are bases, and in b, U+20DD, grave the
grave still goes over b, the base of the cluster.

The reference holds the font, each text and the glyph ID and place of each glyph, in font units from the start of the line, as
HarfBuzz gives them. MarksWithoutGlyphClassesTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.ttLib import TTFont

from harfbuzz_reference import FONTS, build_font, load_font, shape_line, write_reference

FONT = "MarksWithoutGlyphClasses.ttf"
FALLBACK_FONT = "MarksWithoutGlyphClassesFallback.ttf"
REFERENCE = os.path.join(FONTS, "MarksWithoutGlyphClasses.harfbuzz.json")

CODE_POINTS = [0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x0300, 0x0301, 0x0316, 0x034F, 0x1D165, 0x20DD]

FEATURES = """
languagesystem DFLT dflt; languagesystem latn dflt;
markClass [u0301 u0300] <anchor 0 0> @Accent;
feature ccmp { sub u0063 by u0064 u0065; sub u0066 by u0064 u0067; } ccmp;
feature liga {
    sub u0301 u0061 by acute_a;
    lookup IgnoreMarks { lookupflag IgnoreMarks; sub u0061 u0062 by a_b; sub u0062 u0062 by b_b; } IgnoreMarks;
} liga;
feature mark {
    pos base u0061 <anchor 100 500> mark @Accent;
    pos base u0062 <anchor 200 500> mark @Accent;
    pos base u0064 <anchor 300 500> mark @Accent;
    pos base u0065 <anchor 400 500> mark @Accent;
    pos base acute_a <anchor 500 500> mark @Accent;
    pos ligature a_b <anchor 110 600> mark @Accent ligComponent <anchor 710 600> mark @Accent;
} mark;
feature mkmk { pos mark u0301 <anchor 50 900> mark @Accent; } mkmk;
"""

TEXTS = [
    "a\u0301\u0300",
    "a\u0300\u0301",
    "\u0301a\u0300",
    "\u0301ab",
    "\u0301b",
    "\u0301\u034Fa",
    "b\U0001D165b",
    "b\u0301b",
    "b\u034Fb",
    "b\u0301\u034F\u0316b",
    "a\u034F\u0301",
    "a\u0301\u034F\u0300",
    "a\U0001D165\u0301",
    "c\u0301",
    "f\u0301",
    "a\u0301b\u0300",
    "b\u20ddb",
    "a\u20dd\u0301",
]

FALLBACK_CODE_POINTS = [0x61, 0x62, 0x0300, 0x0301, 0x1D165, 0x20DD]

FALLBACK_FEATURES = "languagesystem DFLT dflt; languagesystem latn dflt; feature liga { sub u0301 u0061 by acute_a; } liga;"

FALLBACK_TEXTS = ["\u0301a\u0300", "b\u0300", "\u0301\u0300", "b\U0001D165", "b\u20dd", "b\u20dd\u0300"]


def build_font_without_gdef(file_name, code_points, features, extra_glyphs):
    path = build_font(file_name, code_points, features, extra_glyphs=extra_glyphs)
    font = TTFont(path)
    if "GDEF" in font:
        del font["GDEF"]
    font.save(path)
    return load_font(path)


def main():
    rows = []
    for file_name, code_points, features, extra_glyphs, texts in [
            (FONT, CODE_POINTS, FEATURES, ["acute_a", "a_b", "b_b"], TEXTS),
            (FALLBACK_FONT, FALLBACK_CODE_POINTS, FALLBACK_FEATURES, ["acute_a"], FALLBACK_TEXTS)]:
        hb_font = build_font_without_gdef(file_name, code_points, features, extra_glyphs)
        rows.extend([file_name, text, shape_line(hb_font, text)[0]] for text in texts)
    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
