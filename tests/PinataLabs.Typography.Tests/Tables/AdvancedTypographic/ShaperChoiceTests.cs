// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
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
    // The cases of the last font are one letter, which the GPOS features that every shaper applies move.
    [Test]
    public async Task EveryScriptHasTheShaperThatHarfBuzzChoosesForTheScriptTagOfTheFont()
    {
        Dictionary<string, Font> fonts = [];
        List<string> different = [];
        foreach (ReferenceCase expected in HarfBuzz.Cases)
        {
            if (!fonts.TryGetValue(expected.Font, out Font? font))
            {
                font = CreateUnscaledFont(expected.Font);
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

    // Each case is one letter, upright in vertical text. Of the three GPOS features of the font, HarfBuzz applies
    // abvm and blwm to vertical text, and not dist. A case names the glyph of the letter, because a feature of
    // the font can put another glyph beside it.
    [Test]
    public async Task VerticalTextHasTheMarkPositioningFeaturesThatHarfBuzzApplies()
    {
        Font positioning = CreateUnscaledFont(HarfBuzz.VerticalPositioningFont);
        Font plain = CreateUnscaledFont(HarfBuzz.VerticalPlainFont);
        List<string> different = [];
        foreach (VerticalCase expected in HarfBuzz.VerticalCases)
        {
            Vector2 actual = VerticalOffset(positioning, expected) - VerticalOffset(plain, expected);
            if (actual != new Vector2(expected.X, expected.Y))
            {
                different.Add($"{expected.Name}: expected <{expected.X}, {expected.Y}> actual {actual}");
            }
        }

        await Assert.That(HarfBuzz.VerticalCases).IsNotEmpty();
        await Assert.That(different).IsEmpty();
    }

    // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
    private static Font CreateUnscaledFont(string name)
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(name));
        return family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
    }

    private static Vector2 VerticalOffset(Font font, VerticalCase letter)
    {
        TextOptions options = new(font) { LayoutMode = LayoutMode.VerticalLeftRight };
        return TextLayout.GenerateLayout(letter.Text.AsSpan(), options).Single(g => g.Glyph.GlyphMetrics.GlyphId == letter.Id).Glyph.GlyphMetrics.Offset;
    }

    private sealed class Reference
    {
        public ReferenceCase[] Cases { get; set; } = [];

        public string VerticalPositioningFont { get; set; } = string.Empty;

        public string VerticalPlainFont { get; set; } = string.Empty;

        public VerticalCase[] VerticalCases { get; set; } = [];
    }

    private sealed class VerticalCase
    {
        public string Name { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;

        public int Id { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
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
