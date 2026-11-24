// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Loads real fonts after their optional tables are removed or damaged, the way a PDF producer leaves a subset,
/// and checks that every glyph keeps its outline and that the program says what it did without.
/// </summary>
public class StrippedSfntTests
{
    private const string TrueTypeFont = "OpenSans-Regular.ttf";
    private const string CffFont = "SourceSansPro-Regular.otf";
    private const string NamesFont = "FontPrograms/tt-names.ttf";

    private const OptionalFontTables AllTables = OptionalFontTables.Name | OptionalFontTables.Os2 | OptionalFontTables.Post
        | OptionalFontTables.Cmap | OptionalFontTables.Hhea | OptionalFontTables.Hmtx;

    [Test]
    [Arguments(TrueTypeFont)]
    [Arguments(NamesFont)]
    [Arguments(CffFont)]
    public async Task AFullFontReportsNoMissingAndNoMalformedTable(string fontFile)
    {
        FontProgram font = FontProgram.Load(Read(fontFile));

        await Assert.That(font.MissingOptionalTables).IsEqualTo(OptionalFontTables.None);
        await Assert.That(font.MalformedOptionalTables).IsEqualTo(OptionalFontTables.None);
    }

    [Test]
    [Arguments(TrueTypeFont)]
    [Arguments(NamesFont)]
    [Arguments(CffFont)]
    public async Task WithoutNameOs2AndPostAFontLoadsWithTheSameOutlines(string fontFile)
    {
        byte[] full = Read(fontFile);
        byte[] stripped = SfntRewriter.Remove(full, "name", "OS/2", "post");

        FontProgram font = FontProgram.Load(stripped);

        await Assert.That(font.MissingOptionalTables).IsEqualTo(OptionalFontTables.Name | OptionalFontTables.Os2 | OptionalFontTables.Post);
        await Assert.That(font.MalformedOptionalTables).IsEqualTo(OptionalFontTables.None);
        await AssertSameOutlines(FontProgram.Load(full), font);
    }

    [Test]
    [Arguments(TrueTypeFont)]
    [Arguments(NamesFont)]
    [Arguments(CffFont)]
    public async Task WithoutCmapAFontLoadsWithTheSameOutlinesAndNoBuiltInEncodingFromTheTable(string fontFile)
    {
        byte[] full = Read(fontFile);

        FontProgram font = FontProgram.Load(SfntRewriter.Remove(full, "cmap"));

        await Assert.That(font.MissingOptionalTables).IsEqualTo(OptionalFontTables.Cmap);
        await Assert.That(font.MalformedOptionalTables).IsEqualTo(OptionalFontTables.None);
        await AssertSameOutlines(FontProgram.Load(full), font);

        // A TrueType program takes its encoding from 'cmap'. A CFF program takes it from the CFF data.
        await Assert.That(font.TryGetBuiltInEncoding(new ushort[256])).IsEqualTo(font.Format == FontProgramFormat.Cff);
    }

    [Test]
    [Arguments(TrueTypeFont)]
    [Arguments(NamesFont)]
    [Arguments(CffFont)]
    public async Task WithoutHheaAndHmtxAFontLoadsWithTheSameOutlines(string fontFile)
    {
        byte[] full = Read(fontFile);

        FontProgram font = FontProgram.Load(SfntRewriter.Remove(full, "hhea", "hmtx"));

        await Assert.That(font.MissingOptionalTables).IsEqualTo(OptionalFontTables.Hhea | OptionalFontTables.Hmtx);
        await Assert.That(font.MalformedOptionalTables).IsEqualTo(OptionalFontTables.None);
        await AssertSameOutlines(FontProgram.Load(full), font);

        // A TrueType program has no other source of advances. A CFF program reads them from its charstrings.
        await Assert.That(font.TryGetAdvanceWidth(1, out _)).IsEqualTo(font.Format == FontProgramFormat.Cff);
    }

    [Test]
    [Arguments(TrueTypeFont, "hhea", OptionalFontTables.Hhea)]
    [Arguments(NamesFont, "hhea", OptionalFontTables.Hhea)]
    [Arguments(TrueTypeFont, "hmtx", OptionalFontTables.Hmtx)]
    [Arguments(NamesFont, "hmtx", OptionalFontTables.Hmtx)]
    [Arguments(CffFont, "hhea", OptionalFontTables.Hhea)]
    [Arguments(CffFont, "hmtx", OptionalFontTables.Hmtx)]
    public async Task WithoutOneOfHheaAndHmtxAFontLoadsAndNamesTheMissingOne(string fontFile, string tag, OptionalFontTables expected)
    {
        byte[] full = Read(fontFile);

        FontProgram font = FontProgram.Load(SfntRewriter.Remove(full, tag));

        await Assert.That(font.MissingOptionalTables).IsEqualTo(expected);
        await AssertSameOutlines(FontProgram.Load(full), font);
    }

    [Test]
    [Arguments(TrueTypeFont)]
    [Arguments(NamesFont)]
    [Arguments(CffFont)]
    public async Task WithoutAnyOptionalTableAFontLoadsWithTheSameOutlines(string fontFile)
    {
        byte[] full = Read(fontFile);

        FontProgram font = FontProgram.Load(SfntRewriter.Remove(full, "name", "OS/2", "post", "cmap", "hhea", "hmtx"));

        await Assert.That(font.MissingOptionalTables).IsEqualTo(AllTables);
        await Assert.That(font.MalformedOptionalTables).IsEqualTo(OptionalFontTables.None);
        await AssertSameOutlines(FontProgram.Load(full), font);
    }

    [Test]
    [Arguments(TrueTypeFont, "name", new byte[] { 0 }, OptionalFontTables.Name)]
    [Arguments(NamesFont, "name", new byte[] { 0 }, OptionalFontTables.Name)]
    [Arguments(TrueTypeFont, "name", new byte[] { 0, 0, 0, 3, 0, 6 }, OptionalFontTables.Name)]
    [Arguments(NamesFont, "name", new byte[] { 0, 0, 0, 3, 0, 6 }, OptionalFontTables.Name)]
    [Arguments(TrueTypeFont, "OS/2", new byte[] { 0, 4, 1, 2, 3 }, OptionalFontTables.Os2)]
    [Arguments(NamesFont, "OS/2", new byte[] { 0, 4, 1, 2, 3 }, OptionalFontTables.Os2)]
    [Arguments(TrueTypeFont, "post", new byte[] { 0, 2, 0, 0 }, OptionalFontTables.Post)]
    [Arguments(NamesFont, "post", new byte[] { 0, 2, 0, 0 }, OptionalFontTables.Post)]
    [Arguments(TrueTypeFont, "cmap", new byte[] { 0, 0 }, OptionalFontTables.Cmap)]
    [Arguments(NamesFont, "cmap", new byte[] { 0, 0 }, OptionalFontTables.Cmap)]
    [Arguments(TrueTypeFont, "cmap", new byte[] { 0, 0, 0xFF, 0xFF, 0, 3, 0, 0, 0, 0, 0, 4 }, OptionalFontTables.Cmap)]
    [Arguments(NamesFont, "cmap", new byte[] { 0, 0, 0xFF, 0xFF, 0, 3, 0, 0, 0, 0, 0, 4 }, OptionalFontTables.Cmap)]
    [Arguments(TrueTypeFont, "hhea", new byte[] { 0, 1, 0, 0, 1, 2, 3 }, OptionalFontTables.Hhea)]
    [Arguments(NamesFont, "hhea", new byte[] { 0, 1, 0, 0, 1, 2, 3 }, OptionalFontTables.Hhea)]
    [Arguments(TrueTypeFont, "hmtx", new byte[] { 1, 2 }, OptionalFontTables.Hmtx)]
    [Arguments(NamesFont, "hmtx", new byte[] { 1, 2 }, OptionalFontTables.Hmtx)]
    [Arguments(CffFont, "name", new byte[] { 0 }, OptionalFontTables.Name)]
    [Arguments(CffFont, "OS/2", new byte[] { 0, 4, 1, 2, 3 }, OptionalFontTables.Os2)]
    [Arguments(CffFont, "post", new byte[] { 0, 2, 0, 0 }, OptionalFontTables.Post)]
    [Arguments(CffFont, "cmap", new byte[] { 0, 0 }, OptionalFontTables.Cmap)]
    [Arguments(CffFont, "hhea", new byte[] { 0, 1, 0, 0, 1, 2, 3 }, OptionalFontTables.Hhea)]
    [Arguments(CffFont, "hmtx", new byte[] { 1, 2 }, OptionalFontTables.Hmtx)]
    public async Task AMalformedOptionalTableIsIgnoredAndNamed(string fontFile, string tag, byte[] damaged, OptionalFontTables expected)
    {
        byte[] full = Read(fontFile);

        FontProgram font = FontProgram.Load(SfntRewriter.Replace(full, tag, damaged));

        await Assert.That(font.MalformedOptionalTables).IsEqualTo(expected);
        await Assert.That(font.MissingOptionalTables).IsEqualTo(OptionalFontTables.None);
        await AssertSameOutlines(FontProgram.Load(full), font);
    }

    [Test]
    [Arguments(TrueTypeFont)]
    [Arguments(NamesFont)]
    [Arguments(CffFont)]
    public async Task AHheaThatSaysTheFontHasNoMetricsIsMalformed(string fontFile)
    {
        byte[] full = Read(fontFile);
        byte[] hhea = SfntRewriter.GetTable(full, "hhea")!;
        hhea[34] = 0;
        hhea[35] = 0;

        FontProgram font = FontProgram.Load(SfntRewriter.Replace(full, "hhea", hhea));

        await Assert.That(font.MalformedOptionalTables).IsEqualTo(OptionalFontTables.Hhea);
        await AssertSameOutlines(FontProgram.Load(full), font);
    }

    [Test]
    [Arguments(TrueTypeFont)]
    [Arguments(NamesFont)]
    [Arguments(CffFont)]
    public async Task EveryOptionalTableFilledWithGarbageLoadsWithTheSameOutlines(string fontFile)
    {
        byte[] full = Read(fontFile);
        Dictionary<string, byte[]?> garbage = [];
        foreach (string tag in (string[])["name", "OS/2", "post", "cmap", "hhea", "hmtx"])
        {
            garbage[tag] = Fill(SfntRewriter.GetTable(full, tag)!.Length, 0xFF);
        }

        FontProgram font = FontProgram.Load(SfntRewriter.Rewrite(full, garbage));

        await Assert.That(font.MalformedOptionalTables).IsEqualTo(OptionalFontTables.Name | OptionalFontTables.Post | OptionalFontTables.Cmap);
        await Assert.That(font.MissingOptionalTables).IsEqualTo(OptionalFontTables.None);
        await AssertSameOutlines(FontProgram.Load(full), font);
    }

    [Test]
    public async Task AMalformedPostGivesNoGlyphNamesAndAMalformedCmapGivesNoEncoding()
    {
        byte[] full = Read(NamesFont);
        FontProgram reference = FontProgram.Load(full);
        await Assert.That(reference.GetGlyphName(1)).IsEqualTo("A");
        await Assert.That(reference.TryGetBuiltInEncoding(new ushort[256])).IsTrue();

        FontProgram font = FontProgram.Load(SfntRewriter.Replace(SfntRewriter.Replace(full, "post", [0, 2, 0, 0]), "cmap", [0, 0]));

        await Assert.That(font.GetGlyphName(1)).IsNull();
        await Assert.That(font.TryGetGlyphId("A", out _)).IsFalse();
        await Assert.That(font.TryGetBuiltInEncoding(new ushort[256])).IsFalse();
    }

    [Test]
    public async Task AFontWithoutItsHeadTableStillLoadsAsOpenTypeCff()
    {
        byte[] full = Read(CffFont);

        FontProgram font = FontProgram.Load(SfntRewriter.Remove(full, "head", "maxp"));

        await AssertSameOutlines(FontProgram.Load(full), font);
    }

    [Test]
    [Arguments(TrueTypeFont, "head")]
    [Arguments(TrueTypeFont, "maxp")]
    [Arguments(TrueTypeFont, "loca")]
    [Arguments(TrueTypeFont, "glyf")]
    [Arguments(CffFont, "CFF ")]
    public async Task ARequiredTableThatIsMissingFailsTheLoad(string fontFile, string tag)
    {
        byte[] stripped = SfntRewriter.Remove(Read(fontFile), tag);

        await Assert.That(() => FontProgram.Load(stripped)).Throws<InvalidFontFileException>();
    }

    [Test]
    [Arguments("head")]
    [Arguments("maxp")]
    public async Task ARequiredTableThatIsTruncatedFailsTheLoad(string tag)
    {
        byte[] damaged = SfntRewriter.Replace(Read(TrueTypeFont), tag, [0, 1, 0, 0]);

        await Assert.That(() => FontProgram.Load(damaged)).Throws<InvalidFontFileException>();
    }

    [Test]
    public async Task AStrippedProgramRetainsOnlyItsData()
    {
        byte[] stripped = SfntRewriter.Remove(Read(TrueTypeFont), "name", "OS/2", "post", "cmap", "hhea", "hmtx");

        FontProgram font = FontProgram.Load(stripped);

        await Assert.That(font.RetainedByteCount).IsEqualTo(stripped.Length);
    }

    private static byte[] Read(string fontFile) => File.ReadAllBytes(TestFonts.GetPath(fontFile));

    private static byte[] Fill(int length, byte value)
    {
        byte[] bytes = new byte[length];
        Array.Fill(bytes, value);
        return bytes;
    }

    private static async Task AssertSameOutlines(FontProgram expected, FontProgram actual)
    {
        await Assert.That(actual.Format).IsEqualTo(expected.Format);
        await Assert.That(actual.GlyphCount).IsEqualTo(expected.GlyphCount);
        await Assert.That(actual.FontMatrix).IsEqualTo(expected.FontMatrix);

        int differing = 0;
        string? first = null;
        for (int glyph = 0; glyph < expected.GlyphCount; glyph++)
        {
            GlyphOutline expectedOutline = new();
            GlyphOutline actualOutline = new();
            bool expectedOk = expected.TryWriteOutline((ushort)glyph, ref expectedOutline);
            bool actualOk = actual.TryWriteOutline((ushort)glyph, ref actualOutline);
            if (expectedOk != actualOk || expectedOutline.ToString() != actualOutline.ToString())
            {
                differing++;
                first ??= $"glyph {glyph}: expected {expectedOk} '{expectedOutline}' but was {actualOk} '{actualOutline}'";
            }
        }

        await Assert.That(first).IsNull();
        await Assert.That(differing).IsEqualTo(0);
    }
}
