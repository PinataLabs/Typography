// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

public class FontMetricsTests
{
    [Test]
    public async Task FontMetricsMatchesReference()
    {
        // Compared to EveryFonts TTFDump metrics
        // https://everythingfonts.com/ttfdump
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.OpenSansFile);
        Font font = family.CreateFont(12);

        await Assert.That(font.FontMetrics.UnitsPerEm).IsEqualTo(2048);
        await Assert.That(font.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo(2189);
        await Assert.That(font.FontMetrics.HorizontalMetrics.Descender).IsEqualTo(-600);
        await Assert.That(font.FontMetrics.HorizontalMetrics.LineGap).IsEqualTo(0);
        await Assert.That(font.FontMetrics.HorizontalMetrics.LineHeight).IsEqualTo(2789);
        await Assert.That(font.FontMetrics.HorizontalMetrics.AdvanceWidthMax).IsEqualTo(2470);
        await Assert.That(font.FontMetrics.HorizontalMetrics.AdvanceHeightMax).IsEqualTo(font.FontMetrics.HorizontalMetrics.LineHeight);

        await Assert.That(font.FontMetrics.SubscriptXSize).IsEqualTo(1331);
        await Assert.That(font.FontMetrics.SubscriptYSize).IsEqualTo(1229);
        await Assert.That(font.FontMetrics.SubscriptXOffset).IsEqualTo(0);
        await Assert.That(font.FontMetrics.SubscriptYOffset).IsEqualTo(154);

        await Assert.That(font.FontMetrics.SuperscriptXSize).IsEqualTo(1331);
        await Assert.That(font.FontMetrics.SuperscriptYSize).IsEqualTo(1229);
        await Assert.That(font.FontMetrics.SuperscriptXOffset).IsEqualTo(0);
        await Assert.That(font.FontMetrics.SuperscriptYOffset).IsEqualTo(717);

        await Assert.That(font.FontMetrics.StrikeoutSize).IsEqualTo(50);
        await Assert.That(font.FontMetrics.StrikeoutPosition).IsEqualTo(658);

        await Assert.That(font.FontMetrics.UnderlinePosition).IsEqualTo(-100);
        await Assert.That(font.FontMetrics.UnderlineThickness).IsEqualTo(50);

        await Assert.That(font.FontMetrics.ItalicAngle).IsEqualTo(0);

        await Assert.That(font.IsBold).IsFalse();
        await Assert.That(font.IsItalic).IsFalse();
    }

    [Test]
    public async Task FontMetricsVerticalFontMatchesReference()
    {
        // Compared to EveryFonts TTFDump metrics
        // https://everythingfonts.com/ttfdump
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.NotoSansSCThinFile);
        Font font = family.CreateFont(12);

        await Assert.That(font.FontMetrics.UnitsPerEm).IsEqualTo(1000);

        await Assert.That(font.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo(806);
        await Assert.That(font.FontMetrics.HorizontalMetrics.Descender).IsEqualTo(-256);
        await Assert.That(font.FontMetrics.HorizontalMetrics.LineGap).IsEqualTo(90);
        await Assert.That(font.FontMetrics.HorizontalMetrics.LineHeight).IsEqualTo(1152);
        await Assert.That(font.FontMetrics.HorizontalMetrics.AdvanceWidthMax).IsEqualTo(1000);
        await Assert.That(font.FontMetrics.HorizontalMetrics.AdvanceHeightMax).IsEqualTo(1000);

        await Assert.That(font.FontMetrics.VerticalMetrics.Ascender).IsEqualTo(451);
        await Assert.That(font.FontMetrics.VerticalMetrics.Descender).IsEqualTo(-453);
        await Assert.That(font.FontMetrics.VerticalMetrics.LineGap).IsEqualTo(90);
        await Assert.That(font.FontMetrics.VerticalMetrics.LineHeight).IsEqualTo(994);
        await Assert.That(font.FontMetrics.VerticalMetrics.AdvanceWidthMax).IsEqualTo(1000);
        await Assert.That(font.FontMetrics.VerticalMetrics.AdvanceHeightMax).IsEqualTo(1000);

        await Assert.That(font.FontMetrics.SubscriptXSize).IsEqualTo(650);
        await Assert.That(font.FontMetrics.SubscriptYSize).IsEqualTo(700);
        await Assert.That(font.FontMetrics.SubscriptXOffset).IsEqualTo(0);
        await Assert.That(font.FontMetrics.SubscriptYOffset).IsEqualTo(140);

        await Assert.That(font.FontMetrics.SuperscriptXSize).IsEqualTo(650);
        await Assert.That(font.FontMetrics.SuperscriptYSize).IsEqualTo(700);
        await Assert.That(font.FontMetrics.SuperscriptXOffset).IsEqualTo(0);
        await Assert.That(font.FontMetrics.SuperscriptYOffset).IsEqualTo(480);

        await Assert.That(font.FontMetrics.StrikeoutSize).IsEqualTo(49);
        await Assert.That(font.FontMetrics.StrikeoutPosition).IsEqualTo(258);

        await Assert.That(font.FontMetrics.UnderlinePosition).IsEqualTo(-75);
        await Assert.That(font.FontMetrics.UnderlineThickness).IsEqualTo(50);

        await Assert.That(font.FontMetrics.ItalicAngle).IsEqualTo(0);

        await Assert.That(font.IsBold).IsFalse();
        await Assert.That(font.IsItalic).IsFalse();
    }

    [Test]
    public async Task FontMetricsVerticalFontMatchesReferenceCFF()
    {
        // Compared to OpenTypeJS Font Inspector metrics
        // https://opentype.js.org/font-inspector.html
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.NotoSansKRRegular);
        Font font = family.CreateFont(12);

        await Assert.That(font.FontMetrics.UnitsPerEm).IsEqualTo(1000);

        await Assert.That(font.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo(1160);
        await Assert.That(font.FontMetrics.HorizontalMetrics.Descender).IsEqualTo(-288);
        await Assert.That(font.FontMetrics.HorizontalMetrics.LineGap).IsEqualTo(0);
        await Assert.That(font.FontMetrics.HorizontalMetrics.LineHeight).IsEqualTo(1448);
        await Assert.That(font.FontMetrics.HorizontalMetrics.AdvanceWidthMax).IsEqualTo(3000);
        await Assert.That(font.FontMetrics.HorizontalMetrics.AdvanceHeightMax).IsEqualTo(3000);

        await Assert.That(font.FontMetrics.VerticalMetrics.Ascender).IsEqualTo(500);
        await Assert.That(font.FontMetrics.VerticalMetrics.Descender).IsEqualTo(-500);
        await Assert.That(font.FontMetrics.VerticalMetrics.LineGap).IsEqualTo(0);
        await Assert.That(font.FontMetrics.VerticalMetrics.LineHeight).IsEqualTo(1000);
        await Assert.That(font.FontMetrics.VerticalMetrics.AdvanceWidthMax).IsEqualTo(3000);
        await Assert.That(font.FontMetrics.VerticalMetrics.AdvanceHeightMax).IsEqualTo(3000);

        await Assert.That(font.FontMetrics.SubscriptXSize).IsEqualTo(650);
        await Assert.That(font.FontMetrics.SubscriptYSize).IsEqualTo(600);
        await Assert.That(font.FontMetrics.SubscriptXOffset).IsEqualTo(0);
        await Assert.That(font.FontMetrics.SubscriptYOffset).IsEqualTo(75);

        await Assert.That(font.FontMetrics.SuperscriptXSize).IsEqualTo(650);
        await Assert.That(font.FontMetrics.SuperscriptYSize).IsEqualTo(600);
        await Assert.That(font.FontMetrics.SuperscriptXOffset).IsEqualTo(0);
        await Assert.That(font.FontMetrics.SuperscriptYOffset).IsEqualTo(350);

        await Assert.That(font.FontMetrics.StrikeoutSize).IsEqualTo(50);
        await Assert.That(font.FontMetrics.StrikeoutPosition).IsEqualTo(325);

        await Assert.That(font.FontMetrics.UnderlinePosition).IsEqualTo(-125);
        await Assert.That(font.FontMetrics.UnderlineThickness).IsEqualTo(50);

        await Assert.That(font.FontMetrics.ItalicAngle).IsEqualTo(0);

        await Assert.That(font.IsBold).IsFalse();
        await Assert.That(font.IsItalic).IsFalse();
    }

    [Test]
    public async Task GlyphMetricsMatchesReference()
    {
        // Compared to EveryFonts TTFDump metrics
        // https://everythingfonts.com/ttfdump
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.OpenSansFile);
        Font font = family.CreateFont(12);

        var codePoint = new CodePoint('A');

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            codePoint,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics> metrics)).IsTrue();
        GlyphMetrics glyphMetrics = metrics[0];

        await Assert.That(glyphMetrics.CodePoint).IsEqualTo(codePoint);
        await Assert.That(glyphMetrics.UnitsPerEm).IsEqualTo(font.FontMetrics.UnitsPerEm);
        await Assert.That(glyphMetrics.ScaleFactor).IsEqualTo(new Vector2(glyphMetrics.UnitsPerEm * 72F));
        await Assert.That(glyphMetrics.AdvanceWidth).IsEqualTo(1295);
        await Assert.That(glyphMetrics.AdvanceHeight).IsEqualTo(2789);
        await Assert.That(glyphMetrics.Width).IsEqualTo(1293);
        await Assert.That(glyphMetrics.Height).IsEqualTo(1468);
        await Assert.That(glyphMetrics.LeftSideBearing).IsEqualTo(0);
        await Assert.That(glyphMetrics.TopSideBearing).IsEqualTo(721);
        await Assert.That(glyphMetrics.GlyphType).IsEqualTo(GlyphType.Standard);
    }

    [Test]
    public async Task GlyphMetricsMatchesReference_WithWoff1format()
    {
        // Compared to EveryFonts TTFDump metrics
        // https://everythingfonts.com/ttfdump
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.OpenSansFileWoff1);
        Font font = family.CreateFont(12);

        var codePoint = new CodePoint('A');

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            codePoint,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics> metrics)).IsTrue();
        GlyphMetrics glyphMetrics = metrics[0];

        await Assert.That(glyphMetrics.CodePoint).IsEqualTo(codePoint);
        await Assert.That(glyphMetrics.UnitsPerEm).IsEqualTo(font.FontMetrics.UnitsPerEm);
        await Assert.That(glyphMetrics.ScaleFactor).IsEqualTo(new Vector2(glyphMetrics.UnitsPerEm * 72F));
        await Assert.That(glyphMetrics.AdvanceWidth).IsEqualTo(1295);
        await Assert.That(glyphMetrics.AdvanceHeight).IsEqualTo(2789);
        await Assert.That(glyphMetrics.Width).IsEqualTo(1293);
        await Assert.That(glyphMetrics.Height).IsEqualTo(1468);
        await Assert.That(glyphMetrics.LeftSideBearing).IsEqualTo(0);
        await Assert.That(glyphMetrics.TopSideBearing).IsEqualTo(721);
        await Assert.That(glyphMetrics.GlyphType).IsEqualTo(GlyphType.Standard);
    }

    [Test]
    public async Task GlyphMetricsMatchesReference_WithWoff2format()
    {
        // Compared to EveryFonts TTFDump metrics
        // https://everythingfonts.com/ttfdump
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.OpenSansFileWoff2);
        Font font = family.CreateFont(12);

        var codePoint = new CodePoint('A');

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            codePoint,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics> metrics)).IsTrue();
        GlyphMetrics glyphMetrics = metrics[0];

        await Assert.That(glyphMetrics.CodePoint).IsEqualTo(codePoint);
        await Assert.That(glyphMetrics.UnitsPerEm).IsEqualTo(font.FontMetrics.UnitsPerEm);
        await Assert.That(glyphMetrics.ScaleFactor).IsEqualTo(new Vector2(glyphMetrics.UnitsPerEm * 72F));
        await Assert.That(glyphMetrics.AdvanceWidth).IsEqualTo(1295);
        await Assert.That(glyphMetrics.AdvanceHeight).IsEqualTo(2789);
        await Assert.That(glyphMetrics.Width).IsEqualTo(1293);
        await Assert.That(glyphMetrics.Height).IsEqualTo(1468);
        await Assert.That(glyphMetrics.LeftSideBearing).IsEqualTo(0);
        await Assert.That(glyphMetrics.TopSideBearing).IsEqualTo(721);
        await Assert.That(glyphMetrics.GlyphType).IsEqualTo(GlyphType.Standard);
    }

    [Test]
    public async Task GlyphMetricsVerticalMatchesReference()
    {
        // Compared to EveryFonts TTFDump metrics
        // https://everythingfonts.com/ttfdump
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.NotoSansSCThinFile);
        Font font = family.CreateFont(12);

        var codePoint = new CodePoint('A');

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            codePoint,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics> metrics)).IsTrue();
        GlyphMetrics glyphMetrics = metrics[0];

        // Position 0.
        await Assert.That(glyphMetrics.CodePoint).IsEqualTo(codePoint);
        await Assert.That(glyphMetrics.UnitsPerEm).IsEqualTo(font.FontMetrics.UnitsPerEm);
        await Assert.That(glyphMetrics.ScaleFactor).IsEqualTo(new Vector2(glyphMetrics.UnitsPerEm * 72F));
        await Assert.That(glyphMetrics.AdvanceWidth).IsEqualTo(364);
        await Assert.That(glyphMetrics.AdvanceHeight).IsEqualTo(1000);
        await Assert.That(glyphMetrics.Width).IsEqualTo(265);
        await Assert.That(glyphMetrics.Height).IsEqualTo(666);
        await Assert.That(glyphMetrics.LeftSideBearing).IsEqualTo(33);
        await Assert.That(glyphMetrics.TopSideBearing).IsEqualTo(134);
        await Assert.That(glyphMetrics.GlyphType).IsEqualTo(GlyphType.Fallback);
    }
}
