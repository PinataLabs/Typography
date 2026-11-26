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
/// Mongolian Baiti shapes with joining types and GSUB lookups that the library shaped differently from HarfBuzz
/// (issue #111). MongolianLookups.ttf has each of them, and each row of its reference names the one it shows.
/// </summary>
public class MongolianLookupsTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.MongolianLookupsHarfBuzzReference),
        Options)!;

    private static readonly Font Font = CreateFont();

    // Each row is a cause, a text, the glyph ID and the place of each glyph that HarfBuzz gives, and the advance of
    // the text, in font units.
    [Test]
    [Arguments("joining")]
    [Arguments("context-match")]
    [Arguments("mark-filtering-set")]
    [Arguments("reverse-chaining")]
    public async Task TextGivesTheGlyphsThatHarfBuzzGives(string cause)
    {
        List<string> different = [];
        JsonElement[][] rows = HarfBuzz.Rows.Where(row => row[0].GetString() == cause).ToArray();
        foreach (JsonElement[] row in rows)
        {
            string text = row[1].GetString()!;
            (int Id, int X, int Y)[] expected = row[2].EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())).ToArray();
            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(Font));
            (int Id, int X, int Y)[] actual = layout
                .Select(g => (
                    (int)g.Glyph.GlyphMetrics.GlyphId,
                    (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                    (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                different.Add($"[{CodePoints(text)}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(rows).IsNotEmpty();
        await Assert.That(different).IsEmpty();
    }

    private static Font CreateFont()
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath("MongolianLookups.ttf"));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        return family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
    }

    private static string CodePoints(string text) => string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
