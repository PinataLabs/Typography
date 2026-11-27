#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds ScriptRunContext.ttf and writes ScriptRunContext.harfbuzz.json, which lookups HarfBuzz applies across the
boundary of two script runs (issue #194).

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_script_run_context_reference.py

A caller of HarfBuzz itemizes text by script and shapes each run in a buffer of its own, with the text around it as
context. A lookup then matches and attaches only glyphs of the run it shapes. The script itemizes each text as the
library does: a character of the scripts Common, Inherited or Unknown joins the run before it, or the run after it at
the start of the text. Each case of the font has a lookup that applies only when the glyph of the other run is there:

* BrahmiBefore and BrahmiAfter, the probe of the issue: a calt lookup gives Devanagari ka its alternate after Brahmi
  A, and another one before Brahmi A.
* Ligature: an IgnoreMarks lookup ligates Latin a with Greek alpha, with a mark between them that joins the Latin run.
* ContextInput: a context lookup whose input is Latin b and Greek beta.
* ReverseBacktrack: a reverse chaining lookup gives Latin c its alternate after Greek gamma.
* CommonInRun and CommonAtBoundary: exclamation mark is Common. It is in the run of Latin d before it, where a lookup
  matches it, and in the run of Devanagari kha before it, where a lookup that needs Latin e after it does not.
* HanBeforeKana, KanaBeforeHan and HanBeforeProlongedSoundMark: Han, Hiragana and Katakana are scripts of their own,
  so a lookup does not match across them. The prolonged sound mark U+30FC is Common and joins the Han run.
* Pair, ChainedBacktrack, ChainedLookahead and ChainedInputPosition: GPOS kerns Latin f before Greek delta, moves
  Latin g after Greek epsilon and h before Greek zeta, and widens n of the input n theta.
* MarkToBase, MarkToMark and MarkToLigature: a Cyrillic mark after a Latin base, a Latin mark, and a Latin ligature.
* Cursive: Latin m and Greek eta join.
* DirectionChange and DirectionChangePair: a caller also splits text where its direction changes, as the Unicode
  Bidirectional Algorithm resolves it. A digit after a Hebrew letter is a run from left to right in a run from right
  to left. A calt lookup gives Hebrew alef its alternate before digit one, and GPOS moves digit two after Hebrew bet.

Each text is shaped as the runs of a caller, and, for the cases that a script names, also in one buffer of that
script, which shows that the lookup applies. Each row holds the case, the text, how the text is split, and the glyph
ID, x and y of each glyph, from left to right. The text is split into script runs (""), into script runs and runs of
one direction ("bidi"), or not at all, in one buffer of the script that an ISO 15924 code names. ScriptRunContextTests
reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb
from fontTools import unicodedata
from fontTools.ttLib import TTFont

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

FILE_NAME = "ScriptRunContext.ttf"
REFERENCE = os.path.join(FONTS, "ScriptRunContext.harfbuzz.json")

# The case, its text, and the script of one buffer that shows that the lookup applies, if any. Each text is split into
# script runs, which run from left to right.
CASES = [
    ("BrahmiBefore", [0x11005, 0x0915], "Latn"),
    ("BrahmiAfter", [0x0915, 0x11005], "Latn"),
    ("Ligature", [0x0061, 0x0301, 0x03B1], "Latn"),
    ("ContextInput", [0x0062, 0x03B2], "Latn"),
    ("ReverseBacktrack", [0x03B3, 0x0063], "Latn"),
    ("CommonInRun", [0x0064, 0x0021], None),
    ("CommonAtBoundary", [0x0916, 0x0021, 0x0065], "Latn"),
    ("HanBeforeKana", [0x6F22, 0x3042], "Hani"),
    ("KanaBeforeHan", [0x30A2, 0x5B57], "Hani"),
    ("HanBeforeProlongedSoundMark", [0x5B57, 0x30FC], None),
    ("Pair", [0x0066, 0x03B4], "Latn"),
    ("ChainedBacktrack", [0x03B5, 0x0067], "Latn"),
    ("ChainedLookahead", [0x0068, 0x03B6], "Latn"),
    ("ChainedInputPosition", [0x006E, 0x03B8], "Latn"),
    ("MarkToBase", [0x0069, 0x0483], "Latn"),
    ("MarkToMark", [0x006A, 0x0301, 0x0484], "Latn"),
    ("MarkToLigature", [0x006B, 0x006C, 0x0487], "Latn"),
    ("Cursive", [0x006D, 0x03B7], "Latn"),
]

# The case, its text, the script of one buffer, and the runs of the text from left to right: their start, end and
# direction. In a paragraph from right to left, a digit after a letter is a run from left to right at its left.
BIDI_CASES = [
    ("DirectionChange", [0x05D0, 0x0031], "Hebr", [(1, 2, "ltr"), (0, 1, "rtl")]),
    ("DirectionChangePair", [0x05D1, 0x0032], "Hebr", [(1, 2, "ltr"), (0, 1, "rtl")]),
]

FEATURES = """
languagesystem DFLT dflt;
languagesystem latn dflt;
languagesystem grek dflt;
languagesystem cyrl dflt;
languagesystem dev2 dflt;
languagesystem brah dflt;
languagesystem hani dflt;
languagesystem kana dflt;
languagesystem hebr dflt;

table GDEF {
    GlyphClassDef , [u006B_u006C], [u0301 u0483 u0484 u0487], ;
} GDEF;

lookup B_ALT {
    sub u0062 by u0062.alt;
} B_ALT;

lookup N_MOVE {
    pos u006E 100;
} N_MOVE;

feature calt {
    lookup ISSUE {
        sub u11005 u0915' by u0915.back;
        sub u0915' u11005 by u0915.ahead;
    } ISSUE;

    lookup LIGATURE {
        lookupflag IgnoreMarks;
        sub u0061 u03B1 by u0061_u03B1;
    } LIGATURE;

    lookup CONTEXT_INPUT {
        sub u0062' lookup B_ALT u03B2';
    } CONTEXT_INPUT;

    lookup REVERSE {
        rsub u03B3 u0063' by u0063.rsub;
    } REVERSE;

    lookup COMMON {
        sub u0064' u0021 by u0064.alt;
        sub u0021' u0065 by u0021.alt;
    } COMMON;

    lookup CJK {
        sub u6F22 u3042' by u3042.back;
        sub u30A2' u5B57 by u30A2.ahead;
        sub u5B57 u30FC' by u30FC.back;
    } CJK;

    lookup LIGATE_KL {
        sub u006B u006C by u006B_u006C;
    } LIGATE_KL;

    lookup DIRECTION {
        sub u05D0' u0031 by u05D0.alt;
    } DIRECTION;
} calt;

feature kern {
    lookup PAIR {
        pos u0066 u03B4 -100;
    } PAIR;

    lookup CHAINED_POSITION {
        pos u03B5 u0067' <100 0 0 0>;
        pos u0068' <100 0 0 0> u03B6;
    } CHAINED_POSITION;

    lookup CHAINED_INPUT {
        pos u006E' lookup N_MOVE u03B8';
    } CHAINED_INPUT;

    lookup DIRECTION_PAIR {
        pos u05D1 <0 0 0 0> u0032 <100 0 0 0>;
    } DIRECTION_PAIR;
} kern;

markClass u0483 <anchor 0 500> @TOP;
markClass u0484 <anchor 0 500> @MARK_TOP;
markClass u0487 <anchor 0 500> @LIGATURE_TOP;

feature mark {
    lookup MARK_TO_BASE {
        pos base u0069 <anchor 300 700> mark @TOP;
    } MARK_TO_BASE;

    lookup MARK_TO_LIGATURE {
        pos ligature u006B_u006C <anchor 150 700> mark @LIGATURE_TOP
            ligComponent <anchor 450 700> mark @LIGATURE_TOP;
    } MARK_TO_LIGATURE;
} mark;

feature mkmk {
    lookup MARK_TO_MARK {
        pos mark u0301 <anchor 0 800> mark @MARK_TOP;
    } MARK_TO_MARK;
} mkmk;

feature curs {
    lookup CURSIVE {
        pos cursive u006D <anchor NULL> <anchor 600 100>;
        pos cursive u03B7 <anchor 0 0> <anchor NULL>;
    } CURSIVE;
} curs;
"""

EXTRA_GLYPHS = [
    "u0915.back", "u0915.ahead", "u0061_u03B1", "u0062.alt", "u0063.rsub", "u0064.alt", "u0021.alt",
    "u3042.back", "u30A2.ahead", "u30FC.back", "u006B_u006C", "u05D0.alt",
]


def itemize(text):
    """Splits the text into script runs from left to right, as the library does. A character of no script of its own
    joins the run before it, or the first run at the start of the text."""
    runs = []
    start = 0
    script = None
    for i, character in enumerate(text):
        current = unicodedata.script(character)
        if current in ("Zyyy", "Zinh", "Zzzz"):
            continue
        if script is not None and current != script:
            runs.append((start, i, "ltr"))
            start = i
        script = current
    runs.append((start, len(text), "ltr"))
    return runs


def shape(font, code_points, runs, script=None):
    """Shapes each run in a buffer of its own, with the rest of the text as context, and gives the glyph ID, x and y of
    each glyph, in font units from the left of the line. The runs are in their order from left to right."""
    glyphs = []
    x = 0
    for start, end, direction in runs:
        buffer = hb.Buffer()
        buffer.add_codepoints(code_points, start, end - start)
        if script:
            buffer.script = script
        buffer.direction = direction
        buffer.guess_segment_properties()
        hb.shape(font, buffer)
        for info, position in zip(buffer.glyph_infos, buffer.glyph_positions):
            glyphs.append([info.codepoint, x + position.x_offset, position.y_offset])
            x += position.x_advance
    return glyphs


def main():
    code_points = sorted({c for case in CASES + BIDI_CASES for c in case[1]})
    path = build_font(FILE_NAME, code_points, FEATURES, extra_glyphs=EXTRA_GLYPHS)

    # Check that the lookups have the types that the cases test.
    # The lookups of type 1 are B_ALT, N_MOVE and those of the inline substitutions and positions.
    font = TTFont(path)
    types = [lookup.LookupType for lookup in font["GSUB"].table.LookupList.Lookup]
    assert types == [1, 6, 1, 1, 4, 5, 8, 6, 1, 6, 1, 4, 6, 1], types
    types = [lookup.LookupType for lookup in font["GPOS"].table.LookupList.Lookup]
    assert types == [1, 2, 8, 1, 8, 2, 4, 5, 6, 3], types

    font = load_font(path)
    rows = []
    for case, text, script in CASES:
        string = "".join(map(chr, text))
        rows.append([case, string, "", shape(font, text, itemize(string))])
        if script:
            rows.append([case, string, script, shape(font, text, [(0, len(text), "ltr")], script)])

    for case, text, script, runs in BIDI_CASES:
        string = "".join(map(chr, text))
        rows.append([case, string, "bidi", shape(font, text, runs, script)])
        rows.append([case, string, script, shape(font, text, [(0, len(text), "rtl")], script)])

    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
