#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Measures where a browser puts upright and rotated glyphs across a vertical-rl column (issue #50).

Usage (from the repository root, after build_font.py):

    python tests/tools/central_baseline/measure_browser.py [path-to-msedge-or-chrome]

The script takes a headless screenshot of central-baseline.html and reads it pixel by pixel.
Each column's line box is the gray stripe at its top. Below the stripe, each run of rows with
ink is one glyph, and the script reads the ink's left and right edges to a fraction of a pixel.
The test font's Latin glyphs fill the ascent to descent range, so the ink centre of a rotated
Latin glyph is its central baseline. A glyph whose ink is wider than it is tall is rotated.

It prints JSON with each glyph's ink edges relative to the column's left edge, in px at 100px.
"""

import json
import os
import struct
import subprocess
import sys
import tempfile
import time
import zlib

HERE = os.path.dirname(os.path.abspath(__file__))
PAGE = os.path.join(HERE, "central-baseline.html")

# Column left edges and the stripe rows, as set in central-baseline.html.
COLUMNS = {"em": 200, "normal": 500}
STRIPE_ROWS = range(25, 45)
TEXT_TOP = 50

CANDIDATES = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    "/usr/bin/google-chrome",
    "/usr/bin/chromium",
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
]


def find_browser():
    if len(sys.argv) > 1:
        return sys.argv[1]
    for path in CANDIDATES:
        if os.path.exists(path):
            return path
    sys.exit("No Chromium browser found. Pass its path as the first argument.")


def screenshot(browser):
    work = tempfile.mkdtemp(prefix="central-baseline-")
    shot = os.path.join(work, "shot.png")
    subprocess.run(
        [
            browser,
            "--headless=new",
            "--disable-gpu",
            "--hide-scrollbars",
            "--force-device-scale-factor=1",
            "--allow-file-access-from-files",
            "--window-size=800,1100",
            "--virtual-time-budget=5000",
            "--user-data-dir=" + os.path.join(work, "profile"),
            "--screenshot=" + shot,
            "file:///" + PAGE.replace("\\", "/"),
        ],
        capture_output=True,
        timeout=120,
    )

    # On Windows msedge.exe can return before the browser process it starts writes the file.
    deadline = time.time() + 60
    last = -1
    while time.time() < deadline:
        size = os.path.getsize(shot) if os.path.exists(shot) else -1
        if size > 0 and size == last:
            return shot
        last = size
        time.sleep(1)
    sys.exit("The browser wrote no screenshot.")


def read_png(path):
    """Returns rows of darkness values, 0 for white and 1 for black."""
    data = open(path, "rb").read()
    pos = 8
    idat = b""
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            width, height, depth, color = struct.unpack(">IIBB", body[:10])
            if depth != 8 or color not in (2, 6):
                sys.exit("Unsupported PNG format")
            channels = 3 if color == 2 else 4
        elif kind == b"IDAT":
            idat += body
        pos += 12 + length

    raw = zlib.decompress(idat)
    stride = width * channels
    rows = []
    prev = bytearray(stride)
    i = 0
    for _ in range(height):
        f = raw[i]
        line = bytearray(raw[i + 1:i + 1 + stride])
        i += 1 + stride
        for x in range(stride):
            a = line[x - channels] if x >= channels else 0
            b = prev[x]
            c = prev[x - channels] if x >= channels else 0
            if f == 1:
                line[x] = (line[x] + a) & 0xFF
            elif f == 2:
                line[x] = (line[x] + b) & 0xFF
            elif f == 3:
                line[x] = (line[x] + ((a + b) >> 1)) & 0xFF
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[x] = (line[x] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 0xFF
        rows.append([1 - (line[x * channels] + line[x * channels + 1] + line[x * channels + 2]) / 765 for x in range(width)])
        prev = line
    return rows


def edges(row, x0, x1, full):
    """Subpixel left and right edges of the dark run in row[x0:x1], where full is the run's darkness."""
    dark = [x for x in range(x0, x1) if row[x] > 0.02]
    if not dark:
        return None
    left, right = dark[0], dark[-1]
    return left + 1 - row[left] / full, right + row[right] / full


def mean(values):
    return sum(values) / len(values)


def measure_column(rows, left_guess):
    x0, x1 = left_guess - 90, left_guess + 250
    stripe = [edges(rows[y], x0, x1, 0.498) for y in STRIPE_ROWS]
    col_left = mean([s[0] for s in stripe])
    col_right = mean([s[1] for s in stripe])

    glyphs = []
    run = []
    for y in range(TEXT_TOP + 2, len(rows)):
        e = edges(rows[y], x0, x1, 1.0)
        if e is not None:
            run.append(e)
        elif run:
            # Drop the antialiased first and last rows of the run.
            body = run[2:-2] or run
            l, r = mean([b[0] for b in body]) - col_left, mean([b[1] for b in body]) - col_left
            glyphs.append({
                "rotated": r - l > len(run),
                "inkLeft": round(l, 2),
                "inkRight": round(r, 2),
                "inkCentre": round((l + r) / 2, 2),
            })
            run = []

    width = col_right - col_left
    return {"left": round(col_left, 2), "width": round(width, 2), "centre": round(width / 2, 2), "glyphs": glyphs}


def main():
    browser = find_browser()
    rows = read_png(screenshot(browser))
    result = {"browser": browser, "columns": {name: measure_column(rows, x) for name, x in COLUMNS.items()}}
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
