// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Fakes;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

public class TextLayoutTests
{
    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom, true)]
    [Arguments(LayoutMode.HorizontalBottomTop, true)]
    [Arguments(LayoutMode.VerticalLeftRight, false)]
    [Arguments(LayoutMode.VerticalRightLeft, false)]
    [Arguments(LayoutMode.VerticalMixedLeftRight, false)]
    [Arguments(LayoutMode.VerticalMixedRightLeft, false)]
    public async Task CanDetectHorizontalLayoutMode(LayoutMode mode, bool vertical)
    {
        await Assert.That(mode.IsHorizontal()).IsEqualTo(vertical);
    }

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom, false)]
    [Arguments(LayoutMode.HorizontalBottomTop, false)]
    [Arguments(LayoutMode.VerticalLeftRight, true)]
    [Arguments(LayoutMode.VerticalRightLeft, true)]
    [Arguments(LayoutMode.VerticalMixedLeftRight, false)]
    [Arguments(LayoutMode.VerticalMixedRightLeft, false)]
    public async Task CanDetectVerticalLayoutMode(LayoutMode mode, bool vertical)
    {
        await Assert.That(mode.IsVertical()).IsEqualTo(vertical);
    }

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom, false)]
    [Arguments(LayoutMode.HorizontalBottomTop, false)]
    [Arguments(LayoutMode.VerticalLeftRight, false)]
    [Arguments(LayoutMode.VerticalRightLeft, false)]
    [Arguments(LayoutMode.VerticalMixedLeftRight, true)]
    [Arguments(LayoutMode.VerticalMixedRightLeft, true)]
    public async Task CanDetectVerticalMixedLayoutMode(LayoutMode mode, bool vertical)
    {
        await Assert.That(mode.IsVerticalMixed()).IsEqualTo(vertical);
    }

    [Test]
    public async Task FakeFontGetGlyph()
    {
        Font font = CreateFont("hello world");

        await Assert.That(font.TryGetGlyphs(new CodePoint('h'), ColorFontSupport.None, out IReadOnlyList<Glyph>? glyphs)).IsTrue();
        Glyph glyph = glyphs![0];
        await Assert.That(glyph).IsNotEqualTo(default);
    }

    [Test]
    [Arguments(
        VerticalAlignment.Top,
        HorizontalAlignment.Left,
        0,
        10)]
    [Arguments(
        VerticalAlignment.Top,
        HorizontalAlignment.Right,
        0,
        -320)]
    [Arguments(
        VerticalAlignment.Top,
        HorizontalAlignment.Center,
        0,
        -155)]
    [Arguments(
        VerticalAlignment.Bottom,
        HorizontalAlignment.Left,
        -60,
        10)]
    [Arguments(
        VerticalAlignment.Bottom,
        HorizontalAlignment.Right,
        -60,
        -320)]
    [Arguments(
        VerticalAlignment.Bottom,
        HorizontalAlignment.Center,
        -60,
        -155)]
    [Arguments(
        VerticalAlignment.Center,
        HorizontalAlignment.Left,
        -30,
        10)]
    [Arguments(
        VerticalAlignment.Center,
        HorizontalAlignment.Right,
        -30,
        -320)]
    [Arguments(
        VerticalAlignment.Center,
        HorizontalAlignment.Center,
        -30,
        -155)]
    public async Task CanAlignText(
        VerticalAlignment vertical,
        HorizontalAlignment horizontal,
        float top,
        float left)
    {
        const string text = "hello world\nhello";
        Font font = CreateFont(text);

        TextOptions options = new(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };

        IReadOnlyList<GlyphLayout> glyphsToRender = TextLayout.GenerateLayout(text.AsSpan(), options);
        FontRectangle bound = TextMeasurer.GetBounds(glyphsToRender, options.Dpi);

        await Assert.That(Math.Round(bound.Width, 3)).IsEqualTo(310);
        await Assert.That(Math.Round(bound.Height, 3)).IsEqualTo(40);
        await Assert.That(Math.Round(bound.Left, 3)).IsEqualTo(Math.Round(left, 3));
        await Assert.That(Math.Round(bound.Top, 3)).IsEqualTo(Math.Round(top, 3));
    }

    [Test]
    [Arguments(
        VerticalAlignment.Top,
        HorizontalAlignment.Left,
        0,
        180)]
    [Arguments(
        VerticalAlignment.Top,
        HorizontalAlignment.Right,
        0,
        -320)]
    [Arguments(
        VerticalAlignment.Top,
        HorizontalAlignment.Center,
        0,
        -70)]
    [Arguments(
        VerticalAlignment.Bottom,
        HorizontalAlignment.Left,
        -60,
        180)]
    [Arguments(
        VerticalAlignment.Bottom,
        HorizontalAlignment.Right,
        -60,
        -320)]
    [Arguments(
        VerticalAlignment.Bottom,
        HorizontalAlignment.Center,
        -60,
        -70)]
    [Arguments(
        VerticalAlignment.Center,
        HorizontalAlignment.Left,
        -30,
        180)]
    [Arguments(
        VerticalAlignment.Center,
        HorizontalAlignment.Right,
        -30,
        -320)]
    [Arguments(
        VerticalAlignment.Center,
        HorizontalAlignment.Center,
        -30,
        -70)]
    public async Task CanAlignWithWrapping(
        VerticalAlignment vertical,
        HorizontalAlignment horizontal,
        float top,
        float left)
    {
        // Using a string with a forced line break shorter than the wrapping
        // width covers cases where the offset should be expanded for both single and multiple lines.
        const string text = "hello world\nhello";
        Font font = CreateFont(text);

        TextOptions options = new(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            WrappingLength = 500,
            TextAlignment = TextAlignment.End
        };

        IReadOnlyList<GlyphLayout> glyphsToRender = TextLayout.GenerateLayout(text.AsSpan(), options);
        FontRectangle bound = TextMeasurer.GetBounds(glyphsToRender, options.Dpi);

        await Assert.That(Math.Round(bound.Width, 3)).IsEqualTo(310);
        await Assert.That(Math.Round(bound.Height, 3)).IsEqualTo(40);
        await Assert.That(Math.Round(bound.Left, 3)).IsEqualTo(Math.Round(left, 3));
        await Assert.That(Math.Round(bound.Top, 3)).IsEqualTo(Math.Round(top, 3));
    }

    [Test]
    public async Task MeasureTextWithSpan()
    {
        Font font = CreateFont("hello");

        FontRectangle size = Measure(font);

        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(10);
        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(130);

        static FontRectangle Measure(Font font)
        {
            Span<char> text = stackalloc char[]
            {
                'h',
                'e',
                'l',
                'l',
                'o'
            };

            // 72 * emSize means 1pt = 1px
            return TextMeasurer.MeasureBounds(text, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        }
    }

    [Test]
    [Arguments("h", 10, 10)]
    [Arguments("he", 10, 40)]
    [Arguments("hel", 10, 70)]
    [Arguments("hello", 10, 130)]
    [Arguments("hello world", 10, 310)]
    [Arguments("hello world\nhello world", 40, 310)]
    [Arguments("hello\nworld", 40, 130)]
    public async Task MeasureText(string text, float height, float width)
    {
        Font font = CreateFont(text);
        FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
    }

    [Test]
    public async Task TryMeasureCharacterBounds()
    {
        const string text = "a b\nc";
        GlyphBounds[] expectedGlyphMetrics =
        {
            new(new CodePoint('a'), new FontRectangle(10, 0, 10, 10)),
            new(new CodePoint(' '), new FontRectangle(40, 0, 30, 10)),
            new(new CodePoint('b'), new FontRectangle(70, 0, 10, 10)),
            new(new CodePoint('c'), new FontRectangle(10, 30, 10, 10)),
        };
        Font font = CreateFont(text);

        bool measuredBounds = TextMeasurer.TryMeasureCharacterBounds(
            text.AsSpan(),
            new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor },
            out ReadOnlySpan<GlyphBounds> boundsSpan);
        GlyphBounds[] bounds = boundsSpan.ToArray();
        await Assert.That(measuredBounds).IsTrue();

        // Newline should not be returned.
        await Assert.That(bounds.Length).IsEqualTo(text.Length - 1);
        for (int i = 0; i < bounds.Length; i++)
        {
            GlyphBounds expected = expectedGlyphMetrics[i];
            GlyphBounds actual = bounds[i];
            await Assert.That(actual.Codepoint).IsEqualTo(expected.Codepoint);

            // 4 dp as there is minor offset difference in the float values
            await Assert.That(Math.Round(actual.Bounds.X, 4)).IsEqualTo(Math.Round(expected.Bounds.X, 4));
            await Assert.That(Math.Round(actual.Bounds.Y, 4)).IsEqualTo(Math.Round(expected.Bounds.Y, 4));
            await Assert.That(Math.Round(actual.Bounds.Height, 4)).IsEqualTo(Math.Round(expected.Bounds.Height, 4));
            await Assert.That(Math.Round(actual.Bounds.Width, 4)).IsEqualTo(Math.Round(expected.Bounds.Width, 4));
        }
    }

    [Test]
    [Arguments("hello world", 10, 310)]
    [Arguments(
        "hello world hello world hello world",
        70, // 30 actual line height * 2 + 10 actual height
        310)]
    [Arguments(// See upstream drawing library issue #115 (pre-fork)
        "这是一段长度超出设定的换行宽度的文本，但是没有在设定的宽度处换行。这段文本用于演示问题。希望可以修复。如果有需要可以联系我。",
        160, // 30 actual line height * 2 + 10 actual height
        310)]
    public async Task MeasureTextWordWrappingHorizontalTopBottom(string text, float height, float width)
    {
        Font font = CreateFont(text);
        FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            WrappingLength = 350,
            LayoutMode = LayoutMode.HorizontalTopBottom
        });

        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
    }

    [Test]
    [Arguments("hello world", 10, 310)]
    [Arguments(
        "hello world hello world hello world",
        70, // 30 actual line height * 2 + 10 actual height
        310)]
    [Arguments(// See upstream drawing library issue #115 (pre-fork)
        "这是一段长度超出设定的换行宽度的文本，但是没有在设定的宽度处换行。这段文本用于演示问题。希望可以修复。如果有需要可以联系我。",
        160, // 30 actual line height * 2 + 10 actual height
        310)]
    public async Task MeasureTextWordWrappingHorizontalBottomTop(string text, float height, float width)
    {
        Font font = CreateFont(text);
        FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            WrappingLength = 350,
            LayoutMode = LayoutMode.HorizontalBottomTop
        });

        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
    }

    [Test]
    [Arguments("hello world", 310, 10)]
    [Arguments("hello world hello world hello world", 310, 70)]
    [Arguments("这是一段长度超出设定的换行宽度的文本，但是没有在设定的宽度处换行。这段文本用于演示问题。希望可以修复。如果有需要可以联系我。", 310, 160)]
    public async Task MeasureTextWordWrappingVerticalLeftRight(string text, float height, float width)
    {
        Font font = CreateFont(text);
        FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            WrappingLength = 350,
            LayoutMode = LayoutMode.VerticalLeftRight
        });

        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
    }

    [Test]
    [Arguments("hello world", 310, 10)]
    [Arguments("hello world hello world hello world", 310, 70)]
    [Arguments("这是一段长度超出设定的换行宽度的文本，但是没有在设定的宽度处换行。这段文本用于演示问题。希望可以修复。如果有需要可以联系我。", 310, 160)]
    public async Task MeasureTextWordWrappingVerticalRightLeft(string text, float height, float width)
    {
        Font font = CreateFont(text);
        FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            WrappingLength = 350,
            LayoutMode = LayoutMode.VerticalRightLeft
        });

        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
    }

    [Test]
    [Arguments("hello world", 310, 10)]
    [Arguments("hello world hello world hello world", 310, 70)]
    [Arguments("这是一段长度超出设定的换行宽度的文本，但是没有在设定的宽度处换行。这段文本用于演示问题。希望可以修复。如果有需要可以联系我。", 310, 160)]
    public async Task MeasureTextWordWrappingVerticalMixedLeftRight(string text, float height, float width)
    {
        Font font = CreateFont(text);
        FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            WrappingLength = 350,
            LayoutMode = LayoutMode.VerticalMixedLeftRight
        });

        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
    }

#if OS_WINDOWS
    [Test]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious Taumatawhakatangihangakoauauotamateaturipukakapikimaungahoronukupokaiwhenuakitanatahu グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalTopBottom, WordBreaking.Standard, 100, 870)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious Taumatawhakatangihangakoauauotamateaturipukakapikimaungahoronukupokaiwhenuakitanatahu グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalTopBottom, WordBreaking.BreakAll, 120, 399)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious Taumatawhakatangihangakoauauotamateaturipukakapikimaungahoronukupokaiwhenuakitanatahu グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalTopBottom, WordBreaking.BreakWord, 120, 400)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalTopBottom, WordBreaking.KeepAll, 60, 699)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious Taumatawhakatangihangakoauauotamateaturipukakapikimaungahoronukupokaiwhenuakitanatahu グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalBottomTop, WordBreaking.Standard, 101, 870)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious Taumatawhakatangihangakoauauotamateaturipukakapikimaungahoronukupokaiwhenuakitanatahu グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalBottomTop, WordBreaking.BreakAll, 121, 399)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious Taumatawhakatangihangakoauauotamateaturipukakapikimaungahoronukupokaiwhenuakitanatahu グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalBottomTop, WordBreaking.BreakWord, 121, 400)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious グレートブリテンおよび北アイルランド連合王国という言葉は本当に長い言葉", LayoutMode.HorizontalBottomTop, WordBreaking.KeepAll, 61, 699)]
    public async Task MeasureTextWordBreak(string text, LayoutMode layoutMode, WordBreaking wordBreaking, float height, float width)
    {
        // Testing using Windows only to ensure that actual glyphs are rendered
        // against known physically tested values.
        FontFamily arial = SystemFonts.Get("Arial");
        FontFamily jhengHei = SystemFonts.Get("Microsoft JhengHei");

        Font font = arial.CreateFont(20);
        FontRectangle size = TextMeasurer.MeasureSize(
            text,
            new TextOptions(font)
            {
                WrappingLength = 400,
                LayoutMode = layoutMode,
                WordBreaking = wordBreaking,
                FallbackFontFamilies = new[] { jhengHei }
            });

        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
    }
#endif

    [Test]
    [Arguments("ab", 477, 1081, false)] // no kerning rules defined for lowercase ab so widths should stay the same
    [Arguments("ab", 477, 1081, true)]
    [Arguments("AB", 465, 1033, false)] // width changes between kerning enabled or not
    [Arguments("AB", 465, 654, true)]
    public async Task MeasureTextWithKerning(string text, float height, float width, bool applyKerning)
    {
        var c = new FontCollection();
        Font font = c.Add(TestFonts.SimpleFontFileData()).CreateFont(12);
        FontRectangle size = TextMeasurer.MeasureBounds(
            text,
            new TextOptions(new Font(font, 1))
            {
                Dpi = font.FontMetrics.ScaleFactor,
                KerningMode = applyKerning ? KerningMode.Standard : KerningMode.None,
            });

        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(Math.Round(height, 4));
        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(Math.Round(width, 4));
    }

    [Test]
    [Arguments("a", 100, 100, 125, 396)]
    public async Task LayoutWithLocation(string text, float x, float y, float expectedX, float expectedY)
    {
        var c = new FontCollection();
        Font font = c.Add(TestFonts.SimpleFontFileData()).CreateFont(12);

        var glyphRenderer = new GlyphRenderer();
        var renderer = new TextRenderer(glyphRenderer);
        renderer.RenderText(
            text,
            new TextOptions(new Font(font, 1))
            {
                Dpi = font.FontMetrics.ScaleFactor,
                Origin = new Vector2(x, y)
            });

        await Assert.That(Math.Round(glyphRenderer.GlyphRects[0].Location.X, 2)).IsEqualTo(Math.Round(expectedX, 2));
        await Assert.That(Math.Round(glyphRenderer.GlyphRects[0].Location.Y, 2)).IsEqualTo(Math.Round(expectedY, 2));
    }

    // See upstream issue #244 (pre-fork)
    [Test]
    public async Task MeasureTextLeadingFraction()
    {
        FontCollection c = new();
        Font font = c.Add(TestFonts.SimpleFontFileData()).CreateFont(12);
        TextOptions textOptions = new(font);
        FontRectangle measurement = TextMeasurer.MeasureSize("/ This will fail", textOptions);

        await Assert.That(measurement).IsNotEqualTo(FontRectangle.Empty);
    }

    [Test]
    [Arguments("hello world", 1)]
    [Arguments("hello world\nhello world", 2)]
    [Arguments("hello world\nhello world\nhello world", 3)]
    public async Task CountLines(string text, int usedLines)
    {
        Font font = CreateFont(text);
        int count = TextMeasurer.CountLines(text, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

        await Assert.That(count).IsEqualTo(usedLines);
    }

    [Test]
    public async Task CountLinesWithSpan()
    {
        Font font = CreateFont("hello\n!");

        int count = CountLines(font);

        await Assert.That(count).IsEqualTo(2);

        static int CountLines(Font font)
        {
            Span<char> text = stackalloc char[]
            {
                'h',
                'e',
                'l',
                'l',
                'o',
                '\n',
                '!'
            };

            return TextMeasurer.CountLines(text, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        }
    }

    [Test]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious", 25, 7)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious", 50, 7)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious", 100, 6)]
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious", 200, 6)]
    public async Task CountLinesWrappingLength(string text, int wrappingLength, int usedLines)
    {
        Font font = CreateFont(text);
        int count = TextMeasurer.CountLines(text, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor, WrappingLength = wrappingLength });

        await Assert.That(count).IsEqualTo(usedLines);
    }

    [Test]
    public async Task BuildTextRuns_EmptyReturnsDefaultRun()
    {
        const string text = "This is a long and Honorificabilitudinitatibus califragilisticexpialidocious";
        Font font = CreateFont(text);
        TextOptions options = new(font);

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count == 1).IsTrue();
        await Assert.That(runs[0].Font).IsEqualTo(font);
        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].End).IsEqualTo(CodePoint.GetCodePointCount(text.AsSpan()));
    }

    [Test]
    public async Task BuildTextRuns_ReturnsCreatesInterimRuns()
    {
        const string text = "This is a long and Honorificabilitudinitatibus califragilisticexpialidocious";
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 9, End = 23, Font = font2 },
                new TextRun() { Start = 35, End = 54, Font = font2 },
                new TextRun() { Start = 68, End = 70, Font = font2 },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count == 7).IsTrue();

        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].End).IsEqualTo(9);
        await Assert.That(runs[0].Font).IsEqualTo(font);
        await Assert.That(runs[0].Slice(text.AsSpan()).Length).IsEqualTo(9);

        await Assert.That(runs[1].Start).IsEqualTo(9);
        await Assert.That(runs[1].End).IsEqualTo(23);
        await Assert.That(runs[1].Font).IsEqualTo(font2);
        await Assert.That(runs[1].Slice(text.AsSpan()).Length).IsEqualTo(14);

        await Assert.That(runs[2].Start).IsEqualTo(23);
        await Assert.That(runs[2].End).IsEqualTo(35);
        await Assert.That(runs[2].Font).IsEqualTo(font);
        await Assert.That(runs[2].Slice(text.AsSpan()).Length).IsEqualTo(12);

        await Assert.That(runs[3].Start).IsEqualTo(35);
        await Assert.That(runs[3].End).IsEqualTo(54);
        await Assert.That(runs[3].Font).IsEqualTo(font2);
        await Assert.That(runs[3].Slice(text.AsSpan()).Length).IsEqualTo(19);

        await Assert.That(runs[4].Start).IsEqualTo(54);
        await Assert.That(runs[4].End).IsEqualTo(68);
        await Assert.That(runs[4].Font).IsEqualTo(font);
        await Assert.That(runs[4].Slice(text.AsSpan()).Length).IsEqualTo(14);

        await Assert.That(runs[5].Start).IsEqualTo(68);
        await Assert.That(runs[5].End).IsEqualTo(70);
        await Assert.That(runs[5].Font).IsEqualTo(font2);
        await Assert.That(runs[5].Slice(text.AsSpan()).Length).IsEqualTo(2);

        await Assert.That(runs[6].Start).IsEqualTo(70);
        await Assert.That(runs[6].End).IsEqualTo(76);
        await Assert.That(runs[6].Font).IsEqualTo(font);
        await Assert.That(runs[6].Slice(text.AsSpan()).Length).IsEqualTo(6);
    }

    [Test]
    [Arguments("abcde", 4)]
    [Arguments("abcdef", 4)]
    [Arguments("abcdef", 5)]
    public async Task BuildTextRuns_ExclusiveEndIsNotExtendedToFinalCharacter(string text, int end)
    {
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 0, End = end, Font = font2 },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].End).IsEqualTo(end);
        await Assert.That(runs[0].Font).IsEqualTo(font2);
        await Assert.That(runs[1].Start).IsEqualTo(end);
        await Assert.That(runs[1].End).IsEqualTo(text.Length);
        await Assert.That(runs[1].Font).IsEqualTo(font);
    }

    [Test]
    public async Task BuildTextRuns_TrailingMultiCodePointGraphemeGetsOwnRun()
    {
        // Four graphemes: a, b, c and "e" followed by a combining acute accent.
        const string text = "abcé";
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 0, End = 1, Font = font2 },
                new TextRun() { Start = 2, End = 3, Font = font2 },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(4);
        await Assert.That(runs[2].End).IsEqualTo(3);
        await Assert.That(runs[2].Font).IsEqualTo(font2);
        await Assert.That(runs[3].Start).IsEqualTo(3);
        await Assert.That(runs[3].End).IsEqualTo(4);
        await Assert.That(runs[3].Font).IsEqualTo(font);
        await Assert.That(runs[3].Slice(text.AsSpan()).ToString()).IsEqualTo("é");
    }

    [Test]
    public async Task BuildTextRuns_EndEqualToLengthAddsNoTrailingRun()
    {
        const string text = "abcde";
        Font font = CreateFont(text);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 1, End = 5 },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[1].Start).IsEqualTo(1);
        await Assert.That(runs[1].End).IsEqualTo(5);
    }

    [Test]
    public async Task BuildTextRuns_PreventsOverlappingRun()
    {
        const string text = "This is a long and Honorificabilitudinitatibus califragilisticexpialidocious";
        Font font = CreateFont(text);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 0, End = 23 },
                new TextRun() { Start = 1, End = 76 },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Font).IsEqualTo(font);
        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].End).IsEqualTo(1);

        await Assert.That(runs[1].Font).IsEqualTo(font);
        await Assert.That(runs[1].Start).IsEqualTo(1);
        await Assert.That(runs[1].End).IsEqualTo(76);
    }

    [Test]
    [Arguments(TextDirection.LeftToRight)]
    [Arguments(TextDirection.RightToLeft)]
    public async Task TextJustification_InterCharacter_Horizontal(TextDirection direction)
    {
        const string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nunc ornare maximus vehicula. Duis nisi velit, dictum id mauris vitae, lobortis pretium quam. Quisque sed nisi pulvinar, consequat justo id, feugiat leo. Cras eu elementum dui.";
        const float wrappingLength = 400;
        const float pointSize = 20;
        Font font = CreateFont(text, pointSize);
        TextOptions options = new(font)
        {
            TextDirection = direction,
            WrappingLength = wrappingLength,
            TextJustification = TextJustification.InterCharacter
        };

        // Collect the first line so we can compare it to the target wrapping length.
        IReadOnlyList<GlyphLayout> justifiedGlyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> justifiedLine = CollectFirstLine(justifiedGlyphs);
        GlyphBounds[] justifiedCharacterBounds = GetCharacterAdvances(justifiedLine, options.Dpi);

        await Assert.That(Math.Round(justifiedCharacterBounds.Sum(x => x.Bounds.Width), 4)).IsEqualTo(Math.Round(wrappingLength, 4));

        // Now compare character widths.
        options.TextJustification = TextJustification.None;
        IReadOnlyList<GlyphLayout> glyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> line = CollectFirstLine(glyphs);
        GlyphBounds[] characterBounds = GetCharacterAdvances(line, options.Dpi);

        // All but the last justified character advance should be greater than the
        // corresponding character advance.
        for (int i = 0; i < characterBounds.Length; i++)
        {
            if (i == characterBounds.Length - 1)
            {
                await Assert.That(characterBounds[i].Bounds.Width).IsEqualTo(justifiedCharacterBounds[i].Bounds.Width);
            }
            else
            {
                await Assert.That(justifiedCharacterBounds[i].Bounds.Width > characterBounds[i].Bounds.Width).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(TextDirection.LeftToRight)]
    [Arguments(TextDirection.RightToLeft)]
    public async Task TextJustification_InterWord_Horizontal(TextDirection direction)
    {
        const string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nunc ornare maximus vehicula. Duis nisi velit, dictum id mauris vitae, lobortis pretium quam. Quisque sed nisi pulvinar, consequat justo id, feugiat leo. Cras eu elementum dui.";
        const float wrappingLength = 400;
        const float pointSize = 20;
        Font font = CreateFont(text, pointSize);
        TextOptions options = new(font)
        {
            TextDirection = direction,
            WrappingLength = wrappingLength,
            TextJustification = TextJustification.InterWord
        };

        // Collect the first line so we can compare it to the target wrapping length.
        IReadOnlyList<GlyphLayout> justifiedGlyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> justifiedLine = CollectFirstLine(justifiedGlyphs);
        GlyphBounds[] justifiedCharacterBounds = GetCharacterAdvances(justifiedLine, options.Dpi);

        await Assert.That(Math.Round(justifiedCharacterBounds.Sum(x => x.Bounds.Width), 4)).IsEqualTo(Math.Round(wrappingLength, 4));

        // Now compare character widths.
        options.TextJustification = TextJustification.None;
        IReadOnlyList<GlyphLayout> glyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> line = CollectFirstLine(glyphs);
        GlyphBounds[] characterBounds = GetCharacterAdvances(line, options.Dpi);

        // All but the last justified whitespace character advance should be greater than the
        // corresponding character advance.
        for (int i = 0; i < characterBounds.Length; i++)
        {
            if (CodePoint.IsWhiteSpace(characterBounds[i].Codepoint) && i != characterBounds.Length - 1)
            {
                await Assert.That(justifiedCharacterBounds[i].Bounds.Width > characterBounds[i].Bounds.Width).IsTrue();
            }
            else
            {
                await Assert.That(characterBounds[i].Bounds.Width).IsEqualTo(justifiedCharacterBounds[i].Bounds.Width);
            }
        }
    }

    [Test]
    [Arguments(TextDirection.LeftToRight)]
    [Arguments(TextDirection.RightToLeft)]
    public async Task TextJustification_InterCharacter_Vertical(TextDirection direction)
    {
        const string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nunc ornare maximus vehicula. Duis nisi velit, dictum id mauris vitae, lobortis pretium quam. Quisque sed nisi pulvinar, consequat justo id, feugiat leo. Cras eu elementum dui.";
        const float wrappingLength = 400;
        const float pointSize = 20;
        Font font = CreateFont(text, pointSize);
        TextOptions options = new(font)
        {
            LayoutMode = LayoutMode.VerticalLeftRight,
            TextDirection = direction,
            WrappingLength = wrappingLength,
            TextJustification = TextJustification.InterCharacter
        };

        // Collect the first line so we can compare it to the target wrapping length.
        IReadOnlyList<GlyphLayout> justifiedGlyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> justifiedLine = CollectFirstLine(justifiedGlyphs);
        GlyphBounds[] justifiedCharacterBounds = GetCharacterAdvances(justifiedLine, options.Dpi);

        await Assert.That(Math.Round(justifiedCharacterBounds.Sum(x => x.Bounds.Height), 4)).IsEqualTo(Math.Round(wrappingLength, 4));

        // Now compare character widths.
        options.TextJustification = TextJustification.None;
        IReadOnlyList<GlyphLayout> glyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> line = CollectFirstLine(glyphs);
        GlyphBounds[] characterBounds = GetCharacterAdvances(line, options.Dpi);

        // All but the last justified character advance should be greater than the
        // corresponding character advance.
        for (int i = 0; i < characterBounds.Length; i++)
        {
            if (i == characterBounds.Length - 1)
            {
                await Assert.That(characterBounds[i].Bounds.Height).IsEqualTo(justifiedCharacterBounds[i].Bounds.Height);
            }
            else
            {
                await Assert.That(justifiedCharacterBounds[i].Bounds.Height > characterBounds[i].Bounds.Height).IsTrue();
            }
        }
    }

    [Test]
    [Arguments(TextDirection.LeftToRight)]
    [Arguments(TextDirection.RightToLeft)]
    public async Task TextJustification_InterWord_Vertical(TextDirection direction)
    {
        const string text = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nunc ornare maximus vehicula. Duis nisi velit, dictum id mauris vitae, lobortis pretium quam. Quisque sed nisi pulvinar, consequat justo id, feugiat leo. Cras eu elementum dui.";
        const float wrappingLength = 400;
        const float pointSize = 20;
        Font font = CreateFont(text, pointSize);
        TextOptions options = new(font)
        {
            LayoutMode = LayoutMode.VerticalLeftRight,
            TextDirection = direction,
            WrappingLength = wrappingLength,
            TextJustification = TextJustification.InterWord
        };

        // Collect the first line so we can compare it to the target wrapping length.
        IReadOnlyList<GlyphLayout> justifiedGlyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> justifiedLine = CollectFirstLine(justifiedGlyphs);
        GlyphBounds[] justifiedCharacterBounds = GetCharacterAdvances(justifiedLine, options.Dpi);

        await Assert.That(Math.Round(justifiedCharacterBounds.Sum(x => x.Bounds.Height), 4)).IsEqualTo(Math.Round(wrappingLength, 4));

        // Now compare character widths.
        options.TextJustification = TextJustification.None;
        IReadOnlyList<GlyphLayout> glyphs = TextLayout.GenerateLayout(text.AsSpan(), options);
        IReadOnlyList<GlyphLayout> line = CollectFirstLine(glyphs);
        GlyphBounds[] characterBounds = GetCharacterAdvances(line, options.Dpi);

        // All but the last justified whitespace character advance should be greater than the
        // corresponding character advance.
        for (int i = 0; i < characterBounds.Length; i++)
        {
            if (CodePoint.IsWhiteSpace(characterBounds[i].Codepoint) && i != characterBounds.Length - 1)
            {
                await Assert.That(justifiedCharacterBounds[i].Bounds.Height > characterBounds[i].Bounds.Height).IsTrue();
            }
            else
            {
                await Assert.That(characterBounds[i].Bounds.Height).IsEqualTo(justifiedCharacterBounds[i].Bounds.Height);
            }
        }
    }

    public static System.Collections.Generic.List<(char Character, FontRectangle Expected)> OpenSans_Data
        = new()
        {
            ('!', new(0, 0, 2, 10)),
            ('"', new(0, 0, 4, 5)),
            ('#', new(0, 0, 7, 9)),
            ('$', new(0, 0, 6, 10)),
            ('%', new(0, 0, 8, 9)),
            ('&', new(0, 0, 8, 9)),
            ('\'', new(0, 0, 2, 5)),
            ('(', new(0, 0, 3, 11)),
            (')', new(0, 0, 3, 11)),
            ('*', new(0, 0, 6, 6)),
            ('+', new(0, 0, 6, 8)),
            (',', new(0, 0, 2, 11)),
            ('-', new(0, 0, 3, 7)),
            ('.', new(0, 0, 2, 10)),
            ('/', new(0, 0, 4, 9)),
            ('0', new(0, 0, 6, 9)),
            ('1', new(0, 0, 4, 9)),
            ('2', new(0, 0, 6, 9)),
            ('3', new(0, 0, 6, 9)),
            ('4', new(0, 0, 6, 9)),
            ('5', new(0, 0, 6, 9)),
            ('6', new(0, 0, 6, 9)),
            ('7', new(0, 0, 6, 9)),
            ('8', new(0, 0, 6, 9)),
            ('9', new(0, 0, 6, 9)),
            (':', new(0, 0, 2, 10)),
            (';', new(0, 0, 2, 11)),
            ('<', new(0, 0, 6, 8)),
            ('=', new(0, 0, 6, 7)),
            ('>', new(0, 0, 6, 8)),
            ('?', new(0, 0, 5, 10)),
            ('@', new(0, 0, 9, 10)),
            ('A', new(0, 0, 7, 9)),
            ('B', new(0, 0, 6, 9)),
            ('C', new(0, 0, 6, 9)),
            ('D', new(0, 0, 7, 9)),
            ('E', new(0, 0, 5, 9)),
            ('F', new(0, 0, 5, 9)),
            ('G', new(0, 0, 7, 9)),
            ('H', new(0, 0, 7, 9)),
            ('I', new(0, 0, 2, 9)),
            ('J', new(0, 0, 2, 11)),
            ('K', new(0, 0, 7, 9)),
            ('L', new(0, 0, 5, 9)),
            ('M', new(0, 0, 9, 9)),
            ('N', new(0, 0, 7, 9)),
            ('O', new(0, 0, 8, 9)),
            ('P', new(0, 0, 6, 9)),
            ('Q', new(0, 0, 8, 11)),
            ('R', new(0, 0, 7, 9)),
            ('S', new(0, 0, 6, 9)),
            ('T', new(0, 0, 6, 9)),
            ('U', new(0, 0, 7, 9)),
            ('V', new(0, 0, 6, 9)),
            ('W', new(0, 0, 10, 9)),
            ('X', new(0, 0, 6, 9)),
            ('Y', new(0, 0, 6, 9)),
            ('Z', new(0, 0, 6, 9)),
            ('[', new(0, 0, 4, 11)),
            ('\\', new(0, 0, 4, 9)),
            (']', new(0, 0, 3, 11)),
            ('^', new(0, 0, 6, 7)),
            ('_', new(0, 0, 5, 11)),
            ('`', new(0, 0, 3, 3)),
            ('a', new(0, 0, 5, 9)),
            ('b', new(0, 0, 6, 9)),
            ('c', new(0, 0, 5, 9)),
            ('d', new(0, 0, 6, 9)),
            ('e', new(0, 0, 6, 9)),
            ('f', new(0, 0, 4, 9)),
            ('g', new(0, 0, 6, 12)),
            ('h', new(0, 0, 6, 9)),
            ('i', new(0, 0, 2, 9)),
            ('j', new(0, 0, 2, 12)),
            ('k', new(0, 0, 6, 9)),
            ('l', new(0, 0, 2, 9)),
            ('m', new(0, 0, 9, 9)),
            ('n', new(0, 0, 6, 9)),
            ('o', new(0, 0, 6, 9)),
            ('p', new(0, 0, 6, 12)),
            ('q', new(0, 0, 6, 12)),
            ('r', new(0, 0, 4, 9)),
            ('s', new(0, 0, 5, 9)),
            ('t', new(0, 0, 4, 9)),
            ('u', new(0, 0, 6, 9)),
            ('v', new(0, 0, 5, 9)),
            ('w', new(0, 0, 8, 9)),
            ('x', new(0, 0, 6, 9)),
            ('y', new(0, 0, 6, 12)),
            ('z', new(0, 0, 5, 9)),
            ('{', new(0, 0, 4, 11)),
            ('|', new(0, 0, 4, 12)),
            ('}', new(0, 0, 4, 11)),
            ('~', new(0, 0, 6, 6)),
        };

    [Test]
    [MethodDataSource(nameof(OpenSans_Data))]
    public async Task TrueTypeHinting_CanHintSmallOpenSans(char c, FontRectangle expected)
    {
        TextOptions options = new(OpenSansTTF)
        {
            KerningMode = KerningMode.Standard,
            HintingMode = HintingMode.Standard
        };

        FontRectangle actual = TextMeasurer.MeasureSize(c.ToString(), options);
        await Assert.That(actual).IsEqualTo(expected);

        options = new(OpenSansWoff)
        {
            KerningMode = KerningMode.Standard,
            HintingMode = HintingMode.Standard
        };

        actual = TextMeasurer.MeasureSize(c.ToString(), options);
        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    public async Task CanMeasureTextAdvance()
    {
        FontFamily family = new FontCollection().Add(TestFonts.OpenSansFile);
        family.TryGetMetrics(FontStyle.Regular, out FontMetrics? metrics);

        TextOptions options = new(family.CreateFont(metrics!.UnitsPerEm))
        {
            LineSpacing = 1F
        };

        const string text = "Hello World!";
        FontRectangle first = TextMeasurer.MeasureAdvance(text, options);

        // TODO: We should probably drop the 32bit test runner.
        await Assert.That(first).IsEqualTo(new FontRectangle(0, 0, 11729, 2049));

        options.LineSpacing = 2F;
        FontRectangle second = TextMeasurer.MeasureAdvance(text, options);
        await Assert.That(second).IsEqualTo(new FontRectangle(0, 0, 11729, 4096));
    }

    [Test]
    public async Task CanMeasureCharacterLayouts()
    {
        FontFamily family = new FontCollection().Add(TestFonts.OpenSansFile);
        family.TryGetMetrics(FontStyle.Regular, out FontMetrics? metrics);

        TextOptions options = new(family.CreateFont(metrics!.UnitsPerEm))
        {
            LineSpacing = 1.5F
        };

        const string text = "Hello World!";

        bool measuredAdvances = TextMeasurer.TryMeasureCharacterAdvances(text, options, out ReadOnlySpan<GlyphBounds> advancesSpan);
        GlyphBounds[] advances = advancesSpan.ToArray();
        await Assert.That(measuredAdvances).IsTrue();

        bool measuredSizes = TextMeasurer.TryMeasureCharacterSizes(text, options, out ReadOnlySpan<GlyphBounds> sizesSpan);
        GlyphBounds[] sizes = sizesSpan.ToArray();
        await Assert.That(measuredSizes).IsTrue();

        bool measuredBounds = TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> boundsSpan);
        GlyphBounds[] bounds = boundsSpan.ToArray();
        await Assert.That(measuredBounds).IsTrue();

        await Assert.That(sizes.Length).IsEqualTo(advances.Length);
        await Assert.That(bounds.Length).IsEqualTo(advances.Length);

        for (int i = 0; i < advances.Length; i++)
        {
            GlyphBounds advance = advances[i];
            GlyphBounds size = sizes[i];
            GlyphBounds bound = bounds[i];

            await Assert.That(size.Codepoint).IsEqualTo(advance.Codepoint);
            await Assert.That(bound.Codepoint).IsEqualTo(advance.Codepoint);

            // Since this is a single line starting at 0,0 the following should be predictable.
            await Assert.That(size.Bounds.X).IsEqualTo(advance.Bounds.X);
            await Assert.That(bound.Bounds.Bottom).IsEqualTo(size.Bounds.Bottom);
        }
    }

    [Test]
    public async Task CanMeasureMultilineCharacterLayouts()
    {
        FontFamily family = new FontCollection().Add(TestFonts.OpenSansFile);
        family.TryGetMetrics(FontStyle.Regular, out FontMetrics? metrics);

        TextOptions options = new(family.CreateFont(metrics!.UnitsPerEm))
        {
            LineSpacing = 1.5F
        };

        const string text = "A\nA\nA\nA";

        bool measuredAdvances = TextMeasurer.TryMeasureCharacterAdvances(text, options, out ReadOnlySpan<GlyphBounds> advancesSpan);
        GlyphBounds[] advances = advancesSpan.ToArray();
        await Assert.That(measuredAdvances).IsTrue();

        bool measuredSizes = TextMeasurer.TryMeasureCharacterSizes(text, options, out ReadOnlySpan<GlyphBounds> sizesSpan);
        GlyphBounds[] sizes = sizesSpan.ToArray();
        await Assert.That(measuredSizes).IsTrue();

        await Assert.That(sizes.Length).IsEqualTo(advances.Length);

        GlyphBounds? lastAdvance = null;
        GlyphBounds? lastSize = null;
        for (int i = 0; i < advances.Length; i++)
        {
            GlyphBounds advance = advances[i];
            GlyphBounds size = sizes[i];

            await Assert.That(size.Codepoint).IsEqualTo(advance.Codepoint);

            if (lastAdvance.HasValue)
            {
                await Assert.That(advance.Bounds).IsEqualTo(lastAdvance.Value.Bounds);
                await Assert.That(Math.Round(size.Bounds.Width, 2)).IsEqualTo(Math.Round(lastSize!.Value.Bounds.Width, 2));
                await Assert.That(Math.Round(size.Bounds.Height, 2)).IsEqualTo(Math.Round(lastSize!.Value.Bounds.Height, 2));
            }

            lastAdvance = advance;
            lastSize = size;
        }
    }

    private static readonly Font OpenSansTTF = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(10);
    private static readonly Font OpenSansWoff = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(10);

#if OS_WINDOWS
    public static System.Collections.Generic.List<(char Character, FontRectangle Expected)> SegoeUi_Data
        = new()
        {
            ('!', new(0, 0, 2, 10)),
            ('"', new(0, 0, 4, 5)),
            ('#', new(0, 0, 6, 9)),
            ('$', new(0, 0, 5, 11)),
            ('%', new(0, 0, 8, 10)),
            ('&', new(0, 0, 8, 10)),
            ('\'', new(0, 0, 2, 5)),
            ('(', new(0, 0, 3, 11)),
            (')', new(0, 0, 3, 11)),
            ('*', new(0, 0, 4, 6)),
            ('+', new(0, 0, 6, 9)),
            (',', new(0, 0, 2, 11)),
            ('-', new(0, 0, 4, 7)),
            ('.', new(0, 0, 2, 10)),
            ('/', new(0, 0, 4, 11)),
            ('0', new(0, 0, 5, 10)),
            ('1', new(0, 0, 4, 10)),
            ('2', new(0, 0, 5, 10)),
            ('3', new(0, 0, 5, 10)),
            ('4', new(0, 0, 6, 10)),
            ('5', new(0, 0, 5, 10)),
            ('6', new(0, 0, 5, 10)),
            ('7', new(0, 0, 5, 10)),
            ('8', new(0, 0, 5, 10)),
            ('9', new(0, 0, 5, 10)),
            (':', new(0, 0, 2, 10)),
            (';', new(0, 0, 2, 11)),
            ('<', new(0, 0, 6, 9)),
            ('=', new(0, 0, 6, 8)),
            ('>', new(0, 0, 6, 9)),
            ('?', new(0, 0, 4, 10)),
            ('@', new(0, 0, 9, 11)),
            ('A', new(0, 0, 7, 10)),
            ('B', new(0, 0, 6, 10)),
            ('C', new(0, 0, 6, 10)),
            ('D', new(0, 0, 7, 10)),
            ('E', new(0, 0, 5, 10)),
            ('F', new(0, 0, 5, 10)),
            ('G', new(0, 0, 7, 10)),
            ('H', new(0, 0, 7, 10)),
            ('I', new(0, 0, 2, 10)),
            ('J', new(0, 0, 3, 10)),
            ('K', new(0, 0, 6, 10)),
            ('L', new(0, 0, 5, 10)),
            ('M', new(0, 0, 9, 10)),
            ('N', new(0, 0, 7, 10)),
            ('O', new(0, 0, 8, 10)),
            ('P', new(0, 0, 6, 10)),
            ('Q', new(0, 0, 8, 11)),
            ('R', new(0, 0, 6, 10)),
            ('S', new(0, 0, 5, 10)),
            ('T', new(0, 0, 6, 10)),
            ('U', new(0, 0, 7, 10)),
            ('V', new(0, 0, 7, 10)),
            ('W', new(0, 0, 10, 10)),
            ('X', new(0, 0, 6, 10)),
            ('Y', new(0, 0, 6, 10)),
            ('Z', new(0, 0, 6, 10)),
            ('[', new(0, 0, 3, 11)),
            ('\\', new(0, 0, 4, 11)),
            (']', new(0, 0, 3, 11)),
            ('^', new(0, 0, 6, 7)),
            ('_', new(0, 0, 5, 11)),
            ('`', new(0, 0, 3, 4)),
            ('a', new(0, 0, 5, 10)),
            ('b', new(0, 0, 6, 10)),
            ('c', new(0, 0, 5, 10)),
            ('d', new(0, 0, 6, 10)),
            ('e', new(0, 0, 5, 10)),
            ('f', new(0, 0, 4, 10)),
            ('g', new(0, 0, 6, 12)),
            ('h', new(0, 0, 5, 10)),
            ('i', new(0, 0, 2, 10)),
            ('j', new(0, 0, 2, 12)),
            ('k', new(0, 0, 5, 10)),
            ('l', new(0, 0, 2, 10)),
            ('m', new(0, 0, 8, 10)),
            ('n', new(0, 0, 5, 10)),
            ('o', new(0, 0, 6, 10)),
            ('p', new(0, 0, 6, 12)),
            ('q', new(0, 0, 6, 12)),
            ('r', new(0, 0, 4, 10)),
            ('s', new(0, 0, 4, 10)),
            ('t', new(0, 0, 4, 10)),
            ('u', new(0, 0, 5, 10)),
            ('v', new(0, 0, 5, 10)),
            ('w', new(0, 0, 8, 10)),
            ('x', new(0, 0, 5, 10)),
            ('y', new(0, 0, 5, 12)),
            ('z', new(0, 0, 5, 10)),
            ('{', new(0, 0, 3, 11)),
            ('|', new(0, 0, 2, 12)),
            ('}', new(0, 0, 3, 11)),
            ('~', new(0, 0, 6, 7))
        };

    [Test]
    [MethodDataSource(nameof(SegoeUi_Data))]
    public async Task TrueTypeHinting_CanHintSmallSegoeUi(char c, FontRectangle expected)
    {
        TextOptions options = new(SegoeUi)
        {
            KerningMode = KerningMode.Standard,
            HintingMode = HintingMode.Standard
        };

        FontRectangle actual = TextMeasurer.MeasureSize(c.ToString(), options);
        await Assert.That(actual).IsEqualTo(expected);
    }

    private static readonly Font SegoeUi = SystemFonts.CreateFont("Segoe Ui", 10);
#endif

    private static GlyphBounds[] GetCharacterAdvances(IReadOnlyList<GlyphLayout> glyphLayouts, float dpi)
    {
        TextMeasurer.TryGetCharacterAdvances(glyphLayouts, dpi, out ReadOnlySpan<GlyphBounds> characterBounds);
        return characterBounds.ToArray();
    }

    private static IReadOnlyList<GlyphLayout> CollectFirstLine(IReadOnlyList<GlyphLayout> glyphs)
    {
        List<GlyphLayout> line = new()
        {
            glyphs[0]
        };

        for (int i = 1; i < glyphs.Count; i++)
        {
            if (glyphs[i].IsStartOfLine)
            {
                break;
            }

            line.Add(glyphs[i]);
        }

        return line;
    }

#if OS_WINDOWS
    [Test]
    public void BenchmarkTest()
    {
        Font font = Arial;
        _ = TextMeasurer.MeasureSize("The quick brown fox jumped over the lazy dog", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
    }

    private static readonly Font Arial = SystemFonts.CreateFont("Arial", 12);
#endif

    public static Font CreateFont(string text)
    {
        var fc = (IFontMetricsCollection)new FontCollection();
        Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
        return new Font(d, 1);
    }

    public static Font CreateFont(string text, float pointSize)
    {
        var fc = (IFontMetricsCollection)new FontCollection();
        Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
        return new Font(d, pointSize);
    }
}
