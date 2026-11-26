#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes ThaiPua.harfbuzz.json, how HarfBuzz draws Thai marks in a font that has no GSUB script for Thai.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_thai_pua_reference.py

When the GSUB table of a font has no `thai` script, HarfBuzz draws a Thai mark that would clash with its base or
with the mark before it with a glyph of the private use area, as Windows and Mac Thai fonts provide them: a tone
mark lower or further left, a vowel below a consonant with a descender lower, and YO YING and THO THAN without their
descender. It looks for the Windows code point first and then for the Mac one.

The script builds three fonts with Thai consonants, marks and SARA AM, and the space:

* ThaiPua.ttf also has the private use glyphs of Windows and of Mac, and no GSUB table.
* ThaiPuaMac.ttf has only the private use glyphs of Mac.
* ThaiPuaGsub.ttf has both and a GSUB table with the `thai` script, so HarfBuzz uses no private use glyph.

The reference holds the glyph IDs and where HarfBuzz draws each glyph for each consonant of each kind, a digit and a
space, followed by one or two marks, and the same texts in FDArrayTest257.otf. ThaiPuaTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

from harfbuzz_reference import FONTS, build_font, load_font, shape_line, write_reference

REFERENCE = os.path.join(FONTS, "ThaiPua.harfbuzz.json")

# KO KAI, PO PLA, FO FA, YO YING, THO THAN, DO CHADA, TO PATAK, a digit and the space: consonants with no
# descender, with an ascender, with a descender that can go, and with one that stays, and characters that are not.
BASES = [0x0E01, 0x0E1B, 0x0E1F, 0x0E0D, 0x0E10, 0x0E0E, 0x0E0F, 0x0E51, 0x0020]

# MAI HAN-AKAT, SARA I, MAITAIKHU, NIKHAHIT (above), SARA U, PHINTHU (below), MAI EK, THANTHAKHAT (tone marks)
# and SARA AM.
MARKS = [0x0E31, 0x0E34, 0x0E47, 0x0E4D, 0x0E38, 0x0E3A, 0x0E48, 0x0E4C, 0x0E33]

THAI = sorted(set(BASES + MARKS + [0x0E32, 0x0E39, 0x0E49]))
WINDOWS_PUA = list(range(0xF700, 0xF71B))
MAC_PUA = list(range(0xF884, 0xF89F))

FDARRAY = "FDArrayTest257.otf"


def main():
    build_font("ThaiPua.ttf", THAI + WINDOWS_PUA + MAC_PUA)
    build_font("ThaiPuaMac.ttf", THAI + MAC_PUA)
    build_font("ThaiPuaGsub.ttf", THAI + WINDOWS_PUA + MAC_PUA, "languagesystem thai dflt;\nfeature ccmp { sub u0E01 by u0E01; } ccmp;\n")

    texts = []
    for base in BASES:
        for first in MARKS:
            texts.append([base, first])
            texts += [[base, first, second] for second in MARKS]

    rows = []
    for file_name in ("ThaiPua.ttf", "ThaiPuaMac.ttf", "ThaiPuaGsub.ttf", FDARRAY):
        font = load_font(os.path.join(FONTS, file_name))
        for text in texts:
            text = "".join(map(chr, text))
            rows.append([file_name, text, shape_line(font, text)[0]])

    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
