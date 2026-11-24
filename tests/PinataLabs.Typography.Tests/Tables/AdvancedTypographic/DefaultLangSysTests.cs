// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// With no language requested, a shaper uses the features of the LangSysRecord tagged <c>dflt</c>, or else of the
/// default LangSys of the script. A script with neither contributes no feature, as in HarfBuzz. A font without the
/// script of the text gives the LangSys of its <c>DFLT</c>, <c>dflt</c> or <c>latn</c> script, in that order. The
/// required feature of the LangSys applies to every glyph.
/// </summary>
public class DefaultLangSysTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.DefaultLangSysHarfBuzzReference),
        Options)!;

    [Test]
    [Arguments("OghamKern")]
    [Arguments("OghamLocl")]
    [Arguments("RunicKern")]
    [Arguments("RunicLocl")]
    [Arguments("DejaVuSansKern")]
    [Arguments("DfltRecordOnly")]
    [Arguments("DfltRecordBeforeDefault")]
    [Arguments("RequiredBeforeLiga")]
    [Arguments("RequiredListedLiga")]
    [Arguments("RequiredUnlistedLiga")]
    [Arguments("RequiredWithRvrn")]
    [Arguments("FallbackToDFLT")]
    [Arguments("FallbackToDfltScript")]
    [Arguments("FallbackToLatn")]
    [Arguments("FallbackToNone")]
    public async Task GlyphsAreShapedAsHarfBuzzShapesThem(string name)
    {
        ReferenceCase expected = HarfBuzz.Cases.Single(c => c.Name == name);
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(expected.Font));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(expected.Text.AsSpan(), new TextOptions(font));

        (int Id, int X, int Y)[] actual = layout
            .Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
            .ToArray();

        await Assert.That(actual).IsEquivalentTo(expected.Glyphs.Select(g => (g.Id, g.X, g.Y)), CollectionOrdering.Matching);
    }

    /// <summary>
    /// HarfBuzz takes <c>vert</c> from the whole FeatureList when the chosen language system lacks it.
    /// </summary>
    [Test]
    [Arguments("VertNotInLangSys")]
    [Arguments("VertWithoutLangSys")]
    public async Task VerticalGlyphsAreSubstitutedAsHarfBuzzSubstitutesThem(string name)
    {
        ReferenceCase expected = HarfBuzz.Cases.Single(c => c.Name == name);
        Font font = new FontCollection().Add(TestFonts.GetPath(expected.Font)).CreateFont(12);
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(
            expected.Text.AsSpan(),
            new TextOptions(font) { LayoutMode = LayoutMode.VerticalLeftRight });

        int[] actual = layout.Select(g => (int)g.Glyph.GlyphMetrics.GlyphId).ToArray();

        await Assert.That(actual).IsEquivalentTo(expected.Glyphs.Select(g => g.Id), CollectionOrdering.Matching);
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
