// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// A bare CFF font program, as a PDF embeds it in a <c>/FontFile3</c> stream. The glyph-by-glyph comparison
/// with fontTools is in <see cref="FontProgramParityTests"/>, through the bare-*.json fixtures.
/// </summary>
public class BareCffTests
{
    [Test]
    public async Task ANameKeyedBareCffLoadsAsCff()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("FontPrograms/bare-SourceSansPro-Regular.cff")));

        await Assert.That(font.Format).IsEqualTo(FontProgramFormat.Cff);
        await Assert.That(font.IsCidKeyed).IsFalse();
        await Assert.That(font.TryGetGlyphId("A", out ushort a)).IsTrue();
        await Assert.That(font.GetGlyphName(a)).IsEqualTo("A");
        await Assert.That(font.TryGetBuiltInEncoding(new ushort[256])).IsTrue();
    }

    [Test]
    public async Task ACidKeyedBareCffLoadsAsCff()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("FontPrograms/bare-FDArrayTest257.cff")));

        await Assert.That(font.Format).IsEqualTo(FontProgramFormat.Cff);
        await Assert.That(font.IsCidKeyed).IsTrue();
        await Assert.That(font.TryGetBuiltInEncoding(new ushort[256])).IsFalse();
    }

    [Test]
    public async Task ABareCffHasTheSameOutlinesAndWidthsAsItsOpenTypeFont()
    {
        byte[] otf = File.ReadAllBytes(TestFonts.GetPath("SourceSansPro-Regular.otf"));
        FontProgram wrapped = FontProgram.Load(otf);
        FontProgram bare = FontProgram.Load(GetTable(otf, "CFF "));

        await Assert.That(bare.GlyphCount).IsEqualTo(wrapped.GlyphCount);
        for (ushort i = 0; i < bare.GlyphCount; i += 7)
        {
            GlyphOutline expected = new();
            GlyphOutline actual = new();
            await Assert.That(bare.TryWriteOutline(i, ref actual)).IsTrue();
            await Assert.That(wrapped.TryWriteOutline(i, ref expected)).IsTrue();
            await Assert.That(actual.ToString()).IsEqualTo(expected.ToString());
        }
    }

    [Test]
    public async Task ABareCffRetainsItsDataCharsetAndFontDicts()
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/bare-SourceSansPro-Regular.cff"));
        byte[] wrapped = File.ReadAllBytes(TestFonts.GetPath("SourceSansPro-Regular.otf"));

        FontProgram bare = FontProgram.Load(data);
        FontProgram otf = FontProgram.Load(wrapped);

        // The same arrays as the OpenType font, with the length of the bare data in place of the file.
        await Assert.That(bare.RetainedByteCount - data.Length).IsEqualTo(otf.RetainedByteCount - wrapped.Length);
    }

    [Test]
    public async Task ABareCffLargerThanTheLimitIsRefusedBeforeParsing()
    {
        byte[] data = [1, 0, 4, 1, 0, 0, 0, 0];

        await Assert.That(() => FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = 7 }))
            .ThrowsExactly<FontProgramTooLargeException>();
        await Assert.That(() => FontProgram.Load(data, new FontProgramOptions { MaxRetainedBytes = 8 }))
            .ThrowsExactly<InvalidFontFileException>();
    }

    [Test]
    public async Task ABareCff2IsRejected()
    {
        byte[] cff2 = GetTable(File.ReadAllBytes(TestFonts.SourceSans3VariableCff2File), "CFF2");

        await Assert.That(cff2[0]).IsEqualTo((byte)2);
        await Assert.That(() => FontProgram.Load(cff2)).ThrowsExactly<InvalidFontFileException>();
    }

    [Test]
    [Arguments(new byte[] { 1, 0, 3, 1 })]
    [Arguments(new byte[] { 1, 0, 4, 0 })]
    [Arguments(new byte[] { 1, 0, 4, 5 })]
    [Arguments(new byte[] { 1, 0, 4, 1 })]
    [Arguments(new byte[] { 1, 0, 4, 1, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 })]
    public async Task AMalformedBareCffThrowsInvalidFontFile(byte[] data)
    {
        await Assert.That(() => FontProgram.Load(data)).ThrowsExactly<InvalidFontFileException>();
    }

    [Test]
    public async Task ATruncatedBareCffOnlyThrowsInvalidFontFile()
    {
        byte[] data = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/bare-FDArrayTest257.cff"));
        int loaded = 0;

        for (int length = 0; length < data.Length; length += Math.Max(1, data.Length / 61))
        {
            try
            {
                FontProgram.Load(data.AsSpan(0, length).ToArray());
                loaded++;
            }
            catch (InvalidFontFileException)
            {
            }
        }

        await Assert.That(loaded).IsEqualTo(0);
    }

    [Test]
    public async Task NoOtherFormatLooksLikeABareCff()
    {
        // Every other format starts with a byte other than 1: the sfnt tags (0x00, 'O', 't', 'w'), Type 1 ('%', 0x80).
        foreach (string file in (string[])["OpenSans-Regular.ttf", "SourceSansPro-Regular.otf", "Type1/cmr10.pfb", "Type1/bchr8a.pfa"])
        {
            byte[] data = File.ReadAllBytes(TestFonts.GetPath(file));
            await Assert.That(data[0]).IsNotEqualTo((byte)1);
        }

        await Assert.That(FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("Type1/cmr10.pfb"))).Format).IsEqualTo(FontProgramFormat.Type1);
        await Assert.That(FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("SourceSansPro-Regular.otf"))).Format).IsEqualTo(FontProgramFormat.Cff);
    }

    private static byte[] GetTable(byte[] sfnt, string tag)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(sfnt.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> record = sfnt.AsSpan(12 + (16 * i), 16);
            if (Encoding.ASCII.GetString(record.Slice(0, 4)) == tag)
            {
                int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(8));
                int length = (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(12));
                return sfnt.AsSpan(offset, length).ToArray();
            }
        }

        throw new InvalidOperationException(tag);
    }
}
