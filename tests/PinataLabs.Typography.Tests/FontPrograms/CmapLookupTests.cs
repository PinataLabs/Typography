// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.FontPrograms.Type1;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// <see cref="FontProgram.TryGetGlyphIdFromCmap"/>. The lookups of real and built fonts are compared with the
/// fontTools reference in Fonts/FontPrograms/cmap/lookups.json, which tests/tools/generate_font_program_fixtures.py writes.
/// </summary>
public class CmapLookupTests
{
    private static readonly int[] SyntheticFormats = [0, 2, 4, 6, 12, 13];

    private static readonly JsonSerializerOptions FixtureJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string NamesFont = "FontPrograms/tt-names.ttf";

    public static IEnumerable<object[]> Fonts
        => ReadFixture().Fonts.Select((x, i) => new object[] { i, Path.GetFileName(x.Font) });

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task EverySubtableMatchesFontTools(int fontIndex, string name)
    {
        FixtureFont fixture = ReadFixture().Fonts[fontIndex];
        FontProgram font = FontProgram.Load(File.ReadAllBytes(Path.GetFullPath(Path.Combine(TestFonts.GetPath("FontPrograms/cmap"), fixture.Font))));
        List<string> differences = [];

        foreach (FixtureSubtable subtable in fixture.Subtables)
        {
            ushort platform = (ushort)subtable.Platform;
            ushort encoding = (ushort)subtable.Encoding;
            Dictionary<int, int> expected = subtable.Map.ToDictionary(x => x[0], x => x[1]);

            foreach ((int code, int glyph) in expected)
            {
                if (!font.TryGetGlyphIdFromCmap(platform, encoding, code, out ushort actual) || actual != glyph)
                {
                    differences.Add($"{name} ({platform},{encoding}) format {subtable.Format} code {code}: {actual}, expected {glyph}");
                }
            }

            IEnumerable<int> probes = Enumerable.Range(0, 0x3200)
                .Concat([0xFFFE, 0xFFFF, 0x10000, 0x1F600, 0x1F603, 0x20000, 0x20001, 0x10FFFF, 0x110000, int.MaxValue]);
            foreach (int code in probes.Where(x => !expected.ContainsKey(x)))
            {
                if (font.TryGetGlyphIdFromCmap(platform, encoding, code, out ushort actual) || actual != 0)
                {
                    differences.Add($"{name} ({platform},{encoding}) code {code} is unmapped but gave {actual}");
                }
            }
        }

        await Assert.That(fixture.Subtables).IsNotEmpty();
        await Assert.That(differences.Take(10)).IsEmpty();
    }

    [Test]
    public async Task TheFixtureCoversTheWindowsSymbolMacintoshAndUnicodeSubtablesAndEveryFormat()
    {
        FixtureSubtable[] subtables = ReadFixture().Fonts.SelectMany(x => x.Subtables).ToArray();

        await Assert.That(subtables.Any(x => x is { Platform: 3, Encoding: 1 })).IsTrue();
        await Assert.That(subtables.Any(x => x is { Platform: 3, Encoding: 0 })).IsTrue();
        await Assert.That(subtables.Any(x => x is { Platform: 1, Encoding: 0 })).IsTrue();
        await Assert.That(subtables.Select(x => x.Format).Distinct().Order()).IsEquivalentTo(SyntheticFormats);
    }

    [Test]
    public async Task WhenSeveralRecordsShareAPlatformAndEncodingTheFirstIsUsed()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("FontPrograms/tt-cmap.ttf")));

        // The font has two (3,1) subtables. The second maps 0x20 to glyph 11 and 0x21 to glyph 10.
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 1, 0x20, out ushort first)).IsTrue();
        await Assert.That(first).IsEqualTo((ushort)1);
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 1, 0x41, out ushort other)).IsTrue();
        await Assert.That(other).IsEqualTo((ushort)9);
    }

    [Test]
    [Arguments(3, 4)]
    [Arguments(0, 0)]
    [Arguments(1, 1)]
    [Arguments(ushort.MaxValue, ushort.MaxValue)]
    public async Task ASubtableTheFontLacksFindsNothing(int platform, int encoding)
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("OpenSans-Regular.ttf")));

        await Assert.That(font.TryGetGlyphIdFromCmap((ushort)platform, (ushort)encoding, 65, out ushort glyphId)).IsFalse();
        await Assert.That(glyphId).IsEqualTo((ushort)0);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(int.MinValue)]
    [Arguments(0x110000)]
    [Arguments(int.MaxValue)]
    public async Task ACodeOutsideTheRangeOfTheSubtableFindsNothing(int code)
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("OpenSans-Regular.ttf")));

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 1, code, out ushort glyphId)).IsFalse();
        await Assert.That(glyphId).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task ABareCffProgramHasNoCmap()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("FontPrograms/bare-SourceSansPro-Regular.cff")));

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 1, 65, out ushort glyphId)).IsFalse();
        await Assert.That(glyphId).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task AType1ProgramHasNoCmap()
    {
        byte[] pfb = new Type1FontBuilder()
            .AddGlyph(".notdef", "0 500 hsbw endchar")
            .AddGlyph("A", "10 500 hsbw 100 vlineto 100 hlineto closepath endchar")
            .BuildPfb();
        FontProgram font = FontProgram.Load(pfb);

        await Assert.That(font.Format).IsEqualTo(FontProgramFormat.Type1);
        await Assert.That(font.TryGetGlyphIdFromCmap(1, 0, 65, out _)).IsFalse();
    }

    [Test]
    public async Task AnOpenTypeCffProgramWithoutCmapFindsNothing()
    {
        byte[] full = File.ReadAllBytes(TestFonts.GetPath("SourceSansPro-Regular.otf"));
        FontProgram font = FontProgram.Load(SfntRewriter.Remove(full, "cmap"));

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 1, 65, out _)).IsFalse();
    }

    [Test]
    public async Task Format10MapsARangeOf32BitCodes()
    {
        byte[] cmap = Cmap((3, 10, Format10(0x10000, 1, 2, 3, 0)));
        FontProgram font = Load(cmap);

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 0x10000, out ushort a)).IsTrue();
        await Assert.That(a).IsEqualTo((ushort)1);
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 0x10002, out ushort c)).IsTrue();
        await Assert.That(c).IsEqualTo((ushort)3);
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 0x10003, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 0xFFFF, out _)).IsFalse();
    }

    [Test]
    public async Task AGlyphBeyondTheGlyphCountOfTheFontFindsNothing()
    {
        // tt-names.ttf has 8 glyphs.
        FontProgram font = Load(Cmap((3, 10, Format12((0x41, 0x43, 6)))));

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 0x41, out ushort glyphId)).IsTrue();
        await Assert.That(glyphId).IsEqualTo((ushort)6);
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 0x42, out glyphId)).IsTrue();
        await Assert.That(glyphId).IsEqualTo((ushort)7);
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 0x43, out glyphId)).IsFalse();
        await Assert.That(glyphId).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task AFormat12GroupThatRunsPastGlyph65535FindsNothing()
    {
        FontProgram font = Load(Cmap((3, 10, Format12((0, 0xFFFFFFFF, 0xFFFFFF00)))));

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, 1, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 10, int.MaxValue, out _)).IsFalse();
    }

    [Test]
    [MethodDataSource(nameof(MalformedSubtableNames))]
    public async Task AMalformedSubtableFindsNothingAndDoesNotThrow(string name)
    {
        FontProgram font = Load(Cmap((3, 1, MalformedSubtables[name])));
        List<int> found = [];

        foreach (int code in (int[])[0, 1, 0x20, 0x41, 0x42, 0xFF, 0x100, 0x8140, 0xFFFF, 0x10000, 0x10FFFF, int.MaxValue])
        {
            if (font.TryGetGlyphIdFromCmap(3, 1, code, out ushort glyphId) || glyphId != 0)
            {
                found.Add(code);
            }
        }

        await Assert.That(found).IsEmpty();
    }

    public static IEnumerable<string> MalformedSubtableNames => MalformedSubtables.Keys;

    private static Dictionary<string, byte[]> MalformedSubtables { get; } = BuildMalformedSubtables();

    private static Dictionary<string, byte[]> BuildMalformedSubtables()
    {
        Dictionary<string, byte[]> result = [];

        // The subtable ends before its header does.
        result.Add("empty", []);
        result.Add("format 4 header only", U16(4, 24, 0, 2));
        result.Add("format 0 without its 256 bytes", U16(0, 262, 0).Concat(new byte[] { 0, 0, 0 }).ToArray());
        result.Add("format 6 count past the end", U16(6, 14, 0, 0x1000, 1000, 1, 2));
        result.Add("format 6 header only", U16(6, 10, 0));

        // The segment arrays do not fit in the table, or the segment count is odd or zero.
        result.Add("format 4 segment count past the end", U16(4, 24, 0, 2000, 0, 0, 0, 0xFFFF, 0, 0, 0, 0));
        result.Add("format 4 odd segment byte count", U16(4, 24, 0, 3, 0, 0, 0, 0xFFFF, 0, 0, 0, 0));
        result.Add("format 4 zero segments", U16(4, 16, 0, 0, 0, 0, 0));

        // A range offset that points outside the table.
        result.Add("format 4 range offset past the end", U16(4, 24, 0, 2, 0, 0, 0, 0xFFFF, 0, 0, 0, 0x7000));
        result.Add("format 4 range offset wraps", U16(4, 24, 0, 2, 0, 0, 0, 0xFFFF, 0, 0, 0, 0xFFFE));

        // Group counts that the table cannot hold.
        result.Add("format 12 group count past the end", U16(12, 0, 0, 0, 0, 0, 0, 0xFFFF, 0, 0, 0, 0, 0, 0, 0));
        result.Add("format 12 zero groups", U16(12, 0, 0, 16, 0, 0, 0, 0, 0, 0));
        result.Add("format 13 group count past the end", U16(13, 0, 0, 0, 0, 0, 0, 0x7FFF, 0xFFFF, 0, 0, 0, 0, 0, 0));
        result.Add("format 10 count past the end", U16(10, 0, 0, 24, 0, 0, 2, 0, 0xFFFF, 0xFFFF, 1, 2));
        result.Add("format 2 without its sub-headers", U16(2, 100, 0).Concat(new byte[] { 0, 0, 0, 0 }).ToArray());

        // A format that is not read.
        result.Add("format 14", U16(14, 0, 0, 10, 0, 0, 0, 0));
        result.Add("format 99", U16(99, 0, 0));

        return result;
    }

    [Test]
    public async Task ARecordWhoseOffsetIsOutsideTheTableFindsNothing()
    {
        byte[] cmap = [0, 0, 0, 1, 0, 3, 0, 1, 0, 0, 0xFF, 0xFF];
        FontProgram font = Load(cmap);

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 1, 65, out _)).IsFalse();
    }

    [Test]
    public async Task ARecordThatFailsToParseDoesNotHideALaterRecordOfAnotherEncoding()
    {
        FontProgram font = Load(Cmap((3, 1, U16(99, 0, 0)), (1, 0, Format0(65, 5))));

        await Assert.That(font.TryGetGlyphIdFromCmap(3, 1, 65, out _)).IsFalse();
        await Assert.That(font.TryGetGlyphIdFromCmap(1, 0, 65, out ushort glyphId)).IsTrue();
        await Assert.That(glyphId).IsEqualTo((ushort)5);
    }

    [Test]
    public async Task ManySegmentsOfFormat4AreSearchedWithoutReadingEveryOne()
    {
        // 8000 one-code segments, each mapping its code to glyph 1 by a delta. The answer must be exact at both ends.
        const int segments = 8000;
        List<int> table = [4, 14 + (8 * segments), 0, 2 * segments, 0, 0, 0];
        table.AddRange(Enumerable.Range(0, segments).Select(x => 0x100 + x));
        table.Add(0);
        table.AddRange(Enumerable.Range(0, segments).Select(x => 0x100 + x));
        table.AddRange(Enumerable.Range(0, segments).Select(x => (1 - (0x100 + x)) & 0xFFFF));
        table.AddRange(Enumerable.Repeat(0, segments));
        FontProgram font = Load(Cmap((3, 1, U16([.. table]))));

        int mapped = 0;
        for (int i = 0; i < 1_000_000; i++)
        {
            int code = i % 0x2000;
            bool found = font.TryGetGlyphIdFromCmap(3, 1, code, out ushort glyphId);
            bool expected = code >= 0x100 && code < 0x100 + segments;
            if (found != expected || (found && glyphId != 1))
            {
                throw new InvalidOperationException($"code {code}: {found} {glyphId}");
            }

            mapped += found ? 1 : 0;
        }

        await Assert.That(mapped).IsGreaterThan(0);
    }

    [Test]
    public async Task TheBuiltInEncodingStillReadsFormat4ThroughTheSharedReader()
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath(NamesFont)));
        ushort[] encoding = new ushort[256];

        await Assert.That(font.TryGetBuiltInEncoding(encoding)).IsTrue();
        await Assert.That(font.TryGetGlyphIdFromCmap(3, 0, 0xF041, out ushort glyphId)).IsTrue();
        await Assert.That(encoding[0x41]).IsEqualTo(glyphId);
    }

    private static FontProgram Load(byte[] cmap)
        => FontProgram.Load(SfntRewriter.Replace(File.ReadAllBytes(TestFonts.GetPath(NamesFont)), "cmap", cmap));

    // A cmap table: version 0, one encoding record for each subtable, then the subtables.
    private static byte[] Cmap(params (int Platform, int Encoding, byte[] Subtable)[] subtables)
    {
        List<byte> result = [.. U16(0, subtables.Length)];
        int offset = 4 + (8 * subtables.Length);
        foreach ((int platform, int encoding, byte[] subtable) in subtables)
        {
            result.AddRange(U16(platform, encoding, offset >> 16, offset & 0xFFFF));
            offset += subtable.Length;
        }

        foreach ((_, _, byte[] subtable) in subtables)
        {
            result.AddRange(subtable);
        }

        return [.. result];
    }

    private static byte[] Format0(int code, int glyph)
    {
        byte[] result = new byte[262];
        result[result.Length - 256 + code] = (byte)glyph;
        return result;
    }

    private static byte[] Format10(int start, params int[] glyphs)
        => [.. U16(10, 0, 0, 20 + (2 * glyphs.Length), 0, 0), .. U16(start >> 16, start & 0xFFFF, 0, glyphs.Length), .. U16(glyphs)];

    private static byte[] Format12(params (long Start, long End, long Glyph)[] groups)
    {
        List<int> words = [12, 0, 0, 16 + (12 * groups.Length), 0, 0, 0, groups.Length];
        foreach ((long start, long end, long glyph) in groups)
        {
            foreach (long value in (long[])[start, end, glyph])
            {
                words.Add((int)(value >> 16));
                words.Add((int)(value & 0xFFFF));
            }
        }

        return U16([.. words]);
    }

    private static byte[] U16(params int[] values)
    {
        byte[] result = new byte[values.Length * 2];
        for (int i = 0; i < values.Length; i++)
        {
            result[2 * i] = (byte)(values[i] >> 8);
            result[(2 * i) + 1] = (byte)values[i];
        }

        return result;
    }

    private static Fixture ReadFixture()
        => JsonSerializer.Deserialize<Fixture>(
            File.ReadAllText(TestFonts.GetPath("FontPrograms/cmap/lookups.json")),
            FixtureJsonOptions)!;

    internal sealed class Fixture
    {
        public FixtureFont[] Fonts { get; set; } = [];
    }

    internal sealed class FixtureFont
    {
        public string Font { get; set; } = string.Empty;

        public FixtureSubtable[] Subtables { get; set; } = [];
    }

    internal sealed class FixtureSubtable
    {
        public int Platform { get; set; }

        public int Encoding { get; set; }

        public int Format { get; set; }

        public int[][] Map { get; set; } = [];
    }
}
