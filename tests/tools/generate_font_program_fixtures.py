#!/usr/bin/env python3
# Copyright (c) PinataLabs.
# Licensed under the Apache License, Version 2.0.
"""Generates the fontTools reference data for the FontProgram tests.

Usage (from the repository root, fontTools 4.68 or later):

    python tests/tools/generate_font_program_fixtures.py

The script writes into tests/PinataLabs.Typography.Tests/Fonts/FontPrograms:

* small synthetic fonts for features that no shipped test font has, and
* one JSON fixture per font, for the synthetic fonts and for some shipped fonts.

A fixture holds what fontTools reads from the font: glyph names, advances, the built-in
encoding, the FontMatrix, and each glyph outline in glyph space. Where fontTools cannot read
a feature (encoding supplements), the expected value is the one the script built into the font.
The output is deterministic, so a second run changes no file.
"""

import json
import os
import struct
import sys

os.environ["SOURCE_DATE_EPOCH"] = "0"

from fontTools import cffLib
from fontTools.encodings.StandardEncoding import StandardEncoding
from fontTools.fontBuilder import FontBuilder
from fontTools.misc.psCharStrings import T2CharString
from fontTools.pens.basePen import BasePen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.ttLib.tables import _g_l_y_f as glyf_module
from fontTools.ttLib.tables._c_m_a_p import CmapSubtable
from fontTools.ttLib.tables.DefaultTable import DefaultTable

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
FONTS = os.path.join(ROOT, "tests", "PinataLabs.Typography.Tests", "Fonts")
OUT = os.path.join(FONTS, "FontPrograms")

# Shipped fonts that get a fixture. The path is relative to the Fonts directory.
SHIPPED = ["SourceSansPro-Regular.otf", "FDArrayTest257.otf", "OpenSans-Regular.ttf", "SixLaborsSampleAB.ttf"]

# Expert Encoding, from Adobe Technical Note #5176, Appendix B: code -> glyph name.
EXPERT_ENCODING = {
    32: "space", 33: "exclamsmall", 34: "Hungarumlautsmall", 36: "dollaroldstyle", 37: "dollarsuperior",
    38: "ampersandsmall", 39: "Acutesmall", 40: "parenleftsuperior", 41: "parenrightsuperior",
    42: "twodotenleader", 43: "onedotenleader", 44: "comma", 45: "hyphen", 46: "period", 47: "fraction",
    48: "zerooldstyle", 49: "oneoldstyle", 50: "twooldstyle", 51: "threeoldstyle", 52: "fouroldstyle",
    53: "fiveoldstyle", 54: "sixoldstyle", 55: "sevenoldstyle", 56: "eightoldstyle", 57: "nineoldstyle",
    58: "colon", 59: "semicolon", 60: "commasuperior", 61: "threequartersemdash", 62: "periodsuperior",
    63: "questionsmall", 65: "asuperior", 66: "bsuperior", 67: "centsuperior", 68: "dsuperior",
    69: "esuperior", 73: "isuperior", 76: "lsuperior", 77: "msuperior", 78: "nsuperior", 79: "osuperior",
    82: "rsuperior", 83: "ssuperior", 84: "tsuperior", 86: "ff", 87: "fi", 88: "fl", 89: "ffi", 90: "ffl",
    91: "parenleftinferior", 93: "parenrightinferior", 94: "Circumflexsmall", 95: "hyphensuperior",
    96: "Gravesmall", 97: "Asmall", 98: "Bsmall", 99: "Csmall", 100: "Dsmall", 101: "Esmall", 102: "Fsmall",
    103: "Gsmall", 104: "Hsmall", 105: "Ismall", 106: "Jsmall", 107: "Ksmall", 108: "Lsmall", 109: "Msmall",
    110: "Nsmall", 111: "Osmall", 112: "Psmall", 113: "Qsmall", 114: "Rsmall", 115: "Ssmall", 116: "Tsmall",
    117: "Usmall", 118: "Vsmall", 119: "Wsmall", 120: "Xsmall", 121: "Ysmall", 122: "Zsmall",
    123: "colonmonetary", 124: "onefitted", 125: "rupiah", 126: "Tildesmall", 161: "exclamdownsmall",
    162: "centoldstyle", 163: "Lslashsmall", 166: "Scaronsmall", 167: "Zcaronsmall", 168: "Dieresissmall",
    169: "Brevesmall", 170: "Caronsmall", 172: "Dotaccentsmall", 175: "Macronsmall", 178: "figuredash",
    179: "hypheninferior", 182: "Ogoneksmall", 183: "Ringsmall", 184: "Cedillasmall", 188: "onequarter",
    189: "onehalf", 190: "threequarters", 191: "questiondownsmall", 192: "oneeighth", 193: "threeeighths",
    194: "fiveeighths", 195: "seveneighths", 196: "onethird", 197: "twothirds", 200: "zerosuperior",
    201: "onesuperior", 202: "twosuperior", 203: "threesuperior", 204: "foursuperior", 205: "fivesuperior",
    206: "sixsuperior", 207: "sevensuperior", 208: "eightsuperior", 209: "ninesuperior", 210: "zeroinferior",
    211: "oneinferior", 212: "twoinferior", 213: "threeinferior", 214: "fourinferior", 215: "fiveinferior",
    216: "sixinferior", 217: "seveninferior", 218: "eightinferior", 219: "nineinferior",
    220: "centinferior", 221: "dollarinferior", 222: "periodinferior", 223: "commainferior",
    224: "Agravesmall", 225: "Aacutesmall", 226: "Acircumflexsmall", 227: "Atildesmall",
    228: "Adieresissmall", 229: "Aringsmall", 230: "AEsmall", 231: "Ccedillasmall", 232: "Egravesmall",
    233: "Eacutesmall", 234: "Ecircumflexsmall", 235: "Edieresissmall", 236: "Igravesmall",
    237: "Iacutesmall", 238: "Icircumflexsmall", 239: "Idieresissmall", 240: "Ethsmall", 241: "Ntildesmall",
    242: "Ogravesmall", 243: "Oacutesmall", 244: "Ocircumflexsmall", 245: "Otildesmall",
    246: "Odieresissmall", 247: "OEsmall", 248: "Oslashsmall", 249: "Ugravesmall", 250: "Uacutesmall",
    251: "Ucircumflexsmall", 252: "Udieresissmall", 253: "Yacutesmall", 254: "Thornsmall",
    255: "Ydieresissmall",
}


class OutlinePen(BasePen):
    """Records an outline as the commands of IGlyphOutlineSink."""

    def __init__(self, glyph_set=None):
        super().__init__(glyph_set)
        self.commands = []

    def _moveTo(self, pt):
        self.commands.append(("M", pt))

    def _lineTo(self, pt):
        self.commands.append(("L", pt))

    def _curveToOne(self, pt1, pt2, pt3):
        self.commands.append(("C", pt1, pt2, pt3))

    def _qCurveToOne(self, pt1, pt2):
        self.commands.append(("Q", pt1, pt2))

    def _closePath(self):
        self.commands.append(("Z",))

    def _endPath(self):
        self.commands.append(("Z",))


def number(value):
    value = round(float(value), 4)
    return str(int(value)) if value == int(value) else repr(value)


def outline_text(commands):
    parts = []
    for command in commands:
        parts.append(command[0])
        for point in command[1:]:
            parts.append(number(point[0]))
            parts.append(number(point[1]))
    return " ".join(parts)


def truetype_outline(font, name):
    """Flattens composites with Glyph.getCoordinates, which applies component transforms,
    scaled offsets and point matching, and then draws the points as one simple glyph."""
    table = font["glyf"]
    coordinates, end_points, flags = table[name].getCoordinates(table)
    flat = glyf_module.Glyph()
    flat.numberOfContours = len(end_points)
    flat.coordinates = coordinates
    flat.endPtsOfContours = end_points
    flat.flags = flags
    pen = OutlinePen()
    if end_points:
        flat.draw(pen, table)
    return pen.commands


def concatenate(inner, outer):
    """The matrix that applies `inner` and then `outer`, both in [a b c d e f] form."""
    a1, b1, c1, d1, e1, f1 = inner
    a2, b2, c2, d2, e2, f2 = outer
    return [
        a1 * a2 + b1 * c2, a1 * b2 + b1 * d2,
        c1 * a2 + d1 * c2, c1 * b2 + d1 * d2,
        e1 * a2 + f1 * c2 + e2, e1 * b2 + f1 * d2 + f2,
    ]


def builtin_encoding(names_by_code, order):
    gid = {}
    for index, name in enumerate(order):
        gid.setdefault(name, index)
    return [gid.get(names_by_code.get(code, ".notdef"), 0) for code in range(256)]


def truetype_encoding(font):
    """The (3,0) subtable by low byte, else the (1,0) subtable."""
    cmap = font["cmap"]
    order = font.getGlyphOrder()
    symbol = cmap.getcmap(3, 0)
    if symbol is not None:
        result = []
        for code in range(256):
            name = None
            for base in (0x0000, 0xF000, 0xF100, 0xF200):
                name = symbol.cmap.get(base + code)
                if name is not None:
                    break
            result.append(order.index(name) if name is not None else 0)
        return result
    mac = cmap.getcmap(1, 0)
    if mac is not None:
        return [order.index(mac.cmap[code]) if code in mac.cmap else 0 for code in range(256)]
    return None


def truetype_names(font):
    post = font["post"]
    order = font.getGlyphOrder()
    if post.formatType not in (1.0, 2.0):
        return [None] * len(order)
    mapping = getattr(post, "mapping", {})
    return [mapping.get(name, name) for name in order]


def cff_fixture(font, encoding_override):
    top = font["CFF "].cff.topDictIndex[0]
    order = font.getGlyphOrder()
    is_cid = hasattr(top, "ROS")
    has_top_matrix = "FontMatrix" in top.rawDict
    font_matrix = list(top.FontMatrix)
    char_strings = top.CharStrings
    hmtx = font["hmtx"] if "hmtx" in font else None

    if is_cid:
        encoding = None
    elif encoding_override is not None:
        encoding = encoding_override
    elif top.Encoding == "StandardEncoding":
        encoding = builtin_encoding(dict(enumerate(StandardEncoding)), order)
    elif top.Encoding == "ExpertEncoding":
        encoding = builtin_encoding(EXPERT_ENCODING, order)
    else:
        encoding = builtin_encoding(dict(enumerate(top.Encoding)), order)

    glyphs = []
    for index, name in enumerate(order):
        # The charstrings resolve the components of a seac endchar.
        pen = OutlinePen(char_strings)
        char_string, fd_index = char_strings.getItemAndSelector(name)
        char_string.draw(pen)
        width = char_string.width
        glyph = {"name": None if is_cid else name}
        if is_cid:
            glyph["cid"] = int(name[3:]) if index else 0
            font_dict = top.FDArray[fd_index]
            if "FontMatrix" in font_dict.rawDict:
                # A Font DICT matrix applies first. The Top DICT matrix follows when the font has one.
                matrix = list(font_dict.FontMatrix)
                if has_top_matrix:
                    matrix = concatenate(matrix, font_matrix)
                if matrix != font_matrix:
                    glyph["matrix"] = matrix
        if hmtx is not None:
            glyph["advance"] = hmtx[name][0]
        elif "matrix" in glyph:
            # FontProgram reports every width in the units of its one FontMatrix.
            glyph["advance"] = round(width * glyph["matrix"][0] / font_matrix[0], 6)
        else:
            glyph["advance"] = width
        glyph["outline"] = outline_text(pen.commands)
        glyphs.append(glyph)

    return {
        "format": "Cff",
        "isCidKeyed": is_cid,
        "fontMatrix": font_matrix,
        "encoding": encoding,
        "glyphs": glyphs,
    }


def truetype_fixture(font):
    order = font.getGlyphOrder()
    names = truetype_names(font)
    hmtx = font["hmtx"] if "hmtx" in font else None
    scale = 1.0 / font["head"].unitsPerEm
    glyphs = []
    for index, name in enumerate(order):
        glyphs.append({
            "name": names[index],
            "advance": hmtx[name][0] if hmtx is not None else None,
            "outline": outline_text(truetype_outline(font, name)),
        })
    return {
        "format": "TrueType",
        "isCidKeyed": False,
        "fontMatrix": [scale, 0, 0, scale, 0, 0],
        "encoding": truetype_encoding(font),
        "glyphs": glyphs,
    }


def write_fixture(font_path, encoding_override=None):
    font = TTFont(font_path)
    fixture = truetype_fixture(font) if "glyf" in font else cff_fixture(font, encoding_override)
    name = os.path.splitext(os.path.basename(font_path))[0]
    relative = os.path.relpath(font_path, OUT).replace(os.sep, "/")
    lines = ["{"]
    lines.append('  "font": %s,' % json.dumps(relative))
    for key in ("format", "isCidKeyed", "fontMatrix", "encoding"):
        lines.append("  %s: %s," % (json.dumps(key), json.dumps(fixture[key], separators=(",", ":"))))
    lines.append('  "glyphs": [')
    glyphs = fixture["glyphs"]
    for index, glyph in enumerate(glyphs):
        comma = "," if index + 1 < len(glyphs) else ""
        lines.append("    " + json.dumps(glyph, separators=(",", ":")) + comma)
    lines.append("  ]")
    lines.append("}")
    with open(os.path.join(OUT, name + ".json"), "w", newline="\n", encoding="utf-8") as stream:
        stream.write("\n".join(lines) + "\n")
    return len(glyphs)


# ---------------------------------------------------------------------------------------------
# Synthetic CFF fonts
# ---------------------------------------------------------------------------------------------

def box(x, y, width, height):
    return [x, y, "rmoveto", width, "hlineto", height, "vlineto", -width, "hlineto"]


def char_string(program):
    return T2CharString(program=list(program))


def build_cff(file_name, glyphs, private=None, font_info=None, tables=True, customize=None):
    """glyphs: list of (name, program). Returns the path of the saved font."""
    order = [name for name, _ in glyphs]
    builder = FontBuilder(unitsPerEm=1000, isTTF=False)
    builder.setupGlyphOrder(order)
    builder.setupCharacterMap({0xE000 + index: name for index, name in enumerate(order) if index})
    builder.setupCFF(
        os.path.splitext(file_name)[0],
        font_info or {},
        {name: char_string(program) for name, program in glyphs},
        private or {},
    )
    builder.setupHorizontalMetrics({name: (500 + 10 * index, 0) for index, name in enumerate(order)})
    builder.setupHorizontalHeader(ascent=800, descent=-200)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2()
    builder.setupPost()
    font = builder.font
    if customize is not None:
        customize(font)
    if not tables:
        del font["hmtx"]
        del font["hhea"]
    path = os.path.join(OUT, file_name)
    font.save(path)
    return path


def predefined_charset_font(file_name, names, encoding, expected_charset):
    def customize(font):
        font["CFF "].cff.topDictIndex[0].Encoding = encoding

    path = build_cff(file_name, [(name, box(10 * index, 0, 100, 100 + index)) for index, name in enumerate(names)],
                     customize=customize)
    raw = TTFont(path)["CFF "].cff.topDictIndex[0].rawDict
    assert raw.get("charset", 0) == expected_charset, (file_name, raw.get("charset"))
    assert raw.get("Encoding", 0) == (1 if encoding == "ExpertEncoding" else 0), file_name
    return path


def custom_encoding_font(file_name, names, codes, supplements, expected_format):
    """codes: glyph name -> code. supplements: list of (code, glyph name), appended as Table 14 data,
    which fontTools neither writes nor reads."""
    encoding = [".notdef"] * 256
    for name, code in codes.items():
        encoding[code] = name

    original = cffLib.EncodingCompiler.__init__

    def with_supplement(self, strings, encoding_list, parent):
        original(self, strings, encoding_list, parent)
        assert self.data[0] == expected_format, (file_name, self.data[0])
        extra = bytes([len(supplements)])
        for code, name in supplements:
            extra += struct.pack(">BH", code, parent.strings.getSID(name))
        self.data = bytes([self.data[0] | 0x80]) + self.data[1:] + extra

    def customize(font):
        font["CFF "].cff.topDictIndex[0].Encoding = encoding

    cffLib.EncodingCompiler.__init__ = with_supplement
    try:
        path = build_cff(file_name, [(name, box(0, 0, 100 + index, 200)) for index, name in enumerate(names)],
                         customize=customize)
    finally:
        cffLib.EncodingCompiler.__init__ = original

    expected = dict(codes)
    by_code = {code: name for name, code in expected.items()}
    for code, name in supplements:
        by_code[code] = name
    return path, builtin_encoding(by_code, names)


def type2_font(file_name, tables=True):
    def customize(font):
        font_set = font["CFF "].cff
        top = font_set.topDictIndex[0]
        private = top.Private
        private.Subrs = cffLib.SubrsIndex()
        # Local subr 0 draws a line and calls global subr 0. Local subr 1 ends the glyph.
        local_programs = [[30, 40, "rlineto", -107, "callgsubr", "return"], [-50, "hlineto", "endchar"]]
        global_programs = [[20, -10, 5, 15, 25, 35, "rrcurveto", "return"]]
        for program in local_programs:
            subr = char_string(program)
            subr.private = private
            subr.globalSubrs = font_set.GlobalSubrs
            private.Subrs.append(subr)
        for program in global_programs:
            subr = char_string(program)
            subr.private = private
            subr.globalSubrs = font_set.GlobalSubrs
            font_set.GlobalSubrs.append(subr)

    glyphs = [
        (".notdef", ["endchar"]),
        ("flex", [50, 100, "rmoveto", 10.5, 20, 30, 40.25, 50, -10, 20, -30, 40, 10, 5, -5.5, 50, "flex",
                  -100, "vlineto", "endchar"]),
        ("hflex", [10, 20, "rmoveto", 30, 40, 50, 60, 70, 80, 90, "hflex", -200, "vlineto", "endchar"]),
        ("hflex1", [10, 20, "rmoveto", 30, 5, 40, 50, 60, 70, 80, -50, 90, "hflex1", -200, "vlineto",
                    "endchar"]),
        ("flex1h", [0, 0, "rmoveto", 100, 10, 100, 20, 100, -30, 100, 5, 100, 15, 60, "flex1", -90,
                    "vlineto", "endchar"]),
        ("flex1v", [0, 0, "rmoveto", 10, 100, 20, 100, -30, 100, 5, 100, 15, 100, 60, "flex1", -90,
                    "hlineto", "endchar"]),
        ("fixed", [1.75, -2.5, "rmoveto", 100.5, 0.25, "rlineto", -0.75, 200.125, "rlineto", -99.5, -3,
                   "rlineto", "endchar"]),
        ("curves", [100, 100, "rmoveto",
                    10, 20, 30, 40, 50, 60, 5, 5, "rcurveline",
                    10, 0, 0, 10, 15, 25, 35, 45, 55, 65, "rlinecurve",
                    7, 10, 20, 30, 40, 50, 60, 70, 80, "vvcurveto",
                    -7, 10, 20, 30, 40, 50, 60, 70, 80, "hhcurveto",
                    10, 20, 30, 40, 50, 60, 70, 80, 15, "vhcurveto",
                    10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, -15, "hvcurveto",
                    -20, 30, -40, "hlineto", 20, -30, 40, "vlineto",
                    "endchar"]),
        ("hints", [-120, 10, 20, "hstemhm", 30, 40, 50, 60, "hintmask", bytes([0b11100000]),
                   100, 200, "rmoveto", "cntrmask", bytes([0b10100000]), 50, 60, "rlineto",
                   -50, "hlineto", "endchar"]),
        ("subrs", [10, 10, "rmoveto", -107, "callsubr", 60, "vlineto", -106, "callsubr"]),
        ("nomove", [100, 0, "rlineto", 0, 100, "rlineto", "endchar"]),
        ("two", box(0, 0, 100, 100) + box(200, -100, 50, 50) + ["endchar"]),
        ("width-hmoveto", [130, 25, "hmoveto", 100, 100, "rlineto", "endchar"]),
        ("width-vmoveto", [-40, 25, "vmoveto", 100, 100, "rlineto", "endchar"]),
        ("width-endchar", [77, "endchar"]),
    ]
    return build_cff(file_name, glyphs, private={"defaultWidthX": 480, "nominalWidthX": 600},
                     tables=tables, customize=customize)


def seac_font(file_name, tables=True):
    glyphs = [
        (".notdef", ["endchar"]),
        ("A", [0, 0, "rmoveto", 300, 700, "rlineto", 300, -700, "rlineto", "endchar"]),
        ("acute", [100, 500, "rmoveto", 80, 120, "rlineto", 40, "hlineto", "endchar"]),
        # adx ady bchar achar endchar: 65 is A and 194 is acute in the Standard Encoding.
        ("Aacute", [150, 230, 65, 194, "endchar"]),
        ("Aacute.width", [-45, -20.5, 300, 65, 194, "endchar"]),
    ]
    return build_cff(file_name, glyphs, private={"defaultWidthX": 480, "nominalWidthX": 600}, tables=tables)


def cid_font(file_name, select_format, top_matrix=None):
    # (FontMatrix or None, defaultWidthX, nominalWidthX)
    font_dicts = [
        (None, 500, 0),
        ([0.002, 0, 0, 0.001, 0, 0], 700, 300),
        ([0.0005, 0, 0.0001, 0.0005, 0.01, -0.02], 250, -100),
    ]
    # (CID, Font DICT index, program)
    cid_glyphs = [
        (0, 0, ["endchar"]),
        (5, 0, box(0, 0, 400, 600) + ["endchar"]),
        (3, 1, [40, 10, 20, "rmoveto", 100, 200, "rlineto", -107, "callsubr", "endchar"]),
        (700, 1, [10, 20, "rmoveto", -107, "callsubr", 33, "hlineto", "endchar"]),
        (2, 2, [10, 100, 200, "rmoveto", 300, 0, 0, 300, -300, 0, "rrcurveto", -107, "callsubr", "endchar"]),
        (40000, 2, ["endchar"]),
        (41, 0, [-20, 5, 5, "rmoveto", 10, 20, 30, 40, 50, 60, "rrcurveto", "endchar"]),
    ]
    # Each Font DICT has its own local subr 0, so the wrong Font DICT gives a different outline.
    local_subrs = [[11, 0, "rlineto", "return"], [0, 22, "rlineto", "return"], [33, 33, "rlineto", "return"]]

    def customize(font):
        font_set = font["CFF "].cff
        top = font_set.topDictIndex[0]
        names = [".notdef"] + ["cid%05d" % cid for cid, _, _ in cid_glyphs[1:]]
        top.ROS = ("Adobe", "Identity", 0)
        top.CIDCount = 65535
        fd_array = cffLib.FDArrayIndex()
        fd_array.strings = None
        fd_array.GlobalSubrs = font_set.GlobalSubrs
        privates = []
        for index, (matrix, default_width, nominal_width) in enumerate(font_dicts):
            font_dict = cffLib.FontDict()
            font_dict.setCFF2(False)
            font_dict.FontName = "FD%d" % index
            if matrix is not None:
                font_dict.FontMatrix = matrix
                font_dict.rawDict["FontMatrix"] = matrix
            private = cffLib.PrivateDict()
            private.defaultWidthX = default_width
            private.nominalWidthX = nominal_width
            private.Subrs = cffLib.SubrsIndex()
            subr = char_string(local_subrs[index])
            subr.private = private
            subr.globalSubrs = font_set.GlobalSubrs
            private.Subrs.append(subr)
            font_dict.Private = private
            fd_array.append(font_dict)
            privates.append(private)
        top.FDArray = fd_array
        select = cffLib.FDSelect()
        select.format = select_format
        select.gidArray = [fd for _, fd, _ in cid_glyphs]
        top.FDSelect = select
        del top.Private
        if "Encoding" in top.rawDict:
            del top.rawDict["Encoding"]

        old_order = font.getGlyphOrder()
        strings = top.CharStrings
        programs = {}
        for old_name, new_name, (_, fd, _) in zip(old_order, names, cid_glyphs):
            item = strings.charStrings[old_name]
            item.private = privates[fd]
            item.fdSelectIndex = fd
            programs[new_name] = item
        strings.charStrings = programs
        strings.fdSelect = select
        strings.fdArray = fd_array
        top.charset = names
        font.setGlyphOrder(names)
        for table in ("hmtx",):
            font[table].metrics = {new: font[table].metrics[old] for old, new in zip(old_order, names)}
        for subtable in font["cmap"].tables:
            subtable.cmap = {code: names[old_order.index(name)] for code, name in subtable.cmap.items()}

    glyphs = [("g%d" % index if index else ".notdef", program) for index, (_, _, program) in enumerate(cid_glyphs)]
    font_info = {"FontMatrix": top_matrix} if top_matrix is not None else None
    path = build_cff(file_name, glyphs, font_info=font_info, tables=False, customize=customize)
    check = TTFont(path)["CFF "].cff.topDictIndex[0]
    assert check.FDSelect.format == select_format, (file_name, check.FDSelect.format)
    return path


# ---------------------------------------------------------------------------------------------
# Synthetic TrueType fonts
# ---------------------------------------------------------------------------------------------

def simple_glyph(contours):
    """contours: list of lists of (x, y, on_curve)."""
    glyph = glyf_module.Glyph()
    glyph.numberOfContours = len(contours)
    coordinates = []
    flags = bytearray()
    end_points = []
    for contour in contours:
        for x, y, on_curve in contour:
            coordinates.append((x, y))
            flags.append(1 if on_curve else 0)
        end_points.append(len(coordinates) - 1)
    glyph.coordinates = glyf_module.GlyphCoordinates(coordinates)
    glyph.flags = flags
    glyph.endPtsOfContours = end_points
    glyph.program = glyf_module.ttProgram.Program()
    glyph.program.fromBytecode(b"")
    return glyph


def component(name, x=0, y=0, transform=None, flags=0, points=None):
    item = glyf_module.GlyphComponent()
    item.glyphName = name
    item.flags = flags
    if points is not None:
        item.firstPt, item.secondPt = points
    else:
        item.x, item.y = x, y
    if transform is not None:
        item.transform = transform
    return item


def composite_glyph(components):
    glyph = glyf_module.Glyph()
    glyph.numberOfContours = -1
    glyph.components = components
    return glyph


def build_truetype(file_name, glyphs, cmap_tables, post=None):
    order = [name for name, _ in glyphs]
    builder = FontBuilder(unitsPerEm=2048, isTTF=True)
    builder.setupGlyphOrder(order)
    builder.setupCharacterMap({})
    builder.setupGlyf({name: glyph for name, glyph in glyphs})
    builder.setupHorizontalMetrics({name: (600 + 25 * index, 0) for index, name in enumerate(order)})
    builder.setupHorizontalHeader(ascent=1600, descent=-400)
    builder.setupNameTable({"familyName": os.path.splitext(file_name)[0], "styleName": "Regular"})
    builder.setupOS2()
    builder.setupPost()
    font = builder.font
    tables = []
    for platform, encoding, table_format, mapping in cmap_tables:
        subtable = CmapSubtable.newSubtable(table_format)
        subtable.platformID = platform
        subtable.platEncID = encoding
        subtable.language = 0
        subtable.cmap = dict(mapping)
        tables.append(subtable)
    font["cmap"].tables = tables
    if post is not None:
        table = DefaultTable("post")
        table.data = post
        font["post"] = table
    path = os.path.join(OUT, file_name)
    font.save(path)
    return path


SCALED_COMPONENT_OFFSET = 0x0800
UNSCALED_COMPONENT_OFFSET = 0x1000


def composite_font(file_name):
    square = [(0, 0, True), (0, 500, True), (500, 500, True), (500, 0, True)]
    glyphs = [
        (".notdef", simple_glyph([])),
        ("box", simple_glyph([square])),
        # Starts with an off-curve point, has two off-curve points in a row, and ends off-curve.
        ("round", simple_glyph([[(0, 300, False), (100, 600, True), (400, 700, False), (700, 500, False),
                                 (800, 200, True), (600, -100, True), (200, -150, False)]])),
        ("alloff", simple_glyph([[(0, 0, False), (0, 400, False), (400, 400, False), (400, 0, False)]])),
        ("dot", simple_glyph([[(250, 250, True)], square])),
        ("plain", composite_glyph([component("box", 100, 50), component("round", -300, 1200)])),
        ("scaledoffset", composite_glyph([
            component("box", 100, 200, [[0.5, 0], [0, 0.5]], SCALED_COMPONENT_OFFSET),
            component("round", 100, 200, [[0.5, 0], [0, 1.5]], UNSCALED_COMPONENT_OFFSET),
            component("alloff", -64, 32, [[0.75, 0], [0, 0.75]]),
        ])),
        ("twobytwo", composite_glyph([
            component("round", 300, -100, [[0.5, 0.25], [-0.25, 0.75]]),
            component("box", 40, 60, [[0.0, 1.0], [-1.0, 0.0]], SCALED_COMPONENT_OFFSET),
        ])),
        ("nested", composite_glyph([
            component("scaledoffset", 500, 0, [[1.5, 0], [0, 0.5]]),
            component("twobytwo", -200, 300),
            component("dot", 0, 0),
        ])),
        # The second component moves so that its point 1 meets point 2 of the first component.
        ("pointmatch", composite_glyph([
            component("box", 20, 30),
            component("round", transform=[[0.5, 0], [0, 0.5]], points=(2, 1)),
            component("plain", points=(5, 3)),
        ])),
    ]
    mac = {65 + index: name for index, (name, _) in enumerate(glyphs[1:])}
    return build_truetype(file_name, glyphs, [(1, 0, 6, mac)])


def post_format2(glyph_name_indices, pool):
    data = struct.pack(">IIhhIIIII", 0x00020000, 0, -100, 50, 0, 0, 0, 0, 0)
    data += struct.pack(">H", len(glyph_name_indices))
    data += b"".join(struct.pack(">H", index) for index in glyph_name_indices)
    for name in pool:
        encoded = name.encode("ascii")
        data += bytes([len(encoded)]) + encoded
    return data


def names_font(file_name):
    square = [(0, 0, True), (0, 500, True), (500, 500, True), (500, 0, True)]
    order = [".notdef", "g1", "g2", "g3", "g4", "g5", "g6", "g7"]
    glyphs = [(name, simple_glyph([[(x + 10 * index, y, on) for x, y, on in square]] if index else []))
              for index, name in enumerate(order)]
    # Indices below 258 are standard Macintosh names: 0 .notdef, 3 space, 36 A, 37 B.
    # The pool is used out of order, one string is unused, and two glyphs share the name "A".
    pool = ["custom_a", "unused", "custom_b", "A"]
    indices = [0, 36, 258 + 2, 3, 258 + 0, 37, 258 + 3, 36]
    symbol = {0xF041: "g1", 0xF042: "g5", 0xF120: "g3", 0x0043: "g4", 0xF043: "g2", 0xF2FF: "g7"}
    mac = {65: "g6", 66: "g2"}
    return build_truetype(file_name, glyphs, [(1, 0, 0, mac), (3, 0, 4, symbol)], post_format2(indices, pool))


def main():
    os.makedirs(OUT, exist_ok=True)
    expert = list(cffLib.cffIExpertStrings)
    subset = list(cffLib.cffExpertSubsetStrings)
    iso = list(cffLib.cffISOAdobeStrings)

    built = [
        (predefined_charset_font("cff-isoadobe.otf", iso[:40], "StandardEncoding", 0), None),
        (predefined_charset_font("cff-expert.otf", expert, "ExpertEncoding", 1), None),
        (predefined_charset_font("cff-expertsubset.otf", subset, "ExpertEncoding", 2), None),
        custom_encoding_font(
            "cff-encoding-format0.otf",
            [".notdef", "alpha", "beta", "gamma", "delta"],
            {"alpha": 200, "beta": 65, "gamma": 3, "delta": 130},
            [(66, "beta"), (201, "alpha"), (255, "delta")],
            0),
        custom_encoding_font(
            "cff-encoding-format1.otf",
            [".notdef"] + ["n%d" % index for index in range(1, 13)],
            {**{"n%d" % index: 47 + index for index in range(1, 7)},
             **{"n%d" % index: 90 + index for index in range(7, 13)}},
            [(32, "n3")],
            1),
        (type2_font("cff-type2.otf"), None),
        (type2_font("cff-type2-widths.otf", tables=False), None),
        (seac_font("cff-seac.otf"), None),
        (seac_font("cff-seac-widths.otf", tables=False), None),
        (cid_font("cff-cid-fdselect0.otf", 0), None),
        # With a Top DICT matrix, a Font DICT matrix is concatenated with it.
        (cid_font("cff-cid-fdselect3.otf", 3, [0.0005, 0, 0, 0.00025, 0, 0]), None),
        (composite_font("tt-composite.ttf"), None),
        (names_font("tt-names.ttf"), None),
    ]
    built += [(os.path.join(FONTS, name), None) for name in SHIPPED]

    for path, encoding_override in built:
        count = write_fixture(path, encoding_override)
        print("%-40s %5d glyphs" % (os.path.basename(path), count))


if __name__ == "__main__":
    sys.exit(main())
