// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontSubsetting;

/// <summary>
/// What a subset is, for every format that <see cref="FontSubsetter"/> reads: a kept glyph keeps its ID, its outline and its
/// advance, every other glyph is empty unless a kept glyph needs it, and the file is a font file that checks out.
/// </summary>
public class FontSubsetterTests
{
    private const string OpenType = "SourceSansPro-Regular.otf";

    private static readonly string[] OpenTypeCffFonts =
    [
        OpenType,
        "FDArrayTest257.otf",
        "NoOs2-BoldCondensedItalic.otf",
        "FontPrograms/cff-seac.otf",
        "FontPrograms/cff-seac-widths.otf",
        "FontPrograms/cff-type2.otf",
        "FontPrograms/cff-type2-widths.otf",
        "FontPrograms/cff-cid-fdselect0.otf",
        "FontPrograms/cff-cid-fdselect3.otf",
        "FontPrograms/cff-cid-top-identity.otf",
        "FontPrograms/cff-cid-upem2048.otf",
        "FontPrograms/cff-encoding-format0.otf",
        "FontPrograms/cff-encoding-format1.otf",
        "FontPrograms/cff-expert.otf",
        "FontPrograms/cff-expertsubset.otf",
        "FontPrograms/cff-isoadobe.otf",
        "FontPrograms/cff-paint-outline.otf",
        "FontPrograms/cff-paint-stroked.otf",
        "FontPrograms/cff-paint-stroked-real.otf",
    ];

    private static readonly string[] BareCffFonts =
    [
        "FontPrograms/bare-cff-cid-fdselect0.cff",
        "FontPrograms/bare-cff-cid-fdselect3.cff",
        "FontPrograms/bare-cff-paint-stroked.cff",
        "FontPrograms/bare-cff-seac-widths.cff",
        "FontPrograms/bare-cff-type2-widths.cff",
        "FontPrograms/bare-FDArrayTest257.cff",
        "FontPrograms/bare-SourceSansPro-Regular.cff",
    ];

    /// <summary>
    /// Gets every font that the tests subset.
    /// </summary>
    public static IEnumerable<object[]> Fonts
        => OpenTypeCffFonts.Concat(BareCffFonts).Select(font => new object[] { font });

    /// <summary>
    /// Gets the fonts that are sfnt files.
    /// </summary>
    public static IEnumerable<object[]> SfntFonts
        => OpenTypeCffFonts.Select(font => new object[] { font });

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task AKeptGlyphKeepsItsIdItsOutlineAndItsAdvance(string fontFile)
    {
        byte[] font = ReadFont(fontFile);
        int glyphCount = FontProgram.Load(font).GlyphCount;
        List<string> failures = [];

        // Nothing, one glyph, the last, and every few glyphs from several starts.
        List<ushort[]> choices = [[], [1], [(ushort)(glyphCount - 1)], SubsetComparison.Pick(glyphCount, 0, 1)];
        for (int start = 1; start <= 3; start++)
        {
            choices.Add(SubsetComparison.Pick(glyphCount, start, 3 + (start * 4)));
        }

        foreach (ushort[] requested in choices.Where(c => c.All(id => id < glyphCount)))
        {
            byte[] subset = FontSubsetter.Subset(font, requested);
            foreach (string difference in SubsetComparison.Compare(font, subset, requested, out _))
            {
                failures.Add($"{requested.Length} glyphs from {requested.FirstOrDefault()}: {difference}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(SfntFonts))]
    public async Task ASubsetOfAnSfntFontIsAnSfntFileThatChecksOut(string fontFile)
    {
        byte[] font = ReadFont(fontFile);

        byte[] subset = FontSubsetter.Subset(font, GlyphIds(font, 1, 2));

        await Assert.That(SfntFileChecks.Verify(subset)).IsEmpty();
        await Assert.That(subset.AsSpan(0, 4).SequenceEqual(font.AsSpan(0, 4))).IsTrue();
    }

    [Test]
    [Arguments(OpenType)]
    public async Task ADefaultSubsetKeepsTheTablesOfAPdfReaderAndDropsTheOthers(string fontFile)
    {
        byte[] font = ReadFont(fontFile);
        List<string> before = SfntFileChecks.GetTags(font);

        List<string> tags = SfntFileChecks.GetTags(FontSubsetter.Subset(font, [1]));

        await Assert.That(before).Contains("GSUB");
        await Assert.That(before).Contains("GPOS");
        await Assert.That(tags).IsEquivalentTo(before.Where(tag => FontSubsetOptions.DefaultTables.Contains(tag)));
        await Assert.That(tags).Contains("OS/2");
        await Assert.That(tags).Contains("cmap");
        await Assert.That(tags).Contains("head");
        await Assert.That(tags).DoesNotContain("GSUB");
        await Assert.That(tags).DoesNotContain("GPOS");
    }

    [Test]
    public async Task TheDefaultTablesNameTheTablesOfAPdfReader()
    {
        string[] expected = ["CFF ", "OS/2", "cmap", "cvt ", "fpgm", "glyf", "head", "hhea", "hmtx", "loca", "maxp", "name", "post", "prep"];

        await Assert.That(FontSubsetOptions.DefaultTables).IsEquivalentTo(expected);
    }

    [Test]
    [Arguments(OpenType, new[] { "CFF ", "head", "maxp" })]
    public async Task TheTablesOptionChoosesTheTablesAndTheOutlineTablesStay(string fontFile, string[] outlineTables)
    {
        byte[] font = ReadFont(fontFile);

        List<string> some = SfntFileChecks.GetTags(FontSubsetter.Subset(font, [1], new FontSubsetOptions { Tables = ["GSUB", "name", "cmap", "XXXX"] }));
        List<string> none = SfntFileChecks.GetTags(FontSubsetter.Subset(font, [1], new FontSubsetOptions { Tables = [] }));

        await Assert.That(some).IsEquivalentTo(outlineTables.Concat(["GSUB", "cmap", "name"]));
        await Assert.That(none).IsEquivalentTo(outlineTables);
    }

    [Test]
    [Arguments("CFF")]
    [Arguments("cmapp")]
    [Arguments("")]
    [Arguments("cmäp")]
    public async Task ATableTagThatIsNotFourPrintableCharactersIsRefused(string tag)
    {
        byte[] font = ReadFont(OpenType);

        await Assert.That(() => FontSubsetter.Subset(font, [1], new FontSubsetOptions { Tables = [tag] })).Throws<ArgumentException>();
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task ASubsetOfASubsetIsTheSubset(string fontFile)
    {
        byte[] font = ReadFont(fontFile);
        ushort[] glyphs = GlyphIds(font, 5, 6, 7, 1);
        byte[] first = FontSubsetter.Subset(font, glyphs);

        byte[] second = FontSubsetter.Subset(first, glyphs);

        await Assert.That(second.AsSpan().SequenceEqual(first)).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task ASliceOfALargerBufferSubsetsAsTheSameBytesDo(string fontFile)
    {
        byte[] font = ReadFont(fontFile);
        byte[] larger = new byte[font.Length + 1000];
        font.CopyTo(larger, 500);

        ushort[] glyphs = GlyphIds(font, 1, 2, 3);

        byte[] subset = FontSubsetter.Subset(new ReadOnlyMemory<byte>(larger, 500, font.Length), glyphs);

        await Assert.That(subset.AsSpan().SequenceEqual(FontSubsetter.Subset(font, glyphs))).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task TheOrderAndRepeatsOfTheGlyphIdsDoNotMatter(string fontFile)
    {
        byte[] font = ReadFont(fontFile);

        byte[] subset = FontSubsetter.Subset(font, GlyphIds(font, 4, 4, 0, 1, 4, 2));

        await Assert.That(subset.AsSpan().SequenceEqual(FontSubsetter.Subset(font, GlyphIds(font, 1, 2, 4)))).IsTrue();
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task AGlyphIdPastTheLastGlyphIsRefused(string fontFile)
    {
        byte[] font = ReadFont(fontFile);
        int glyphCount = FontProgram.Load(font).GlyphCount;

        await Assert.That(() => FontSubsetter.Subset(font, [(ushort)glyphCount])).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => FontSubsetter.Subset(font, [ushort.MaxValue])).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task NoGlyphListIsRefused()
    {
        byte[] font = ReadFont(OpenType);

        await Assert.That(() => FontSubsetter.Subset(font, null!)).Throws<ArgumentNullException>();
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task ALimitOnRetainedBytesRefusesASubsetThatNeedsMoreAndTheReportedSizeIsEnough(string fontFile)
    {
        byte[] font = ReadFont(fontFile);
        ushort[] glyphs = GlyphIds(font, 1, 2);
        byte[] unlimited = FontSubsetter.Subset(font, glyphs);

        // The data alone fills a limit of its own length, so anything that the subsetter allocates passes it.
        FontProgramTooLargeException exception = (await Assert.That(
            () => FontSubsetter.Subset(font, glyphs, new FontSubsetOptions { ProgramOptions = new FontProgramOptions { MaxRetainedBytes = font.Length } }))
            .Throws<FontProgramTooLargeException>())!;
        await Assert.That(exception.MaxRetainedBytes).IsEqualTo(font.Length);
        await Assert.That(exception.RetainedByteCount).IsGreaterThan(font.Length);

        // Setting the limit to what each refusal reports reaches a limit that passes, and it gives the same bytes.
        long limit = exception.RetainedByteCount;
        byte[]? limited = null;
        for (int attempt = 0; attempt < 12 && limited is null; attempt++)
        {
            try
            {
                limited = FontSubsetter.Subset(font, glyphs, new FontSubsetOptions { ProgramOptions = new FontProgramOptions { MaxRetainedBytes = limit } });
            }
            catch (FontProgramTooLargeException e)
            {
                await Assert.That(e.RetainedByteCount).IsGreaterThan(limit);
                limit = e.RetainedByteCount;
            }
        }

        await Assert.That(limited).IsNotNull();
        await Assert.That(limited!.AsSpan().SequenceEqual(unlimited)).IsTrue();

        // The array that the subsetter returns counts too: a limit that is a byte short of the figure refuses it.
        await Assert.That(limit).IsGreaterThanOrEqualTo(font.Length + 24L + unlimited.Length);
        await Assert.That(() => FontSubsetter.Subset(font, glyphs, new FontSubsetOptions { ProgramOptions = new FontProgramOptions { MaxRetainedBytes = limit - 1 } }))
            .Throws<FontProgramTooLargeException>();
    }

    [Test]
    public async Task AnUnsupportedFormatIsRefusedAsAnInvalidFont()
    {
        byte[] woff = File.ReadAllBytes(TestFonts.OpenSansFileWoff1);
        byte[] cff2 = File.ReadAllBytes(TestFonts.SourceSans3VariableCff2File);
        byte[] collection = File.ReadAllBytes(TestFonts.GetPath("Helvetica.ttc"));

        await Assert.That(() => FontSubsetter.Subset(woff, [1])).Throws<InvalidFontFileException>();
        await Assert.That(() => FontSubsetter.Subset(cff2, [1])).Throws<InvalidFontFileException>();
        await Assert.That(() => FontSubsetter.Subset(collection, [1])).Throws<InvalidFontFileException>();
        await Assert.That(() => FontSubsetter.Subset(Array.Empty<byte>(), [1])).Throws<InvalidFontFileException>();
    }

    [Test]
    [Arguments(OpenType)]
    [Arguments("FDArrayTest257.otf")]
    [Arguments("FontPrograms/cff-seac.otf")]
    [Arguments("FontPrograms/bare-cff-cid-fdselect3.cff")]
    public async Task ATruncatedFontIsRefusedAsAnInvalidFontOrSubsetsAsFarAsItGoes(string fontFile)
    {
        byte[] font = ReadFont(fontFile);
        List<string> failures = [];

        for (int length = 0; length < font.Length; length += Math.Max(1, font.Length / 97))
        {
            try
            {
                FontSubsetter.Subset(font.AsMemory(0, length), [1]);
            }
            catch (InvalidFontFileException)
            {
            }
            catch (Exception e)
            {
                failures.Add($"{length} bytes: {e.GetType().Name} {e.Message}");
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    private static byte[] ReadFont(string fontFile) => File.ReadAllBytes(TestFonts.GetPath(fontFile));

    // The glyph IDs of a font that may have only three glyphs: each number counts around the glyphs of the font.
    private static ushort[] GlyphIds(byte[] font, params int[] ids)
    {
        int glyphCount = FontProgram.Load(font).GlyphCount;
        return [.. ids.Select(id => (ushort)(id % glyphCount))];
    }
}
