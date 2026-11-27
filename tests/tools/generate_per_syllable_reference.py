#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds the PerSyllable fonts and writes PerSyllable.harfbuzz.json, which GSUB lookups HarfBuzz matches within one
syllable only.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_per_syllable_reference.py

HarfBuzz 15.0.0 plans some features of its syllabic shapers with F_PER_SYLLABLE. A lookup of such a feature matches
only glyphs of the syllable of the glyph it applies to. Each font has lookups that apply to the first of two letters
when the second follows it. A letter alone is a syllable, so the second letter is in the next syllable, and the
lookup changes the first letter only when it does not match per syllable.

* PerSyllableKhmer.ttf (khmr) has such a lookup in locl, ccmp, blwf, abvf and pstf, which HarfBuzz plans per
  syllable, and in pres, which it does not. blwf, abvf and pstf apply after the base, so their lookups apply to the
  consonant after a coeng and look at the letter of the next syllable. A blwf lookup without the next letter shows
  that blwf applies.
* PerSyllableMyanmar.ttf (mym2) has one in locl, ccmp, pref, blwf and pstf, which HarfBuzz plans per syllable, and
  in pres, which it does not.
* PerSyllableBalinese.ttf (bali), for the universal shaper, has one in locl, ccmp, nukt, akhn, pref, rkrf, abvf, blwf,
  half, pstf, vatu and cjct, which HarfBuzz plans per syllable, and in pres, which it does not.
* PerSyllableShared.ttf (dev2) has lookups that two features of one stage share, one of them per syllable and the
  other not. HarfBuzz sorts the lookups of a stage by their index, and keeps the flags of the first feature that
  added the lookup: the features are added in the order of their tags. So a lookup of pres and calt, or of init and
  clig, does not match per syllable, as calt and clig come first, and a lookup of abvs and calt does.
* PerSyllableSorted48.ttf and PerSyllableSorted60.ttf (dev2) have 48 and 60 lookups in calt, and pres shares eight of
  them. HarfBuzz sorts the entries of a stage with hb_qsort_inline, which is a quicksort for more than 24 entries and
  is not stable, so the first entry of a shared lookup can be the one of pres. Each font has a lookup that then
  matches per syllable.
* PerSyllableRequired.ttf has the required feature ccmp for khmr, mym2, bali and dev2, which the language system
  also lists. HarfBuzz adds the lookups of the required feature first, and not per syllable, so its lookup matches
  across syllables.
* PerSyllableRuns.ttf has a locl lookup that changes a letter after a letter of another script: Devanagari ka after
  Brahmi A, Bengali ka after Devanagari kha, Myanmar ka after Devanagari ga, and Khmer ka after Devanagari gha. The
  pairs cover the universal, Indic, Myanmar and Khmer shapers, and Devanagari and Bengali share a shaper. In the last
  text, Bengali kha follows Devanagari nga, ca and cha, three syllables, and a calt lookup ligates nga and ca, so the
  Devanagari run ends with fewer glyphs than syllables. A caller of HarfBuzz shapes each script run in a buffer of its
  own, with the text around it as context, so the lookup does not match. The script shapes the texts so.

Each row holds the font, the text and the glyph IDs that HarfBuzz gives. PerSyllableTests reads the reference.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb
from fontTools.ttLib import TTFont

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

REFERENCE = os.path.join(FONTS, "PerSyllable.harfbuzz.json")

KHMER_COENG = 0x17D2


def lookahead(feature, first, second, before=()):
    """A feature whose lookup gives `first` its alternate glyph when `second` follows it."""
    context = "".join("u%04X " % c for c in before)
    return "feature %s { sub %su%04X' u%04X by u%04X.%s; } %s;\n" % (feature, context, first, second, first, feature, feature)


def build(file_name, tag, code_points, features, glyphs):
    fea = "languagesystem DFLT dflt;\nlanguagesystem %s dflt;\n" % tag + features
    return build_font(file_name, sorted(set(code_points)), fea, extra_glyphs=glyphs)


def pair_font(letters, per_syllable, other, before=()):
    """Gives each feature its own two letters, from `letters`, and the texts that test them. With `before`, each text
    starts with the last letter and `before`, and the lookup looks back at `before`."""
    features = ""
    glyphs = []
    texts = []
    code_points = list(before) + (letters[-1:] if before else [])
    for i, feature in enumerate(per_syllable + other):
        first, second = letters[2 * i], letters[2 * i + 1]
        features += lookahead(feature, first, second, before)
        glyphs.append("u%04X.%s" % (first, feature))
        code_points += [first, second]
        texts.append([*(letters[-1:] if before else []), *before, first, second])
    return features, glyphs, code_points, texts


def shape(font, text):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    hb.shape(font, buffer)
    return [info.codepoint for info in buffer.glyph_infos]


def shape_runs(font, text, split):
    """Shapes the characters before `split` and those after it each in a buffer of their own, with the rest of the
    text as context, as a caller of HarfBuzz shapes two script runs."""
    glyphs = []
    for start, end in ((0, split), (split, len(text))):
        buffer = hb.Buffer()
        buffer.add_str(text, start, end - start)
        buffer.guess_segment_properties()
        hb.shape(font, buffer)
        glyphs += [info.codepoint for info in buffer.glyph_infos]
    return glyphs


def make_required(path, tags, feature_tag):
    """Makes the feature the required feature of the default language system of each tag, which still lists it."""
    font = TTFont(path)
    table = font["GSUB"].table
    index = next(i for i, record in enumerate(table.FeatureList.FeatureRecord) if record.FeatureTag == feature_tag)
    for record in table.ScriptList.ScriptRecord:
        if record.ScriptTag in tags:
            record.Script.DefaultLangSys.ReqFeatureIndex = index
    font.save(path)


def main():
    rows = []

    def add(file_name, path, texts):
        font = load_font(path)
        for text in texts:
            text = "".join(map(chr, text))
            rows.append([file_name, text, shape(font, text)])

    # Khmer: locl and ccmp apply to a letter, the basic features to the consonant after a coeng.
    file_name = "PerSyllableKhmer.ttf"
    letters = list(range(0x1780, 0x17A3))
    features, glyphs, code_points, texts = pair_font(letters, ["locl", "ccmp"], ["pres"])
    basic, basic_glyphs, basic_code_points, basic_texts = pair_font(
        letters[6:], ["blwf", "abvf", "pstf"], [], before=[KHMER_COENG])
    # The consonant after a coeng takes blwf without the letter after it.
    features += basic + "feature blwf { sub u17D2 u%04X' by u%04X.blwf.alone; } blwf;\n" % (letters[12], letters[12])
    path = build(file_name, "khmr", code_points + basic_code_points + [letters[12]], features,
                 glyphs + basic_glyphs + ["u%04X.blwf.alone" % letters[12]])
    add(file_name, path, texts + basic_texts + [[letters[-1], KHMER_COENG, letters[12]]])

    # Myanmar.
    file_name = "PerSyllableMyanmar.ttf"
    features, glyphs, code_points, texts = pair_font(
        list(range(0x1000, 0x1021)), ["locl", "ccmp", "pref", "blwf", "pstf"], ["pres"])
    add(file_name, build(file_name, "mym2", code_points, features, glyphs), texts)

    # Balinese, for the universal shaper.
    file_name = "PerSyllableBalinese.ttf"
    features, glyphs, code_points, texts = pair_font(
        list(range(0x1B13, 0x1B34)),
        ["locl", "ccmp", "nukt", "akhn", "pref", "rkrf", "abvf", "blwf", "half", "pstf", "vatu", "cjct"], ["pres"])
    add(file_name, build(file_name, "bali", code_points, features, glyphs), texts)

    # A lookup that two features of one stage share.
    file_name = "PerSyllableShared.ttf"
    features = ""
    glyphs = []
    code_points = []
    texts = []
    for i, (a, b) in enumerate((("pres", "calt"), ("abvs", "calt"), ("init", "clig"))):
        first, second = 0x0915 + 2 * i, 0x0916 + 2 * i
        name = "Shared%d" % i
        features += "lookup %s { sub u%04X' u%04X by u%04X.alt; } %s;\n" % (name, first, second, first, name)
        features += "feature %s { lookup %s; } %s;\nfeature %s { lookup %s; } %s;\n" % (a, name, a, b, name, b)
        glyphs.append("u%04X.alt" % first)
        code_points += [first, second]
        texts.append([first, second])
    add(file_name, build(file_name, "dev2", code_points, features, glyphs), texts)

    # More than 24 lookups in a stage, which HarfBuzz sorts with a quicksort.
    for total, positions in ((48, [0, 7, 14, 21, 28, 35, 42, 47]), (60, [3, 9, 17, 30, 31, 44, 52, 59])):
        file_name = "PerSyllableSorted%d.ttf" % total
        features = ""
        names = []
        pairs = []
        for i in range(total):
            if i in positions:
                first = 0x0915 + 2 * positions.index(i)
                name = "Shared%d" % first
                features += "lookup %s { sub u%04X' u%04X by u%04X.alt; } %s;\n" % (name, first, first + 1, first, name)
                pairs.append([first, first + 1])
            else:
                # Ha, which no text has, takes the glyph of sa.
                name = "Other%d" % i
                features += "lookup %s { sub u0939 by u0938; } %s;\n" % (name, name)
            names.append(name)
        features += "feature calt {\n" + "".join("  lookup %s;\n" % name for name in names) + "} calt;\n"
        features += "feature pres {\n" + "".join("  lookup Shared%d;\n" % first for first, _ in pairs) + "} pres;\n"
        code_points = [c for pair in pairs for c in pair] + [0x0938, 0x0939]
        glyphs = ["u%04X.alt" % first for first, _ in pairs]
        add(file_name, build(file_name, "dev2", code_points, features, glyphs), pairs)

    # The required feature.
    file_name = "PerSyllableRequired.ttf"
    pairs = {"khmr": (0x1780, 0x1781), "mym2": (0x1000, 0x1001), "bali": (0x1B13, 0x1B14), "dev2": (0x0915, 0x0916)}
    fea = "languagesystem DFLT dflt;\n" + "".join("languagesystem %s dflt;\n" % tag for tag in pairs)
    fea += "feature ccmp {\n" + "".join("  sub u%04X' u%04X by u%04X.ccmp;\n" % (a, b, a) for a, b in pairs.values()) + "} ccmp;\n"
    path = build_font(file_name, sorted(c for pair in pairs.values() for c in pair), fea,
                      extra_glyphs=["u%04X.ccmp" % a for a, _ in pairs.values()])
    make_required(path, pairs, "ccmp")
    add(file_name, path, [list(pair) for pair in pairs.values()])

    # A letter after a letter of another script. Each script run is a buffer of its own.
    file_name = "PerSyllableRuns.ttf"
    runs = [([0x11005], [0x0915]), ([0x0916], [0x0995]), ([0x0917], [0x1000]), ([0x0918], [0x1780]),
            ([0x0919, 0x091A, 0x091B], [0x0996])]
    fea = "languagesystem DFLT dflt;\n" + "".join(
        "languagesystem %s dflt;\n" % tag for tag in ("brah", "dev2", "bng2", "mym2", "khmr"))
    fea += "feature calt { sub u0919 u091A by u0919_u091A; } calt;\n"
    fea += "feature locl {\n" + "".join(
        "  sub u%04X u%04X' by u%04X.locl;\n" % (before[-1], after[0], after[0]) for before, after in runs) + "} locl;\n"
    # The letters of the last text come after the others, which keep their glyph IDs.
    code_points = sorted(c for before, after in runs[:4] for c in before + after) + runs[4][0] + runs[4][1]
    glyphs = ["u%04X.locl" % after[0] for _, after in runs] + ["u0919_u091A"]
    font = load_font(build_font(file_name, code_points, fea, extra_glyphs=glyphs))
    for before, after in runs:
        text = "".join(map(chr, before + after))
        rows.append([file_name, text, shape_runs(font, text, len(before))])

    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
