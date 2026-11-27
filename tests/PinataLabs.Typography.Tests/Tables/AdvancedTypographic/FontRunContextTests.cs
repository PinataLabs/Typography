// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// FontRunContext.ttf, the font of the text, and FontRunContextFallback.ttf give the same glyph IDs to different
/// characters, and each has GPOS lookups that would apply if they took a glyph of the other font for their own: a pair,
/// a chained context and a mark-to-base lookup. A caller of HarfBuzz shapes the characters that each font draws in a
/// buffer of that font, so none of them applies (issue #198). In one buffer of the font, each of them applies.
/// </summary>
public class FontRunContextTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonElement[][] Rows = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.FontRunContextHarfBuzzReference),
        Options)!.Rows;

    // Each row is a case, a text, the fonts that shape it, and the glyph ID, x and y of each glyph. The text of a "runs"
    // row takes the characters that the font of the text lacks from the fallback font.
    [Test]
    public async Task LayoutGivesTheGlyphsAndPlacesThatHarfBuzzGivesTheFontRuns()
    {
        FontCollection collection = new();
        FontFamily primary = collection.Add(TestFonts.FontRunContextFile);
        FontFamily fallback = collection.Add(TestFonts.FontRunContextFallbackFile);
        List<string> different = [];
        foreach (JsonElement[] row in Rows)
        {
            string text = row[1].GetString()!;
            string fonts = row[2].GetString()!;

            // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
            TextOptions options = fonts switch
            {
                "runs" => new(primary.CreateFont(72 * 1000)) { FallbackFontFamilies = [fallback] },
                "primary" => new(primary.CreateFont(72 * 1000)),
                _ => new(fallback.CreateFont(72 * 1000)),
            };

            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
            string actual = Describe(layout.Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y))));
            if (Describe(row[3]) != actual)
            {
                different.Add($"{row[0].GetString()} {fonts} expected [{Describe(row[3])}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private static string Describe(JsonElement glyphs)
        => Describe(glyphs.EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())));

    private static string Describe(IEnumerable<(int Id, int X, int Y)> glyphs)
        => string.Join(' ', glyphs.Select(g => $"{g.Id}@{g.X},{g.Y}"));

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
