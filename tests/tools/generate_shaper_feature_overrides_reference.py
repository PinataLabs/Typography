#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds ShaperFeatureOverrides.ttf and writes ShaperFeatureOverrides.harfbuzz.json, the features that the syllabic
shapers of HarfBuzz turn on or off whatever the caller asks for, and the glyphs that the Myanmar rphf applies to.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_shaper_feature_overrides_reference.py

HarfBuzz 15.0.0 calls the override_features function of a shaper after it adds the features of the caller, so the
shaper has the last word on them:

* override_features_indic turns liga off.
* override_features_khmer turns clig on and liga off.
* The Myanmar and universal shapers have no such function, so liga and clig apply as in any other script.

The font has, for Devanagari (dev2), Khmer (khmr), Myanmar (mym2), Balinese (bali) and Latin (latn), a liga lookup
that makes a ligature of the first two letters of the text and a clig lookup that makes one of the next two. Each text
is shaped with no features, with liga on, and with clig off. The Latin rows show that the lookups apply where no shaper
overrides them. In vertical text HarfBuzz applies neither liga nor clig, but the Khmer shaper turns clig on there too,
so the vertical list holds each text shaped from top to bottom.

collect_features_myanmar enables rphf on every glyph, F_MANUAL_ZWJ and F_PER_SYLLABLE. The font has an rphf lookup
that gives ka (U+1000) and ga (U+1002) an alternate glyph, and one that makes a ligature of a kinzi (nga, asat,
virama). A lone ka takes its rphf form, as do ga after ka and a ka that a kinzi comes before.

Each row holds the text, the features that the caller turns on (a tag) or off (a tag after a minus), and the glyph IDs
that HarfBuzz gives. ShaperFeatureOverridesTests reads the reference.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

import uharfbuzz as hb

from harfbuzz_reference import FONTS, build_font, load_font, write_reference

FONT = "ShaperFeatureOverrides.ttf"
REFERENCE = os.path.join(FONTS, "ShaperFeatureOverrides.harfbuzz.json")

# Per script tag: the two letters of the liga lookup and the two letters of the clig lookup.
PAIRS = {
    "dev2": ((0x0915, 0x0916), (0x0917, 0x0918)),
    "khmr": ((0x1780, 0x1781), (0x1782, 0x1783)),
    "mym2": ((0x1001, 0x1003), (0x1005, 0x1006)),
    "bali": ((0x1B13, 0x1B14), (0x1B15, 0x1B16)),
    "latn": ((0x0061, 0x0062), (0x0063, 0x0064)),
}

FEATURE_SETS = [[], ["liga"], ["-clig"]]

# Myanmar ka, ga, nga, asat, virama.
KA, GA, NGA, ASAT, VIRAMA = 0x1000, 0x1002, 0x1004, 0x103A, 0x1039

RPHF_TEXTS = [[KA], [GA], [KA, GA], [NGA, ASAT, VIRAMA, KA]]


def ligature(first, second):
    return "u%04X_u%04X" % (first, second)


def features():
    fea = "languagesystem DFLT dflt;\n" + "".join("languagesystem %s dflt;\n" % tag for tag in PAIRS)
    fea += "feature liga {\n" + "".join("  sub u%04X u%04X by %s;\n" % (a, b, ligature(a, b))
                                        for (a, b), _ in PAIRS.values()) + "} liga;\n"
    fea += "feature clig {\n" + "".join("  sub u%04X u%04X by %s;\n" % (a, b, ligature(a, b))
                                        for _, (a, b) in PAIRS.values()) + "} clig;\n"
    fea += "feature rphf {\n  sub u%04X by u%04X.rphf;\n  sub u%04X by u%04X.rphf;\n" % (KA, KA, GA, GA)
    fea += "  sub u%04X u%04X u%04X by kinzi;\n} rphf;\n" % (NGA, ASAT, VIRAMA)
    return fea


def shape(font, text, feature_set, direction=None):
    buffer = hb.Buffer()
    buffer.add_str(text)
    buffer.guess_segment_properties()
    if direction:
        buffer.direction = direction
    hb.shape(font, buffer, {tag.lstrip("-"): not tag.startswith("-") for tag in feature_set})
    return [info.codepoint for info in buffer.glyph_infos]


def main():
    pairs = [pair for both in PAIRS.values() for pair in both]
    code_points = sorted({c for pair in pairs for c in pair} | {KA, GA, NGA, ASAT, VIRAMA})
    glyphs = [ligature(a, b) for a, b in pairs] + ["u%04X.rphf" % KA, "u%04X.rphf" % GA, "kinzi"]
    font = load_font(build_font(FONT, code_points, features(), extra_glyphs=glyphs))

    rows = []
    for liga, clig in PAIRS.values():
        text = "".join(map(chr, liga + clig))
        rows.extend([text, feature_set, shape(font, text, feature_set)] for feature_set in FEATURE_SETS)
    for text in RPHF_TEXTS:
        text = "".join(map(chr, text))
        rows.append([text, [], shape(font, text, [])])
    vertical = []
    for liga, clig in PAIRS.values():
        text = "".join(map(chr, liga + clig))
        vertical.append([text, shape(font, text, [], "ttb")])
    write_reference(REFERENCE, rows, font=FONT, vertical=vertical)


if __name__ == "__main__":
    main()
