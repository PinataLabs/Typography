// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

public class FontProgramTests
{
    [Test]
    public async Task LoadsATrueTypeProgram()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.OpenSansFile));

        await Assert.That(font.Format).IsEqualTo(FontProgramFormat.TrueType);
        await Assert.That(font.IsCidKeyed).IsFalse();
        await Assert.That(font.GlyphCount).IsEqualTo(1086);
        await Assert.That(font.FontMatrix).IsEqualTo(Matrix3x2.CreateScale(1F / 2048));
    }

    [Test]
    public async Task LoadsACffProgram()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("SourceSansPro-Regular.otf")));

        await Assert.That(font.Format).IsEqualTo(FontProgramFormat.Cff);
        await Assert.That(font.IsCidKeyed).IsFalse();
        await Assert.That(font.GlyphCount).IsEqualTo(1942);
        await Assert.That(font.FontMatrix).IsEqualTo(Matrix3x2.CreateScale(0.001F));
    }

    [Test]
    public async Task LoadsACidKeyedCffProgram()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.FDArrayTest257File));

        await Assert.That(font.Format).IsEqualTo(FontProgramFormat.Cff);
        await Assert.That(font.IsCidKeyed).IsTrue();
        await Assert.That(font.GetGlyphName(1)).IsNull();
    }

    [Test]
    [Arguments("WOFF")]
    [Arguments("WOFF2")]
    [Arguments("a font collection")]
    [Arguments("CFF2")]
    [Arguments("no data")]
    [Arguments("text")]
    [Arguments("a truncated directory")]
    [Arguments("TrueType without glyf")]
    [Arguments("OpenType without CFF")]
    public async Task LoadRejectsUnsupportedData(string kind)
    {
        byte[] data = kind switch
        {
            "WOFF" => File.ReadAllBytes(TestFonts.OpenSansFileWoff1),
            "WOFF2" => File.ReadAllBytes(TestFonts.OpenSansFileWoff2),
            "a font collection" => File.ReadAllBytes(TestFonts.SimpleTrueTypeCollection),
            "CFF2" => File.ReadAllBytes(TestFonts.SourceSans3VariableCff2File),
            "no data" => [],
            "text" => Encoding.ASCII.GetBytes("%!PS-AdobeFont-1.0: this is not an sfnt font"),
            "a truncated directory" => File.ReadAllBytes(TestFonts.OpenSansFile).AsSpan(0, 40).ToArray(),
            "TrueType without glyf" => RenameTable(File.ReadAllBytes(TestFonts.OpenSansFile), "glyf"),
            _ => RenameTable(File.ReadAllBytes(TestFonts.FDArrayTest257File), "CFF "),
        };

        await Assert.That(() => FontProgram.Load(data)).ThrowsExactly<InvalidFontFileException>();
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    public async Task GlyphIdOutOfRangeFails(string fontFile)
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath(fontFile)));
        ushort glyphId = (ushort)font.GlyphCount;
        GlyphOutline outline = new();

        await Assert.That(font.TryWriteOutline(glyphId, ref outline)).IsFalse();
        await Assert.That(outline.ToString()).IsEqualTo(string.Empty);
        await Assert.That(font.TryGetAdvanceWidth(glyphId, out _)).IsFalse();
        await Assert.That(font.GetGlyphName(glyphId)).IsNull();
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    public async Task GlyphWithoutAnOutlineWritesNothing(string fontFile)
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath(fontFile)));
        GlyphOutline outline = new();

        await Assert.That(font.TryGetGlyphId("space", out ushort space)).IsTrue();
        await Assert.That(font.TryWriteOutline(space, ref outline)).IsTrue();
        await Assert.That(outline.ToString()).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task BuiltInEncodingNeedsRoomFor256Codes()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("SourceSansPro-Regular.otf")));

        await Assert.That(() => font.TryGetBuiltInEncoding(new ushort[255])).ThrowsExactly<ArgumentException>();
    }

    [Test]
    [Arguments("tt-composite", 8)]
    [Arguments("cff-cid-fdselect0", 4)]
    [Arguments("OpenSans-Regular", 40)]
    public async Task StructSinkReceivesTheWholeOutline(string fixtureName, int glyphId)
    {
        FontProgramFixture fixture = FontProgramFixture.Load(fixtureName);
        FontProgram font = FontProgram.Load(File.ReadAllBytes(fixture.FontPath));
        CountingSink sink = default;

        await Assert.That(font.TryWriteOutline((ushort)glyphId, ref sink)).IsTrue();

        int expected = fixture.Glyphs[glyphId].Outline.Split(' ').Length;
        await Assert.That(expected).IsGreaterThan(10);
        await Assert.That(sink.Tokens).IsEqualTo(expected);
    }

    [Test]
    [Arguments(2)]
    [Arguments(3)]
    public async Task SeacWithAMissingComponentFails(int glyphId)
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("FontPrograms/cff-seac-missing.otf")));
        GlyphOutline outline = new();

        await Assert.That(font.TryWriteOutline((ushort)glyphId, ref outline)).IsFalse();
    }

    [Test]
    public async Task ProgramReadsTheMemoryItWasLoadedFrom()
    {
        byte[] data = File.ReadAllBytes(FontProgramFixture.Load("tt-names").FontPath);
        FontProgram font = FontProgram.Load(data);
        GlyphOutline before = new();
        GlyphOutline after = new();
        font.TryWriteOutline(1, ref before);

        Array.Clear(data, 12, data.Length - 12);

        await Assert.That(before.ToString()).IsNotEqualTo(string.Empty);
        await Assert.That(font.TryWriteOutline(1, ref after)).IsTrue();
        await Assert.That(after.ToString()).IsEqualTo(string.Empty);
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("SourceSansPro-Regular.otf")]
    public async Task OneProgramServesManyThreads(string fontFile)
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath(fontFile)));
        string[] expected = new string[font.GlyphCount];
        for (int i = 0; i < expected.Length; i++)
        {
            expected[i] = Describe(font, i);
        }

        int mismatches = 0;
        Parallel.For(0, expected.Length * 4, i =>
        {
            int glyphId = i % expected.Length;
            if (Describe(font, glyphId) != expected[glyphId])
            {
                System.Threading.Interlocked.Increment(ref mismatches);
            }
        });

        await Assert.That(mismatches).IsEqualTo(0);
    }

    private static string Describe(FontProgram font, int glyphId)
    {
        GlyphOutline outline = new();
        font.TryWriteOutline((ushort)glyphId, ref outline);
        font.TryGetAdvanceWidth((ushort)glyphId, out float advance);
        return $"{font.GetGlyphName((ushort)glyphId)} {advance} {outline}";
    }

    private static byte[] RenameTable(byte[] data, string tag)
    {
        int tableCount = (data[4] << 8) | data[5];
        for (int i = 0; i < tableCount; i++)
        {
            int entry = 12 + (i * 16);
            if (Encoding.ASCII.GetString(data, entry, 4) == tag)
            {
                data[entry] = (byte)'X';
                return data;
            }
        }

        throw new InvalidOperationException($"The font has no '{tag}' table.");
    }

    /// <summary>
    /// Counts the tokens of the outline text of a fixture: one for each command and one for each coordinate.
    /// </summary>
    private struct CountingSink : IGlyphOutlineSink
    {
        public int Tokens { get; private set; }

        public void MoveTo(Vector2 point) => this.Tokens += 3;

        public void LineTo(Vector2 point) => this.Tokens += 3;

        public void QuadraticBezierTo(Vector2 control, Vector2 point) => this.Tokens += 5;

        public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point) => this.Tokens += 7;

        public void ClosePath() => this.Tokens++;
    }
}
