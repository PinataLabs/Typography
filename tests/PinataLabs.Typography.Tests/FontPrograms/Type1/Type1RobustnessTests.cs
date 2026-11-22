// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms.Type1;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

/// <summary>
/// Damaged font programs either fail to load with <see cref="InvalidFontFileException"/> or load and
/// return <see langword="false"/> for the glyphs they cannot draw. No other exception escapes.
/// </summary>
public class Type1RobustnessTests
{
    private const string Flex =
        "0 500 hsbw 100 100 rmoveto 1 callsubr 50 0 rmoveto 2 callsubr -40 2 rmoveto 2 callsubr 20 3 rmoveto 2 callsubr "
        + "20 hmoveto 2 callsubr 20 0 rmoveto 2 callsubr 20 -3 rmoveto 2 callsubr 10 -2 rmoveto 2 callsubr "
        + "50 200 100 0 callsubr 10 hlineto closepath endchar";

    [Test]
    [Arguments("bchr8a.pfb")]
    [Arguments("bchr8a.pfa")]
    [Arguments("bchr8a.fontfile")]
    [Arguments("cmr10.pfb")]
    public async Task TruncatedFontsFailCleanly(string fontFile)
    {
        byte[] data = Type1TestFonts.Read(fontFile);
        Tally tally = default;
        int step = Math.Max(1, data.Length / 600);
        for (int length = 0; length < data.Length; length += length < 64 ? 1 : step)
        {
            Exercise(data.AsMemory(0, length), ref tally);
        }

        await Assert.That(tally.Rejected).IsGreaterThan(0);
        await Assert.That(tally.Loaded + tally.Rejected).IsGreaterThan(600);
    }

    [Test]
    [Arguments("bchr8a.pfb", 1)]
    [Arguments("bchr8a.pfa", 2)]
    [Arguments("bchr8a.fontfile", 3)]
    [Arguments("cmr10.pfb", 4)]
    public async Task GarbledFilesFailCleanly(string fontFile, int seed)
    {
        // eexec decryption carries every changed byte forward, so nearly all of these fail at load.
        byte[] original = Type1TestFonts.Read(fontFile);
        Random random = new(seed);
        Tally tally = default;
        for (int variant = 0; variant < 400; variant++)
        {
            Exercise(Garble(original, random, 1 << random.Next(0, 9)), ref tally);
        }

        await Assert.That(tally.Rejected).IsGreaterThan(0);
        await Assert.That(tally.Loaded + tally.Rejected).IsEqualTo(400);
    }

    [Test]
    [Arguments("bchr8a.pfb", 11)]
    [Arguments("cmr10.pfb", 12)]
    [Arguments("qtmr.pfb", 13)]
    public async Task GarbledPrivatePartsFailCleanly(string fontFile, int seed)
    {
        // The damage goes into the decrypted private part, which is then encrypted again, so that it
        // stays where it was put: in a charstring, a subroutine, or the PostScript text around them.
        (byte[] clearText, byte[] privatePart) = SplitPfb(Type1TestFonts.Read(fontFile));
        Random random = new(seed);
        Tally tally = default;
        for (int variant = 0; variant < 400; variant++)
        {
            byte[] damaged = Garble(privatePart, random, 1 << random.Next(0, 5));
            Exercise((byte[])[.. clearText, .. Type1FontBuilder.EncryptEexec(damaged)], ref tally);
        }

        // The damage must reach both failure paths, or the test proves nothing.
        await Assert.That(tally.Rejected).IsGreaterThan(0);
        await Assert.That(tally.Loaded).IsGreaterThan(0);
        await Assert.That(tally.FailedGlyphs).IsGreaterThan(0);
        await Assert.That(tally.DrawnGlyphs).IsGreaterThan(0);
    }

    [Test]
    public async Task GarbledCharStringsFailCleanly()
    {
        // lenIV -1 stores the charstrings unencrypted, so every change lands in the interpreter.
        Type1FontBuilder builder = new() { LenIV = -1 };
        builder.AddSubr("3 0 callothersubr pop pop setcurrentpoint return");
        builder.AddSubr("0 1 callothersubr return");
        builder.AddSubr("0 2 callothersubr return");
        builder.AddSubr("return");
        builder.AddSubr("1 3 callothersubr pop callsubr return");
        builder.AddGlyph(".notdef", "0 500 hsbw endchar");
        builder.AddGlyph("A", "50 600 hsbw 100 vlineto 100 hlineto 1 2 3 4 5 6 rrcurveto 3 4 callsubr closepath endchar");
        builder.AddGlyph("acute", "20 300 hsbw 10 vlineto 1 2 div hlineto 1 2 3 4 hvcurveto closepath endchar");
        builder.AddGlyph("Aacute", "70 600 hsbw 20 200 500 65 194 seac");
        builder.AddGlyph("flex", Flex);

        Random random = new(6);
        Tally tally = default;
        for (int variant = 0; variant < 20000; variant++)
        {
            Type1FontBuilder damaged = new() { LenIV = -1 };
            damaged.Subrs.AddRange(builder.Subrs.ConvertAll(x => (byte[])x.Clone()));
            damaged.Glyphs.AddRange(builder.Glyphs.ConvertAll(x => (x.Name, (byte[])x.CharString.Clone())));
            int changes = random.Next(1, 4);
            for (int i = 0; i < changes; i++)
            {
                byte[] target = random.Next(4) == 0
                    ? damaged.Subrs[random.Next(damaged.Subrs.Count)]
                    : damaged.Glyphs[random.Next(damaged.Glyphs.Count)].CharString;
                target[random.Next(target.Length)] = (byte)random.Next(256);
            }

            Exercise(damaged.BuildPfa(hex: false, trailer: false), ref tally);
        }

        await Assert.That(tally.Rejected).IsEqualTo(0);
        await Assert.That(tally.FailedGlyphs).IsGreaterThan(10000);
        await Assert.That(tally.DrawnGlyphs).IsGreaterThan(10000);
    }

    private static byte[] Garble(byte[] original, Random random, int changes)
    {
        byte[] data = (byte[])original.Clone();
        for (int i = 0; i < changes; i++)
        {
            data[random.Next(data.Length)] = (byte)random.Next(256);
        }

        return data;
    }

    private static (byte[] ClearText, byte[] PrivatePart) SplitPfb(byte[] pfb)
    {
        byte[] clearText = [];
        List<byte> encrypted = [];
        for (int position = 0; pfb[position + 1] != 3;)
        {
            int length = BitConverter.ToInt32(pfb, position + 2);
            ReadOnlySpan<byte> segment = pfb.AsSpan(position + 6, length);
            if (pfb[position + 1] == 2)
            {
                encrypted.AddRange(segment);
            }
            else if (encrypted.Count == 0)
            {
                clearText = segment.ToArray();
            }

            position += 6 + length;
        }

        byte[] plain = new byte[encrypted.Count];
        ushort r = 55665;
        for (int i = 0; i < plain.Length; i++)
        {
            plain[i] = (byte)(encrypted[i] ^ (r >> 8));
            r = (ushort)(((encrypted[i] + r) * 52845) + 22719);
        }

        return (clearText, plain[4..]);
    }

    private static void Exercise(ReadOnlyMemory<byte> data, ref Tally tally)
    {
        Type1Program font;
        try
        {
            font = Type1Program.Load(data);
        }
        catch (InvalidFontFileException)
        {
            tally.Rejected++;
            return;
        }

        tally.Loaded++;
        Span<ushort> encoding = stackalloc ushort[256];
        font.TryGetBuiltInEncoding(encoding);
        for (int glyphId = 0; glyphId < font.GlyphCount; glyphId++)
        {
            OutlineRecorder recorder = new();
            bool drawn = font.TryWriteOutline((ushort)glyphId, ref recorder);
            font.TryGetAdvanceWidth((ushort)glyphId, out _);
            font.TryGetGlyphId(font.GetGlyphName((ushort)glyphId), out _);
            if (drawn)
            {
                tally.DrawnGlyphs++;
            }
            else
            {
                tally.FailedGlyphs++;
            }
        }
    }

    private struct Tally
    {
        public int Rejected;
        public int Loaded;
        public int DrawnGlyphs;
        public int FailedGlyphs;
    }
}
