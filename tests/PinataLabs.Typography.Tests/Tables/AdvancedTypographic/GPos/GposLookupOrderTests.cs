// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

/// <summary>
/// GposLookupOrder.ttf lists its GPOS lookups in an order that differs from the order in which
/// the shapers plan the features. Each text is positioned as HarfBuzz positions it only when
/// the lookups are applied once each, in LookupList order.
/// </summary>
public class GposLookupOrderTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GposLookupOrderHarfBuzzReference),
        Options)!;

    [Test]
    [Arguments("MarkAlone")]
    [Arguments("KernBeforeMark")]
    [Arguments("MkmkBeforeMark")]
    [Arguments("PairOfTwoFeatures")]
    [Arguments("ContextOfTwoFeatures")]
    [Arguments("MarkBeforeAbvm")]
    [Arguments("MarkBeforeCalt")]
    public async Task GlyphsArePositionedAsHarfBuzzPositionsThem(string name)
    {
        ReferenceCase expected = HarfBuzz.Cases.Single(c => c.Name == name);

        // One em is 1000 font units, so at 1000 pixels per em a pixel is a font unit.
        Font font = new FontCollection().Add(TestFonts.GposLookupOrderFile).CreateFont(1000);
        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, expected.Text, new TextOptions(font));

        // Every glyph has the same outline, so the bounds differ by exactly the glyph positions.
        // The y axis of the bounds points down.
        Dictionary<int, FontRectangle> bounds = renderer.GlyphKeys
            .Select((key, i) => (Id: (int)key.GlyphId, Rect: renderer.GlyphRects[i]))
            .ToDictionary(g => g.Id, g => g.Rect);

        ReferenceGlyph origin = expected.Glyphs[0];
        await Assert.That(bounds.Keys.Order()).IsEquivalentTo(expected.Glyphs.Select(g => g.Id).Order());
        foreach (ReferenceGlyph glyph in expected.Glyphs)
        {
            int x = (int)MathF.Round(bounds[glyph.Id].X - bounds[origin.Id].X);
            int y = (int)MathF.Round(bounds[origin.Id].Y - bounds[glyph.Id].Y);
            await Assert.That((x, y)).IsEqualTo((glyph.X - origin.X, glyph.Y - origin.Y));
        }
    }

    private sealed class Reference
    {
        public ReferenceCase[] Cases { get; set; } = [];
    }

    private sealed class ReferenceCase
    {
        public string Name { get; set; } = string.Empty;

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
