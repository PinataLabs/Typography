#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes UniversalShapingCategory.harfbuzz.txt, the universal shaper category of every code point in HarfBuzz.

Usage (from the repository root, Python 3.9 or later, with network access):

    python tests/tools/derive_harfbuzz_use_categories.py [path-to-hb-ot-shaper-use-table.hh]

HarfBuzz builds its universal shaper (USE) category table with gen-use-table.py and ships the result as
src/hb-ot-shaper-use-table.hh. HarfBuzz has no API that returns the category of a code point, so the script reads
that file at the HarfBuzz tag below, or at the path given, and runs its lookup function for every code point. The
lookup is plain array arithmetic, which this script interprets.

The output lists each range of code points whose category is not O, the category of every other code point.
UniversalShaperTests checks that the library gives every code point the same category.
The output is deterministic for one HarfBuzz version.
"""

import ast
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

    match = re.search(r"return u<(\d+)u \? \(uint8_t\)\((.*)\) : (\w+);", fast)
    limit, expression, default = int(match.group(1)), match.group(2), match.group(3)
    tree = ast.parse(re.sub(r"(\d+)u\b", r"\1", expression), mode="eval").body

    def lookup(u):
        return evaluate(tree, namespace, u) if u < limit else default

    return lookup


OPERATORS = {
    ast.Add: lambda a, b: a + b,
    ast.BitAnd: lambda a, b: a & b,
    ast.LShift: lambda a, b: a << b,
    ast.RShift: lambda a, b: a >> b,
}


def evaluate(node, arrays, u):
    # Interprets the array arithmetic of the lookup, so no downloaded text runs as code.
    if isinstance(node, ast.Constant) and isinstance(node.value, int):
        return node.value
    if isinstance(node, ast.Name) and node.id == "u":
        return u
    if isinstance(node, ast.BinOp) and type(node.op) in OPERATORS:
        return OPERATORS[type(node.op)](evaluate(node.left, arrays, u), evaluate(node.right, arrays, u))
    if isinstance(node, ast.Subscript) and isinstance(node.value, ast.Name):
        return arrays[node.value.id][evaluate(node.slice, arrays, u)]
    if isinstance(node, ast.Call) and getattr(node.func, "id", None) == "hb_use_b4":
        array, index = arrays[node.args[0].id], evaluate(node.args[1], arrays, u)
        return (array[index >> 1] >> ((index & 1) << 2)) & 15
    raise ValueError(f"unexpected expression in hb-ot-shaper-use-table.hh: {ast.dump(node)}")


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
