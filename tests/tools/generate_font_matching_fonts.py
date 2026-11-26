#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Generates the Matching Sans fonts, the static family that the font matching tests use.

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_font_matching_fonts.py

The family has nine static weights, three italic faces, two oblique faces and five faces
of other widths, so one family has more than the four faces that name ID 1 can group.
The fonts go to tests/PinataLabs.Typography.Tests/Fonts/Matching. A test picks the faces
for a family by choosing which files it adds to a FontCollection.

Each face is built as the font of a real family is:

* The four faces that the legacy family name groups (Regular, Italic, Bold and Bold Italic)
  have name ID 1 "Matching Sans" and no name IDs 16 and 17. The library falls back to ID 1.
* Every other face has its own name ID 1, such as "Matching Sans Light", and the typographic
  names, ID 16 "Matching Sans" and ID 17 with its style, so that it joins one family with the rest.
* OS/2.usWeightClass and usWidthClass give the weight and the width, fsSelection ITALIC or OBLIQUE
  gives the slope, and post.italicAngle gives the angle of an oblique face.
* Glyph A is one box with an advance that is different in each face, so that a test
  can tell from the measured advance which face a layout drew.

The output is deterministic, so a second run changes no file.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts", "Matching")

FAMILY = "Matching Sans"

ITALIC = 1 << 0
BOLD = 1 << 5
REGULAR = 1 << 6
USE_TYPO_METRICS = 1 << 7
OBLIQUE = 1 << 9

# usWidthClass to percent, from the OS/2 chapter of the OpenType specification:
# 1 = 50, 2 = 62.5, 3 = 75, 4 = 87.5, 5 = 100, 6 = 112.5, 7 = 125, 8 = 150, 9 = 200.
#
# (file stem, usWeightClass, usWidthClass, slope, name ID 1, name ID 2, name ID 17 or None)
# The slope is "normal", "italic" or the italic angle in degrees of an oblique face (post.italicAngle).
FACES = [
    ("Thin", 100, 5, "normal", "Matching Sans Thin", "Regular", "Thin"),
    ("ExtraLight", 200, 5, "normal", "Matching Sans ExtraLight", "Regular", "ExtraLight"),
    ("Light", 300, 5, "normal", "Matching Sans Light", "Regular", "Light"),
    ("Regular", 400, 5, "normal", "Matching Sans", "Regular", None),
    ("Medium", 500, 5, "normal", "Matching Sans Medium", "Regular", "Medium"),
    ("SemiBold", 600, 5, "normal", "Matching Sans SemiBold", "Regular", "SemiBold"),
    ("Bold", 700, 5, "normal", "Matching Sans", "Bold", None),
    ("ExtraBold", 800, 5, "normal", "Matching Sans ExtraBold", "Regular", "ExtraBold"),
    ("Black", 900, 5, "normal", "Matching Sans Black", "Regular", "Black"),
    ("LightItalic", 300, 5, "italic", "Matching Sans Light", "Italic", "Light Italic"),
    ("Italic", 400, 5, "italic", "Matching Sans", "Italic", None),
    ("BoldItalic", 700, 5, "italic", "Matching Sans", "Bold Italic", None),
    ("Oblique", 400, 5, -10.0, "Matching Sans Oblique", "Regular", "Oblique"),
    ("SteepOblique", 400, 5, -20.0, "Matching Sans Steep", "Regular", "Steep Oblique"),
    ("ExtraCondensed", 400, 2, "normal", "Matching Sans ExtraCondensed", "Regular", "ExtraCondensed"),
    ("Condensed", 400, 3, "normal", "Matching Sans Condensed", "Regular", "Condensed"),
    ("CondensedBold", 700, 3, "normal", "Matching Sans Condensed", "Bold", "Condensed Bold"),
    ("Expanded", 400, 7, "normal", "Matching Sans Expanded", "Regular", "Expanded"),
    ("ExtraExpanded", 400, 8, "normal", "Matching Sans ExtraExpanded", "Regular", "ExtraExpanded"),
]

GLYPHS = [".notdef", "space", "A"]
CMAP = {0x20: "space", 0x41: "A"}


def box(width):
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((width - 50, 700))
    pen.lineTo((width - 50, 0))
    pen.closePath()
    return pen.glyph()


def build(index, face):
    stem, weight, width_class, slope, id1, id2, id17 = face
    # The advance of glyph A is unique to the face.
    advance = 600 + 10 * index
    advances = {".notdef": 500, "space": 250, "A": advance}

    fs_selection = USE_TYPO_METRICS
    if slope == "italic":
        fs_selection |= ITALIC
    elif slope != "normal":
        fs_selection |= OBLIQUE
    if id2 in ("Bold", "Bold Italic"):
        fs_selection |= BOLD
    if id2 == "Regular":
        fs_selection |= REGULAR

    names = {
        "familyName": id1,
        "styleName": id2,
        "uniqueFontIdentifier": "Matching Sans " + stem,
        "fullName": "Matching Sans " + (id17 or id2),
        "version": "Version 1.000",
        "psName": "MatchingSans-" + stem,
    }
    if id17 is not None:
        names["typographicFamily"] = FAMILY
        names["typographicSubfamily"] = id17

    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    builder.setupGlyf({name: box(advances[name]) for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (advances[name], 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable(names, mac=False)
    builder.setupOS2(
        version=4,
        usWeightClass=weight,
        usWidthClass=width_class,
        fsSelection=fs_selection,
        sTypoAscender=800,
        sTypoDescender=-200,
        usWinAscent=800,
        usWinDescent=200,
    )
    if slope == "normal":
        italic_angle = 0.0
    elif slope == "italic":
        italic_angle = -12.0
    else:
        italic_angle = slope
    builder.setupPost(italicAngle=italic_angle, keepGlyphNames=False)

    mac_style = 0
    if id2 in ("Bold", "Bold Italic"):
        mac_style |= 1
    if slope == "italic":
        mac_style |= 2
    builder.font["head"].macStyle = mac_style

    path = os.path.join(OUT, "MatchingSans-%s.ttf" % stem)
    builder.font.save(path)
    return path


def main():
    os.makedirs(OUT, exist_ok=True)
    for index, face in enumerate(FACES):
        print(os.path.relpath(build(index, face), ROOT))


if __name__ == "__main__":
    main()
