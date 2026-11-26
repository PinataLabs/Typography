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
/// A sign that has no letter of its script before it gets the base that HarfBuzz gives it. The universal shaper
/// takes a space, a digit or a full stop before the sign as its base. Without one, both the universal and the
/// Indic shaper put a dotted circle before the sign, and move a sign that goes in front of its base before it.
/// </summary>
public class ClusterTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ClustersHarfBuzzReference),
        Options)!;

    // Each row is a font, the ISO 15924 code of its script, a text, and the glyph ID and the place of each glyph
    // that HarfBuzz gives. The fonts have no GSUB table, so only the shaper changes the order and the number of
    // the glyphs.
    [Test]
    public async Task SignWithoutALetterBeforeItHasTheBaseThatHarfBuzzGives()
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

            string text = row[2].GetString()!;
            (int Id, int X)[] expected = row[3].EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32())).ToArray();
            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));
            (int Id, int X)[] actual = layout
                .Select(g => (
                    (int)g.Glyph.GlyphMetrics.GlyphId,
                    (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X)))
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"{row[1]} [{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
