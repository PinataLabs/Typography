#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes TextRunContext.harfbuzz.json, the glyphs of a word that two text runs style, as HarfBuzz shapes them.

Usage (from the repository root, uharfbuzz):

    python tests/tools/generate_text_run_context_reference.py

A caller of HarfBuzz that styles a word in two runs shapes each run in a buffer of its own, and gives the buffer the
text before and after the run as context. HarfBuzz then joins the letters at the boundary as in one run. It does not
apply a GSUB lookup across the boundary.

For each word and each grapheme boundary to split it at, the reference holds the number of graphemes before the
split, as a TextRun counts them, and the glyph IDs of both runs in the order they are drawn, left to right. The
words are Arabic in Segoe UI, Dubai and me_quran, and Mongolian in UniversalShaperJoining.ttf. Issues_109 reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os
import unicodedata

import uharfbuzz as hb

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "TextRunContext.harfbuzz.json")

WORDS = [
    ("Segoeui.ttf", "مرحبا"),
    ("Segoeui.ttf", "بببب"),
    ("Segoeui.ttf", "بـب"),
    ("Dubai-Regular.ttf", "العربية"),
    ("Dubai-Regular.ttf", "سلام"),
    ("me_quran_volt_newmet.ttf", "بِسْمِ"),
    ("UniversalShaperJoining.ttf", "ᠮᠣᠩᠭᠣᠯ"),
    ("UniversalShaperJoining.ttf", "ᠠ᠋ᠠ"),
]


def shape_run(font, text, start, end):
    """Shapes text[start:end] with the rest of the text as its context, and gives the glyph IDs left to right."""
    buffer = hb.Buffer()
    buffer.add_codepoints([ord(c) for c in text], start, end - start)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    return [info.codepoint for info in buffer.glyph_infos], buffer.direction


def main():
    rows = []
    fonts = {}
    for file_name, word in WORDS:
        if file_name not in fonts:
            with open(os.path.join(FONTS, file_name), "rb") as file:
                fonts[file_name] = hb.Font(hb.Face(file.read()))
        font = fonts[file_name]
        # A mark belongs to the grapheme of the character before it.
        boundaries = [i for i in range(1, len(word)) if unicodedata.category(word[i]) not in ("Mn", "Me")]
        for grapheme, split in enumerate(boundaries, 1):
            first, direction = shape_run(font, word, 0, split)
            second, _ = shape_run(font, word, split, len(word))
            glyphs = second + first if direction == "rtl" else first + second
            rows.append([file_name, word, grapheme, glyphs])

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "rows": [\n')
        file.write(",\n".join("    " + json.dumps(row, separators=(",", ":")) for row in rows))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(rows), "rows")


if __name__ == "__main__":
    main()
