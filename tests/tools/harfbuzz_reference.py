# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Helpers that the HarfBuzz reference generators of this folder share: build a font of empty glyphs, shape a line
with HarfBuzz, and write a reference file.

A generator imports it with `import harfbuzz_reference`. Python finds it because it is in the folder of the script.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.ttLib.tables._g_l_y_f import Glyph

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")


def build_font(file_name, code_points, features=None, extra_glyphs=(), units_per_em=1000, advances=None):
    """Saves a TrueType font of empty glyphs, 600 units wide, one for each code point, and gives its path.

    `features` is feature code for the GSUB table, and `extra_glyphs` are glyph names that no code point maps to.
    `units_per_em` is the size of the em, and `advances` maps a code point to a width other than 600."""
    names = [".notdef"] + ["u%04X" % c for c in code_points] + list(extra_glyphs)
    widths = {"u%04X" % c: w for c, w in (advances or {}).items()}
    builder = FontBuilder(units_per_em, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({c: "u%04X" % c for c in code_points})
    builder.setupGlyf({name: Glyph() for name in names})
    builder.setupHorizontalMetrics({name: (widths.get(name, 600), 0) for name in names})
    builder.setupHorizontalHeader(ascent=units_per_em * 4 // 5, descent=-(units_per_em // 5))
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=units_per_em * 4 // 5, sTypoDescender=-(units_per_em // 5),
                     usWinAscent=units_per_em * 4 // 5, usWinDescent=units_per_em // 5)
    builder.setupPost(keepGlyphNames=False)
    if features:
        addOpenTypeFeaturesFromString(builder.font, features)
    path = os.path.join(FONTS, file_name)
    builder.font.save(path)
    return path


def load_font(path):
    with open(path, "rb") as file:
        return hb.Font(hb.Face(file.read()))


def shape_line(font, text):
    """Gives the glyph ID and place of each glyph, in font units from the start of the line, and the advance."""
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
        x += position.x_advance
    return glyphs, x


def write_reference(path, rows, **header):
    """Writes the HarfBuzz version, the `header` entries and one JSON row for each line of `rows`."""
    with open(path, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        for key, value in header.items():
            file.write('  "%s": %s,\n' % (key, json.dumps(value)))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(path, ROOT), len(rows), "rows")


def write_unique_rows(path, texts, hb_font, **header):
    """Shapes each distinct text of `texts`, which are lists of code points, and writes the reference."""
    rows = [[text, shape_line(hb_font, text)[0]] for text in dict.fromkeys("".join(map(chr, text)) for text in texts)]
    write_reference(path, rows, **header)
