// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class VerticalGlyphFormTests
{
    // Glyph ids in NotoSansKR-Regular.otf. The horizontal forms are what the cmap returns,
    // the vertical forms are the single substitutions of the 'vert' feature.
    private const ushort LeftLenticularBracket = 1412;
    private const ushort RightLenticularBracket = 1413;
    private const ushort LeftCornerBracket = 1408;
    private const ushort RightCornerBracket = 1409;
    private const ushort IdeographicComma = 1397;
    private const ushort ProlongedSoundMark = 1645;

    private const ushort VerticalLeftLenticularBracket = 21614;
    private const ushort VerticalRightLenticularBracket = 21615;
    private const ushort VerticalLeftCornerBracket = 21620;
    private const ushort VerticalRightCornerBracket = 21621;
    private const ushort VerticalIdeographicComma = 21594;
    private const ushort VerticalProlongedSoundMark = 24724;

    private static List<ushort> GlyphIds(string text, LayoutMode mode)
    {
        FontFamily family = new FontCollection().Add(TestFonts.NotoSansKRRegular);
        TextOptions options = new(family.CreateFont(30)) { LayoutMode = mode };
        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, text, options);
        return renderer.GlyphKeys.Select(k => k.GlyphId).ToList();
    }

    [Test]
    public async Task HorizontalLayoutKeepsHorizontalBracketForms()
    {
        List<ushort> ids = GlyphIds("【】「」、ー", LayoutMode.HorizontalTopBottom);

        await Assert.That(ids).IsEquivalentTo(new ushort[]
        {
            LeftLenticularBracket,
            RightLenticularBracket,
            LeftCornerBracket,
            RightCornerBracket,
            IdeographicComma,
            ProlongedSoundMark,
        });
    }

    [Test]
    [Arguments(LayoutMode.VerticalRightLeft)]
    [Arguments(LayoutMode.VerticalLeftRight)]
    public async Task VerticalLayoutUsesVerticalAlternates(LayoutMode mode)
    {
        List<ushort> ids = GlyphIds("【】「」、ー", mode);

        await Assert.That(ids).IsEquivalentTo(new ushort[]
        {
            VerticalLeftLenticularBracket,
            VerticalRightLenticularBracket,
            VerticalLeftCornerBracket,
            VerticalRightCornerBracket,
            VerticalIdeographicComma,
            VerticalProlongedSoundMark,
        });
    }

    [Test]
    [Arguments("あ", LayoutMode.VerticalRightLeft)]
    [Arguments("ー", LayoutMode.VerticalRightLeft)]
    [Arguments("日", LayoutMode.VerticalRightLeft)]
    [Arguments("、", LayoutMode.VerticalRightLeft)]
    [Arguments("ー", LayoutMode.VerticalLeftRight)]
    [Arguments("あ", LayoutMode.VerticalMixedRightLeft)]
    public async Task UprightGlyphsAreCentredInTheColumn(string text, LayoutMode mode)
    {
        const float originX = 100F;
        FontFamily family = new FontCollection().Add(TestFonts.NotoSansKRRegular);
        TextOptions options = new(family.CreateFont(30))
        {
            LayoutMode = mode,
            Origin = new System.Numerics.Vector2(originX, 10),
        };

        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, text, options);

        float inkCentre = (renderer.ControlPoints.Min(p => p.X) + renderer.ControlPoints.Max(p => p.X)) * .5F;
        float columnCentre = originX + (TextMeasurer.MeasureAdvance(text, options).Width * .5F);

        await Assert.That(inkCentre).IsEqualTo(columnCentre).Within(1.5F);
    }
}
