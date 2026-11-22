// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Tests.Fakes;
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

    private static List<ushort> GlyphIds(string text, LayoutMode mode, params Tag[] featureTags)
    {
        FontFamily family = new FontCollection().Add(TestFonts.NotoSansKRRegular);
        TextOptions options = new(family.CreateFont(30)) { LayoutMode = mode, FeatureTags = featureTags };
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

    [Test]
    public async Task TrCharacterWithNonVertSubstitutionIsStillRotated()
    {
        // A ccmp/locl style substitution changes the glyph id but is not a vertical form,
        // so the character must stay rotated and use its horizontal advance.
        CodePoint prolonged = new(0x30FC);
        CodePoint other = new('A');
        SubstitutingFakeFontInstance instance = new("ーA", prolonged, other);
        Font font = ((IFontMetricsCollection)new FontCollection()).AddMetrics(instance, CultureInfo.InvariantCulture).CreateFont(12);

        FontRectangle horizontal = TextMeasurer.MeasureAdvance("ー", new TextOptions(font) { LayoutMode = LayoutMode.HorizontalTopBottom });
        FontRectangle mixed = TextMeasurer.MeasureAdvance("ー", new TextOptions(font) { LayoutMode = LayoutMode.VerticalMixedRightLeft });
        FontRectangle vertical = TextMeasurer.MeasureAdvance("ー", new TextOptions(font) { LayoutMode = LayoutMode.VerticalRightLeft });

        // The font must distinguish the two advances, otherwise this test proves nothing.
        await Assert.That(vertical.Height).IsGreaterThan(horizontal.Width);
        await Assert.That(mixed.Height).IsEqualTo(horizontal.Width).Within(0.001F);
    }

    [Test]
    public async Task RotatedTrGlyphIsShapedWithHorizontalFeatures()
    {
        List<string> tags = PlanTrGlyph(hasVerticalForm: false);

        await Assert.That(tags).Contains("kern");
        await Assert.That(tags).Contains("liga");
        await Assert.That(tags).DoesNotContain("vert");
    }

    [Test]
    public async Task UprightTrGlyphIsShapedWithVertOnly()
    {
        List<string> tags = PlanTrGlyph(hasVerticalForm: true);

        await Assert.That(tags).Contains("vert");
        await Assert.That(tags).DoesNotContain("kern");
    }

    [Test]
    public async Task RotatedGlyphIsShapedWithVrtrInVerticalMixedLayout()
    {
        List<string> tags = PlanTrGlyph(hasVerticalForm: false);

        await Assert.That(tags).Contains("vrtr");
    }

    [Test]
    [Arguments("ー", true)]
    [Arguments("あ", false)]
    public async Task UprightGlyphIsNotShapedWithVrtrInVerticalMixedLayout(string text, bool hasVerticalForm)
    {
        List<string> tags = PlanGlyph(text, LayoutMode.VerticalMixedRightLeft, hasVerticalForm);

        await Assert.That(tags).Contains("vert");
        await Assert.That(tags).DoesNotContain("vrtr");
    }

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom)]
    [Arguments(LayoutMode.VerticalRightLeft)]
    [Arguments(LayoutMode.VerticalLeftRight)]
    public async Task VrtrIsOnlyUsedInVerticalMixedLayout(LayoutMode mode)
    {
        List<string> tags = PlanGlyph("A", mode, false);

        await Assert.That(tags).DoesNotContain("vrtr");
    }

    [Test]
    [Arguments("A", LayoutMode.HorizontalTopBottom)]
    [Arguments("A", LayoutMode.VerticalRightLeft)]
    [Arguments("A", LayoutMode.VerticalMixedRightLeft)]
    [Arguments("あ", LayoutMode.VerticalMixedRightLeft)]
    public async Task Vrt2IsNotUsedByDefault(string text, LayoutMode mode)
    {
        List<string> tags = PlanGlyph(text, mode, false);

        await Assert.That(tags).DoesNotContain("vrt2");
    }

    [Test]
    public async Task UserVrt2IsNotUsedForRotatedGlyphInVerticalMixedLayout()
    {
        List<string> tags = PlanGlyph("A", LayoutMode.VerticalMixedRightLeft, false, Tag.Parse("vrt2"));

        await Assert.That(tags).DoesNotContain("vrt2");
    }

    [Test]
    [Arguments("あ", LayoutMode.VerticalMixedRightLeft)]
    [Arguments("A", LayoutMode.VerticalRightLeft)]
    public async Task UserVrt2IsUsedForUprightGlyph(string text, LayoutMode mode)
    {
        List<string> tags = PlanGlyph(text, mode, false, Tag.Parse("vrt2"));

        await Assert.That(tags).Contains("vrt2");
    }

    [Test]
    public async Task UserVrt2DoesNotRotateRotatedGlyphTwiceInVerticalMixedLayout()
    {
        // U+2026 HORIZONTAL ELLIPSIS has vertical orientation R. NotoSansKR's vrt2 substitutes a
        // vertical glyph for it, otherwise this test proves nothing.
        await Assert.That(GlyphIds("…", LayoutMode.HorizontalTopBottom, Tag.Parse("vrt2")))
            .IsNotEquivalentTo(GlyphIds("…", LayoutMode.HorizontalTopBottom));

        // In vertical mixed layout the ellipsis is rotated synthetically, so it must keep its horizontal glyph.
        await Assert.That(GlyphIds("あ…い", LayoutMode.VerticalMixedRightLeft, Tag.Parse("vrt2")))
            .IsEquivalentTo(GlyphIds("あ…い", LayoutMode.VerticalMixedRightLeft));
    }

    private static List<string> PlanTrGlyph(bool hasVerticalForm)
        => PlanGlyph("ー", LayoutMode.VerticalMixedRightLeft, hasVerticalForm);

    private static List<string> PlanGlyph(string text, LayoutMode mode, bool hasVerticalForm, params Tag[] featureTags)
    {
        Font font = FakeFont.CreateFont(text);
        TextOptions options = new(font) { LayoutMode = mode, FeatureTags = featureTags };
        GlyphSubstitutionCollection collection = new(options);
        collection.AddGlyph(1, CodePoint.DecodeFromUtf16At(text, 0), TextDirection.LeftToRight, new TextRun { Start = 0, End = 1, Font = font }, 0);
        collection[0].HasVerticalForm = hasVerticalForm;

        new DefaultShaper(ScriptClass.Common, options).Plan(collection, 0, 1);

        return collection[0].Features.Select(f => f.Tag.ToString()).ToList();
    }
}
