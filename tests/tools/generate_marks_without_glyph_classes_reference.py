#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds MarksWithoutGlyphClasses.ttf and writes MarksWithoutGlyphClasses.harfbuzz.json, how HarfBuzz attaches marks
in a font that has no GDEF table.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_marks_without_glyph_classes_reference.py

Without glyph classes, HarfBuzz takes the glyph of a nonspacing mark for a mark. The font has the letter a and the
combining acute and grave accents, as empty glyphs 600 wide. Its mark lookup attaches each accent to a, and its mkmk
lookup attaches each accent to the acute. feaLib builds a GDEF table for the mark class, and the script removes it.

* a, acute, grave: mkmk attaches the grave to the acute.
* a, grave, acute: mark attaches the acute to a past the grave, which is a mark.

The reference holds each text and the glyph ID and place of each glyph, in font units from the start of the line, as
HarfBuzz gives them. MarksWithoutGlyphClassesTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.ttLib import TTFont

from harfbuzz_reference import FONTS, build_font, load_font, shape_line, write_reference

FONT = "MarksWithoutGlyphClasses.ttf"
REFERENCE = os.path.join(FONTS, "MarksWithoutGlyphClasses.harfbuzz.json")

FEATURES = (
    "languagesystem DFLT dflt; languagesystem latn dflt;"
    " markClass [u0301 u0300] <anchor 0 0> @Accent;"
    " feature mark { pos base u0061 <anchor 100 500> mark @Accent; } mark;"
    " feature mkmk { pos mark u0301 <anchor 50 900> mark @Accent; } mkmk;")

TEXTS = ["á̀", "à́"]


def main():
    path = build_font(FONT, [0x61, 0x0300, 0x0301], FEATURES)
    font = TTFont(path)
    del font["GDEF"]
    font.save(path)
    hb_font = load_font(path)
    write_reference(REFERENCE, [[text, shape_line(hb_font, text)[0]] for text in TEXTS])


if __name__ == "__main__":
    main()
