// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// Before GSUB, the characters of the text are composed and decomposed to the glyphs that the font has,
/// and each run of marks is sorted, as in HarfBuzz. A font without GPOS also places its marks as HarfBuzz does.
/// </summary>
public class NormalizationTests
{
    private const string CaseFont = "Normalization";

    private const string BaseFont = "NormalizationBase";

    private const string BaseKernFont = "NormalizationBaseKern";

    private const string HangulFont = "NormalizationHangul";

    private const string HangulLeadFont = "NormalizationHangulLead";

    // TextShaperHangulNotdef.ttf has U+AC00 and lacks the tone marks, and its .notdef glyph is 0 wide.
    private const string HangulNotdefFont = "TextShaperHangulNotdef";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.NormalizationHarfBuzzReference),
        Options)!;

    public static IEnumerable<string> CaseNames() => HarfBuzz.Cases.Select(c => c.Name);

    [Test]
    [MethodDataSource(nameof(CaseNames))]
    public async Task TextIsMappedToTheGlyphsThatHarfBuzzGives(string name)
    {
        ReferenceCase expected = HarfBuzz.Cases.Single(c => c.Name == name);
        Font font = new FontCollection().Add(TestFonts.GetPath(expected.Font)).CreateFont(12);

        await Assert.That(Shape(font, expected.Text)).IsEquivalentTo(expected.Glyphs, CollectionOrdering.Matching);
    }

    public static IEnumerable<string> PositionedCaseNames() => HarfBuzz.Positioned.Select(c => c.Name);

    // A space that the font lacks takes the glyph of U+0020 and a width of its own, and a mark takes no width
    // in a font that has no GPOS table. So these cases compare the place of each glyph, in font units.
    [Test]
    [MethodDataSource(nameof(PositionedCaseNames))]
    public async Task GlyphsAreDrawnWhereHarfBuzzDrawsThem(string name)
    {
        PositionedCase expected = HarfBuzz.Positioned.Single(c => c.Name == name);

        await Assert.That(Place(expected)).IsEquivalentTo(expected.Glyphs.Select(g => (g.Id, g.X, g.Y)), CollectionOrdering.Matching);
    }

    // Text runs that have the same font share its glyphs. Each glyph must still be positioned once.
    [Test]
    [MethodDataSource(nameof(PositionedCaseNames))]
    public async Task GlyphsOfTwoTextRunsAreDrawnWhereHarfBuzzDrawsThem(string name)
    {
        PositionedCase expected = HarfBuzz.Positioned.Single(c => c.Name == name);
        (int Id, int X, int Y)[] actual = Place(expected, [new TextRun { Start = 0, End = 1 }]);

        await Assert.That(actual).IsEquivalentTo(expected.Glyphs.Select(g => (g.Id, g.X, g.Y)), CollectionOrdering.Matching);
    }

    // What a font without GPOS does with a mark shows the shaper of the script: the default shaper places the mark,
    // the universal shaper only takes its advance away, and the Indic shaper leaves it.
    [Test]
    public async Task EveryScriptHasTheShaperThatHarfBuzzGivesIt()
        => await Assert.That(NamesOfDifferentCases(HarfBuzz.Scripts)).IsEmpty();

    // Only the shapers that place a mark without GPOS also kern with the `kern` table of the font.
    [Test]
    public async Task EveryScriptIsKernedByTheKernTableAsInHarfBuzz()
        => await Assert.That(NamesOfDifferentCases(HarfBuzz.KernedScripts)).IsEmpty();

    private static List<string> NamesOfDifferentCases(PositionedCase[] cases)
        => cases
            .Where(expected => !Place(expected).SequenceEqual(expected.Glyphs.Select(g => (g.Id, g.X, g.Y))))
            .Select(expected => expected.Name)
            .ToList();

    // Gives the glyph ID and the place of each glyph, in font units from the start of the line.
    private static (int Id, int X, int Y)[] Place(PositionedCase text, IReadOnlyList<TextRun>? textRuns = null)
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(text.Font));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.Text.AsSpan(), new TextOptions(font) { TextRuns = textRuns ?? [] });

        return layout
            .Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
            .ToArray();
    }

    // The glyph of U+0020 draws a no-break space that the font lacks. The line must still not break there.
    [Test]
    [Arguments("aaa aaa", 1)]
    [Arguments("aaa aaa", 2)]
    public async Task SpaceThatTheFontLacksBreaksTheLineAsItsCharacterDoes(string text, int lines)
    {
        Font font = new FontCollection().Add(TestFonts.GetPath("NormalizationSpace.ttf")).CreateFont(10);

        await Assert.That(TextMeasurer.CountLines(text, new TextOptions(font) { Dpi = 72, WrappingLength = 30 })).IsEqualTo(lines);
    }

    // Each row is one character with a canonical decomposition, its parts, or two marks after a base.
    [Test]
    public async Task EveryCanonicalMappingGivesTheGlyphsThatHarfBuzzGives()
    {
        Dictionary<string, Font> fonts = HarfBuzz.ScanFonts.ToDictionary(
            f => f.Key,
            f => new FontCollection().Add(TestFonts.GetPath(f.Value)).CreateFont(12));

        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Scan)
        {
            string text = row[1].GetString()!;
            int[] expected = row[2].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            int[] actual = Shape(fonts[row[0].GetString()!], text);
            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"{row[0]} [{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // A fallback font replaces the glyphs that the primary font lacks, one character at a time.
    // So it must not compose a mark into a base that the primary font has already drawn.
    [Test]
    public async Task FallbackFontKeepsTheMarkOfABaseThatThePrimaryFontHas()
    {
        (int CodePoint, string Font)[] glyphs = ShapeWithFallback("ó");

        await Assert.That(glyphs).IsEquivalentTo([(0x6F, BaseFont), (0x301, CaseFont)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task FallbackFontComposesABaseAndMarkThatThePrimaryFontLacks()
    {
        (int CodePoint, string Font)[] glyphs = ShapeWithFallback("Å");

        await Assert.That(glyphs).IsEquivalentTo([(0xC5, CaseFont)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task MarkThatThePrimaryFontLacksKeepsItsPlaceForTheFallbackFont()
    {
        (int CodePoint, string Font)[] glyphs = ShapeWithFallback("q̣́");

        await Assert.That(glyphs).IsEquivalentTo([(0x71, BaseFont), (0x301, CaseFont), (0x323, BaseFont)], CollectionOrdering.Matching);
    }

    // The Hangul shaper composes a syllable before GSUB, so a fallback font must not compose a vowel into a lead
    // that the primary font has already drawn.
    [Test]
    public async Task FallbackFontKeepsTheVowelOfALeadThatThePrimaryFontHas()
    {
        (int CodePoint, string Font)[] glyphs = ShapeWithFallback("\u1100\u1161", HangulLeadFont, HangulFont);

        await Assert.That(glyphs).IsEquivalentTo([(0x1100, HangulLeadFont), (0x1161, HangulFont)], CollectionOrdering.Matching);
    }

    // The Hangul shaper of each font moves a tone mark that the primary font lacks before its syllable, so the glyph
    // that the fallback font gives the tone mark takes the place that the primary font gave it.
    [Test]
    public async Task ToneMarkThatThePrimaryFontLacksGoesBeforeTheSyllable()
    {
        (int CodePoint, string Font)[] glyphs = ShapeWithFallback("\uAC00\u302E", HangulNotdefFont, HangulFont);

        await Assert.That(glyphs).IsEquivalentTo([(0x302E, HangulFont), (0xAC00, HangulNotdefFont)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ToneMarkGoesBeforeJamoThatTwoFontsDraw()
    {
        (int CodePoint, string Font)[] glyphs = ShapeWithFallback("\u1100\u1161\u302E", HangulLeadFont, HangulFont);

        await Assert.That(glyphs).IsEquivalentTo([(0x302E, HangulFont), (0x1100, HangulLeadFont), (0x1161, HangulFont)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task FallbackFontComposesASyllableThatThePrimaryFontLacks()
    {
        (int CodePoint, string Font)[] glyphs = ShapeWithFallback("\u1100\u1161", BaseFont, HangulFont);

        await Assert.That(glyphs).IsEquivalentTo([(0xAC00, HangulFont)], CollectionOrdering.Matching);
    }

    // The primary font has GPOS and lacks U+0323. The fallback font calls U+0323 a base, so the mark keeps its advance
    // as it does when the fallback font draws the whole text.
    [Test]
    public async Task FallbackFontPositionsItsOwnMark()
    {
        PositionedCase expected = HarfBuzz.Positioned.Single(c => c.Name == "GposKeepsTheAdvanceOfAGdefBase");
        FontFamily primary = new FontCollection().Add(TestFonts.GetPath(BaseKernFont + ".ttf"));
        FontFamily fallback = new FontCollection().Add(TestFonts.GetPath(expected.Font));
        Font font = primary.CreateFont(72 * primary.CreateFont(1).FontMetrics.UnitsPerEm);
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(expected.Text.AsSpan(), new TextOptions(font) { FallbackFontFamilies = [fallback] });

        (int X, int Y)[] actual = layout
            .Select(g => (
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(expected.Glyphs.Select(g => (g.X, g.Y)), CollectionOrdering.Matching);
    }

    private static (int CodePoint, string Font)[] ShapeWithFallback(string text, string primaryFont = BaseFont, string fallbackFont = CaseFont)
    {
        FontCollection collection = new();
        Font primary = collection.Add(TestFonts.GetPath(primaryFont + ".ttf")).CreateFont(12);
        FontFamily fallback = collection.Add(TestFonts.GetPath(fallbackFont + ".ttf"));
        TextOptions options = new(primary) { FallbackFontFamilies = [fallback] };

        return TextLayout.GenerateLayout(text.AsSpan(), options)
            .Select(g => (g.Glyph.GlyphMetrics.CodePoint.Value, g.Glyph.GlyphMetrics.FontMetrics.Description.FontFamilyInvariantCulture))
            .ToArray();
    }

    private static int[] Shape(Font font, string text)
        => TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font))
            .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
            .ToArray();

    private sealed class Reference
    {
        public string Font { get; set; } = string.Empty;

        public ReferenceCase[] Cases { get; set; } = [];

        public PositionedCase[] Positioned { get; set; } = [];

        public PositionedCase[] Scripts { get; set; } = [];

        public PositionedCase[] KernedScripts { get; set; } = [];

        public Dictionary<string, string> ScanFonts { get; set; } = [];

        public JsonElement[][] Scan { get; set; } = [];
    }

    private sealed class ReferenceCase
    {
        public string Name { get; set; } = string.Empty;

        public string Font { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int[] Glyphs { get; set; } = [];
    }

    private sealed class PositionedCase
    {
        public string Name { get; set; } = string.Empty;

        public string Font { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public PositionedGlyph[] Glyphs { get; set; } = [];
    }

    private sealed class PositionedGlyph
    {
        public int Id { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
