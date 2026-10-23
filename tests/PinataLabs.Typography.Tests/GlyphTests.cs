// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using NSubstitute;
using PinataLabs.Typography.Tables.TrueType;
using PinataLabs.Typography.Tables.TrueType.Glyphs;
using PinataLabs.Typography.Tests.Fakes;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

public class GlyphTests
{
    private readonly GlyphRenderer renderer = new();

    [Test]
    public async Task RenderToPointAndSingleDPI()
    {
        const string text = "A";
        CodePoint codePoint = this.AsCodePoint(text);
        Font font = CreateFont(text, 10);
        TextRun textRun = new() { Start = 0, End = 1, Font = font };

        FontMetrics metrics = font.FontMetrics;
        TrueTypeGlyphMetrics glyphMetrics = new(
            (StreamFontMetrics)metrics,
            0,
            codePoint,
            new GlyphVector(
                Array.Empty<ControlPoint>(),
                Array.Empty<ushort>(),
                new Bounds(0, metrics.UnitsPerEm, 0, metrics.UnitsPerEm),
                Array.Empty<byte>(),
                false),
            0,
            0,
            0,
            0,
            metrics.UnitsPerEm,
            textRun.TextAttributes,
            textRun.TextDecorations);

        Glyph glyph = new(glyphMetrics.CloneForRendering(textRun), font.Size);

        Vector2 locationInFontSpace = new Vector2(99, 99) / 72; // glyph ends up 10px over due to offset in fake glyph
        glyph.RenderTo(this.renderer, locationInFontSpace, Vector2.Zero, GlyphLayoutMode.Horizontal, new TextOptions(font));

        await Assert.That(this.renderer.GlyphRects.Single()).IsEqualTo(new FontRectangle(99, 89, 0, 0));
    }

    [Test]
    public async Task IdenticalGlyphsInDifferentPlacesCreateIdenticalKeys()
    {
        Font fakeFont = CreateFont("AB");
        var textRenderer = new TextRenderer(this.renderer);

        textRenderer.RenderText("ABA", new TextOptions(fakeFont));

        await Assert.That(this.renderer.GlyphKeys[2]).IsEqualTo(this.renderer.GlyphKeys[0]);
        await Assert.That(this.renderer.GlyphKeys[2]).IsNotEqualTo(this.renderer.GlyphKeys[1]);
    }

    [Test]
    public void BeginGlyph_ReturnsFalse_SkipRenderingFigures()
    {
        IGlyphRenderer renderer = Substitute.For<IGlyphRenderer>();
        renderer.BeginGlyph(default, default).ReturnsForAnyArgs(false);
        Font fakeFont = CreateFont("A");
        var textRenderer = new TextRenderer(renderer);

        textRenderer.RenderText("ABA", new TextOptions(fakeFont));
        renderer.DidNotReceive().BeginFigure();
    }

    [Test]
    public void BeginGlyph_ReturnsTrue_RendersFigures()
    {
        IGlyphRenderer renderer = Substitute.For<IGlyphRenderer>();
        renderer.BeginGlyph(default, default).ReturnsForAnyArgs(true);
        Font fakeFont = CreateFont("A");
        var textRenderer = new TextRenderer(renderer);

        textRenderer.RenderText("ABA", new TextOptions(fakeFont));
        renderer.Received(3).BeginFigure();
    }

    public static Font CreateFont(string text, float pointSize = 1)
    {
        var fc = (IFontMetricsCollection)new FontCollection();
        Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
        return new Font(d, pointSize);
    }

    [Test]
    public async Task LoadGlyph()
    {
        Font font = new FontCollection().Add(TestFonts.SimpleFontFileData()).CreateFont(12);

        // Get letter A
        await Assert.That(font.TryGetGlyphs(new CodePoint(41), ColorFontSupport.None, out IReadOnlyList<Glyph>? glyphs)).IsTrue();
        Glyph g = glyphs![0];
        GlyphVector instance = ((TrueTypeGlyphMetrics)g.GlyphMetrics).GetOutline();

        await Assert.That(instance.ControlPoints.Count).IsEqualTo(20);
    }

    [Test]
    public async Task RenderColrGlyph()
    {
        Font font = new FontCollection().Add(TestFonts.TwemojiMozillaData()).CreateFont(12);

        // Get letter Grinning Face emoji
        var instance = (StreamFontMetrics)font.FontMetrics;
        CodePoint codePoint = this.AsCodePoint("😀");
        await Assert.That(instance.TryGetGlyphId(codePoint, out ushort idx)).IsTrue();
        IEnumerable<GlyphMetrics> vectors = instance.GetGlyphMetrics(
            codePoint,
            idx,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.MicrosoftColrFormat);

        await Assert.That(vectors.Count()).IsEqualTo(3);
    }

    [Test]
    public async Task RenderColrGlyphTextRenderer()
    {
        Font font = new FontCollection().Add(TestFonts.TwemojiMozillaData()).CreateFont(12);

        var renderer = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(renderer, "😀", new TextOptions(font)
        {
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });

        await Assert.That(renderer.Colors.Count).IsEqualTo(3);
    }

    [Test]
    public async Task RenderColrGlyphWithVariationSelector()
    {
        Font font = new FontCollection().Add(TestFonts.TwemojiMozillaData()).CreateFont(12);

        string text = "\u263A\uFE0F"; // Fully-qualified sequence for emoji 'smiling face'
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));

        // Check that no glyphs were generated by the variation selector
        await Assert.That(layout.All(v => v.Glyph.GlyphMetrics.CodePoint.Value == 0x263A)).IsTrue();
        await Assert.That(layout.Count).IsEqualTo(4);
    }

    [Test]
    public async Task EmojiWidthIsComputedCorrectlyWithSubstitutionOnZwj()
    {
        Font font = new FontCollection().Add(TestFonts.SegoeuiEmojiData()).CreateFont(72);

        string text = "\U0001F469\U0001F3FB\u200D\U0001F91D\u200D\U0001F469\U0001F3FC"; // women holding hands: light skin tone, medium-light skin tone
        string text2 = "\U0001F46D\U0001F3FB"; // women holding hands: light skin tone

        FontRectangle size = TextMeasurer.MeasureSize(text, new TextOptions(font));
        FontRectangle size2 = TextMeasurer.MeasureSize(text2, new TextOptions(font));

        await Assert.That(size.Width).IsEqualTo(75F);
        await Assert.That(size2.Width).IsEqualTo(75F);
    }

    [Test]
    [Arguments(false, false, 1238)]
    [Arguments(false, true, 1238)]
    [Arguments(true, false, 1238)]
    [Arguments(true, true, 1238)]
    public async Task RenderWoffGlyphs_IsEqualToTtfGlyphs(bool applyKerning, bool applyHinting, int expectedControlPoint)
    {
        Font fontTtf = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(12);
        Font fontWoff = new FontCollection().Add(TestFonts.OpenSansFileWoff1).CreateFont(12);
        string testStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        var rendererTtf = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererTtf, testStr, new TextOptions(fontTtf)
        {
            KerningMode = applyKerning ? KerningMode.Standard : KerningMode.None,
            HintingMode = applyHinting ? HintingMode.Standard : HintingMode.None,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });
        var rendererWoff = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererWoff, testStr, new TextOptions(fontWoff)
        {
            KerningMode = applyKerning ? KerningMode.Standard : KerningMode.None,
            HintingMode = applyHinting ? HintingMode.Standard : HintingMode.None,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });

        await Assert.That(rendererWoff.ControlPoints.Count).IsEqualTo(expectedControlPoint);
        await Assert.That(rendererTtf.ControlPoints.SequenceEqual(rendererWoff.ControlPoints)).IsTrue();
    }

    [Test]
    [Arguments("\uFB00")]
    [Arguments("\uFB01")]
    [Arguments("\uFB02")]
    [Arguments("\uFB03")]
    [Arguments("\uFB04")]
    public async Task RenderWoff_CompositeGlyphs_IsEqualToTtfGlyphs(string testStr)
    {
        Font fontTtf = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(12);
        Font fontWoff = new FontCollection().Add(TestFonts.OpenSansFileWoff1).CreateFont(12);

        var rendererTtf = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererTtf, testStr, new TextOptions(fontTtf)
        {
            HintingMode = HintingMode.Standard,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });
        var rendererWoff = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererWoff, testStr, new TextOptions(fontWoff)
        {
            HintingMode = HintingMode.Standard,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });

        await Assert.That(rendererTtf.ControlPoints.Count > 0).IsTrue();
        await Assert.That(rendererTtf.ControlPoints.SequenceEqual(rendererWoff.ControlPoints)).IsTrue();
    }

    [Test]
    [Arguments(false, false, 1238)]
    [Arguments(false, true, 1238)]
    [Arguments(true, false, 1238)]
    [Arguments(true, true, 1238)]
    public async Task RenderWoff2Glyphs_IsEqualToTtfGlyphs(bool applyKerning, bool applyHinting, int expectedControlPoints)
    {
        Font fontTtf = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(12);
        Font fontWoff2 = new FontCollection().Add(TestFonts.OpenSansFileWoff2).CreateFont(12);
        string testStr = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        var rendererTtf = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererTtf, testStr, new TextOptions(fontTtf)
        {
            KerningMode = applyKerning ? KerningMode.Standard : KerningMode.None,
            HintingMode = applyHinting ? HintingMode.Standard : HintingMode.None,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });
        var rendererWoff2 = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererWoff2, testStr, new TextOptions(fontWoff2)
        {
            KerningMode = applyKerning ? KerningMode.Standard : KerningMode.None,
            HintingMode = applyHinting ? HintingMode.Standard : HintingMode.None,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });

        await Assert.That(rendererWoff2.ControlPoints.Count).IsEqualTo(expectedControlPoints);
        await Assert.That(rendererTtf.ControlPoints.SequenceEqual(rendererWoff2.ControlPoints)).IsTrue();
    }

    [Test]
    [Arguments("\uFB00")]
    [Arguments("\uFB01")]
    [Arguments("\uFB02")]
    [Arguments("\uFB03")]
    [Arguments("\uFB04")]
    public async Task RenderWoff2_CompositeGlyphs_IsEqualToTtfGlyphs(string testStr)
    {
        Font fontTtf = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(12);
        Font fontWoff2 = new FontCollection().Add(TestFonts.OpenSansFileWoff2).CreateFont(12);

        var rendererTtf = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererTtf, testStr, new TextOptions(fontTtf)
        {
            HintingMode = HintingMode.Standard,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });
        var rendererWoff2 = new ColorGlyphRenderer();
        TextRenderer.RenderTextTo(rendererWoff2, testStr, new TextOptions(fontWoff2)
        {
            HintingMode = HintingMode.Standard,
            ColorFontSupport = ColorFontSupport.MicrosoftColrFormat
        });

        await Assert.That(rendererTtf.ControlPoints.Count > 0).IsTrue();
        await Assert.That(rendererTtf.ControlPoints.SequenceEqual(rendererWoff2.ControlPoints)).IsTrue();
    }

#if OS_WINDOWS
    [Test]
    [Arguments("Arial")]
    [Arguments("Segoe UI Emoji")]
    public async Task RendererIsThreadsafe(string fontName)
    {
        const int threadCount = 10;
        var results = new (int Count1, int Count2, bool SameControlPoints)[threadCount];
        Parallel.For(0, threadCount, i =>
        {
            ColorGlyphRenderer renderer1 = new();
            TextRenderer.RenderTextTo(renderer1, "A 🙂 ", new TextOptions(SystemFonts.CreateFont(fontName, 15)));

            ColorGlyphRenderer renderer2 = new();
            TextRenderer.RenderTextTo(renderer2, "A 🙂 ", new TextOptions(SystemFonts.CreateFont(fontName, 15)));

            results[i] = (
                renderer1.ControlPoints.Count,
                renderer2.ControlPoints.Count,
                renderer1.ControlPoints.SequenceEqual(renderer2.ControlPoints));
        });

        foreach ((int count1, int count2, bool sameControlPoints) in results)
        {
            await Assert.That(count1 > 0).IsTrue();
            await Assert.That(count2 > 0).IsTrue();
            await Assert.That(sameControlPoints).IsTrue();
        }
    }

#endif
    private CodePoint AsCodePoint(string text) => CodePoint.DecodeFromUtf16At(text.AsSpan(), 0);
}
