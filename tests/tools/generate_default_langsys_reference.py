#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes DefaultLangSys.harfbuzz.json, the HarfBuzz reference for script and language system selection.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_default_langsys_reference.py

The `latn` script of NotoSansOgham-Regular.ttf and NotoSansRunic-Regular.ttf has only the `MOL `
and `ROM ` language systems and no default LangSys. With no language requested, HarfBuzz applies
no feature of that script, so Latin text gets no `kern` from GPOS and no `locl` from GSUB.
DejaVuSans.ttf has a default LangSys for `latn`, so HarfBuzz kerns the same text in that font.

The script also builds DfltLangSysRecord.ttf, whose scripts have a LangSysRecord tagged `dflt`:

* `arab` has only that record and no default LangSys. Its features are `init`, `medi`, `fina`
  and a `kern` of -50 between beh.medi and beh.fina.
* `latn` has both. The `liga` of the default LangSys gives a.default, and the `liga` of the
  `dflt` record gives a.dflt.

HarfBuzz looks for a `dflt` record before it uses the default LangSys, so it joins and kerns the
Arabic text and gives a.dflt for "a". feaLib writes `dflt` as the default LangSys, so the script
moves or renames the LangSys after feaLib has built it.

The script builds RequiredFeature.ttf, whose language systems have a required feature. HarfBuzz
applies the required feature to every glyph, even when the LangSys does not list it. It applies the
lookups in the stage of the shaper feature that has the same tag, or else in the first stage, which
holds only `rvrn`. Within the stage it applies them in LookupList order:

* `latn` requires `rqd1` (b to c), which is not listed. Its lookup comes after the `liga` lookup
  (a to b), but the first stage runs before `liga`, so "aba" becomes b c b. The GPOS of `latn` requires
  `rqdp`, which moves every b by -100.
* `grek` requires `liga` (a to b) and also lists it. `armn` requires the same feature and does not
  list it. Both list `calt` (b to d), whose lookup comes first, so it sees no b and "a" stays b.
* `cyrl` requires `rqd2` (b to c), whose lookup comes before the `rvrn` lookup (a to b). Both run in
  the first stage, so "a" becomes b and "b" becomes c.

The script also builds four ScriptFallback fonts. Each has a `liga` that gives a distinct glyph and a
`kern` with a distinct value for each script. None has a `grek` script, so HarfBuzz shapes Greek text
with the `DFLT` script, else the `dflt` script, else `latn`, and with no feature when the font has none
of them. The first ScriptRecord of three of the fonts is `arab`.

The reference holds the glyph IDs and positions that HarfBuzz gives for each case, in font units
from the origin of the line. DefaultLangSysTests reads it.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib.tables import otTables

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
REFERENCE = os.path.join(FONTS, "DefaultLangSys.harfbuzz.json")
DFLT_RECORD_FONT = "DfltLangSysRecord.ttf"
REQUIRED_FONT = "RequiredFeature.ttf"

GLYPHS = [".notdef", "beh", "beh.init", "beh.medi", "beh.fina", "a", "a.default", "a.dflt"]
CMAP = {0x0628: "beh", ord("a"): "a"}

# feaLib has no syntax for a `dflt` LangSysRecord, so `DEU ` stands in for it and build() renames it.
FEATURES = """
languagesystem arab dflt;
languagesystem latn dflt;
languagesystem latn DEU;

feature init { script arab; sub beh by beh.init; } init;
feature medi { script arab; sub beh by beh.medi; } medi;
feature fina { script arab; sub beh by beh.fina; } fina;
feature kern { script arab; pos beh.medi beh.fina -50; } kern;

feature liga {
    script latn;
    sub a by a.default;
    language DEU exclude_dflt;
    sub a by a.dflt;
} liga;
"""

CASES = [
    ("OghamKern", "Noto/NotoSansOgham-Regular.ttf", "AVATAR Wavy To"),
    ("OghamLocl", "Noto/NotoSansOgham-Regular.ttf", "Şş"),
    ("RunicKern", "Noto/NotoSansRunic-Regular.ttf", "AVATAR Wavy To"),
    ("RunicLocl", "Noto/NotoSansRunic-Regular.ttf", "Şş"),
    ("DejaVuSansKern", "DejaVuSans.ttf", "AVATAR Wavy To"),
    ("DfltRecordOnly", DFLT_RECORD_FONT, "ببب"),
    ("DfltRecordBeforeDefault", DFLT_RECORD_FONT, "a"),
    ("RequiredBeforeLiga", REQUIRED_FONT, "aba"),
    ("RequiredListedLiga", REQUIRED_FONT, "\u03b1"),
    ("RequiredUnlistedLiga", REQUIRED_FONT, "\u0561"),
    ("RequiredWithRvrn", REQUIRED_FONT, "\u0430\u0431"),
    ("FallbackToDFLT", "ScriptFallbackDFLT.ttf", "\u03b1\u03b1"),
    ("FallbackToDfltScript", "ScriptFallbackLowercaseDflt.ttf", "\u03b1\u03b1"),
    ("FallbackToLatn", "ScriptFallbackLatn.ttf", "\u03b1\u03b1"),
    ("FallbackToNone", "ScriptFallbackNone.ttf", "\u03b1\u03b1"),
]

REQUIRED_GLYPHS = [".notdef", "a", "b", "c", "d"]
REQUIRED_CMAP = {
    ord("a"): "a", ord("b"): "b",
    0x03B1: "a", 0x03B2: "b",
    0x0430: "a", 0x0431: "b",
    0x0561: "a", 0x0562: "b",
}

# feaLib cannot declare a required feature, so build_required_feature() sets REQUIRED.
REQUIRED_FEATURES = """
languagesystem DFLT dflt;
languagesystem armn dflt;
languagesystem cyrl dflt;
languagesystem grek dflt;
languagesystem latn dflt;

feature calt { script grek; sub b by d; script armn; sub b by d; } calt;
feature liga { script latn; sub a by b; script grek; sub a by b; script armn; sub a by b; } liga;
feature rqd2 { script cyrl; sub b by c; } rqd2;
feature rvrn { script cyrl; sub a by b; } rvrn;
feature rqd1 { script latn; sub b by c; } rqd1;
feature rqdp { script latn; pos b -100; } rqdp;
"""

# (table, script): (required feature, whether the LangSys also lists it)
REQUIRED = {
    ("GSUB", "latn"): ("rqd1", False),
    ("GSUB", "grek"): ("liga", True),
    ("GSUB", "armn"): ("liga", False),
    ("GSUB", "cyrl"): ("rqd2", True),
    ("GPOS", "latn"): ("rqdp", False),
}

FALLBACK_GLYPHS = [".notdef", "a", "a.arab", "a.cyrl", "a.DFLT", "a.dflt", "a.latn"]
FALLBACK_KERN = {"arab": -10, "cyrl": -20, "DFLT": -30, "dflt": -40, "latn": -50}

# feaLib has no `dflt` script, so `thai` stands in for it and build_script_fallback() renames it.
FALLBACK_FONTS = {
    "ScriptFallbackDFLT.ttf": ["DFLT", "arab", "dflt", "latn"],
    "ScriptFallbackLowercaseDflt.ttf": ["arab", "dflt", "latn"],
    "ScriptFallbackLatn.ttf": ["arab", "cyrl", "latn"],
    "ScriptFallbackNone.ttf": ["arab", "cyrl"],
}


def box():
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((450, 700))
    pen.lineTo((450, 0))
    pen.closePath()
    return pen.glyph()


def dflt_record(lang_sys):
    record = otTables.LangSysRecord()
    record.LangSysTag = "dflt"
    record.LangSys = lang_sys
    return record


def new_font(family, glyphs, cmap, features):
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(glyphs)
    builder.setupCharacterMap(cmap)
    builder.setupGlyf({name: box() for name in glyphs})
    builder.setupHorizontalMetrics({name: (600, 50) for name in glyphs})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": family, "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    addOpenTypeFeaturesFromString(builder.font, features)
    return builder


def build():
    builder = new_font("DfltLangSysRecord", GLYPHS, CMAP, FEATURES)

    for table in ("GSUB", "GPOS"):
        for script_record in builder.font[table].table.ScriptList.ScriptRecord:
            script = script_record.Script
            if script_record.ScriptTag == "arab":
                script.LangSysRecord = [dflt_record(script.DefaultLangSys)]
                script.LangSysCount = 1
                script.DefaultLangSys = None
            else:
                script.LangSysRecord = [dflt_record(r.LangSys) for r in script.LangSysRecord]

    builder.font.save(os.path.join(FONTS, DFLT_RECORD_FONT))


def build_required_feature():
    builder = new_font("RequiredFeature", REQUIRED_GLYPHS, REQUIRED_CMAP, REQUIRED_FEATURES)
    for (table, script), (feature, listed) in REQUIRED.items():
        layout = builder.font[table].table
        records = layout.FeatureList.FeatureRecord
        lang_sys = next(r.Script.DefaultLangSys for r in layout.ScriptList.ScriptRecord if r.ScriptTag == script)
        index = next(i for i in lang_sys.FeatureIndex if records[i].FeatureTag == feature)
        lang_sys.ReqFeatureIndex = index
        if not listed:
            lang_sys.FeatureIndex = [i for i in lang_sys.FeatureIndex if i != index]
            lang_sys.FeatureCount = len(lang_sys.FeatureIndex)

    builder.font.save(os.path.join(FONTS, REQUIRED_FONT))


def build_script_fallback(path, scripts):
    fea_scripts = ["thai" if s == "dflt" else s for s in scripts]
    features = "".join(f"languagesystem {s} dflt;\n" for s in fea_scripts)
    features += "feature liga {\n"
    features += "".join(f"    script {f}; sub a by a.{s};\n" for f, s in zip(fea_scripts, scripts))
    features += "} liga;\nfeature kern {\n"
    features += "".join(f"    script {f}; pos a.{s} a.{s} {FALLBACK_KERN[s]};\n" for f, s in zip(fea_scripts, scripts))
    features += "} kern;\n"

    builder = new_font(os.path.splitext(path)[0], FALLBACK_GLYPHS, {0x03B1: "a"}, features)
    for table in ("GSUB", "GPOS"):
        script_list = builder.font[table].table.ScriptList
        for record in script_list.ScriptRecord:
            if record.ScriptTag == "thai":
                record.ScriptTag = "dflt"
        script_list.ScriptRecord.sort(key=lambda r: r.ScriptTag)

    builder.font.save(os.path.join(FONTS, path))


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
    build()
    build_required_feature()
    for path, scripts in FALLBACK_FONTS.items():
        build_script_fallback(path, scripts)
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
