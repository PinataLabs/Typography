// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

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
    [Arguments(LayoutMode.VerticalRightLeft, true)]
    [Arguments(LayoutMode.VerticalLeftRight, true)]
    [Arguments(LayoutMode.HorizontalTopBottom, false)]
    public async Task CmapVerticalFormsAreUsedOnlyInVerticalLayoutWhenFontHasNoVertFeature(LayoutMode mode, bool expectVerticalForm)
    {
        // The fake font has no GSUB table and so no 'vert' feature.
        CodePoint original = new(0x3010);
        await Assert.That(CodePoint.TryGetVerticalMirror(original, out CodePoint mirror)).IsTrue();

        string text = char.ConvertFromUtf32(original.Value) + char.ConvertFromUtf32(mirror.Value);
        Font font = FakeFont.CreateFont(text);

        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, char.ConvertFromUtf32(original.Value), new TextOptions(font) { LayoutMode = mode });
        ushort id = renderer.GlyphKeys.Single().GlyphId;

        GlyphRenderer expected = new();
        TextRenderer.RenderTextTo(expected, char.ConvertFromUtf32(expectVerticalForm ? mirror.Value : original.Value), new TextOptions(font) { LayoutMode = LayoutMode.HorizontalTopBottom });

        await Assert.That(id).IsEqualTo(expected.GlyphKeys.Single().GlyphId);
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

    [Test]
    [Arguments("あーい", 1)]
    [Arguments("ー", 0)]
    public async Task TrCharacterUsesVertFormInVerticalMixedLayout(string text, int index)
    {
        List<ushort> ids = GlyphIds(text, LayoutMode.VerticalMixedRightLeft);

        await Assert.That(ids[index]).IsEqualTo(VerticalProlongedSoundMark);
    }

    [Test]
    public async Task TrCharacterWithVertFormIsCentredInTheColumnInVerticalMixedLayout()
    {
        const float originX = 100F;
        FontFamily family = new FontCollection().Add(TestFonts.NotoSansKRRegular);
        TextOptions options = new(family.CreateFont(30))
        {
            LayoutMode = LayoutMode.VerticalMixedRightLeft,
            Origin = new System.Numerics.Vector2(originX, 10),
        };

        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "ー", options);

        float inkCentre = (renderer.ControlPoints.Min(p => p.X) + renderer.ControlPoints.Max(p => p.X)) * .5F;
        float columnCentre = originX + (TextMeasurer.MeasureAdvance("ー", options).Width * .5F);

        await Assert.That(inkCentre).IsEqualTo(columnCentre).Within(1.5F);
    }

    [Test]
    public async Task TrCharacterWithoutVertFormIsStillRotated()
    {
        // The fake font has no GSUB table, so U+30FC has no vertical form and must be rotated:
        // its advance along the column is its horizontal advance width.
        Font font = FakeFont.CreateFont("ー");

        FontRectangle horizontal = TextMeasurer.MeasureAdvance("ー", new TextOptions(font) { LayoutMode = LayoutMode.HorizontalTopBottom });
        FontRectangle mixed = TextMeasurer.MeasureAdvance("ー", new TextOptions(font) { LayoutMode = LayoutMode.VerticalMixedRightLeft });

        await Assert.That(mixed.Height).IsEqualTo(horizontal.Width).Within(0.001F);
    }

    [Test]
    [Arguments("あAV")]
    [Arguments("AV、")]
    public async Task RotatedLatinKeepsKerningInMixedRunWithUprightCharacter(string text)
    {
        FontFamily family = new FontCollection().Add(TestFonts.RobotoRegular);
        Font font = family.CreateFont(30);

        float Advance(string value, LayoutMode mode, KerningMode kerning)
        {
            FontRectangle r = TextMeasurer.MeasureAdvance(value, new TextOptions(font) { LayoutMode = mode, KerningMode = kerning });
            return mode == LayoutMode.HorizontalTopBottom ? r.Width : r.Height;
        }

        // The font must kern the pair horizontally, otherwise this test proves nothing.
        await Assert.That(Advance("AV", LayoutMode.HorizontalTopBottom, KerningMode.Standard))
            .IsLessThan(Advance("AV", LayoutMode.HorizontalTopBottom, KerningMode.None));

        await Assert.That(Advance(text, LayoutMode.VerticalMixedRightLeft, KerningMode.Standard))
            .IsLessThan(Advance(text, LayoutMode.VerticalMixedRightLeft, KerningMode.None));
    }
}
