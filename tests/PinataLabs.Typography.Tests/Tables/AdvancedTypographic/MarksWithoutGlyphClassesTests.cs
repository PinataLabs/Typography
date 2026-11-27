// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// MarksWithoutGlyphClasses.ttf has no GDEF table. As in HarfBuzz, a glyph then has the class of its character: a
/// nonspacing mark that is not default ignorable is a mark, any other character is a base, and a ligature of components
/// that are not all marks is a ligature. MarksWithoutGlyphClassesFallback.ttf has no GPOS table either, so marks are
/// placed around their base. tests/tools/generate_marks_without_glyph_classes_reference.py writes the fonts and the
/// glyphs that HarfBuzz gives, and says what each text tests.
/// </summary>
public class MarksWithoutGlyphClassesTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GetPath("MarksWithoutGlyphClasses.harfbuzz.json")),
        Options)!;

    // Each row is a font, a text and the glyph ID and place of each glyph, in font units.
    [Test]
    public async Task GlyphsAreWhereHarfBuzzPutsThem()
    {
        Dictionary<string, Font> fonts = [];
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string fontName = row[0].GetString()!;
            if (!fonts.TryGetValue(fontName, out Font? font))
            {
                FontFamily family = new FontCollection().Add(TestFonts.GetPath(fontName));

                // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
                font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
                fonts.Add(fontName, font);
            }

            string text = row[1].GetString()!;
            string expected = string.Join(' ', row[2].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));
            string actual = string.Join(' ', GposVariationReference.Shape(font, text).Select(g => $"{g.Id}@{g.X},{g.Y}"));
            if (actual != expected)
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"{fontName} [{codePoints}] expected [{expected}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // MarksWithoutGlyphClassesFallback.ttf lacks U+034F. HarfBuzz hides its .notdef and places both accents over a. With
    // a fallback font that draws U+034F, the accents of the font go to the same places.
    [Test]
    public async Task MarksGoOnPastADefaultIgnorableGlyphOfAFallbackFont()
    {
        const string fontName = "MarksWithoutGlyphClassesFallback.ttf";
        const string text = "à͏́";
        JsonElement[] row = HarfBuzz.Rows.Single(r => r[0].GetString() == fontName && r[1].GetString() == text);
        string expected = string.Join(' ', row[2].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));

        FontCollection collection = new();
        FontFamily family = collection.Add(TestFonts.GetPath(fontName));
        TextOptions options = new(family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm))
        {
            FallbackFontFamilies = [collection.Add(TestFonts.GetPath("MarksWithoutGlyphClasses.ttf"))],
        };

        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        string actual = string.Join(' ', layout.Select(g =>
        {
            GlyphMetrics metrics = g.Glyph.GlyphMetrics;
            float x = g.PenLocation.X - layout[0].PenLocation.X + metrics.Offset.X;
            return $"{metrics.GlyphId}@{MathF.Round(x)},{MathF.Round(metrics.Offset.Y)}";
        }));

        await Assert.That(actual).IsEqualTo(expected);
    }

    // A cluster with a base of one font and marks of another. Neither font has GPOS, and each alone places the .notdef of
    // the mark that it lacks where the other font places the glyph, in rows of the reference. A mark of either font goes
    // to that place, on the base of the other font and on the marks before it. P is a glyph of the font and F a glyph of
    // the fallback font. Places are in units of the font: MarksWithoutGlyphClassesLarge.ttf has an em of 2000 units.
    [Test]
    [Arguments("MarksWithoutGlyphClassesBase.ttf", "MarksWithoutGlyphClassesFallback.ttf", "f̀́", "2P@0,0 3P@300,62 4F@300,124")]
    [Arguments("MarksWithoutGlyphClassesFallback.ttf", "MarksWithoutGlyphClassesBase.ttf", "f̀", "2F@0,0 3P@300,62")]
    [Arguments("MarksWithoutGlyphClassesBase.ttf", "MarksWithoutGlyphClassesLarge.ttf", "f̖́", "2P@0,0 2F@300,-62 1F@300,62")]
    [Arguments("MarksWithoutGlyphClassesLarge.ttf", "MarksWithoutGlyphClassesBase.ttf", "f̀́", "2F@0,0 3F@600,124 1P@600,248")]
    public async Task MarksOfOneFontGoOnABaseOfAnother(string fontName, string fallbackName, string text, string expected)
    {
        FontCollection collection = new();
        FontFamily family = collection.Add(TestFonts.GetPath(fontName));
        TextOptions options = new(family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm))
        {
            FallbackFontFamilies = [collection.Add(TestFonts.GetPath(fallbackName))],
        };

        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        string actual = string.Join(' ', layout.Select(g =>
        {
            GlyphMetrics metrics = g.Glyph.GlyphMetrics;
            string drawnBy = metrics.FontMetrics.Description.FontFamilyInvariantCulture == family.Name ? "P" : "F";
            float x = g.PenLocation.X - layout[0].PenLocation.X + (metrics.Offset.X * options.Font.Size / metrics.ScaleFactor.X);
            float y = metrics.Offset.Y * options.Font.Size / metrics.ScaleFactor.Y;
            return $"{metrics.GlyphId}{drawnBy}@{MathF.Round(x)},{MathF.Round(y)}";
        }));

        await Assert.That(actual).IsEqualTo(expected);
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
