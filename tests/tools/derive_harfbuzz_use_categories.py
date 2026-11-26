#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes UniversalShapingCategory.harfbuzz.txt, the universal shaper category of every code point in HarfBuzz.

Usage (from the repository root, Python 3.9 or later, with network access):

    python tests/tools/derive_harfbuzz_use_categories.py [path-to-hb-ot-shaper-use-table.hh]

HarfBuzz builds its universal shaper (USE) category table with gen-use-table.py and ships the result as
src/hb-ot-shaper-use-table.hh. HarfBuzz has no API that returns the category of a code point, so the script reads
that file at the HarfBuzz tag below, or at the path given, and runs its lookup function for every code point. The
lookup is plain array arithmetic, which this script evaluates as Python.

The output lists each range of code points whose category is not O, the category of every other code point.
UniversalShaperTests checks that the library gives every code point the same category.
The output is deterministic for one HarfBuzz version.
"""

import os
import re
import sys
import urllib.request

HARFBUZZ_VERSION = "15.0.0"
URL = f"https://raw.githubusercontent.com/harfbuzz/harfbuzz/{HARFBUZZ_VERSION}/src/hb-ot-shaper-use-table.hh"

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUTPUT = os.path.join(ROOT, "tests", "UnicodeTestData", "UniversalShapingCategory.harfbuzz.txt")


def read_table():
    if len(sys.argv) > 1:
        with open(sys.argv[1], encoding="utf-8") as file:
            return file.read()
    with urllib.request.urlopen(URL) as response:
        return response.read().decode("utf-8")


def build_lookup(source):
    # The file has a fast table and a compact one (HB_OPTIMIZE_SIZE). Both give the same categories; use the first.
    fast = source[source.index("#ifndef HB_OPTIMIZE_SIZE"):source.index("#else")]

    namespace = {}
    for kind, name, body in re.findall(r"static const (uint8_t|uint16_t) (\w+)\[\d+\]=\s*\{(.*?)\};", fast, re.S):
        namespace[name] = [int(v) if v.isdigit() else v for v in (x.strip() for x in body.split(",")) if v]

    namespace["hb_use_b4"] = lambda a, i: (a[i >> 1] >> ((i & 1) << 2)) & 15

    match = re.search(r"return u<(\d+)u \? \(uint8_t\)\((.*)\) : (\w+);", fast)
    limit, expression, default = int(match.group(1)), match.group(2), match.group(3)
    expression = re.sub(r"(\d+)u\b", r"\1", expression)
    code = compile(expression, "hb_use_get_category", "eval")

    def lookup(u):
        if u >= limit:
            return default
        namespace["u"] = u
        return eval(code, namespace)

    return lookup


def main():
    source = read_table()
    lookup = build_lookup(source)

    lines = []
    start, current = 0, lookup(0)
    for u in range(1, 0x110001):
        value = lookup(u) if u < 0x110000 else None
        if value != current:
            if current != "O":
                span = f"{start:04X}" if start == u - 1 else f"{start:04X}..{u - 1:04X}"
                lines.append(f"{span:<14}; {current}")
            start, current = u, value

    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as file:
        file.write(f"# The universal shaper category of each code point in HarfBuzz {HARFBUZZ_VERSION}.\n")
        file.write("# Written by tests/tools/derive_harfbuzz_use_categories.py from src/hb-ot-shaper-use-table.hh.\n")
        file.write("# Every code point not listed has the category O.\n\n")
        file.write("\n".join(lines) + "\n")


if __name__ == "__main__":
    main()
