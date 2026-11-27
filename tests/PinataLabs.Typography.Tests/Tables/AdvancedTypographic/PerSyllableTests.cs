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
/// The PerSyllable fonts have lookups that apply to a letter when the letter of the next syllable follows it. As in
/// HarfBuzz, a lookup of a feature that the Khmer, Myanmar, universal or Indic shaper plans per syllable does not match
/// across syllables. A lookup that two features of one stage share takes the flag of the feature with the lower tag,
/// and the lookup of a required feature matches across syllables. A per-syllable lookup does not match a glyph of the
/// script run before, whose shaper numbers its syllables too.
/// </summary>
public class PerSyllableTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.PerSyllableHarfBuzzReference),
        Options)!;

    // Each row is a font, a text and the glyph IDs that HarfBuzz gives.
    [Test]
    public async Task GlyphsAreTheGlyphsThatHarfBuzzGives()
    {
        Dictionary<string, Font> fonts = [];
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string fontName = row[0].GetString()!;
            if (!fonts.TryGetValue(fontName, out Font? font))
            {
                font = new FontCollection().Add(TestFonts.GetPath(fontName)).CreateFont(12);
                fonts.Add(fontName, font);
            }

            string text = row[1].GetString()!;
            int[] expected = row[2].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            int[] actual = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font))
                .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"{fontName} [{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
