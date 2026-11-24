#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes ScriptTags.harfbuzz.json, the OpenType script tags that HarfBuzz tries for each Unicode script.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/derive_harfbuzz_script_tags.py

The script does not read the HarfBuzz source. It takes each script of ScriptClass.cs and its ISO 15924
code, and asks HarfBuzz which script tags it looks for in a font:

1. The candidates are every tag of the form `xxx2`, `xxx3`, `xxx ` and `xx  `, and the lowercase ISO
   code of every script, that hb_ot_tag_to_script maps to a script of ScriptClass.cs other than Zzzz.
2. A probe font has a GSUB script for each candidate and for `DFLT`. Each script has a required feature
   that changes "a" to a glyph of its own.
3. The script shapes "a" with the buffer script set to the ISO code and reads which script HarfBuzz
   chose. It renames that script so that HarfBuzz cannot find it, and shapes again, until HarfBuzz
   chooses `DFLT`. The scripts it chose, in order, are the tags of the Unicode script.

The script also builds ScriptTags.ttf. It has the scripts `nko `, `lao `, `vai `, `yi  ` and `kana`,
whose ISO codes are Nkoo, Laoo, Vaii, Yiii and Kana, and `DFLT`. Each has a `ccmp` that gives a glyph
of its own and a `kern` with a value of its own. The reference holds the glyph IDs and positions that
HarfBuzz gives for text of each script, and for Hiragana text, which HarfBuzz shapes with `kana`.

Last, it copies NotoSansKannada-Regular.ttf to ScriptTagsKannadaVersion3.ttf with its `knd2` script
renamed to `knd3`. HarfBuzz tries `knd3` first, and the reference holds the glyphs it gives for
Kannada text in that font.

ScriptTagTests reads the reference.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import io
import itertools
import json
import os
import re
import string

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable
from fontTools.ttLib.tables import otTables

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
SCRIPT_CLASS = os.path.join(ROOT, "src", "PinataLabs.Typography", "Unicode", "ScriptClass.cs")
REFERENCE = os.path.join(FONTS, "ScriptTags.harfbuzz.json")
FONT = "ScriptTags.ttf"
KANNADA_SOURCE = "NotoSansKannada-Regular.ttf"
KANNADA_FONT = "ScriptTagsKannadaVersion3.ttf"

HIDDEN = "zz{:02d}"

# (fea script, glyph suffix, ccmp source character, kern value)
FONT_SCRIPTS = [
    ("DFLT", "dflt", None, -10),
    ("nko", "nko", 0x07CA, -20),
    ("lao", "lao", 0x0E81, -30),
    ("vai", "vai", 0xA500, -40),
    ("yi", "yi", 0xA000, -50),
    ("kana", "kana", 0x30A2, -60),
]

CASES = [
    ("Nko", FONT, "ߊߊ"),
    ("Lao", FONT, "ກກ"),
    ("Vai", FONT, "ꔀꔀ"),
    ("Yi", FONT, "ꀀꀀ"),
    ("Katakana", FONT, "アア"),
    ("Hiragana", FONT, "ああ"),
    ("KannadaVersion3", KANNADA_FONT, "ಕನ್ನಡ ಕ್ಷ"),
]


def script_classes():
    with open(SCRIPT_CLASS, encoding="utf-8") as file:
        source = file.read()
    return re.findall(r"Shortcode: (\w{4})[^\n]*\n\s*/// </summary>\s*\n\s*(\w+),", source)


def candidate_tags(codes):
    pool = {code.lower() for code in codes}
    letters = string.ascii_lowercase
    for prefix in itertools.product(letters, repeat=3):
        for last in "23 ":
            pool.add("".join(prefix) + last)
    for prefix in itertools.product(letters, repeat=2):
        pool.add("".join(prefix) + "  ")
    # HarfBuzz maps a tag that it does not know to Zzzz, so only the ISO code stands for that script.
    return sorted(t for t in pool if t == "zzzz" or hb.ot_tag_to_script(t) in codes - {"Zzzz"})


def probe_font(tags):
    glyphs = [".notdef", "a"] + [f"m{i}" for i in range(len(tags))]
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(glyphs)
    builder.setupCharacterMap({ord("a"): "a"})
    builder.setupGlyf({name: TTGlyphPen(None).glyph() for name in glyphs})
    builder.setupHorizontalMetrics({name: (500, 0) for name in glyphs})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "ScriptTagProbe", "styleName": "Regular"})
    builder.setupOS2()
    builder.setupPost()

    lookups = []
    features = []
    records = []
    for i, tag in enumerate(tags):
        lookup = otTables.Lookup()
        lookup.LookupType = 1
        lookup.LookupFlag = 0
        subst = otTables.SingleSubst()
        subst.mapping = {"a": f"m{i}"}
        lookup.SubTable = [subst]
        lookup.SubTableCount = 1
        lookups.append(lookup)

        feature = otTables.FeatureRecord()
        feature.FeatureTag = f"q{i:03d}"
        feature.Feature = otTables.Feature()
        feature.Feature.FeatureParams = None
        feature.Feature.LookupListIndex = [i]
        feature.Feature.LookupCount = 1
        features.append(feature)

        lang_sys = otTables.LangSys()
        lang_sys.LookupOrder = None
        lang_sys.ReqFeatureIndex = i
        lang_sys.FeatureIndex = []
        lang_sys.FeatureCount = 0
        record = otTables.ScriptRecord()
        record.ScriptTag = tag
        record.Script = otTables.Script()
        record.Script.DefaultLangSys = lang_sys
        record.Script.LangSysRecord = []
        record.Script.LangSysCount = 0
        records.append(record)

    gsub = otTables.GSUB()
    gsub.Version = 0x00010000
    gsub.ScriptList = otTables.ScriptList()
    gsub.ScriptList.ScriptRecord = records
    gsub.ScriptList.ScriptCount = len(records)
    gsub.FeatureList = otTables.FeatureList()
    gsub.FeatureList.FeatureRecord = features
    gsub.FeatureList.FeatureCount = len(features)
    gsub.LookupList = otTables.LookupList()
    gsub.LookupList.Lookup = lookups
    gsub.LookupList.LookupCount = len(lookups)
    table = builder.font["GSUB"] = newTable("GSUB")
    table.table = gsub
    return builder.font


def chosen_tag(font, tags, code):
    gsub = font["GSUB"].table
    gsub.ScriptList.ScriptRecord.sort(key=lambda r: r.ScriptTag)
    data = io.BytesIO()
    font.save(data)
    buffer = hb.Buffer()
    buffer.add_str("a")
    buffer.script = code
    buffer.direction = "ltr"
    hb.shape(hb.Font(hb.Face(data.getvalue())), buffer)
    glyph = buffer.glyph_infos[0].codepoint
    assert glyph >= 2, f"HarfBuzz chose no probe script for {code}"
    return tags[glyph - 2]


def derive_tags(classes):
    codes = {code for code, _ in classes}
    candidates = candidate_tags(codes) + ["DFLT"]
    font = probe_font(candidates)
    records = {r.ScriptTag: r for r in font["GSUB"].table.ScriptList.ScriptRecord}
    scripts = []
    for code, name in classes:
        tags = []
        hidden = []
        while (tag := chosen_tag(font, candidates, code)) != "DFLT":
            tags.append(tag)
            hidden.append(tag)
            records[tag].ScriptTag = HIDDEN.format(len(hidden))
        for tag in hidden:
            records[tag].ScriptTag = tag
        scripts.append({"name": name, "iso": code, "tags": tags})
    return scripts


def box():
    pen = TTGlyphPen(None)
    pen.moveTo((50, 0))
    pen.lineTo((50, 700))
    pen.lineTo((450, 700))
    pen.lineTo((450, 0))
    pen.closePath()
    return pen.glyph()


def build_font():
    glyphs = [".notdef", "base"] + [f"base.{suffix}" for _, suffix, _, _ in FONT_SCRIPTS]
    cmap = {cp: "base" for _, _, cp, _ in FONT_SCRIPTS if cp}
    cmap[0x3042] = "base"
    features = "".join(f"languagesystem {fea} dflt;\n" for fea, _, _, _ in FONT_SCRIPTS)
    features += "feature ccmp {\n"
    features += "".join(f"    script {fea}; sub base by base.{suffix};\n" for fea, suffix, _, _ in FONT_SCRIPTS)
    features += "} ccmp;\nfeature kern {\n"
    features += "".join(
        f"    script {fea}; pos base.{suffix} base.{suffix} {kern};\n" for fea, suffix, _, kern in FONT_SCRIPTS)
    features += "} kern;\n"

    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(glyphs)
    builder.setupCharacterMap(cmap)
    builder.setupGlyf({name: box() for name in glyphs})
    builder.setupHorizontalMetrics({name: (600, 50) for name in glyphs})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": "ScriptTags", "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost()
    addOpenTypeFeaturesFromString(builder.font, features)
    builder.font.save(os.path.join(FONTS, FONT))


def build_kannada_version3():
    font = TTFont(os.path.join(FONTS, KANNADA_SOURCE))
    for table in ("GSUB", "GPOS"):
        script_list = font[table].table.ScriptList
        for record in script_list.ScriptRecord:
            if record.ScriptTag == "knd2":
                record.ScriptTag = "knd3"
        script_list.ScriptRecord.sort(key=lambda r: r.ScriptTag)
    font.save(os.path.join(FONTS, KANNADA_FONT))


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
    scripts = derive_tags(script_classes())
    build_font()
    build_kannada_version3()
    cases = [{"name": name, "font": path, "text": text, "glyphs": shape(path, text)} for name, path, text in CASES]
    reference = {"harfbuzz": hb.version_string(), "scripts": scripts, "cases": cases}
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        json.dump(reference, file, indent=2, ensure_ascii=False)
        file.write("\n")
    print(os.path.relpath(REFERENCE, ROOT))


if __name__ == "__main__":
    main()
