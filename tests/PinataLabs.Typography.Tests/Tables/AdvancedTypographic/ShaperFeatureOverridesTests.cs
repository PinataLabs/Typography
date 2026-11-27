// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// As in HarfBuzz, the Indic shaper turns liga off and the Khmer shaper turns clig on and liga off, whatever the caller
/// asks for, in horizontal and in vertical text, and the Myanmar shaper applies rphf to every glyph.
/// tests/tools/generate_shaper_feature_overrides_reference.py writes the font and the glyphs that HarfBuzz gives.
/// </summary>
public class ShaperFeatureOverridesTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GetPath("ShaperFeatureOverrides.harfbuzz.json")),
        Options)!;

    // Each row is a text, the features that the caller turns on or, after a minus, off, and the glyph IDs.
    [Test]
    public async Task GlyphsAreTheGlyphsThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.GetPath(HarfBuzz.Font)).CreateFont(12);
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string text = row[0].GetString()!;
            ShapingFeature[] features = row[1].EnumerateArray()
                .Select(f => f.GetString()!)
                .Select(f => new ShapingFeature(Tag.Parse(f.TrimStart('-')), !f.StartsWith('-')))
                .ToArray();
            int[] expected = row[2].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            int[] actual = TextShaper.Shape(font, text, new() { Features = features }).Select(g => (int)g.GlyphId).ToArray();
            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"[{codePoints}] [{string.Join(' ', features)}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // Each entry is a text and the glyph IDs that HarfBuzz gives from top to bottom, where it applies neither liga nor
    // clig unless the shaper turns it on.
    [Test]
    public async Task VerticalGlyphsAreTheGlyphsThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.GetPath(HarfBuzz.Font)).CreateFont(12);
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Vertical)
        {
            string text = row[0].GetString()!;
            int[] expected = row[1].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            int[] actual = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font) { LayoutMode = LayoutMode.VerticalLeftRight })
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
        public string Font { get; set; } = string.Empty;

        public JsonElement[][] Vertical { get; set; } = [];

        public JsonElement[][] Rows { get; set; } = [];
    }
}
