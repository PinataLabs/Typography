// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.FontPrograms.Type1;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Writes the seed corpus from the test fonts: small TrueType and OpenType CFF fonts, the CFF tables of some
/// of them as bare CFF, and Type 1 fonts, for the <c>program</c> target as files and for the
/// <c>type1-plain</c> target with the private part decrypted. For the <c>item-variation-store</c> target it writes
/// the stores and the advance width map of the two variable test fonts, and a few synthetic stores and maps
/// that have what the fonts lack: long words, several axes, a null subtable and a map of 32-bit entries.
/// For the <c>metrics-variation</c> target it writes the HVAR, VVAR and MVAR tables of the variable test fonts and of the
/// built font that has all three with every mapping. For the <c>tuple-variation-store</c> target it writes a small 'gvar'
/// table of glyphs of Teko-Variable.ttf, one of its glyphs, and synthetic 'gvar' and 'cvar' tables that have what Teko lacks:
/// intermediate regions, shared point numbers, point numbers in words, two axes and 32-bit offsets.
/// For the <c>font-subsetter</c> target it writes the font seeds of the <c>program</c> target, and not the Type 1 fonts.
/// </summary>
internal static class SeedCorpus
{
    // The variable test fonts, and whether each has a CFF2 table.
    private static readonly (string File, bool HasCff2)[] VariableFonts = [("Teko/Teko-Variable.ttf", false), ("SourceSans3/SourceSans3VF-Upright.otf", true)];

    // The tables with a store, and where each table keeps the offset of its store: HVAR and GDEF 1.3 as 32 bits, MVAR as 16 bits.
    private static readonly (string Tag, int Position, bool IsLong)[] StoreTables = [("HVAR", 4, true), ("MVAR", 10, false), ("GDEF", 14, true)];

    // The tables that the metrics-variation target reads, for each font that has them.
    private static readonly (string File, string[] Tables)[] MetricsVariationFonts =
    [
        ("Teko/Teko-Variable.ttf", ["HVAR", "MVAR"]),
        ("SourceSans3/SourceSans3VF-Upright.otf", ["HVAR", "MVAR"]),
        ("Variations/VariationMetrics.ttf", ["HVAR", "VVAR", "MVAR"]),
    ];

    private static readonly string[] FontFiles =
    [
        "SixLaborsSampleAB.ttf",
        "FontPrograms/tt-composite.ttf",
        "FontPrograms/tt-names.ttf",
        "FontPrograms/cff-type2.otf",
        "FontPrograms/cff-type2-widths.otf",
        "FontPrograms/cff-seac.otf",
        "FontPrograms/cff-seac-missing.otf",
        "FontPrograms/cff-cid-fdselect0.otf",
        "FontPrograms/cff-cid-fdselect3.otf",
        "FontPrograms/cff-cid-top-identity.otf",
        "FontPrograms/cff-encoding-format0.otf",
        "FontPrograms/cff-encoding-format1.otf",
        "FontPrograms/cff-expertsubset.otf",
        "FontPrograms/cff-isoadobe.otf",
    ];

    private static readonly string[] BareCffFiles =
    [
        "FontPrograms/cff-type2.otf",
        "FontPrograms/cff-seac.otf",
        "FontPrograms/cff-cid-fdselect0.otf",
        "FontPrograms/cff-cid-fdselect3.otf",
        "FontPrograms/cff-encoding-format1.otf",
        "FontPrograms/cff-expertsubset.otf",
    ];

    private static readonly string[] Type1Files = ["Type1/bchr8a.pfb", "Type1/cmr10.pfb"];

    public static int Write(string fonts, string corpusRoot)
    {
        string program = Path.Combine(corpusRoot, FontProgramOracle.Program);
        string type1Plain = Path.Combine(corpusRoot, FontProgramOracle.Type1Plain);
        Directory.CreateDirectory(program);
        Directory.CreateDirectory(type1Plain);

        // The font subsetter reads the same fonts, and not Type 1.
        string subsetter = Path.Combine(corpusRoot, FontSubsetterOracle.Target);
        Directory.CreateDirectory(subsetter);
        foreach (string file in FontFiles)
        {
            byte[] font = File.ReadAllBytes(Path.Combine(fonts, file));
            File.WriteAllBytes(Path.Combine(program, "seed-" + Path.GetFileName(file)), font);
            File.WriteAllBytes(Path.Combine(subsetter, "seed-" + Path.GetFileName(file)), font);
        }

        foreach (string file in BareCffFiles)
        {
            byte[] cff = GetTable(File.ReadAllBytes(Path.Combine(fonts, file)), "CFF ");
            File.WriteAllBytes(Path.Combine(program, "seed-bare-" + Path.ChangeExtension(Path.GetFileName(file), ".cff")), cff);
            File.WriteAllBytes(Path.Combine(subsetter, "seed-bare-" + Path.ChangeExtension(Path.GetFileName(file), ".cff")), cff);
        }

        byte[] synthetic = SyntheticType1();
        File.WriteAllBytes(Path.Combine(type1Plain, "seed-synthetic.t1"), synthetic);
        File.WriteAllBytes(Path.Combine(program, "seed-synthetic.fontfile"), FontProgramOracle.EncryptType1PrivatePart(synthetic));

        foreach (string file in Type1Files)
        {
            byte[] pfb = File.ReadAllBytes(Path.Combine(fonts, file));
            File.WriteAllBytes(Path.Combine(type1Plain, "seed-" + Path.ChangeExtension(Path.GetFileName(file), ".t1")), DecryptPfb(pfb));
        }

        File.WriteAllBytes(Path.Combine(program, "seed-bchr8a.pfb"), File.ReadAllBytes(Path.Combine(fonts, "Type1/bchr8a.pfb")));
        string variationStore = Path.Combine(corpusRoot, ItemVariationStoreOracle.Target);
        WriteVariationStores(fonts, variationStore);
        string metricsVariation = Path.Combine(corpusRoot, MetricsVariationOracle.Target);
        WriteMetricsVariations(fonts, metricsVariation);
        string tupleStore = Path.Combine(corpusRoot, TupleVariationStoreOracle.Target);
        WriteTupleVariationStores(fonts, tupleStore);
        Console.WriteLine($"Wrote seeds to {program}, {type1Plain}, {variationStore}, {metricsVariation}, {tupleStore} and {subsetter}.");
        return 0;
    }

    // Each seed is a whole table, which holds the offsets of its store and mappings.
    private static void WriteMetricsVariations(string fonts, string directory)
    {
        Directory.CreateDirectory(directory);
        foreach ((string file, string[] tables) in MetricsVariationFonts)
        {
            byte[] font = File.ReadAllBytes(Path.Combine(fonts, file));
            string name = Path.GetFileNameWithoutExtension(file).Split('-')[0].ToLowerInvariant();
            foreach (string tag in tables)
            {
                WriteSeed(directory, $"{name}-{tag.ToLowerInvariant()}", GetTable(font, tag));
            }
        }
    }

    // Each store runs from its offset in the table to the end of the table, so a seed also has what follows it.
    private static void WriteVariationStores(string fonts, string directory)
    {
        Directory.CreateDirectory(directory);
        foreach ((string file, bool hasCff2) in VariableFonts)
        {
            byte[] font = File.ReadAllBytes(Path.Combine(fonts, file));
            string name = Path.GetFileName(Path.GetDirectoryName(file))!.ToLowerInvariant();
            foreach ((string tag, int position, bool isLong) in StoreTables)
            {
                byte[] table = GetTable(font, tag);

                // Only GDEF 1.3 and later has a store, and its offset is 0 when the table has none.
                if (tag == "GDEF" && (table.Length < position + 4 || BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(2)) < 3))
                {
                    continue;
                }

                int storeOffset = isLong ? (int)BinaryPrimitives.ReadUInt32BigEndian(table.AsSpan(position)) : BinaryPrimitives.ReadUInt16BigEndian(table.AsSpan(position));
                if (storeOffset != 0)
                {
                    WriteSeed(directory, $"{name}-{tag.ToLowerInvariant()}", table.AsSpan(storeOffset));
                }
            }

            // HVAR holds the offset of the advance width map after the offset of its store. Teko has no map.
            byte[] hvar = GetTable(font, "HVAR");
            int mapOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(hvar.AsSpan(8));
            if (mapOffset != 0)
            {
                WriteSeed(directory, $"{name}-hvar-advance-width-map", hvar.AsSpan(mapOffset));
            }

            if (hasCff2)
            {
                byte[] cff2 = GetTable(font, "CFF2");
                if (!Cff2TopDict.TryGetVariationStoreOffset(cff2, out int cff2Offset))
                {
                    throw new InvalidOperationException($"{file}: the CFF2 table has no variation store.");
                }

                int length = BinaryPrimitives.ReadUInt16BigEndian(cff2.AsSpan(cff2Offset));
                WriteSeed(directory, $"{name}-cff2", cff2.AsSpan(cff2Offset, 2 + length));
            }
        }

        short axis = VariationBytes.F2Dot14(1);
        short half = VariationBytes.F2Dot14(0.5);
        short[][] oneAxis = [[0, axis, axis], [0, half, axis]];
        short[][] twoAxes = [[0, axis, axis, VariationBytes.F2Dot14(-1), VariationBytes.F2Dot14(-0.5), 0], [half, 0, axis, 0, 0, 0], [0, half, axis, 0, axis, axis]];
        WriteSeed(directory, "synthetic-long-words", VariationBytes.Store(1, oneAxis, new VariationBytes.ItemData([0, 1], 1, [[100_000, -300], [-5, 7]], LongWords: true), null));
        WriteSeed(directory, "synthetic-two-axes", VariationBytes.Store(2, twoAxes, new VariationBytes.ItemData([0, 1, 2], 2, [[1, 2, 3], [-4, 5, -6]])));
        WriteSeed(directory, "synthetic-cff2", VariationBytes.Cff2Store(2, twoAxes, [0, 1], [2]));
        WriteSeed(directory, "synthetic-map-format-0", VariationBytes.Map(0, 2, 4, (0, 1), (1, 2), (2, 3)));
        WriteSeed(directory, "synthetic-map-format-1", VariationBytes.Map(1, 4, 16, (0, 0), (5, 65535), (300, 2)));
    }

    // The glyphs of Teko that the seeds take: a simple glyph, a composite, a glyph with points a tuple leaves out, and the last glyph.
    private static readonly int[] TekoGlyphs = [1, 3, 12, 1024];

    private static void WriteTupleVariationStores(string fonts, string directory)
    {
        Directory.CreateDirectory(directory);
        byte[] gvar = GetTable(File.ReadAllBytes(Path.Combine(fonts, "Teko/Teko-Variable.ttf")), "gvar");
        int sharedCount = BinaryPrimitives.ReadUInt16BigEndian(gvar.AsSpan(6));
        int sharedOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(gvar.AsSpan(8));
        int dataOffset = (int)BinaryPrimitives.ReadUInt32BigEndian(gvar.AsSpan(16));
        short[][] shared = [.. Enumerable.Range(0, sharedCount).Select(i => new[] { BinaryPrimitives.ReadInt16BigEndian(gvar.AsSpan(sharedOffset + (2 * i))) })];

        // Teko has short offsets, which count in words.
        byte[][] glyphs = [.. TekoGlyphs.Select(glyph =>
        {
            int start = BinaryPrimitives.ReadUInt16BigEndian(gvar.AsSpan(20 + (2 * glyph))) * 2;
            int end = BinaryPrimitives.ReadUInt16BigEndian(gvar.AsSpan(20 + (2 * (glyph + 1)))) * 2;
            return gvar.AsSpan(dataOffset + start, end - start).ToArray();
        })];
        WriteSeed(directory, "teko-gvar", TupleVariationBytes.Gvar(1, shared, glyphs));
        WriteSeed(directory, "teko-gvar-glyph", glyphs[0]);

        short one = VariationBytes.F2Dot14(1);
        short half = VariationBytes.F2Dot14(0.5);
        short quarter = VariationBytes.F2Dot14(0.25);
        int[] zeros = new int[12];
        int[] ramp = [.. Enumerable.Range(0, 12).Select(i => (i * 20) - 100)];

        // Two axes. The first glyph has shared point numbers and an intermediate region, the second has a shared tuple and
        // point numbers in words, and the third has every point.
        byte[] first = TupleVariationBytes.Glyph(
            [1, 3, 5],
            new TupleVariationBytes.TupleData([one, 0], null, null, null, [10, -20, 300], [5, 6, -7]),
            new TupleVariationBytes.TupleData([half, one], [quarter, 0], [one, one], [0, 2], [1, 2], [3, 4]));
        byte[] second = TupleVariationBytes.Glyph(
            null,
            new TupleVariationBytes.TupleData(null, null, null, [300, 700, 1300], ramp[..3], ramp[3..6], 1));
        byte[] third = TupleVariationBytes.Glyph(null, new TupleVariationBytes.TupleData([(short)-one, (short)-half], null, null, null, ramp, zeros));
        short[][] twoAxes = [[one, 0], [0, (short)-one]];
        WriteSeed(directory, "synthetic-gvar-two-axes", TupleVariationBytes.Gvar(2, twoAxes, [first, [], third]));
        WriteSeed(directory, "synthetic-gvar-long-offsets", TupleVariationBytes.Gvar(2, twoAxes, [second, third, first], longOffsets: true));
        WriteSeed(directory, "synthetic-gvar-glyph", first);

        // A count of more than 127 points takes two bytes.
        int[] points = [.. Enumerable.Range(0, 200).Select(i => i * 2)];
        int[] deltas = [.. Enumerable.Range(0, 200).Select(i => (i * 7) - 700)];
        WriteSeed(directory, "synthetic-gvar-many-points", TupleVariationBytes.Glyph(null, new TupleVariationBytes.TupleData([one, half], null, null, points, deltas, deltas)));

        // 'cvar' has no shared tuples, so every tuple holds its peak.
        WriteSeed(
            directory,
            "synthetic-cvar",
            TupleVariationBytes.Cvar(
                [0, 2, 4],
                new TupleVariationBytes.TupleData([one], null, null, null, [10, -10, 20], null),
                new TupleVariationBytes.TupleData([half], [0], [one], [1, 3, 11], [-5, 6, 700], null)));
    }

    private static void WriteSeed(string directory, string name, ReadOnlySpan<byte> data)
        => File.WriteAllBytes(Path.Combine(directory, $"seed-{name}.bin"), data.ToArray());

    private static byte[] GetTable(byte[] font, string tag)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            int record = 12 + (i * 16);
            if (Encoding.ASCII.GetString(font, record, 4) == tag)
            {
                int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
                int length = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 12));
                return font.AsSpan(offset, length).ToArray();
            }
        }

        throw new InvalidDataException($"The font has no '{tag}' table.");
    }

    // The cleartext up to "eexec", a line break, then the decrypted private part.
    private static byte[] DecryptPfb(byte[] pfb)
    {
        ReadOnlySpan<byte> clearText = Type1Eexec.GetPfbClearText(pfb, out int binaryPosition);
        LoadBudget budget = new(0, null);
        byte[] privatePart = Type1Eexec.DecryptPfbBinary(pfb, binaryPosition, ref budget)
            ?? throw new InvalidDataException("The PFB has no binary segment.");
        int eexec = clearText.IndexOf("eexec"u8) + 5;
        return [.. clearText.Slice(0, eexec), (byte)'\n', .. privatePart];
    }

    // A small font with charstrings in plain text (lenIV -1): subroutines, flex, hint replacement, seac and div.
    private static byte[] SyntheticType1()
    {
        List<byte> data = [];
        void Text(string text) => data.AddRange(Encoding.Latin1.GetBytes(text));
        void Binary(string program)
        {
            byte[] charString = CharString(program);
            Text($"{charString.Length} RD ");
            data.AddRange(charString);
        }

        Text("%!PS-AdobeFont-1.0: Synthetic 001.000\n11 dict begin\n/FontName /Synthetic def\n/PaintType 0 def\n");
        Text("/FontMatrix [0.001 0 0 0.001 0 0] readonly def\n/FontBBox {0 -200 1000 800} readonly def\n");
        Text("/Encoding 256 array\n0 1 255 {1 index exch /.notdef put} for\ndup 65 /A put\ndup 66 /B put\ndup 67 /Aacute put\nreadonly def\n");
        Text("currentdict end\ncurrentfile eexec\n");
        Text("dup /Private 8 dict dup begin\n/RD {string currentfile exch readstring pop} executeonly def\n");
        Text("/ND {noaccess def} executeonly def\n/NP {noaccess put} executeonly def\n/lenIV -1 def\n/Subrs 5 array\n");
        string[] subrs =
        [
            "3 0 callothersubr pop pop setcurrentpoint return",
            "0 1 callothersubr return",
            "0 2 callothersubr return",
            "return",
            "100 0 rlineto 0 100 rlineto return",
        ];
        for (int i = 0; i < subrs.Length; i++)
        {
            Text($"dup {i} ");
            Binary(subrs[i]);
            Text(" NP\n");
        }

        Text("ND\n2 index /CharStrings 6 dict dup begin\n");
        (string Name, string Program)[] glyphs =
        [
            (".notdef", "0 500 hsbw endchar"),
            ("A", "20 600 hsbw 0 0 rmoveto 300 700 rlineto 300 -700 rlineto closepath 100 200 rmoveto 50 50 100 50 50 -50 rrcurveto closepath endchar"),
            ("B", "0 0 500 0 sbw 1 0 callsubr 4 callsubr -100 hlineto closepath 3 1 3 callothersubr pop callsubr 10 vlineto 20 30 40 50 vhcurveto 5 6 7 8 hvcurveto closepath endchar"),
            ("acute", "0 300 hsbw 100 600 rmoveto 50 100 rlineto 20 -10 rlineto 600 2 div 0 rlineto closepath endchar"),
            ("Aacute", "20 600 hsbw 0 50 0 65 194 seac"),
            ("flex", "0 500 hsbw 100 100 rmoveto 1 callsubr 50 0 rmoveto 2 callsubr -40 2 rmoveto 2 callsubr 20 3 rmoveto 2 callsubr "
                + "20 hmoveto 2 callsubr 20 0 rmoveto 2 callsubr 20 -3 rmoveto 2 callsubr 10 -2 rmoveto 2 callsubr "
                + "50 200 100 0 callsubr 10 hlineto closepath dotsection 1 2 3 4 5 6 hstem3 endchar"),
        ];
        foreach ((string name, string program) in glyphs)
        {
            Text($"/{name} ");
            Binary(program);
            Text(" ND\n");
        }

        Text("end\nend\nreadonly put\nnoaccess put\ndup /FontName get exch definefont pop\nmark currentfile closefile\n");
        return [.. data];
    }

    private static byte[] CharString(string program)
    {
        Dictionary<string, byte[]> operators = new()
        {
            ["hstem"] = [1], ["vstem"] = [3], ["vmoveto"] = [4], ["rlineto"] = [5], ["hlineto"] = [6], ["vlineto"] = [7],
            ["rrcurveto"] = [8], ["closepath"] = [9], ["callsubr"] = [10], ["return"] = [11], ["hsbw"] = [13],
            ["endchar"] = [14], ["rmoveto"] = [21], ["hmoveto"] = [22], ["vhcurveto"] = [30], ["hvcurveto"] = [31],
            ["dotsection"] = [12, 0], ["vstem3"] = [12, 1], ["hstem3"] = [12, 2], ["seac"] = [12, 6], ["sbw"] = [12, 7],
            ["div"] = [12, 12], ["callothersubr"] = [12, 16], ["pop"] = [12, 17], ["setcurrentpoint"] = [12, 33],
        };

        List<byte> code = [];
        foreach (string token in program.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (operators.TryGetValue(token, out byte[]? op))
            {
                code.AddRange(op);
                continue;
            }

            int value = int.Parse(token, System.Globalization.CultureInfo.InvariantCulture);
            if (value is >= -107 and <= 107)
            {
                code.Add((byte)(value + 139));
            }
            else if (value is >= 108 and <= 1131)
            {
                code.Add((byte)(((value - 108) >> 8) + 247));
                code.Add((byte)(value - 108));
            }
            else if (value is >= -1131 and <= -108)
            {
                code.Add((byte)(((-value - 108) >> 8) + 251));
                code.Add((byte)(-value - 108));
            }
            else
            {
                code.AddRange([255, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
            }
        }

        return [.. code];
    }
}
