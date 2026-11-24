// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Tests.Fakes;
using PinataLabs.Typography.Tests.FontPrograms;
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
    [Arguments(LayoutMode.VerticalMixedRightLeft)]
    [Arguments(LayoutMode.VerticalMixedLeftRight)]
    public async Task RotatedGlyphCentralBaselineIsAtTheColumnCentre(LayoutMode mode)
    {
        // The font's ascent (1100) and descent (-200) are far from symmetric and it has a line gap (100).
        // Its Latin glyphs fill the ascent to descent range, so a rotated Latin glyph's ink centre is its
        // central baseline. Edge puts it at the column centre to within 0.01px, see tests/tools/central_baseline.
        const float originX = 200F;
        const string text = "あいうABCえお";
        FontFamily family = new FontCollection().Add(TestFonts.CentralBaselineTestFile);
        TextOptions options = new(family.CreateFont(100))
        {
            LayoutMode = mode,
            Origin = new Vector2(originX, 20),
        };

        InkBoundsRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, text, options);

        // The column is one em wide, like the browser's line-height: 1 column. MeasureAdvance is not used
        // because it counts a rotated glyph's baseline shift as column width.
        const float columnCentre = originX + 50F;

        List<float> rotated = renderer.InkCentres(GlyphLayoutMode.VerticalRotated);
        List<float> upright = renderer.InkCentres(GlyphLayoutMode.Vertical);

        await Assert.That(rotated.Count).IsEqualTo(3);
        await Assert.That(upright.Count).IsEqualTo(5);
        foreach (float centre in rotated.Concat(upright))
        {
            await Assert.That(centre).IsEqualTo(columnCentre).Within(0.01F);
        }
    }

    [Test]
    [Arguments(LayoutMode.VerticalMixedRightLeft)]
    [Arguments(LayoutMode.VerticalMixedLeftRight)]
    public async Task RotatedGlyphCentralBaselineIgnoresTheSignOfTheDescender(LayoutMode mode)
    {
        // A malformed font stores its descent as a positive number. The glyphs still descend below
        // the baseline, so the central baseline must not move.
        const float originX = 200F;
        byte[] font = WithPositiveDescender(File.ReadAllBytes(TestFonts.CentralBaselineTestFile));
        using MemoryStream stream = new(font);
        FontFamily family = new FontCollection().Add(stream);
        TextOptions options = new(family.CreateFont(100))
        {
            LayoutMode = mode,
            Origin = new Vector2(originX, 20),
        };

        InkBoundsRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "ABC", options);

        List<float> rotated = renderer.InkCentres(GlyphLayoutMode.VerticalRotated);
        await Assert.That(rotated.Count).IsEqualTo(3);
        foreach (float centre in rotated)
        {
            await Assert.That(centre).IsEqualTo(originX + 50F).Within(0.01F);
        }
    }

    [Test]
    [Arguments(LayoutMode.VerticalMixedRightLeft)]
    [Arguments(LayoutMode.VerticalMixedLeftRight)]
    public async Task RotatedGlyphLaysOutWhenTheDescenderIsTheSmallestShort(LayoutMode mode)
    {
        byte[] font = WithDescender(File.ReadAllBytes(TestFonts.CentralBaselineTestFile), short.MinValue);
        using MemoryStream stream = new(font);
        FontFamily family = new FontCollection().Add(stream);
        TextOptions options = new(family.CreateFont(100)) { LayoutMode = mode };

        InkBoundsRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "ABC", options);

        await Assert.That(renderer.InkCentres(GlyphLayoutMode.VerticalRotated).Count).IsEqualTo(3);
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

    private static byte[] WithDescender(byte[] font, short descender)
    {
        byte[] hhea = SfntRewriter.GetTable(font, "hhea")!;
        byte[] os2 = SfntRewriter.GetTable(font, "OS/2")!;
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(6), descender);
        BinaryPrimitives.WriteInt16BigEndian(os2.AsSpan(70), descender);
        return SfntRewriter.Replace(SfntRewriter.Replace(font, "hhea", hhea), "OS/2", os2);
    }

    private static byte[] WithPositiveDescender(byte[] font)
    {
        const int hheaDescender = 6;
        const int os2TypoDescender = 70;
        byte[] hhea = SfntRewriter.GetTable(font, "hhea")!;
        byte[] os2 = SfntRewriter.GetTable(font, "OS/2")!;
        Negate(hhea, hheaDescender);
        Negate(os2, os2TypoDescender);
        return SfntRewriter.Replace(SfntRewriter.Replace(font, "hhea", hhea), "OS/2", os2);

        static void Negate(byte[] table, int offset)
        {
            short value = BinaryPrimitives.ReadInt16BigEndian(table.AsSpan(offset));
            if (value >= 0)
            {
                throw new InvalidOperationException("The source font must have a negative descender.");
            }

            BinaryPrimitives.WriteInt16BigEndian(table.AsSpan(offset), (short)-value);
        }
    }

    private sealed class InkBoundsRenderer : IGlyphRenderer
    {
        private readonly List<(GlyphLayoutMode Mode, float MinX, float MaxX)> glyphs = [];
        private GlyphLayoutMode mode;
        private float minX;
        private float maxX;

        public List<float> InkCentres(GlyphLayoutMode layoutMode)
            => this.glyphs.Where(g => g.Mode == layoutMode).Select(g => (g.MinX + g.MaxX) * .5F).ToList();

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
        {
            this.mode = parameters.LayoutMode;
            this.minX = float.MaxValue;
            this.maxX = float.MinValue;
            return true;
        }

        public void EndGlyph() => this.glyphs.Add((this.mode, this.minX, this.maxX));

        public void MoveTo(Vector2 point) => this.Add(point);

        public void LineTo(Vector2 point) => this.Add(point);

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point) => this.Add(point);

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point) => this.Add(point);

        public void BeginFigure()
        {
        }

        public void EndFigure()
        {
        }

        public void BeginText(in FontRectangle bounds)
        {
        }

        public void EndText()
        {
        }

        public TextDecorations EnabledDecorations() => TextDecorations.None;

        public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)
        {
        }

        private void Add(Vector2 point)
        {
            this.minX = MathF.Min(this.minX, point.X);
            this.maxX = MathF.Max(this.maxX, point.X);
        }
    }
}
