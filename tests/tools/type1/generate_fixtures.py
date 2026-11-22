"""Writes the Type 1 reference fixtures that Type1ParityTests compares against.

Reference: fontTools (t1Lib.T1Font, T1CharString.draw, DecomposingRecordingPen).
Run from anywhere with fontTools 4.68 or later:

    python tests/tools/type1/generate_fixtures.py

Output, next to the fonts in tests/PinataLabs.Typography.Tests/Fonts/Type1:

  <font>.json        FontName, FontMatrix, built-in encoding and, per glyph in
                     /CharStrings order, the advance and the outline.
  bchr8a.pfa         bchr8a.pfb as a PFA with a hexadecimal eexec section.
  bchr8a.fontfile    bchr8a.pfb as a PDF /FontFile stream: cleartext, binary
                     eexec section, trailer.

Outline format: one string per glyph, tokens separated by spaces.
  M x y            moveTo
  L x y            lineTo
  C x1 y1 x2 y2 x y  curveTo
  Z                closePath
  E                endPath (fontTools ends a contour the charstring left open)
Coordinates are in glyph space, y up, exactly as fontTools reports them.
A seac glyph is decomposed the way fontTools does it: the base component at
the origin, the accent at (adx + sbx - asb, ady).
"""

import json
import os
import sys

from fontTools import t1Lib
from fontTools.pens.recordingPen import DecomposingRecordingPen

FONTS = ["bchr8a.pfb", "cmr10.pfb", "cmmi10.pfb", "qtmr.pfb"]
CONTAINER_SOURCE = "bchr8a.pfb"

HERE = os.path.dirname(os.path.abspath(__file__))
FONT_DIR = os.path.normpath(
    os.path.join(HERE, "..", "..", "PinataLabs.Typography.Tests", "Fonts", "Type1")
)


def number(value):
    if isinstance(value, float) and value.is_integer():
        value = int(value)
    return repr(value)


def outline(pen):
    tokens = []
    for operator, points in pen.value:
        if operator == "moveTo":
            tokens.append("M")
        elif operator == "lineTo":
            tokens.append("L")
        elif operator == "curveTo":
            if len(points) != 3:
                raise ValueError("unexpected curveTo with %d points" % len(points))
            tokens.append("C")
        elif operator == "closePath":
            tokens.append("Z")
        elif operator == "endPath":
            tokens.append("E")
        else:
            raise ValueError("unexpected pen operator " + operator)
        for x, y in points:
            tokens.append(number(x))
            tokens.append(number(y))
    return " ".join(tokens)


def describe(file_name):
    font = t1Lib.T1Font(os.path.join(FONT_DIR, file_name))
    font.parse()
    glyph_set = font.getGlyphSet()
    glyphs = []
    for name, char_string in glyph_set.items():
        pen = DecomposingRecordingPen(glyph_set)
        char_string.draw(pen)
        glyphs.append(
            {"name": name, "advance": char_string.width, "outline": outline(pen)}
        )
    return {
        "source": file_name,
        "reference": "fontTools " + __import__("fontTools").version,
        "fontName": font["FontName"],
        "fontMatrix": list(font["FontMatrix"]),
        "encoding": list(font["Encoding"]),
        "glyphs": glyphs,
    }


def write_json(file_name, value):
    with open(os.path.join(FONT_DIR, file_name), "w", newline="\n") as stream:
        json.dump(value, stream, indent=None, separators=(",", ":"))
        stream.write("\n")


def write_converted_fonts():
    data = t1Lib.readPFB(os.path.join(FONT_DIR, CONTAINER_SOURCE))
    clear, encrypted, trailer = [chunk for _, chunk in t1Lib.findEncryptedChunks(data)]
    stem = os.path.splitext(CONTAINER_SOURCE)[0]

    t1Lib.writeOther(os.path.join(FONT_DIR, stem + ".pfa"), data, dohex=True)
    with open(os.path.join(FONT_DIR, stem + ".fontfile"), "wb") as stream:
        stream.write(clear + encrypted + trailer)


def main():
    for file_name in FONTS:
        description = describe(file_name)
        write_json(os.path.splitext(file_name)[0] + ".json", description)
        print("%s: %d glyphs" % (file_name, len(description["glyphs"])))
    write_converted_fonts()
    return 0


if __name__ == "__main__":
    sys.exit(main())
