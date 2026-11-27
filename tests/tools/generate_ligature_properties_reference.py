#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Builds five fonts and writes LigatureProperties.harfbuzz.json, how a substitution keeps or resets the ligature
properties of a glyph in HarfBuzz.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_ligature_properties_reference.py

HarfBuzz gives each glyph a ligature id and a component. A ligature takes a new id, and a mark that it skips takes the
id and the component that the mark follows. Mark-to-ligature attaches a mark of the same id to that component, and any
other mark to the last component.

LigatureProperties.ttf first substitutes the circumflex by the sequence of one glyph circumflex.alt. It then ligates a
and b, skipping marks, then substitutes the acute by acute.alt (a single substitution) and the grave by grave.a and
grave.b (a multiple substitution). Mark-to-ligature has an anchor at x 110 on the first component of a_b and at x 710 on
the second.

* a, acute, b: a single substitution keeps the id and the component of the acute, so acute.alt goes on the first
  component.
* a, grave, b: a multiple substitution of a glyph that has an id keeps the id and the component on each glyph
  (Sequence.hh), so grave.a and grave.b both go on the first component.
* a, circumflex, b: HarfBuzz takes a sequence of one glyph for a single substitution, which leaves the circumflex no
  component (Sequence.hh). So the ligature gives circumflex.alt the first component, as it gives a mark that no lookup
  changed.
* a, b, acute and c, acute, c, grave: the acute after the ligature goes on the last component, and on c the marks
  attach to their base.

LigaturePropertiesWithoutGlyphClasses.ttf has no GDEF table and no GPOS table. It ligates a and b into a_b, b and d
into b_d, and acute and a into acute_a, which are all ligatures. It then substitutes a_b by c and d, b_d by the sequence
of one glyph c, and acute_a by b and b, then, ignoring ligatures, c by c.alt.

* a, b: a multiple substitution of a ligature gives each glyph the base class (Sequence.hh), so the lookup that ignores
  ligatures changes c.
* b, d: a sequence of one glyph is a single substitution, which keeps the ligature class. So the lookup that ignores
  ligatures skips the c that b_d becomes.
* d, acute, a: the ligature of acute and a has the general category Lo where the acute has Mn (ligate_input). The
  glyphs that the multiple substitution makes of it copy that category, so they are no marks, and HarfBuzz places no
  mark on d. They keep their advance.
* c: the lookup that ignores ligatures changes c, which no substitution made. d, acute: the acute is a mark, and
  HarfBuzz places it on d.

LigaturePropertiesIndic.ttf is a Devanagari font with the dev2 script. In ccmp, a multiple substitution lookup
substitutes ra by the sequence of one glyph ra.alt, and na by two na. rphf ligates ra.alt and virama into reph. In pref,
a lookup substitutes ta by ta.alt after ka and virama, and then virama and ta ligate into tapref.

* ra, virama, ka: HarfBuzz takes a sequence of one glyph for a single substitution, so the reph is a ligature that did
  not multiply, which moves after the base ka.
* ka, virama, ta, i: ta is a pre-base-reordering candidate, but its pref ligature does not form, as ta is now ta.alt.
  So HarfBuzz finds the base again at ta.alt, and moves the pre-base matra i from before ka to after the virama.
* pa, virama, ta, i and pa, virama, ta: tapref forms and goes before the base pa, after the matra. ka, virama, ta: no
  tapref forms.

LigaturePropertiesCircle.ttf has no GDEF table and no GPOS table. It has U+25CC and a sign of the Indic, Khmer,
Myanmar and universal shapers: Devanagari i and u, Khmer U+17BB, Myanmar U+102D and Balinese U+1B36. Each sign alone is
a broken cluster, so the shaper inserts a dotted circle. HarfBuzz inserts it after it gives each glyph its class, and
gives it none, not the class of U+25CC (hb-ot-shaper-syllabic.cc). blws ligates the circle and u, U+17BB, U+102D or
U+1B36 into circle_u. psts then substitutes, ignoring ligatures, circle_u by circle_u.alt, and, ignoring base glyphs,
U+25CC by u25CC.alt. LigaturePropertiesCircleGdef.ttf has the same glyphs and features and a GDEF table: U+25CC and
u25CC.alt are bases, circle_u and circle_u.alt ligatures, and the signs marks.

* u, U+17BB, U+102D and U+1B36: in the font without GDEF, the circle has no class, so it is no base, and circle_u is a
  ligature (ligate_input), which the lookup that ignores ligatures skips. With GDEF, circle_u is a ligature too.
* U+25CC, u: the circle of the text is a base, so circle_u is the ligature of a base and a mark, which takes no
  ligature class without GDEF. The lookup that ignores ligatures changes it.
* i: the circle has no class, so the lookup that ignores base glyphs changes it, with GDEF too. U+25CC, i: the circle
  of the text is a base, and the lookup skips it.

The reference holds the font, each text and the glyph ID and place of each glyph, in font units from the start of the
line, as HarfBuzz gives them. LigaturePropertiesTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools.ttLib import TTFont

from harfbuzz_reference import FONTS, build_font, load_font, shape_line, write_reference

REFERENCE = os.path.join(FONTS, "LigatureProperties.harfbuzz.json")

FONT = "LigatureProperties.ttf"
CODE_POINTS = [0x61, 0x62, 0x63, 0x0300, 0x0301, 0x0302]
EXTRA_GLYPHS = ["a_b", "acute.alt", "grave.a", "grave.b", "circumflex.alt"]
FEATURES = """
languagesystem DFLT dflt; languagesystem latn dflt;
markClass [u0301 u0300 u0302 acute.alt grave.a grave.b circumflex.alt] <anchor 0 0> @Accent;
lookup Sequence { sub u0302 by circumflex.alt; sub circumflex.alt by u0302 u0302; } Sequence;
lookup Ligate { lookupflag IgnoreMarks; sub u0061 u0062 by a_b; } Ligate;
lookup Single { sub u0301 by acute.alt; } Single;
lookup Multiple { sub u0300 by grave.a grave.b; } Multiple;
feature liga { lookup Sequence; lookup Ligate; lookup Single; lookup Multiple; } liga;
feature mark {
    pos base u0063 <anchor 300 500> mark @Accent;
    pos ligature a_b <anchor 110 600> mark @Accent ligComponent <anchor 710 600> mark @Accent;
} mark;
"""
TEXTS = ["áb", "àb", "âb", "ab́", "ć", "c̀"]

FONT_WITHOUT_GLYPH_CLASSES = "LigaturePropertiesWithoutGlyphClasses.ttf"
CODE_POINTS_WITHOUT_GLYPH_CLASSES = [0x61, 0x62, 0x63, 0x64, 0x0301]
EXTRA_GLYPHS_WITHOUT_GLYPH_CLASSES = ["a_b", "b_d", "acute_a", "c.alt"]
FEATURES_WITHOUT_GLYPH_CLASSES = """
languagesystem DFLT dflt; languagesystem latn dflt;
lookup Ligate { sub u0061 u0062 by a_b; sub u0062 u0064 by b_d; sub u0301 u0061 by acute_a; } Ligate;
lookup Multiple { sub a_b by u0063 u0064; sub b_d by u0063; sub acute_a by u0062 u0062; } Multiple;
lookup SkipLigatures { lookupflag IgnoreLigatures; sub u0063 by c.alt; } SkipLigatures;
feature liga { lookup Ligate; lookup Multiple; lookup SkipLigatures; } liga;
"""
TEXTS_WITHOUT_GLYPH_CLASSES = ["ab", "bd", "d́a", "c", "d́"]

INDIC_FONT = "LigaturePropertiesIndic.ttf"
INDIC_CODE_POINTS = [0x0915, 0x0924, 0x0928, 0x092A, 0x0930, 0x093F, 0x094D]
INDIC_EXTRA_GLYPHS = ["ra.alt", "reph", "ta.alt", "tapref"]
INDIC_FEATURES = """
languagesystem DFLT dflt; languagesystem dev2 dflt;
lookup Sequence { sub u0930 by ra.alt; sub u0928 by u0928 u0928; } Sequence;
feature ccmp { lookup Sequence; } ccmp;
feature rphf { sub ra.alt u094D by reph; } rphf;
lookup TaAlt { sub u0924 by ta.alt; } TaAlt;
lookup AfterKa { sub u0915 u094D u0924' lookup TaAlt; } AfterKa;
lookup Pref { sub u094D u0924 by tapref; } Pref;
feature pref { lookup AfterKa; lookup Pref; } pref;
"""
INDIC_TEXTS = ["र्क", "क्ति", "प्ति", "क्त",
               "प्त"]


CIRCLE_FONT = "LigaturePropertiesCircle.ttf"
CIRCLE_GDEF_FONT = "LigaturePropertiesCircleGdef.ttf"
CIRCLE_CODE_POINTS = [0x0915, 0x093F, 0x0941, 0x1780, 0x17BB, 0x1000, 0x102D, 0x1B13, 0x1B36, 0x25CC]
CIRCLE_EXTRA_GLYPHS = ["circle_u", "circle_u.alt", "u25CC.alt"]
CIRCLE_FEATURES = """
languagesystem DFLT dflt; languagesystem dev2 dflt; languagesystem khmr dflt; languagesystem mym2 dflt;
languagesystem bali dflt;
feature blws {
    sub u25CC u0941 by circle_u; sub u25CC u17BB by circle_u; sub u25CC u102D by circle_u; sub u25CC u1B36 by circle_u;
} blws;
lookup SkipLigatures { lookupflag IgnoreLigatures; sub circle_u by circle_u.alt; } SkipLigatures;
lookup SkipBases { lookupflag IgnoreBaseGlyphs; sub u25CC by u25CC.alt; } SkipBases;
feature psts { lookup SkipLigatures; lookup SkipBases; } psts;
"""
CIRCLE_GDEF = """
table GDEF {
    GlyphClassDef [u0915 u1780 u1000 u1B13 u25CC u25CC.alt], [circle_u circle_u.alt], [u093F u0941 u17BB u102D u1B36], ;
} GDEF;
"""
CIRCLE_TEXTS = ["ु", "◌ु", "ុ", "ိ", "ᬶ", "ि", "◌ि"]


def build(file_name, code_points, features, extra_glyphs, without_gdef=False):
    path = build_font(file_name, code_points, features, extra_glyphs=extra_glyphs)
    if without_gdef:
        font = TTFont(path)
        if "GDEF" in font:
            del font["GDEF"]
        font.save(path)
    return load_font(path)


def main():
    rows = []
    for file_name, code_points, features, extra_glyphs, without_gdef, texts in [
            (FONT, CODE_POINTS, FEATURES, EXTRA_GLYPHS, False, TEXTS),
            (FONT_WITHOUT_GLYPH_CLASSES, CODE_POINTS_WITHOUT_GLYPH_CLASSES, FEATURES_WITHOUT_GLYPH_CLASSES,
             EXTRA_GLYPHS_WITHOUT_GLYPH_CLASSES, True, TEXTS_WITHOUT_GLYPH_CLASSES),
            (INDIC_FONT, INDIC_CODE_POINTS, INDIC_FEATURES, INDIC_EXTRA_GLYPHS, False, INDIC_TEXTS),
            (CIRCLE_FONT, CIRCLE_CODE_POINTS, CIRCLE_FEATURES, CIRCLE_EXTRA_GLYPHS, True, CIRCLE_TEXTS),
            (CIRCLE_GDEF_FONT, CIRCLE_CODE_POINTS, CIRCLE_FEATURES + CIRCLE_GDEF, CIRCLE_EXTRA_GLYPHS, False,
             CIRCLE_TEXTS)]:
        hb_font = build(file_name, code_points, features, extra_glyphs, without_gdef)
        rows.extend([file_name, text, shape_line(hb_font, text)[0]] for text in texts)
    write_reference(REFERENCE, rows)


if __name__ == "__main__":
    main()
