#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes ShaperChoice.harfbuzz.json, which shows the shaper that HarfBuzz chooses for each script and script tag.

Usage (from the repository root, fontTools 4.68 or later and uharfbuzz):

    python tests/tools/generate_shaper_choice_reference.py

HarfBuzz chooses a shaper from the script of the text and from the script tag that it finds in the GSUB table of
the font. The script does not read the HarfBuzz source. It builds seven fonts that differ only in the scripts of
their GSUB table:

* ShaperChoiceTag1.ttf has the first tag that HarfBuzz looks for, for each script of ScriptTags.harfbuzz.json,
  such as `dev3`, `mym2` and `latn`. ShaperChoiceTag2.ttf has the second tag, such as `dev2` and `mymr`, and
  ShaperChoiceTag3.ttf has the third, such as `deva`.
* ShaperChoiceDFLT.ttf, ShaperChoiceLowercaseDflt.ttf and ShaperChoiceLatn.ttf have only `DFLT`, `dflt` or `latn`.
* ShaperChoiceNone.ttf has only `zzzz`, which HarfBuzz looks for in no script.

Each font has one glyph for the first letter of every script, a glyph for U+0301, and no GPOS table. Every script
of the GSUB table has the features `nukt`, `pref`, `pres`, `haln` and `isol`, and each of them puts a glyph of its
own after the letter. What HarfBuzz does with the letter and the mark shows its shaper:

* The default shaper applies none of the five features, and places the mark on the letter.
* The Arabic shaper applies `isol`, and places the mark on the letter.
* The universal shaper applies `nukt`, `pref`, `pres` and `haln`, and `isol` for a script whose letters join.
  It takes the advance of the mark away.
* The Indic shaper applies `nukt`, `pres` and `haln`, and leaves the mark.
* The Khmer shaper applies `pres`, and leaves the mark.
* The Myanmar shaper applies `pref` and `pres`, and takes the advance of the mark away.

The reference holds the glyph IDs and positions that HarfBuzz gives for the letter and the mark of each script, in
each font that has a tag for the script. ShaperChoiceTests reads the reference.
The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
"""

import copy
import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import uharfbuzz as hb
from fontTools.feaLib.builder import addOpenTypeFeaturesFromString
from fontTools.fontBuilder import FontBuilder

from generate_normalization_reference import box, read_script_letters, shape_positions

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
SCRIPT_TAGS = os.path.join(FONTS, "ScriptTags.harfbuzz.json")
REFERENCE = os.path.join(FONTS, "ShaperChoice.harfbuzz.json")

MARK = 0x0301
FEATURES = ["nukt", "pref", "pres", "haln", "isol"]
NO_SCRIPT = "zzzz"

# (font, tag of every script, or None for the tags of ScriptTags.harfbuzz.json at this index)
FONT_TAGS = [
    ("ShaperChoiceTag1.ttf", 0),
    ("ShaperChoiceTag2.ttf", 1),
    ("ShaperChoiceTag3.ttf", 2),
    ("ShaperChoiceDFLT.ttf", "DFLT"),
    ("ShaperChoiceLowercaseDflt.ttf", "dflt"),
    ("ShaperChoiceLatn.ttf", "latn"),
    ("ShaperChoiceNone.ttf", NO_SCRIPT),
]


def read_scripts():
    """Gives the letter, the direction and the script tags of each script, by its name in ScriptClass.cs."""
    letters = {name.replace("_", ""): letter for name, letter in read_script_letters().items()}
    with open(SCRIPT_TAGS, encoding="utf-8") as file:
        tags = {script["name"]: script["tags"] for script in json.load(file)["scripts"]}
    assert set(letters) <= set(tags), sorted(set(letters) - set(tags))
    return {name: (*letter, tags[name]) for name, letter in letters.items()}


def build(file_name, letters, script_tags):
    names = [".notdef", "letter", "mark"] + FEATURES
    features = "languagesystem DFLT dflt;\n"
    features += "".join(f"feature {tag} {{ sub letter by letter {tag}; }} {tag};\n" for tag in FEATURES)

    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(names)
    builder.setupCharacterMap({**{letter: "letter" for letter in letters}, MARK: "mark"})
    builder.setupGlyf({name: box(-250, 0, -50, 700) if name == "mark" else box() for name in names})
    builder.setupHorizontalMetrics({name: (300, -250) if name == "mark" else (600, 50) for name in names})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    builder.setupPost(keepGlyphNames=False)
    addOpenTypeFeaturesFromString(builder.font, features)

    # Every script of the font shares the one language system that the feature code gives to DFLT.
    script_list = builder.font["GSUB"].table.ScriptList
    records = []
    for tag in sorted(script_tags):
        record = copy.copy(script_list.ScriptRecord[0])
        record.ScriptTag = tag
        records.append(record)
    script_list.ScriptRecord = records
    script_list.ScriptCount = len(records)

    path = os.path.join(FONTS, file_name)
    builder.font.save(path)
    with open(path, "rb") as file:
        return hb.Font(hb.Face(file.read()))


def main():
    scripts = read_scripts()
    letters = sorted(letter for letter, _, _ in scripts.values())
    cases = []
    for file_name, choice in FONT_TAGS:
        if isinstance(choice, int):
            tags = {name: script_tags[choice] for name, (_, _, script_tags) in scripts.items() if len(script_tags) > choice}
        else:
            tags = {name: choice for name in scripts}
        font = build(file_name, letters, set(tags.values()))
        for name, tag in tags.items():
            letter, direction, _ = scripts[name]
            text = chr(letter) + chr(MARK)
            cases.append({
                "name": f"{name}, no script" if tag == NO_SCRIPT else f"{name}, {tag}",
                "font": file_name,
                "text": text,
                "glyphs": shape_positions(font, text, direction),
            })

    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        file.write("{\n")
        file.write('  "harfbuzz": %s,\n' % json.dumps(hb.version_string()))
        file.write('  "cases": [\n')
        file.write(",\n".join("    " + json.dumps(case) for case in cases))
        file.write("\n  ]\n}\n")
    print(os.path.relpath(REFERENCE, ROOT), len(cases), "cases")


if __name__ == "__main__":
    main()
