#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds NestingLimit.ttf and writes NestingLimit.harfbuzz.json, how deep HarfBuzz applies the nested lookups of a
context lookup that names itself (issue #205).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_nesting_limit_reference.py

HarfBuzz applies a nested lookup only while it has nesting levels left. It starts with HB_MAX_NESTING_LEVEL, 64, takes
one for each nested lookup it applies and gives it back after. Each case of the font has a context lookup whose
records name a lookup that moves the glyph one step and then the context lookup itself, so the step counts the levels:

* SelfGsub: a GSUB context lookup gives Latin a the next of 70 alternates and then names itself.
* MutualGsub: two GSUB context lookups give Latin b the next of 70 alternates and name each other. The second one is
  an extension lookup.
* SelfGpos: a GPOS context lookup moves Latin x up 10 units and then names itself.
* NaturalEnd, the positive control: a GSUB context lookup gives Latin d the next of 10 alternates and names itself, and
  stops when the last alternate no longer matches, before the limit.
* TwoRecords, the positive control for GPOS: a GPOS context lookup moves Latin z up 10 units with each of its two
  records.

feaLib does not let a lookup name itself, or a lookup defined after it, so the feature code names a placeholder lookup,
and the script points each record that names a placeholder at the context lookup it stands for.
HarfBuzz reports that shaping failed when it runs out of nesting levels, and uharfbuzz raises MemoryError for that, but
the buffer holds the glyphs that HarfBuzz gives, as hb_shape leaves them for a caller that does not check.
Each row holds the case, the text, whether HarfBuzz reported a failure, and the glyph ID, x and y of each glyph.
NestingLimitTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb
from fontTools.ttLib import TTFont

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

FILE_NAME = "NestingLimit.ttf"
REFERENCE = os.path.join(FONTS, "NestingLimit.harfbuzz.json")

CASES = [
    ("SelfGsub", "a"),
    ("MutualGsub", "b"),
    ("SelfGpos", "x"),
    ("NaturalEnd", "d"),
    ("TwoRecords", "z"),
]

# The number of alternates of each letter that steps through them.
STEPS = {0x61: 70, 0x62: 70, 0x64: 10}


def chain(code_point):
    """Gives the letter and its alternates, in order."""
    name = "u%04X" % code_point
    return [name] + ["%s.%d" % (name, i) for i in range(1, STEPS[code_point] + 1)]


def step(code_point):
    """Gives the lookup body that moves each glyph of the chain to the next one."""
    glyphs = chain(code_point)
    return "sub [%s] by [%s];" % (" ".join(glyphs[:-1]), " ".join(glyphs[1:]))


def input_class(code_point, last=True):
    glyphs = chain(code_point)
    return "[%s]" % " ".join(glyphs if last else glyphs[:-1])


# GSUB lookups 0 to 4 are standalone, 5 to 7 are those of calt. GPOS lookups 0 to 2 are standalone, 3 and 4 those of kern.
FEATURES = f"""
languagesystem DFLT dflt;
languagesystem latn dflt;

lookup A_STEP {{ {step(0x61)} }} A_STEP;
lookup B_STEP {{ {step(0x62)} }} B_STEP;
lookup D_STEP {{ {step(0x64)} }} D_STEP;
lookup PLACEHOLDER {{ sub u0061 by u0061; }} PLACEHOLDER;

lookup B_SECOND useExtension {{
    sub {input_class(0x62)}' lookup B_STEP lookup PLACEHOLDER;
}} B_SECOND;

feature calt {{
    lookup A_SELF {{
        sub {input_class(0x61)}' lookup A_STEP lookup PLACEHOLDER;
    }} A_SELF;

    lookup B_FIRST {{
        sub {input_class(0x62)}' lookup B_STEP lookup B_SECOND;
    }} B_FIRST;

    lookup D_SELF {{
        sub {input_class(0x64, last=False)}' lookup D_STEP lookup PLACEHOLDER;
    }} D_SELF;
}} calt;

lookup X_UP {{ pos u0078 <0 10 0 0>; }} X_UP;
lookup Z_UP {{ pos u007A <0 10 0 0>; }} Z_UP;
lookup POS_PLACEHOLDER {{ pos u0078 0; }} POS_PLACEHOLDER;

feature kern {{
    lookup X_SELF {{
        pos u0078' lookup X_UP lookup POS_PLACEHOLDER;
    }} X_SELF;

    lookup Z_TWICE {{
        pos u007A' lookup Z_UP lookup Z_UP;
    }} Z_TWICE;
}} kern;
"""

EXTRA_GLYPHS = [name for code_point in STEPS for name in chain(code_point)[1:]]

# For each table, the placeholder and the lookup that each context lookup's placeholder record stands for.
GSUB_PLACEHOLDER = 3
GSUB_TARGETS = {4: 6, 5: 5, 7: 7}  # B_SECOND names B_FIRST, A_SELF and D_SELF name themselves.
GPOS_PLACEHOLDER = 2
GPOS_TARGETS = {3: 3}  # X_SELF names itself.


def subtables(lookup):
    for subtable in lookup.SubTable:
        yield getattr(subtable, "ExtSubTable", subtable)


def point_placeholders(table, placeholder, targets):
    """Points each record of a context lookup that names the placeholder at the lookup that it stands for."""
    pointed = 0
    for index, lookup in enumerate(table.LookupList.Lookup):
        for subtable in subtables(lookup):
            for name in ("SubstLookupRecord", "PosLookupRecord"):
                for record in getattr(subtable, name, None) or []:
                    if record.LookupListIndex == placeholder:
                        record.LookupListIndex = targets[index]
                        pointed += 1
    assert pointed == len(targets), pointed


def shape(font, text):
    """Shapes the text in one buffer and gives whether HarfBuzz reported a failure, and the glyph ID, x and y of each
    glyph, in font units from the start of the line."""
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    try:
        hb.shape(font, buffer)
        failed = False
    except MemoryError:
        failed = True
    glyphs = []
    x = 0
    for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
        glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
        x += position.x_advance
    return failed, glyphs


def main():
    code_points = sorted({ord(c) for _, text in CASES for c in text})
    path = build_font(FILE_NAME, code_points, FEATURES, extra_glyphs=EXTRA_GLYPHS)

    font = TTFont(path)
    gsub = font["GSUB"].table
    gpos = font["GPOS"].table

    # The lookups have the types that the cases test: B_SECOND is an extension of a context lookup.
    types = [lookup.LookupType for lookup in gsub.LookupList.Lookup]
    assert types == [1, 1, 1, 1, 7, 5, 5, 5], types
    types = [lookup.LookupType for lookup in gpos.LookupList.Lookup]
    assert types == [1, 1, 1, 8, 8], types

    point_placeholders(gsub, GSUB_PLACEHOLDER, GSUB_TARGETS)
    point_placeholders(gpos, GPOS_PLACEHOLDER, GPOS_TARGETS)
    font.save(path)

    hb_font = load_font(path)
    rows = [[case, text, *shape(hb_font, text)] for case, text in CASES]
    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
