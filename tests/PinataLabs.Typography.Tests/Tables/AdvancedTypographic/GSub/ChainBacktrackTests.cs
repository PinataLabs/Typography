// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GSub;

/// <summary>
/// ChainBacktrack.ttf has a chained context subtable of each format and a reverse chaining subtable, each with the
/// backtrack "a b". The backtrack lists the glyph nearest to the input first, so each input glyph is replaced after
/// "a b" and not after "b a". A reverse chaining subtable that has no substitute for a glyph of its coverage does not
/// apply to that glyph.
/// </summary>
public class ChainBacktrackTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ChainBacktrackHarfBuzzReference),
        Options)!;

    // Each row is a text and the glyph IDs that HarfBuzz gives.
    [Test]
    public async Task GlyphsAreTheGlyphsThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.ChainBacktrackFile).CreateFont(12);
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string text = row[0].GetString()!;
            int[] expected = row[1].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            int[] actual;
            try
            {
                actual = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font))
                    .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
                    .ToArray();
            }
            catch (Exception e)
            {
                different.Add($"{text} expected [{string.Join(' ', expected)}] threw {e.GetType().Name}");
                continue;
            }

            if (!actual.SequenceEqual(expected))
            {
                different.Add($"{text} expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
