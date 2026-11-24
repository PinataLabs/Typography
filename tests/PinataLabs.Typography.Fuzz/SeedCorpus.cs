// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.FontPrograms.Type1;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Writes the seed corpus from the test fonts: small TrueType and OpenType CFF fonts, the CFF tables of some
/// of them as bare CFF, and Type 1 fonts, for the <c>program</c> target as files and for the
/// <c>type1-plain</c> target with the private part decrypted.
/// </summary>
internal static class SeedCorpus
{
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

        foreach (string file in FontFiles)
        {
            File.WriteAllBytes(Path.Combine(program, "seed-" + Path.GetFileName(file)), File.ReadAllBytes(Path.Combine(fonts, file)));
        }

        foreach (string file in BareCffFiles)
        {
            byte[] cff = GetTable(File.ReadAllBytes(Path.Combine(fonts, file)), "CFF ");
            File.WriteAllBytes(Path.Combine(program, "seed-bare-" + Path.ChangeExtension(Path.GetFileName(file), ".cff")), cff);
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
        Console.WriteLine($"Wrote seeds to {program} and {type1Plain}.");
        return 0;
    }

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
