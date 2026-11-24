#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes DefaultLangSys.harfbuzz.json, the HarfBuzz reference for language system selection.

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
]


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


def build():
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(GLYPHS)
    builder.setupCharacterMap(CMAP)
    builder.setupGlyf({name: box() for name in GLYPHS})
    builder.setupHorizontalMetrics({name: (600, 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "DfltLangSysRecord", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    addOpenTypeFeaturesFromString(builder.font, FEATURES)

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
