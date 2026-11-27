#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Writes FontDescriptor.fonttools.json, the fontTools reference for FontMetrics.DescriptorMetrics and
FontMetrics.EmbeddingPermissions (issue #157).

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_font_descriptor_reference.py

For every face of every .ttf, .otf and .ttc file under tests/PinataLabs.Typography.Tests/Fonts that fontTools reads
and that has outlines, the reference holds the values that a PDF /FontDescriptor is written from and the OS/2 fsType
permissions. It reads the tables with fontTools and applies the precedence that docs/specs/003-font-descriptor.md states:

* Ascent and descent: OS/2 sTypoAscender and sTypoDescender when fsSelection bit 7 (USE_TYPO_METRICS) is set, else hhea.
  When either is 0, the typo values if either of them is not 0, else OS/2 usWinAscent and -usWinDescent. When both are
  still 0, head yMax and yMin.
* Cap height and x-height: OS/2 sCapHeight and sxHeight when the version is 2 or later and the value is not 0, else the
  top of the glyph of U+0048 or U+0078 (see glyph_name_of), else the ascent (cap height) or 0 (x-height). The top of a glyph is the yMax of
  its glyf header, or the top of its CFF outline.
* Italic angle: post italicAngle. Bounding box: head xMin, yMin, xMax and yMax.
* Flags: the bits of ISO 32000-1 Table 123 that the tables state.

The script also builds the fonts in Fonts/FontDescriptor, which cover what the other faces do not: the win metrics and the
head fallback, a symbol cmap, the PANOSE classification, and the fsType bits that restrict embedding.

The reference records each source as the name of the table field that gave the value. FontDescriptorMetricsReferenceTests
reads it. The output is deterministic for one fontTools version, which the reference records.
"""

import json
import os

os.environ["SOURCE_DATE_EPOCH"] = "0"

import fontTools
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.boundsPen import BoundsPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTCollection, TTFont
from fontTools.ttLib.tables.O_S_2f_2 import Panose

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
SYNTHETIC = os.path.join(FONTS, "FontDescriptor")
REFERENCE = os.path.join(FONTS, "FontDescriptor.fonttools.json")
EXTENSIONS = (".ttf", ".otf", ".ttc")

FS_SELECTION_ITALIC = 1 << 0
FS_SELECTION_USE_TYPO_METRICS = 1 << 7
FS_SELECTION_OBLIQUE = 1 << 9
MAC_STYLE_ITALIC = 1 << 1

FLAG_FIXED_PITCH = 1 << 0
FLAG_SERIF = 1 << 1
FLAG_SYMBOLIC = 1 << 2
FLAG_SCRIPT = 1 << 3
FLAG_NONSYMBOLIC = 1 << 5
FLAG_ITALIC = 1 << 6

SERIF_FAMILY_CLASSES = {1, 2, 3, 4, 5, 7}
SCRIPT_FAMILY_CLASS = 10
PANOSE_LATIN_TEXT = 2
PANOSE_LATIN_SCRIPT = 3
PANOSE_SERIF_STYLES = range(2, 11)


GLYPHS = {".notdef": (0, 0, 0, 0), "H": (50, 0, 550, 700), "x": (50, 0, 450, 500), "A": (50, -150, 550, 650)}
READ_CMAP_FORMATS = (0, 4, 12)
SYMBOL_OFFSET = 0xF000
FS_TYPE_RESTRICTED_NO_SUBSETTING_BITMAP_ONLY = 0x0302
FS_TYPE_PREVIEW_AND_EDITABLE = 0x000C
FS_TYPE_PREVIEW_NO_SUBSETTING = 0x0104


def clamp_int16(value):
    return max(-32768, min(32767, value))


def glyph_name_of(font, character):
    """The glyph that the cmap gives a character: the first of the Windows, Unicode and Macintosh subtables, in that
    order, of a format that the library reads (0, 4 and 12), that maps it to a glyph other than .notdef."""
    order = {3: 0, 0: 1, 1: 2}
    tables = [table for table in font["cmap"].tables if table.format in READ_CMAP_FORMATS and table.platformID in order]
    for table in sorted(tables, key=lambda table: order[table.platformID]):
        name = table.cmap.get(ord(character))
        if name is not None and name != ".notdef":
            return name
    return None


def glyph_top(font, character):
    glyph_name = glyph_name_of(font, character)
    if glyph_name is None:
        return None
    if "glyf" in font:
        glyph = font["glyf"][glyph_name]
        top = glyph.yMax if glyph.numberOfContours != 0 else 0
    else:
        glyph_set = font.getGlyphSet()
        pen = BoundsPen(glyph_set)
        glyph_set[glyph_name].draw(pen)
        top = int(round(pen.bounds[3])) if pen.bounds is not None else 0
    return top if top > 0 else None


def vertical_metrics(font):
    head = font["head"]
    hhea = font["hhea"]
    os2 = font.get("OS/2")

    if os2 is not None and os2.fsSelection & FS_SELECTION_USE_TYPO_METRICS:
        ascent, descent = os2.sTypoAscender, os2.sTypoDescender
        sources = ("Os2TypoAscender", "Os2TypoDescender")
    else:
        ascent, descent = hhea.ascent, hhea.descent
        sources = ("HheaAscender", "HheaDescender")

    if os2 is not None and (ascent == 0 or descent == 0):
        if os2.sTypoAscender != 0 or os2.sTypoDescender != 0:
            ascent, descent = os2.sTypoAscender, os2.sTypoDescender
            sources = ("Os2TypoAscender", "Os2TypoDescender")
        else:
            ascent, descent = clamp_int16(os2.usWinAscent), clamp_int16(-os2.usWinDescent)
            sources = ("Os2WinAscent", "Os2WinDescent")

    if ascent == 0 and descent == 0:
        ascent, descent = head.yMax, head.yMin
        sources = ("HeadYMax", "HeadYMin")

    return ascent, descent, sources


def design_height(font, os2, field, character, source, glyph_source, fallback):
    if os2 is not None and os2.version >= 2 and getattr(os2, field) != 0:
        return getattr(os2, field), source
    top = glyph_top(font, character)
    if top is not None:
        return top, glyph_source
    return fallback


def has_symbol_cmap(font):
    return any(table.platformID == 3 and table.platEncID == 0 for table in font["cmap"].tables)


def is_serif(os2):
    if os2 is None:
        return False
    family_class = os2.sFamilyClass >> 8
    if family_class != 0:
        return family_class in SERIF_FAMILY_CLASSES
    panose = os2.panose
    return panose.bFamilyType == PANOSE_LATIN_TEXT and panose.bSerifStyle in PANOSE_SERIF_STYLES


def is_script(os2):
    if os2 is None:
        return False
    family_class = os2.sFamilyClass >> 8
    if family_class != 0:
        return family_class == SCRIPT_FAMILY_CLASS
    return os2.panose.bFamilyType == PANOSE_LATIN_SCRIPT


def is_italic(font, os2):
    return (
        (os2 is not None and os2.fsSelection & (FS_SELECTION_ITALIC | FS_SELECTION_OBLIQUE) != 0)
        or font["head"].macStyle & MAC_STYLE_ITALIC != 0
        or font["post"].italicAngle != 0
    )


def usage(fs_type):
    if fs_type & 0x0008:
        return "Editable"
    if fs_type & 0x0004:
        return "PreviewAndPrint"
    if fs_type & 0x0002:
        return "Restricted"
    return "Installable"


def describe(font):
    head = font["head"]
    os2 = font.get("OS/2")
    ascent, descent, (ascent_source, descent_source) = vertical_metrics(font)

    cap_height, cap_source = design_height(
        font, os2, "sCapHeight", "H", "Os2CapHeight", "CapitalHBounds", (ascent, "FallbackAscent"))
    x_height, x_source = design_height(
        font, os2, "sxHeight", "x", "Os2XHeight", "SmallXBounds", (0, "None"))

    flags = 0
    if font["post"].isFixedPitch != 0:
        flags |= FLAG_FIXED_PITCH
    if is_serif(os2):
        flags |= FLAG_SERIF
    flags |= FLAG_SYMBOLIC if has_symbol_cmap(font) else FLAG_NONSYMBOLIC
    if is_script(os2):
        flags |= FLAG_SCRIPT
    if is_italic(font, os2):
        flags |= FLAG_ITALIC

    fs_type = os2.fsType if os2 is not None else 0
    return {
        "unitsPerEm": head.unitsPerEm,
        "ascent": ascent,
        "ascentSource": ascent_source,
        "descent": descent,
        "descentSource": descent_source,
        "capHeight": cap_height,
        "capHeightSource": cap_source,
        "xHeight": x_height,
        "xHeightSource": x_source,
        "italicAngle": font["post"].italicAngle,
        "italicAngleSource": "PostItalicAngle",
        "boundingBox": [head.xMin, head.yMin, head.xMax, head.yMax],
        "boundingBoxSource": "HeadBounds",
        "flags": flags,
        "embedding": {
            "isStated": os2 is not None,
            "fsType": fs_type,
            "usage": usage(fs_type),
            "noSubsetting": fs_type & 0x0100 != 0,
            "bitmapEmbeddingOnly": fs_type & 0x0200 != 0,
        },
    }


def panose(family_kind, serif_style):
    result = Panose()
    result.bFamilyType = family_kind
    result.bSerifStyle = serif_style
    return result


def build_synthetic(file_name, hhea=(800, -200), os2=None, symbol=False, fixed_pitch=False, italic_angle=0):
    """Saves a TrueType font of four boxes. `os2` is the arguments of setupOS2, or None for a font with no OS/2 table."""
    builder = FontBuilder(1000, isTTF=True)
    builder.setupGlyphOrder(list(GLYPHS))
    code_points = {0x48: "H", 0x78: "x", 0x41: "A"}
    builder.setupCharacterMap({(code + SYMBOL_OFFSET if symbol else code): name for code, name in code_points.items()})
    glyphs = {}
    for name, (x_min, y_min, x_max, y_max) in GLYPHS.items():
        pen = TTGlyphPen(None)
        if name != ".notdef":
            pen.moveTo((x_min, y_min))
            pen.lineTo((x_min, y_max))
            pen.lineTo((x_max, y_max))
            pen.lineTo((x_max, y_min))
            pen.closePath()
        glyphs[name] = pen.glyph()
    builder.setupGlyf(glyphs)
    builder.setupHorizontalMetrics({name: (600, 50) for name in GLYPHS})
    builder.setupHorizontalHeader(ascent=hhea[0], descent=hhea[1])
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    if os2 is not None:
        builder.setupOS2(**os2)
    builder.setupPost(keepGlyphNames=False, isFixedPitch=int(fixed_pitch), italicAngle=italic_angle)
    if symbol:
        cmap = builder.font["cmap"]
        cmap.tables = [table for table in cmap.tables if (table.platformID, table.platEncID) == (3, 1)]
        cmap.tables[0].platEncID = 0
    builder.font.save(os.path.join(SYNTHETIC, file_name))


def build_synthetic_fonts():
    os.makedirs(SYNTHETIC, exist_ok=True)
    # hhea and OS/2 typo metrics are 0, so the ascent and descent come from the win metrics. fsType: restricted, no subsetting, bitmap only.
    build_synthetic(
        "Win.ttf",
        hhea=(0, 0),
        os2=dict(version=4, sTypoAscender=0, sTypoDescender=0, usWinAscent=900, usWinDescent=300,
                 fsType=FS_TYPE_RESTRICTED_NO_SUBSETTING_BITMAP_ONLY),
    )
    # No OS/2 table and hhea 0: the ascent and descent come from head, and the cap height and x-height from the glyphs.
    build_synthetic("HeadFallback.ttf", hhea=(0, 0))
    # A (3, 0) symbol cmap, so U+0048 and U+0078 have no glyph and the cap height falls back to the ascent.
    build_synthetic(
        "SymbolScript.ttf",
        os2=dict(version=3, sTypoAscender=750, sTypoDescender=-250, usWinAscent=800, usWinDescent=200, sCapHeight=700,
                 sFamilyClass=10 << 8, fsSelection=1, fsType=FS_TYPE_PREVIEW_AND_EDITABLE),
        symbol=True,
        fixed_pitch=True,
        italic_angle=-12.5,
    )
    # USE_TYPO_METRICS, a serif family class and an OS/2 version 1 table, which has no cap height or x-height.
    build_synthetic(
        "TypoSerifClass.ttf",
        os2=dict(version=1, sTypoAscender=750, sTypoDescender=-250, usWinAscent=800, usWinDescent=200,
                 sFamilyClass=3 << 8, fsSelection=1 << 7, fsType=FS_TYPE_PREVIEW_NO_SUBSETTING),
    )
    # No family class, so the PANOSE classification decides.
    build_synthetic(
        "PanoseSerif.ttf",
        os2=dict(version=4, sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200,
                 sFamilyClass=0, panose=panose(2, 3), sxHeight=480, sCapHeight=690),
    )
    build_synthetic(
        "PanoseScript.ttf",
        os2=dict(version=4, sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200,
                 sFamilyClass=0, panose=panose(3, 0), fsSelection=1 << 9),
    )


def has_outlines(font):
    return any(tag in font for tag in ("glyf", "CFF ", "CFF2"))


def faces_of(path):
    if path.lower().endswith(".ttc"):
        yield from enumerate(TTCollection(path, lazy=True).fonts)
    else:
        yield 0, TTFont(path, lazy=True)


def main():
    build_synthetic_fonts()
    faces = {}
    for folder, _, names in os.walk(FONTS):
        for name in sorted(names):
            if not name.lower().endswith(EXTENSIONS):
                continue
            path = os.path.join(folder, name)
            key = os.path.relpath(path, FONTS).replace(os.sep, "/")
            try:
                for index, font in faces_of(path):
                    if all(tag in font for tag in ("head", "hhea", "post", "cmap")) and has_outlines(font):
                        faces[f"{key}#{index}"] = describe(font)
            except Exception:
                continue

    reference = {"fontTools": fontTools.version, "faces": dict(sorted(faces.items()))}
    with open(REFERENCE, "w", encoding="utf-8", newline="\n") as file:
        json.dump(reference, file, indent=1)
        file.write("\n")
    print(f"{len(faces)} faces -> {os.path.relpath(REFERENCE, ROOT)}")


if __name__ == "__main__":
    main()
