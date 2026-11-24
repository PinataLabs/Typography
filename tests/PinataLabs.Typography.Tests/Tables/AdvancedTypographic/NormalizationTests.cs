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
/// and each run of marks is sorted, as in HarfBuzz.
/// </summary>
public class NormalizationTests
{
    private const string CaseFont = "Normalization";

    private const string BaseFont = "NormalizationBase";

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
        Font font = new FontCollection().Add(TestFonts.GetPath(HarfBuzz.Font)).CreateFont(12);

        await Assert.That(Shape(font, expected.Text)).IsEquivalentTo(expected.Glyphs, CollectionOrdering.Matching);
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
            if (IsKnownDifference(text))
            {
                continue;
            }

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

    private static (int CodePoint, string Font)[] ShapeWithFallback(string text)
    {
        FontCollection collection = new();
        Font primary = collection.Add(TestFonts.NormalizationBaseFile).CreateFont(12);
        FontFamily fallback = collection.Add(TestFonts.GetPath(HarfBuzz.Font));
        TextOptions options = new(primary) { FallbackFontFamilies = [fallback] };

        return TextLayout.GenerateLayout(text.AsSpan(), options)
            .Select(g => (g.Glyph.GlyphMetrics.CodePoint.Value, g.Glyph.GlyphMetrics.FontMetrics.Description.FontFamilyInvariantCulture))
            .ToArray();
    }

    // HarfBuzz composes a Hebrew letter and point to the presentation form (U+FB1D to U+FB4E) when the font has no
    // GPOS 'mark' feature, although Unicode excludes those forms from composition. The library does not.
    // HarfBuzz shapes Dives Akuru (U+11900 to U+1195F) with its universal shaper, and the library with its default one.
    private static bool IsKnownDifference(string text)
    {
        int first = char.ConvertToUtf32(text, 0);
        bool hebrewLetter = first is (>= 0x05D0 and <= 0x05F2) or (>= 0xFB2A and <= 0xFB4A);
        bool hebrewPoint = text[^1] is >= 'ִ' and <= 'ׂ';
        return (hebrewLetter && hebrewPoint) || first is >= 0x11900 and <= 0x1195F;
    }

    private static int[] Shape(Font font, string text)
        => TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font))
            .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
            .ToArray();

    private sealed class Reference
    {
        public string Font { get; set; } = string.Empty;

        public ReferenceCase[] Cases { get; set; } = [];

        public Dictionary<string, string> ScanFonts { get; set; } = [];

        public JsonElement[][] Scan { get; set; } = [];
    }

    private sealed class ReferenceCase
    {
        public string Name { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int[] Glyphs { get; set; } = [];
    }
}
