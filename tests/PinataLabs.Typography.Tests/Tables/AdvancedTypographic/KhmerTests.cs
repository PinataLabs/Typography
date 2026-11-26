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
/// Khmer syllables get the glyphs and places that the Khmer shaper of HarfBuzz gives them: the features of each stage
/// in order, a COENG RO and a vowel that go in front of the base moved before it, and two-part vowels split.
/// </summary>
public class KhmerTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.KhmerHarfBuzzReference),
        Options)!;

    // Each row is a Khmer syllable and the glyph ID and the place of each glyph that HarfBuzz gives.
    [Test]
    public async Task SyllablesHaveTheGlyphsThatHarfBuzzGives()
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(HarfBuzz.Font));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string text = row[0].GetString()!;
            if (IsKnownDifference(text))
            {
                continue;
            }

            (int Id, int X, int Y)[] expected = row[1].EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())).ToArray();
            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));
            (int Id, int X, int Y)[] actual = layout
                .Select(g => (
                    (int)g.Glyph.GlyphMetrics.GlyphId,
                    (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                    (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"[{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The syllables that #114 tracks: ROBAT after a vowel, which HarfBuzz takes as a broken cluster, and five
    // syllables whose last glyph takes another contextual form.
    private static bool IsKnownDifference(string text)
        => (text.Length >= 3 && text[^1] == '៌' && text[^2] is >= 'ា' and <= 'ំ')
            || text is "រ្រ្តេ"
                or "ញ្រឿ"
                or "ញ្រៀ"
                or "ញ៉ុ"
                or "ញ៉ូ";

    private sealed class Reference
    {
        public string Font { get; set; } = string.Empty;

        public JsonElement[][] Rows { get; set; } = [];
    }
}
