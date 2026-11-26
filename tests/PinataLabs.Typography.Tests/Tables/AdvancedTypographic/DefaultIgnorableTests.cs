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
/// A default-ignorable character such as ZWJ, ZWNJ or a Mongolian free variation selector does its work in shaping and
/// then takes no room. As in HarfBuzz, it is drawn with the glyph of U+0020 when the font has one, and is left out of
/// the glyphs otherwise. A character that GSUB substituted keeps the glyph that GSUB gave it.
/// </summary>
public class DefaultIgnorableTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.DefaultIgnorablesHarfBuzzReference),
        Options)!;

    private static readonly Dictionary<string, Font> Fonts = [];

    // Each row is a font, a text, the glyph ID and the place of each glyph that HarfBuzz gives, and the advance of
    // the text, in font units.
    [Test]
    public async Task DefaultIgnorablesGiveTheGlyphsThatHarfBuzzGives()
    {
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            Font font = GetFont(row[0].GetString()!);
            string text = row[1].GetString()!;
            (int Id, int X, int Y)[] expected = row[2].EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())).ToArray();
            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));
            (int Id, int X, int Y)[] actual = layout
                .Select(g => (
                    (int)g.Glyph.GlyphMetrics.GlyphId,
                    (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                    (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                different.Add($"{row[0]} [{CodePoints(text)}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The measured advance and the measured character bounds follow the glyphs: a default-ignorable character adds
    // no width, and one that is left out has no bounds.
    [Test]
    public async Task MeasurementFollowsTheShapedGlyphs()
    {
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            Font font = GetFont(row[0].GetString()!);
            string text = row[1].GetString()!;

            // At one dot per inch, a measured pixel is a font unit.
            TextOptions options = new(font) { Dpi = 1 };
            float advance = TextMeasurer.MeasureAdvance(text, options).Width;
            TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> bounds);
            int glyphs = TextLayout.GenerateLayout(text.AsSpan(), options).Count;

            if (MathF.Round(advance) != row[3].GetInt32() || bounds.Length != glyphs)
            {
                different.Add($"{row[0]} [{CodePoints(text)}] advance {advance} expected {row[3]}, {bounds.Length} bounds for {glyphs} glyphs");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // A line breaks at the space between the two words whether the font draws the joiners or leaves them out.
    [Test]
    [Arguments("DefaultIgnorables.ttf")]
    [Arguments("DefaultIgnorablesNoSpace.ttf")]
    public async Task LineBreaksBetweenWordsWithJoiners(string fontName)
    {
        Font font = GetFont(fontName);
        const string text = "a‍b a‌b";
        TextOptions options = new(font) { Dpi = 1, WrappingLength = 1500 };

        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        float[] lines = layout.Select(g => g.PenLocation.Y).Distinct().ToArray();
        int[] secondLine = layout.Where(g => g.PenLocation.Y == lines[^1]).Select(g => g.CodePoint.Value).ToArray();

        await Assert.That(lines.Length).IsEqualTo(2);
        await Assert.That(secondLine.Where(c => c != 0x200C).ToArray()).IsEquivalentTo([0x61, 0x62], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(TextMeasurer.CountLines(text, options)).IsEqualTo(2);
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

    private static string CodePoints(string text) => string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
