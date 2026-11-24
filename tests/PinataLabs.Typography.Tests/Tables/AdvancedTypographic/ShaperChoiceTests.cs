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
/// The shaper of a text depends on its script and on the script tag that the GSUB table of the font has for it,
/// as in HarfBuzz. For example, Devanagari text has the universal shaper in a font with <c>dev3</c>, the Indic
/// shaper in a font with <c>dev2</c>, and the default shaper in a font with only <c>DFLT</c>.
/// </summary>
public class ShaperChoiceTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ShaperChoiceHarfBuzzReference),
        Options)!;

    // Each case is one letter and a mark, in a font whose features and missing GPOS table show the shaper.
    [Test]
    public async Task EveryScriptHasTheShaperThatHarfBuzzChoosesForTheScriptTagOfTheFont()
    {
        Dictionary<string, Font> fonts = [];
        List<string> different = [];
        foreach (ReferenceCase expected in HarfBuzz.Cases)
        {
            if (!fonts.TryGetValue(expected.Font, out Font? font))
            {
                FontFamily family = new FontCollection().Add(TestFonts.GetPath(expected.Font));

                // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
                font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
                fonts.Add(expected.Font, font);
            }

            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(expected.Text.AsSpan(), new TextOptions(font));
            IEnumerable<(int Id, int X, int Y)> actual = layout.Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)));

            if (!actual.SequenceEqual(expected.Glyphs.Select(g => (g.Id, g.X, g.Y))))
            {
                different.Add($"{expected.Name}: expected [{string.Join(' ', expected.Glyphs.Select(g => (g.Id, g.X, g.Y)))}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public ReferenceCase[] Cases { get; set; } = [];
    }

    private sealed class ReferenceCase
    {
        public string Name { get; set; } = string.Empty;

        public string Font { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public ReferenceGlyph[] Glyphs { get; set; } = [];
    }

    private sealed class ReferenceGlyph
    {
        public int Id { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
