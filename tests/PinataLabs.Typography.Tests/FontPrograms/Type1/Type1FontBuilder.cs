// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PinataLabs.Typography.FontPrograms.Type1;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

/// <summary>
/// Builds small Type 1 font programs for tests, in every container the loader reads.
/// It has its own encryption code, so that the tests do not check the loader against itself.
/// </summary>
internal sealed class Type1FontBuilder
{
    private const ushort EexecKey = 55665;
    private const ushort CharStringKey = 4330;

    private static readonly Dictionary<string, byte[]> Operators = new()
    {
        ["hstem"] = [1],
        ["vstem"] = [3],
        ["vmoveto"] = [4],
        ["rlineto"] = [5],
        ["hlineto"] = [6],
        ["vlineto"] = [7],
        ["rrcurveto"] = [8],
        ["closepath"] = [9],
        ["callsubr"] = [10],
        ["return"] = [11],
        ["hsbw"] = [13],
        ["endchar"] = [14],
        ["rmoveto"] = [21],
        ["hmoveto"] = [22],
        ["vhcurveto"] = [30],
        ["hvcurveto"] = [31],
        ["dotsection"] = [12, 0],
        ["vstem3"] = [12, 1],
        ["hstem3"] = [12, 2],
        ["seac"] = [12, 6],
        ["sbw"] = [12, 7],
        ["div"] = [12, 12],
        ["callothersubr"] = [12, 16],
        ["pop"] = [12, 17],
        ["setcurrentpoint"] = [12, 33],
    };

    public string ClearTextBody { get; set; } =
        "/FontName /Synthetic def\n/PaintType 0 def\n/FontMatrix [0.001 0 0 0.001 0 0] readonly def\n"
        + "/Encoding StandardEncoding def\n/FontBBox {0 -200 1000 800} readonly def\n";

    /// <summary>
    /// Gets or sets the <c>/lenIV</c> entry. <see langword="null"/> leaves it out, which means 4.
    /// </summary>
    public int? LenIV { get; set; }

    public string RD { get; set; } = "RD";

    public string ND { get; set; } = "ND";

    public string NP { get; set; } = "NP";

    public List<(string Name, byte[] CharString)> Glyphs { get; } = [];

    public List<byte[]> Subrs { get; } = [];

    /// <summary>
    /// Encodes a charstring from integers and operator names, for example
    /// <c>"0 500 hsbw 10 20 rmoveto endchar"</c>.
    /// </summary>
    /// <param name="program">The charstring.</param>
    /// <returns>The unencrypted charstring.</returns>
    public static byte[] CharString(string program)
    {
        MemoryStream stream = new();
        foreach (string token in program.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Operators.TryGetValue(token, out byte[]? op))
            {
                stream.Write(op);
                continue;
            }

            int value = int.Parse(token);
            if (value is >= -107 and <= 107)
            {
                stream.WriteByte((byte)(value + 139));
            }
            else if (value is >= 108 and <= 1131)
            {
                stream.WriteByte((byte)(((value - 108) >> 8) + 247));
                stream.WriteByte((byte)(value - 108));
            }
            else if (value is >= -1131 and <= -108)
            {
                stream.WriteByte((byte)(((-value - 108) >> 8) + 251));
                stream.WriteByte((byte)(-value - 108));
            }
            else
            {
                stream.Write([255, (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
            }
        }

        return stream.ToArray();
    }

    public Type1FontBuilder AddGlyph(string name, string program)
    {
        this.Glyphs.Add((name, CharString(program)));
        return this;
    }

    public Type1FontBuilder AddSubr(string program)
    {
        this.Subrs.Add(CharString(program));
        return this;
    }

    public Type1Program Load() => Type1Program.Load(this.BuildPfa());

    public byte[] BuildClearText()
        => Encoding.Latin1.GetBytes("%!PS-AdobeFont-1.0: Synthetic 001.000\n11 dict begin\n" + this.ClearTextBody + "currentdict end\ncurrentfile eexec\n");

    /// <summary>
    /// Builds the private part before eexec encryption.
    /// </summary>
    /// <returns>The private part.</returns>
    public byte[] BuildPrivate()
    {
        MemoryStream stream = new();
        void Text(string text) => stream.Write(Encoding.Latin1.GetBytes(text));

        void Binary(byte[] charString)
        {
            byte[] stored = this.LenIV is < 0 ? charString : Encrypt(charString, CharStringKey, this.LenIV ?? 4);
            Text($"{stored.Length} {this.RD} ");
            stream.Write(stored);
        }

        Text("dup /Private 8 dict dup begin\n");
        Text($"/{this.RD} {{string currentfile exch readstring pop}} executeonly def\n");
        Text($"/{this.ND} {{noaccess def}} executeonly def\n/{this.NP} {{noaccess put}} executeonly def\n");
        Text("/BlueValues [-20 0 700 720] def\n/password 5839 def\n");
        if (this.LenIV is int lenIV)
        {
            Text($"/lenIV {lenIV} def\n");
        }

        Text($"/Subrs {this.Subrs.Count} array\n");
        for (int i = 0; i < this.Subrs.Count; i++)
        {
            Text($"dup {i} ");
            Binary(this.Subrs[i]);
            Text($" {this.NP}\n");
        }

        Text($"{this.ND}\n2 index /CharStrings {this.Glyphs.Count} dict dup begin\n");
        foreach ((string name, byte[] charString) in this.Glyphs)
        {
            Text($"/{name} ");
            Binary(charString);
            Text($" {this.ND}\n");
        }

        Text("end\nend\nreadonly put\nnoaccess put\ndup /FontName get exch definefont pop\nmark currentfile closefile\n");
        return stream.ToArray();
    }

    /// <summary>
    /// Builds a PFA, or with a binary eexec section the stream of a PDF <c>/FontFile</c>.
    /// </summary>
    /// <param name="hex">Whether the eexec section is hexadecimal.</param>
    /// <param name="trailer">Whether the 512 zeros and <c>cleartomark</c> follow the eexec section.</param>
    /// <returns>The font file.</returns>
    public byte[] BuildPfa(bool hex = true, bool trailer = true)
    {
        byte[] encrypted = EncryptEexec(this.BuildPrivate());
        if (hex)
        {
            StringBuilder text = new();
            for (int i = 0; i < encrypted.Length; i++)
            {
                text.Append(encrypted[i].ToString("x2"));
                if (i % 32 == 31)
                {
                    text.Append('\n');
                }
            }

            encrypted = Encoding.Latin1.GetBytes(text.ToString());
        }

        return [.. this.BuildClearText(), .. encrypted, .. trailer ? Trailer() : []];
    }

    /// <summary>
    /// Builds a PFB.
    /// </summary>
    /// <param name="clearTextSegments">The number of ASCII segments that hold the cleartext.</param>
    /// <param name="binarySegmentLength">The largest length of a binary segment.</param>
    /// <returns>The font file.</returns>
    public byte[] BuildPfb(int clearTextSegments = 1, int binarySegmentLength = int.MaxValue)
    {
        MemoryStream stream = new();
        void Segment(byte kind, ReadOnlySpan<byte> data)
        {
            stream.Write([0x80, kind, (byte)data.Length, (byte)(data.Length >> 8), (byte)(data.Length >> 16), (byte)(data.Length >> 24)]);
            stream.Write(data);
        }

        byte[] clearText = this.BuildClearText();
        int part = (clearText.Length + clearTextSegments - 1) / clearTextSegments;
        for (int start = 0; start < clearText.Length; start += part)
        {
            Segment(1, clearText.AsSpan(start, Math.Min(part, clearText.Length - start)));
        }

        byte[] encrypted = EncryptEexec(this.BuildPrivate());
        for (int start = 0; start < encrypted.Length; start += Math.Min(binarySegmentLength, encrypted.Length))
        {
            Segment(2, encrypted.AsSpan(start, Math.Min(binarySegmentLength, encrypted.Length - start)));
        }

        Segment(1, Trailer());
        stream.Write([0x80, 3]);
        return stream.ToArray();
    }

    public static byte[] EncryptEexec(byte[] privatePart) => Encrypt(privatePart, EexecKey, 4);

    private static byte[] Trailer()
        => Encoding.Latin1.GetBytes(string.Concat(Enumerable.Repeat(new string('0', 64) + "\n", 8)) + "cleartomark\n");

    private static byte[] Encrypt(byte[] plain, ushort key, int prefixLength)
    {
        byte[] cipher = new byte[plain.Length + prefixLength];
        ushort r = key;
        for (int i = 0; i < cipher.Length; i++)
        {
            byte value = i < prefixLength ? (byte)0 : plain[i - prefixLength];
            cipher[i] = (byte)(value ^ (r >> 8));
            r = (ushort)(((cipher[i] + r) * 52845) + 22719);
        }

        return cipher;
    }
}
