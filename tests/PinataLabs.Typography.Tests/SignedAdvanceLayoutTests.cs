// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// GPOS kerning can pull the advance of a glyph below zero or push it above 65535. The layout places each glyph at
/// the sum of the advances before it, as HarfBuzz does. The measured advance spans the pen from the leftmost start of
/// a glyph to the rightmost end of one, so a glyph that moves the pen back past the start of the line widens it.
/// </summary>
public class SignedAdvanceLayoutTests
{
    private static readonly Dictionary<string, Font> Fonts = [];

    // TextShaper.ttf kerns o l by -700, so the o advances -100. TextShaperLarge.ttf kerns i o by -12000, so the i
    // advances -3808, and W W by 32000, so the first W advances 72960.
    [Test]
    [Arguments("TextShaper.ttf", "ole")]
    [Arguments("TextShaperLarge.ttf", "io")]
    [Arguments("TextShaperLarge.ttf", "Wio")]
    [Arguments("TextShaperLarge.ttf", "WW")]
    public async Task GlyphsHaveThePlacesAndTheAdvanceThatHarfBuzzGives(string fontName, string text)
    {
        int[][] reference = GetHarfBuzzGlyphs(fontName, text);
        List<(int Id, int X)> expected = [];
        int pen = 0;
        int left = 0;
        int right = 0;
        foreach (int[] glyph in reference)
        {
            expected.Add((glyph[0], pen + glyph[3]));
            left = Math.Min(left, pen);
            pen += glyph[2];
            right = Math.Max(right, pen);
        }

        // At one dot per inch, a measured pixel is a font unit.
        TextOptions options = new(GetFont(fontName)) { Dpi = 1 };
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        (int Id, int X)[] actual = layout
            .Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X)))
            .ToArray();

        await Assert.That(string.Join(' ', actual)).IsEqualTo(string.Join(' ', expected));
        await Assert.That(MathF.Round(TextMeasurer.MeasureAdvance(text, options).Width)).IsEqualTo(right - left);
    }

    // Each glyph of a row is its ID, cluster, x advance, x offset and y offset.
    private static int[][] GetHarfBuzzGlyphs(string fontName, string text)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(TestFonts.GetPath("TextShaper.harfbuzz.json")));
        JsonElement row = document.RootElement.GetProperty("rows").EnumerateArray().Single(r =>
            r.GetProperty("font").GetString() == fontName
            && r.GetProperty("text").GetString() == text
            && !r.TryGetProperty("features", out _)
            && !r.TryGetProperty("script", out _)
            && !r.TryGetProperty("language", out _));

        return row.GetProperty("glyphs").EnumerateArray().Select(g => g.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();
    }

    private static Font GetFont(string fontName)
    {
        lock (Fonts)
        {
            if (!Fonts.TryGetValue(fontName, out Font? font))
            {
                FontFamily family = new FontCollection().Add(TestFonts.GetPath(fontName));

                // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
                font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
                Fonts.Add(fontName, font);
            }

            return font;
        }
    }
}
