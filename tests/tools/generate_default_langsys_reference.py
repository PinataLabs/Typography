#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes DefaultLangSys.harfbuzz.json, the HarfBuzz reference for language system selection.

Usage (from the repository root, uharfbuzz):

    python tests/tools/generate_default_langsys_reference.py

The `latn` script of NotoSansOgham-Regular.ttf and NotoSansRunic-Regular.ttf has only the `MOL `
and `ROM ` language systems and no default LangSys. With no language requested, HarfBuzz applies
no feature of that script, so Latin text gets no `kern` from GPOS and no `locl` from GSUB.
DejaVuSans.ttf has a default LangSys for `latn`, so HarfBuzz kerns the same text in that font.

The reference holds the glyph IDs and positions that HarfBuzz gives for each case, in font units
from the origin of the line. DefaultLangSysTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os

import uharfbuzz as hb

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "DefaultLangSys.harfbuzz.json")

CASES = [
    ("OghamKern", "Noto/NotoSansOgham-Regular.ttf", "AVATAR Wavy To"),
    ("OghamLocl", "Noto/NotoSansOgham-Regular.ttf", "Şş"),
    ("RunicKern", "Noto/NotoSansRunic-Regular.ttf", "AVATAR Wavy To"),
    ("RunicLocl", "Noto/NotoSansRunic-Regular.ttf", "Şş"),
    ("DejaVuSansKern", "DejaVuSans.ttf", "AVATAR Wavy To"),
]


def shape(path, text):
    with open(os.path.join(FONTS, path), "rb") as file:
        font = hb.Font(hb.Face(file.read()))

    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)

    glyphs = []
    x = y = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append({"id": info.codepoint, "x": x + position.x_offset, "y": y + position.y_offset})
        x += position.x_advance
        y += position.y_advance
    return glyphs


def main():
    cases = [
        {"name": name, "font": path, "text": text, "glyphs": shape(path, text)}
        for name, path, text in CASES
    ]
    reference = {"harfbuzz": hb.version_string(), "cases": cases}
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        json.dump(reference, file, indent=2, ensure_ascii=False)
        file.write("\n")
    print(os.path.relpath(REFERENCE, ROOT))


if __name__ == "__main__":
    main()
