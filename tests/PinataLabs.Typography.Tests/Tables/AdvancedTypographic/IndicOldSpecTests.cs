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
/// IndicOldSpec.ttf has only the old-spec tags of the Indic scripts, such as deva. As in HarfBuzz, old spec moves the
/// virama after the base to after the last consonant in every script but Kannada, and gives a Devanagari ra, virama before
/// the base its below-base form unless a ZWJ asks for the eyelash ra.
/// </summary>
public class IndicOldSpecTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.IndicOldSpecHarfBuzzReference),
        Options)!;

    // Each row is a text and the glyph IDs that HarfBuzz gives.
    [Test]
    public async Task GlyphsAreTheGlyphsThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.IndicOldSpecFile).CreateFont(12);
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
