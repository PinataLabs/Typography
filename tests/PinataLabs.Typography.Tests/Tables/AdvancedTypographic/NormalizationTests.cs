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

    // What a font without GPOS does with a mark shows the shaper of the script: the default shaper places the mark,
    // the universal shaper only takes its advance away, and the Indic shaper leaves it. HarfBuzz gives these scripts
    // to its universal shaper, which also joins their letters. UniversalShaper does not join, so the library keeps
    // them with its Arabic shaper, which places the mark.
    private static readonly string[] ScriptsWithAnotherShaper =
    [
        "Mandaic", "Manichaean", "Mongolian", "Nko", "Phags_Pa", "Psalter_Pahlavi",
    ];

    [Test]
    public async Task EveryScriptHasTheShaperThatHarfBuzzGivesIt()
    {
        List<string> different = [];
        foreach (PositionedCase expected in HarfBuzz.Scripts)
        {
            if (!Place(expected).SequenceEqual(expected.Glyphs.Select(g => (g.Id, g.X, g.Y))))
            {
                different.Add(expected.Name);
            }
        }

        await Assert.That(different.Except(ScriptsWithAnotherShaper)).IsEmpty();
        await Assert.That(ScriptsWithAnotherShaper.Except(different)).IsEmpty();
    }

    // Gives the glyph ID and the place of each glyph, in font units from the start of the line.
    private static (int Id, int X, int Y)[] Place(PositionedCase text)
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(text.Font));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.Text.AsSpan(), new TextOptions(font));

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
