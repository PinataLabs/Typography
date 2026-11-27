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
/// IndicReordering.ttf has a pref feature for Devanagari and the rphf, half and blwf features for Telugu. As in
/// HarfBuzz, a pre-base-reordering consonant goes between a left matra and the base, and in the Telugu syllable ra,
/// virama, ZWJ, which keeps no base after the sort, every glyph is one before the base.
/// </summary>
public class IndicReorderingTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.IndicReorderingHarfBuzzReference),
        Options)!;

    // Each row is a text and the glyph IDs that HarfBuzz gives.
    [Test]
    public async Task GlyphsAreTheGlyphsThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.IndicReorderingFile).CreateFont(12);
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string text = row[0].GetString()!;
            int[] expected = row[1].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            int[] actual = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font))
                .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"[{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
