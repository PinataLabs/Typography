#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes the generated tables of the PDF standard encodings and the Adobe Glyph List.

Usage (from the repository root, Python 3.9 or later, no packages):

    python tests/tools/generate_glyph_names.py

It reads the data files in tests/tools/glyph_names and writes two files into
src/PinataLabs.Typography.Unicode/Resources:

- AdobeGlyphListData.Generated.cs: the glyph names of glyphlist.txt and zapfdingbats.txt with their text, and the
  preferred glyph name of each code point.
- PdfEncodingData.Generated.cs: the code to glyph name table of each SimpleFontEncoding.

The data files and where they come from are listed in tests/tools/glyph_names/README.md. Before it writes, the script
checks the data against itself and against other sources, and stops at the first disagreement:

- Every glyph name of every encoding has text in the Adobe Glyph List (the ZapfDingbats names also in zapfdingbats.txt).
- WinAnsi agrees with the cp1252 codec, and MacRoman with the mac_roman codec, except for the entries in EXCEPTIONS.
- Every aglfn.txt name is in glyphlist.txt with the same code point.

The output is deterministic.
"""

import re
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "tests" / "tools" / "glyph_names"
OUT = ROOT / "src" / "PinataLabs.Typography.Unicode" / "Resources"

# (enum member, source). A source is an AFM file whose `C code` lines are the encoding, or a section of
# pdf-latin-encodings.txt.
ENCODINGS = [
    ("Standard", "Times-Roman.afm"),
    ("WinAnsi", "[WinAnsi]"),
    ("MacRoman", "[MacRoman]"),
    ("MacExpert", "[MacExpert]"),
    ("Symbol", "Symbol.afm"),
    ("ZapfDingbats", "ZapfDingbats.afm"),
]

# Codes where the codec and the PDF encoding differ on purpose, with the reason.
EXCEPTIONS = {
    "WinAnsi": {
        0x7F: "unused code, the bullet",
        0x81: "unused code, the bullet",
        0x8D: "unused code, the bullet",
        0x8F: "unused code, the bullet",
        0x90: "unused code, the bullet",
        0x9D: "unused code, the bullet",
        0xA0: "Annex D note: space",
        0xAD: "Annex D note: hyphen",
    },
    "MacRoman": {
        0xCA: "Annex D note: space",
        0xDB: "Mac OS Roman 8.5 has the Euro here, Annex D has the currency sign",
    },
}


def read_list(name):
    """Reads a `name;code points` list. Returns {name: text}."""
    entries = {}
    for line in (DATA / name).read_text(encoding="ascii").splitlines():
        if not line or line.startswith("#"):
            continue
        glyph, values = line.split(";")
        assert glyph not in entries, f"{name}: duplicate {glyph}"
        entries[glyph] = "".join(chr(int(v, 16)) for v in values.split())
    return entries


def read_aglfn():
    """Reads aglfn.txt, `code point;name;description`. Returns {code point: name}."""
    names = {}
    for line in (DATA / "aglfn.txt").read_text(encoding="ascii").splitlines():
        if not line or line.startswith("#"):
            continue
        code, name, _ = line.split(";")
        names[int(code, 16)] = name
    return names


def read_afm(name):
    """Reads the encoded characters of an AFM file. Returns {code: glyph name}."""
    codes = {}
    for line in (DATA / name).read_text(encoding="ascii").splitlines():
        m = re.match(r"C (-?\d+) ; WX \d+ ; N (\S+) ;", line)
        if m and int(m.group(1)) >= 0:
            codes[int(m.group(1))] = m.group(2)
    return codes


def read_latin_sections():
    """Reads pdf-latin-encodings.txt. Returns {"[WinAnsi]": {code: glyph name}, ...}."""
    sections, current = {}, None
    for line in (DATA / "pdf-latin-encodings.txt").read_text(encoding="ascii").splitlines():
        line = line.split("#")[0].strip()
        if not line:
            continue
        if line.startswith("["):
            current = sections.setdefault(line, {})
        else:
            code, name = line.split()
            current[int(code)] = name
    return sections


def check(condition, message):
    if not condition:
        sys.exit(f"generate_glyph_names.py: {message}")


def check_against_codec(label, table, codec, glyph_list):
    """Each code of the table has the text that the codec gives it, up to canonical equivalence."""
    for code, name in sorted(table.items()):
        if code in EXCEPTIONS[label]:
            continue
        expected = bytes([code]).decode(codec)
        actual = glyph_list[name]
        check(
            unicodedata.normalize("NFC", actual) == unicodedata.normalize("NFC", expected),
            f"{label} {code:#04x} is {name} ({actual!r}), the {codec} codec has {expected!r}",
        )


def cs_string(text):
    """A C# string literal of ASCII, with every other character as a \\u escape."""
    return '"' + "".join(c if " " <= c < "\x7f" and c not in '"\\' else f"\\u{ord(c):04X}" for c in text) + '"'


def cs_blob(text):
    """A const string expression of the text, in lines that are not too long."""
    chunks = [text[i:i + 96] for i in range(0, len(text), 96)]
    return "\n        + ".join(cs_string(c) for c in chunks)


def cs_numbers(values, per_line, fmt):
    lines = []
    for i in range(0, len(values), per_line):
        lines.append("        " + ", ".join(fmt(v) for v in values[i:i + per_line]) + ",")
    return "\n".join(lines)


def table_source(field, entries):
    """The GlyphNameTable of a {name: text} list: names sorted by ordinal, text in the same order."""
    names = sorted(entries)
    name_offsets, value_offsets = [0], [0]
    for n in names:
        name_offsets.append(name_offsets[-1] + len(n))
        value_offsets.append(value_offsets[-1] + len(entries[n]))
    check(name_offsets[-1] <= 0xFFFF and value_offsets[-1] <= 0xFFFF, f"{field} does not fit ushort offsets")
    return f"""    public static readonly GlyphNameTable {field} = new(
        {cs_blob("".join(names))},
        new ushort[]
        {{
{cs_numbers(name_offsets, 16, str)}
        }},
        {cs_blob("".join(entries[n] for n in names))},
        new ushort[]
        {{
{cs_numbers(value_offsets, 16, str)}
        }});
"""


def write_glyph_list(glyph_list, zapf, aglfn):
    codes = sorted(aglfn)
    source = f"""// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

// <auto-generated />
// Generated by tests/tools/generate_glyph_names.py from glyphlist.txt, zapfdingbats.txt and aglfn.txt of
// https://github.com/adobe-type-tools/agl-aglfn (Copyright 2002-2019 Adobe, BSD-3-Clause, see NOTICE).
#nullable enable
namespace PinataLabs.Typography.Unicode;

/// <summary>The Adobe Glyph List, the ITC Zapf Dingbats glyph list and the preferred glyph name of each code point.</summary>
internal static class AdobeGlyphListData
{{
    /// <summary>The {len(glyph_list)} glyph names of glyphlist.txt.</summary>
{table_source("GlyphList", glyph_list)}
    /// <summary>The {len(zapf)} glyph names of zapfdingbats.txt.</summary>
{table_source("ZapfDingbatsList", zapf)}
    /// <summary>The code points of aglfn.txt in ascending order. <see cref="PreferredNames"/> has their names.</summary>
    public static readonly int[] PreferredCodePoints = new int[]
    {{
{cs_numbers(codes, 12, lambda v: f"0x{v:04X}")}
    }};

    /// <summary>The glyph name that aglfn.txt gives each of <see cref="PreferredCodePoints"/>.</summary>
    public static readonly string[] PreferredNames = new string[]
    {{
{cs_numbers([aglfn[c] for c in codes], 6, cs_string)}
    }};
}}
"""
    (OUT / "AdobeGlyphListData.Generated.cs").write_text(source, encoding="utf-8", newline="\n")


def write_encodings(tables):
    members = []
    for label, table in tables:
        names = [table.get(code) for code in range(256)]
        members.append(
            f"""    /// <summary>The glyph name of each code of the {label} encoding, <see langword="null"/> for a code without one.</summary>
    public static readonly string?[] {label} = new string?[]
    {{
{cs_numbers(names, 6, lambda n: "null" if n is None else cs_string(n))}
    }};
"""
        )
    source = f"""// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

// <auto-generated />
// Generated by tests/tools/generate_glyph_names.py from the data in tests/tools/glyph_names, see its README.md.
#nullable enable
namespace PinataLabs.Typography.Unicode;

/// <summary>The code to glyph name table of each <c>SimpleFontEncoding</c>, 256 entries each.</summary>
internal static class PdfEncodingData
{{
{chr(10).join(members)}}}
"""
    (OUT / "PdfEncodingData.Generated.cs").write_text(source, encoding="utf-8", newline="\n")


def main():
    glyph_list = read_list("glyphlist.txt")
    zapf = read_list("zapfdingbats.txt")
    aglfn = read_aglfn()
    sections = read_latin_sections()

    for code_point, name in aglfn.items():
        check(glyph_list.get(name) == chr(code_point), f"aglfn {name} is not U+{code_point:04X} in glyphlist.txt")

    tables = []
    for label, source in ENCODINGS:
        table = sections[source] if source.startswith("[") else read_afm(source)
        check(table and all(0 <= c < 256 for c in table), f"{label}: no table")
        # A ZapfDingbats font names its glyphs from zapfdingbats.txt, and `space` from the Adobe Glyph List.
        for code, name in table.items():
            check(name in glyph_list or (label == "ZapfDingbats" and name in zapf),
                  f"{label} {code:#04x}: {name} is not in its glyph list")
        tables.append((label, table))
    by_label = dict(tables)

    check_against_codec("WinAnsi", by_label["WinAnsi"], "cp1252", glyph_list)
    check_against_codec("MacRoman", by_label["MacRoman"], "mac_roman", glyph_list)
    check(all(by_label["WinAnsi"][c] == "bullet" for c in (0x7F, 0x81, 0x8D, 0x8F, 0x90, 0x9D)), "WinAnsi bullets")
    check(by_label["WinAnsi"][0xA0] == "space" and by_label["WinAnsi"][0xAD] == "hyphen", "WinAnsi 0xA0, 0xAD")
    check(by_label["MacRoman"][0xCA] == "space", "MacRoman 0xCA")

    OUT.mkdir(parents=True, exist_ok=True)
    write_glyph_list(glyph_list, zapf, aglfn)
    write_encodings(tables)
    print(f"{len(glyph_list)} AGL names, {len(zapf)} Zapf Dingbats names, {len(aglfn)} preferred names,",
          ", ".join(f"{label} {len(t)}" for label, t in tables))


if __name__ == "__main__":
    main()
