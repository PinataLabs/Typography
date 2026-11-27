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
    [Arguments("AB", 465, 1033, false)] // kern is the required feature of the font; KerningMode.None still turns it off, unlike HarfBuzz
    [Arguments("AB", 465, 654, true)]
    public async Task MeasureTextWithKerning(string text, float height, float width, bool applyKerning)
    {
        var c = new FontCollection();
        Font font = c.Add(TestFonts.SimpleFontFileData()).CreateFont(12);
        FontRectangle size = TextMeasurer.MeasureBounds(
            text,
            new TextOptions(font.WithSize(1))
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
            new TextOptions(font.WithSize(1))
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
    [Arguments("This is a long and Honorificabilitudinitatibus califragilisticexpialidocious", 100, 7)]
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
    public async Task BuildTextRuns_DoesNotMutateCallerRuns()
    {
        const string text = "abcdefghij";
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextRun first = new() { Start = 0, End = 8 };
        TextRun second = new() { Start = 4, End = 6, Font = font2 };
        TextOptions options = new(font) { TextRuns = new List<TextRun> { first, second } };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        // Caller's instances are untouched.
        await Assert.That(first.End).IsEqualTo(8);
        await Assert.That(first.Font).IsNull();
        await Assert.That(second.Start).IsEqualTo(4);
        await Assert.That(second.End).IsEqualTo(6);

        // The result is built from copies.
        await Assert.That(runs.Any(r => ReferenceEquals(r, first) || ReferenceEquals(r, second))).IsFalse();
        await Assert.That(runs[0].Font).IsEqualTo(font);
        await Assert.That(runs[0].End).IsEqualTo(4);
    }

    [Test]
    public async Task BuildTextRuns_ReusedOptionsDoNotKeepStaleFont()
    {
        const string text = "abcdef";
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextRun run = new() { Start = 0, End = 3 };
        TextOptions options = new(font) { TextRuns = new List<TextRun> { run } };

        TextLayout.BuildTextRuns(text.AsSpan(), options);
        options.Font = font2;
        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs[0].Font).IsEqualTo(font2);
    }

    [Test]
    public async Task BuildTextRuns_PreservesDerivedRunTypeAndData()
    {
        const string text = "abcdef";
        Font font = CreateFont(text);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun> { new CustomRun { Start = 0, End = 3, Tag = "bold", TextDecorations = TextDecorations.Strikeout } }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        CustomRun copy = (CustomRun)runs[0];
        await Assert.That(copy.Tag).IsEqualTo("bold");
        await Assert.That(copy.TextDecorations).IsEqualTo(TextDecorations.Strikeout);
        await Assert.That(copy.Font).IsEqualTo(font);
    }

    private sealed class CustomRun : TextRun
    {
        public string? Tag { get; set; }
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
    public async Task BuildTextRuns_SameStart_LaterRunWins()
    {
        const string text = "abcdefghijkl";
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 0, End = 5 },
                new TextRun() { Start = 0, End = 10, Font = font2 },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].End).IsEqualTo(10);
        await Assert.That(runs[0].Font).IsEqualTo(font2);
        await Assert.That(runs[1].Start).IsEqualTo(10);
        await Assert.That(runs[1].End).IsEqualTo(12);
        await Assert.That(runs[1].Font).IsEqualTo(font);
    }

    [Test]
    public async Task BuildTextRuns_NestedRun_OuterRunResumesAfterInnerRun()
    {
        const string text = "abcdefghijkl";
        Font font = CreateFont(text);
        Font fontA = CreateFont(text, 16);
        Font fontB = CreateFont(text, 24);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 0, End = 10, Font = fontA },
                new TextRun() { Start = 2, End = 5, Font = fontB },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(4);
        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].End).IsEqualTo(2);
        await Assert.That(runs[0].Font).IsEqualTo(fontA);
        await Assert.That(runs[1].Start).IsEqualTo(2);
        await Assert.That(runs[1].End).IsEqualTo(5);
        await Assert.That(runs[1].Font).IsEqualTo(fontB);
        await Assert.That(runs[2].Start).IsEqualTo(5);
        await Assert.That(runs[2].End).IsEqualTo(10);
        await Assert.That(runs[2].Font).IsEqualTo(fontA);
        await Assert.That(runs[3].Start).IsEqualTo(10);
        await Assert.That(runs[3].End).IsEqualTo(12);
        await Assert.That(runs[3].Font).IsEqualTo(font);
    }

    [Test]
    public async Task BuildTextRuns_OuterRunListedAfterInnerRun_OuterWins()
    {
        // Later in list order wins, regardless of Start.
        const string text = "abcdefghijkl";
        Font font = CreateFont(text);
        Font fontA = CreateFont(text, 16);
        Font fontB = CreateFont(text, 24);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 2, End = 5, Font = fontB },
                new TextRun() { Start = 0, End = 10, Font = fontA },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Font).IsEqualTo(fontA);
        await Assert.That(runs[0].End).IsEqualTo(10);
        await Assert.That(runs[1].Start).IsEqualTo(10);
    }

    [Test]
    public async Task BuildTextRuns_EndPastGraphemeCount_IsClamped()
    {
        const string text = "abcdefghij";
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>() { new TextRun() { Start = 8, End = 20, Font = font2 } }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[1].Start).IsEqualTo(8);
        await Assert.That(runs[1].End).IsEqualTo(10);
        await Assert.That(runs[1].Font).IsEqualTo(font2);
    }

    [Test]
    public async Task BuildTextRuns_InvalidRuns_AreDroppedOrClamped()
    {
        const string text = "abcdefghij";
        Font font = CreateFont(text);
        Font font2 = CreateFont(text, 16);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new TextRun() { Start = 3, End = 3, Font = font2 },   // zero length
                new TextRun() { Start = 6, End = 2, Font = font2 },   // inverted
                new TextRun() { Start = 10, End = 15, Font = font2 }, // starts at count
                new TextRun() { Start = 12, End = 15, Font = font2 }, // past the end
                new TextRun() { Start = -4, End = 3, Font = font2 },  // negative start
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(2);
        await Assert.That(runs[0].Start).IsEqualTo(0);
        await Assert.That(runs[0].End).IsEqualTo(3);
        await Assert.That(runs[0].Font).IsEqualTo(font2);
        await Assert.That(runs[1].Start).IsEqualTo(3);
        await Assert.That(runs[1].End).IsEqualTo(10);
        await Assert.That(runs[1].Font).IsEqualTo(font);
    }

    [Test]
    public async Task BuildTextRuns_SplitDerivedRun_KeepsTypeOnBothPieces()
    {
        const string text = "abcdefghijkl";
        Font font = CreateFont(text);
        TextOptions options = new(font)
        {
            TextRuns = new List<TextRun>()
            {
                new CustomRun() { Start = 0, End = 10, Tag = "outer" },
                new TextRun() { Start = 2, End = 5 },
            }
        };

        IReadOnlyList<TextRun> runs = TextLayout.BuildTextRuns(text.AsSpan(), options);

        await Assert.That(runs.Count).IsEqualTo(4);
        await Assert.That(((CustomRun)runs[0]).Tag).IsEqualTo("outer");
        await Assert.That(runs[1] is CustomRun).IsFalse();
        await Assert.That(((CustomRun)runs[2]).Tag).IsEqualTo("outer");
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
        FontMetrics metrics = family.MatchFace(FontStyle.Regular);

        TextOptions options = new(family.CreateFont(metrics.UnitsPerEm))
        {
            LineSpacing = 1F
        };

        const string text = "Hello World!";
        FontRectangle first = TextMeasurer.MeasureAdvance(text, options);

        // TODO: We should probably drop the 32bit test runner.
        await Assert.That(first).IsEqualTo(new FontRectangle(0, 0, 11729, 2048));

        options.LineSpacing = 2F;
        FontRectangle second = TextMeasurer.MeasureAdvance(text, options);
        await Assert.That(second).IsEqualTo(new FontRectangle(0, 0, 11729, 4096));
    }

    [Test]
    public async Task CanMeasureCharacterLayouts()
    {
        FontFamily family = new FontCollection().Add(TestFonts.OpenSansFile);
        FontMetrics metrics = family.MatchFace(FontStyle.Regular);

        TextOptions options = new(family.CreateFont(metrics.UnitsPerEm))
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
        FontMetrics metrics = family.MatchFace(FontStyle.Regular);

        TextOptions options = new(family.CreateFont(metrics.UnitsPerEm))
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

    // In a right-to-left paragraph, Start is the right edge of the wrapping box and End is the
    // left edge, as CSS text-align: start and end are with direction: rtl.
    [Test]
    [Arguments(TextAlignment.Start, 300)]
    [Arguments(TextAlignment.End, 0)]
    public async Task TextAlignment_RightToLeft_AlignsToEdgeOfWrappingBox(TextAlignment alignment, float expectedEdge)
    {
        const float wrappingLength = 300;
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);
        TextOptions options = new(font) { TextAlignment = alignment, WrappingLength = wrappingLength };

        FontRectangle bounds = TextMeasurer.MeasureBounds("سلام", options);

        // Ink bounds sit inside the advance by the side bearings, so allow a few pixels.
        float edge = alignment == TextAlignment.Start ? bounds.Right : bounds.Left;
        await Assert.That(Math.Abs(edge - expectedEdge)).IsLessThan(5);
    }

    // A fixed TextDirection sets the paragraph embedding level (CSS direction, UAX #9 HL1).
    // It must not stop strong characters from resolving to their own direction.
    [Test]
    public async Task TextDirection_RightToLeft_KeepsLatinTextInLogicalOrder()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(24);

        string visual = GetVisualOrder("abc", new TextOptions(font) { TextDirection = TextDirection.RightToLeft });

        await Assert.That(visual).IsEqualTo("abc");
    }

    [Test]
    public async Task TextDirection_LeftToRight_ReordersArabicTextVisually()
    {
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);

        string visual = GetVisualOrder("كتب abc", new TextOptions(font) { TextDirection = TextDirection.LeftToRight });

        await Assert.That(visual).IsEqualTo("بتك abc");
    }

    // An explicit RLO ... PDF is how CSS unicode-bidi: bidi-override is defined, so it must
    // still be honoured when the paragraph direction is fixed.
    [Test]
    public async Task TextDirection_LeftToRight_HonoursExplicitDirectionalOverride()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(24);

        string visual = GetVisualOrder("\u202Eabc\u202C", new TextOptions(font) { TextDirection = TextDirection.LeftToRight });

        await Assert.That(string.Concat(visual.Where(char.IsLetter))).IsEqualTo("cba");
    }

    // TextAlignment.Start follows the paragraph direction, as CSS text-align: start does.
    // The paragraph direction is the resolved paragraph embedding level, not the direction
    // of whichever glyph happens to come first on the first line.
    [Test]
    [Arguments(TextDirection.RightToLeft, "abcxyz\nxyz", 'z')]
    [Arguments(TextDirection.Auto, "سلام abc\nسلام", 'س')]
    public async Task TextDirection_RightToLeftParagraph_AlignsStartToTheRight(TextDirection direction, string text, char lastOnEachLine)
    {
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);
        TextOptions options = new(font) { TextDirection = direction, TextAlignment = TextAlignment.Start };

        TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> bounds);
        float[] rightEdges = bounds.ToArray()
            .Where(x => x.Codepoint == new CodePoint(lastOnEachLine))
            .Select(x => x.Bounds.Right)
            .ToArray();

        await Assert.That(rightEdges.Length).IsEqualTo(2);
        await Assert.That(Math.Round(rightEdges[1], 4)).IsEqualTo(Math.Round(rightEdges[0], 4));
    }

    // With Auto, each paragraph takes its own direction, so TextAlignment.Start is the left edge of the box for the
    // left-to-right paragraph and the right edge for the right-to-left one, whichever comes first, as CSS
    // text-align: start does with unicode-bidi: plaintext.
    [Test]
    [Arguments("abc\nكتب")]
    [Arguments("كتب\nabc")]
    public async Task TextDirection_Auto_AlignsEachParagraphToItsOwnStart(string text)
    {
        const float wrappingLength = 300;
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);
        TextOptions options = new(font) { TextAlignment = TextAlignment.Start, WrappingLength = wrappingLength };

        TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> bounds);
        GlyphBounds[] glyphs = bounds.ToArray();
        float latinLeft = glyphs.Where(x => x.Codepoint.Value is >= 'a' and <= 'z').Min(x => x.Bounds.Left);
        float arabicRight = glyphs.Where(x => x.Codepoint.Value is >= 0x0600 and <= 0x06FF).Max(x => x.Bounds.Right);

        // Ink bounds sit inside the advance by the side bearings, so allow a few pixels.
        await Assert.That(latinLeft).IsLessThan(5);
        await Assert.That(Math.Abs(arabicRight - wrappingLength)).IsLessThan(5);
    }

    // U+001C ends a paragraph (UAX #9 P1) but not a line (UAX #14), so one line holds two paragraphs. Rule L1 resets the
    // trailing space to the level of its own paragraph, the right-to-left second one, so it is drawn left of the Arabic,
    // not after it at the level of the first paragraph. The line aligns by the paragraph its first character is in.
    [Test]
    public async Task ALineThatHoldsTwoParagraphs_ResetsEachToItsOwnLevel()
    {
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);

        GlyphLayout[][] lines = GetLines("abc\U0000001Cكتب ", new TextOptions(font));

        await Assert.That(lines.Length).IsEqualTo(1);
        await Assert.That(Visual(lines).Replace("\U0000001C", string.Empty)).IsEqualTo("abc بتك");
    }

    // A line feed ends a paragraph (UAX #9 P1), and with Auto each paragraph takes its direction from its own first
    // strong character (P2, P3). The first paragraph runs right to left, so its Latin word, written last, is drawn left
    // of the Arabic. The second runs left to right, so its Latin word, written first, is drawn left of the Arabic too.
    [Test]
    public async Task TextDirection_Auto_OrdersEachParagraphInItsOwnDirection()
    {
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);

        GlyphLayout[][] lines = GetLines("كتب abc\nabc كتب", new TextOptions(font));

        await Assert.That(Visual(lines)).IsEqualTo("abc بتك|abc بتك");
    }

    // Rule L1 gives a joiner at the end of the line, or before a tab, the paragraph level, so it is in no level run
    // with the beh before it. The beh still joins to it, so it takes its initial form, as before another beh.
    [Test]
    [Arguments("abc \u0628\u200D")]
    [Arguments("abc \u0628\u200D\tdef")]
    public async Task JoinerThatRuleL1ResetsStillJoinsTheLetterBeforeIt(string text)
    {
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);
        ushort joined = TextShaper.Shape(font, "\u0628\u0628", new() { Direction = TextDirection.RightToLeft })
            .Single(g => g.Cluster == 0).GlyphId;
        ushort isolated = TextShaper.Shape(font, "\u0628", new() { Direction = TextDirection.RightToLeft })[0].GlyphId;

        ushort beh = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font) { TextDirection = TextDirection.LeftToRight })
            .Single(g => g.CodePoint == new CodePoint(0x0628)).Glyph.GlyphMetrics.GlyphId;

        await Assert.That(joined).IsNotEqualTo(isolated);
        await Assert.That(beh).IsEqualTo(joined);
    }

    // Rule L1 applies to each line, so the soft hyphen that ends a wrapped line takes the paragraph level and is drawn at
    // the left end of the right-to-left line, not next to the b. The soft hyphen has no advance, so it shares its x with
    // the a.
    [Test]
    public async Task RuleL1_RightToLeftParagraph_MovesSoftHyphenThatEndsAWrappedLineToTheLineEnd()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(24);
        TextOptions options = new(font) { TextDirection = TextDirection.RightToLeft };
        options.WrappingLength = TextMeasurer.MeasureAdvance("abc", options).Width - 1;

        GlyphLayout[][] lines = GetLines($"ab{Shy}cd ef", options);

        await Assert.That(Visual(lines)).IsEqualTo(string.Join("|", $"{Shy}ab", "cd", "ef"));
        await Assert.That(X(lines[0], Shy[0])).IsEqualTo(X(lines[0], 'a'));
    }

    // In a left-to-right paragraph the space between two Arabic words resolves to level 1. When the line wraps after it,
    // rule L1 gives it level 0, so it is drawn at the right end of the line, after the Arabic word, not before it.
    [Test]
    public async Task RuleL1_LeftToRightParagraph_MovesSpaceThatEndsAWrappedLineToTheLineEnd()
    {
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);
        TextOptions options = new(font) { TextDirection = TextDirection.LeftToRight, WordBreaking = WordBreaking.BreakAll };
        options.WrappingLength = TextMeasurer.MeasureAdvance("abc كتب ", options).Width + 1;

        GlyphLayout[][] lines = GetLines("abc كتب جمل", options);

        await Assert.That(Visual(lines)).IsEqualTo(string.Join("|", "abc بتك ", "لمج"));
        await Assert.That(lines[0][^1].CodePoint).IsEqualTo(new CodePoint(' '));
        await Assert.That(lines[0][^1].PenLocation.X).IsGreaterThan(X(lines[0], 'ك'));
    }

    // The tab is a segment separator, so rule L1 resets the soft hyphen before it on every line. The soft hyphen that
    // ends the wrapped line is reset by L1 on that line, so it is drawn at the left end of the right-to-left line.
    [Test]
    public async Task RuleL1_RightToLeftParagraph_ResetsBeforeATabAndAtTheEndOfAWrappedLine()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(24);
        TextOptions options = new(font) { TextDirection = TextDirection.RightToLeft };
        options.WrappingLength = TextMeasurer.MeasureAdvance($"ab{Shy}\tcd{Shy}", options).Width + 1;

        GlyphLayout[][] lines = GetLines($"ab{Shy}\tcd{Shy}ef", options);

        await Assert.That(Visual(lines)).IsEqualTo(string.Join("|", $"{Shy}cd\t{Shy}ab", "ef"));
    }

    // Rule L1 on the last line resets the soft hyphen at the end of the text, as it did when L1 applied to the whole text.
    [Test]
    public async Task RuleL1_RightToLeftParagraph_ResetsSoftHyphenAtTheEndOfTheLastLine()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(24);
        TextOptions options = new(font) { TextDirection = TextDirection.RightToLeft };
        options.WrappingLength = TextMeasurer.MeasureAdvance("abc", options).Width - 1;

        GlyphLayout[][] lines = GetLines($"cd ab{Shy}", options);

        await Assert.That(Visual(lines)).IsEqualTo(string.Join("|", "cd", $"{Shy}ab"));
    }

    // With WordBreaking.BreakAll the first line keeps the space it ends with. That space is at the logical end of the
    // line, so justification gives it no padding, whichever side of the line rule L1 draws it on: the right in a
    // left-to-right paragraph, the left in a right-to-left one. Padding goes after a glyph, so the d, the last glyph
    // before that space or the line end, takes none either.
    [Test]
    [Arguments(TextDirection.LeftToRight, TextJustification.InterWord)]
    [Arguments(TextDirection.RightToLeft, TextJustification.InterWord)]
    [Arguments(TextDirection.LeftToRight, TextJustification.InterCharacter)]
    [Arguments(TextDirection.RightToLeft, TextJustification.InterCharacter)]
    public async Task Justification_GivesNoPaddingToTheSpaceThatEndsAWrappedLine(TextDirection direction, TextJustification justification)
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(24);
        TextOptions options = new(font) { TextDirection = direction, WordBreaking = WordBreaking.BreakAll };
        options.WrappingLength = TextMeasurer.MeasureAdvance("ab cd e", options).Width - 1;

        GlyphLayout[] line = GetLines("ab cd efg", options)[0];
        options.TextJustification = justification;
        GlyphLayout[] justified = GetLines("ab cd efg", options)[0];

        // In visual order the line is "ab cd " in a left-to-right paragraph and " ab cd" in a right-to-left one.
        int trailing = direction == TextDirection.LeftToRight ? line.Length - 1 : 0;
        int between = direction == TextDirection.LeftToRight ? 2 : 3;
        int d = direction == TextDirection.LeftToRight ? line.Length - 2 : line.Length - 1;
        await Assert.That(Visual([justified])).IsEqualTo(direction == TextDirection.LeftToRight ? "ab cd " : " ab cd");
        await Assert.That(justified[trailing].AdvanceX).IsEqualTo(line[trailing].AdvanceX);
        await Assert.That(justified[d].AdvanceX).IsEqualTo(line[d].AdvanceX);
        await Assert.That(justified[between].AdvanceX).IsGreaterThan(line[between].AdvanceX);
        await Assert.That(Math.Round(justified.Sum(x => x.AdvanceX) * options.Dpi, 3)).IsEqualTo(Math.Round(options.WrappingLength, 3));
    }

    // A joiner that starts a wrapped line has no character before it on the line. TextLayout draws it in the run of the
    // character after it, as BidiParagraph.GetVisualRuns does, not at the level it takes from the space before it. The
    // line is just wider than the text after the space, and the space overflows it. A wrapped line does not start with a
    // space, so the next line starts with the joiner.
    [Test]
    [Arguments("\u0627\u0628 \u200Dcd", TextDirection.RightToLeft)]
    [Arguments("ab \u200D\u0627\u0628", TextDirection.LeftToRight)]
    [Arguments("ab \u200Dcd", TextDirection.RightToLeft)]
    public async Task RuleL1_JoinerThatStartsAWrappedLine_IsDrawnAsGetVisualRunsDrawsIt(string text, TextDirection direction)
    {
        Font font = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(24);
        TextOptions options = new(font) { TextDirection = direction };
        options.WrappingLength = TextMeasurer.MeasureAdvance(text.AsSpan(3), options).Width + 1;

        GlyphLayout[][] lines = GetLines(text, options);

        BidiParagraph paragraph = BidiParagraph.Resolve(
            text,
            direction == TextDirection.RightToLeft ? BidiParagraphDirection.RightToLeft : BidiParagraphDirection.LeftToRight);
        string expected = string.Concat(paragraph.GetVisualRuns(3, text.Length - 3).Select(run =>
        {
            string characters = text.Substring(run.Start, run.Length);
            return run.IsRightToLeft ? string.Concat(characters.Reverse()) : characters;
        }));

        await Assert.That(lines.Length).IsEqualTo(2);
        await Assert.That(Visual([lines[1]])).IsEqualTo(expected);
    }

    // The font ligates the space and the line feed after it, so the line feed has no glyph. The line wraps at the space,
    // which does not start the next line, so no glyph is on that line at the mandatory break after the line feed. The
    // break ends no line, and the b starts the second line.
    [Test]
    [Arguments(TextDirection.LeftToRight)]
    [Arguments(TextDirection.RightToLeft)]
    public async Task MandatoryBreakAfterALineFeedWithNoGlyph_AddsNoEmptyLine(TextDirection direction)
    {
        const string text = "aaaa \nb";
        Font font = ((IFontMetricsCollection)new FontCollection())
            .AddMetrics(new PairLigatingFakeFontInstance(text, ' ', '\n'), CultureInfo.InvariantCulture)
            .CreateFont(12);
        TextOptions options = new(font) { TextDirection = direction, WordBreaking = WordBreaking.BreakAll };
        options.WrappingLength = TextMeasurer.MeasureAdvance("aaaa", options).Width + 1;
        float lineHeight = TextMeasurer.MeasureAdvance("a", options).Height;

        GlyphLayout[][] lines = GetLines(text, options);

        await Assert.That(Visual(lines)).IsEqualTo("aaaa|b");
        await Assert.That(lines[1][0].PenLocation.Y - lines[0][0].PenLocation.Y).IsEqualTo(lineHeight / options.Dpi).Within(0.0001F);
    }

    // A line feed ends every isolate (UAX #9 BD9 and X8), so the isolate ends at the line feed and the PDI after it
    // matches no isolate initiator.
    [Test]
    [Arguments("\u2068\n\u2069")]
    [Arguments("\u2066a\n\u2069")]
    [Arguments("\u2067a\n\u2069")]
    public async Task IsolateThatHoldsALineFeed_EndsAtTheLineFeed(string text)
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(12);

        await Assert.That(TextMeasurer.CountLines(text, new TextOptions(font))).IsEqualTo(2);
    }

    // The layout lays out no variation selector, so a line break opportunity at one is taken at the codepoint after it,
    // and the text breaks into the lines it breaks into without the variation selector. A variation selector after a
    // space or a line feed starts a grapheme, and the codepoint after it can be in the same grapheme.
    [Test]
    [Arguments("a\n\uFE0Fb", 2)]
    [Arguments("a \n\uFE0Fb", 2)]
    [Arguments("a \uFE0F\nb", 2)]
    [Arguments("a\n\uFE0F\u0301b", 2)]
    [Arguments("aaaa \uFE0Fbbbb cccc", 3)]
    [Arguments("aaaa\n\uFE0Fbbbb cccc", 3)]
    public async Task BreakAtAVariationSelector_IsTakenAtTheCodepointAfterIt(string text, int lineCount)
    {
        Font font = CreateFont(text, 12);
        string withoutSelector = text.Replace("\uFE0F", string.Empty);
        foreach (WordBreaking wordBreaking in Enum.GetValues<WordBreaking>())
        {
            TextOptions options = new(font) { WordBreaking = wordBreaking };
            options.WrappingLength = TextMeasurer.MeasureAdvance("aaaaa", options).Width + 1;

            string expected = Visual(GetLines(withoutSelector, options));

            await Assert.That(Visual(GetLines(text, options))).IsEqualTo(expected);
            await Assert.That(expected.Split('|').Length).IsEqualTo(lineCount);
        }
    }

    // The b overflows the line, and the line break opportunity is right before it, after the space. No glyph on the
    // line is at or after the opportunity, so the line wraps there, before the b. BreakAll wraps before the glyph that
    // overflows, at a break opportunity or not, so it is not at issue.
    [Test]
    [Arguments("aaaa b", WordBreaking.Standard)]
    [Arguments("aaaa b", WordBreaking.KeepAll)]
    [Arguments("aaaa b", WordBreaking.BreakWord)]
    [Arguments("aaaa \uFE0Fb", WordBreaking.Standard)]
    [Arguments("aaaa \uFE0Fb", WordBreaking.KeepAll)]
    [Arguments("aaaa \uFE0Fb", WordBreaking.BreakWord)]
    public async Task OverflowAtTheCodepointOfABreak_WrapsAtTheBreak(string text, WordBreaking wordBreaking)
    {
        Font font = CreateFont(text, 12);
        TextOptions options = new(font) { WordBreaking = wordBreaking };
        options.WrappingLength = TextMeasurer.MeasureAdvance("aaaaa", options).Width + 1;

        await Assert.That(Visual(GetLines(text, options))).IsEqualTo("aaaa|b");
    }

    // U+FE0F and U+0301 extend the grapheme of the space before them, and the line break opportunity after the space is
    // inside that grapheme. A line wraps only between graphemes, so the line wraps after the grapheme, before the b.
    [Test]
    [Arguments("a \uFE0F\u0301bbbb")]
    [Arguments("a \u0301bbbb")]
    public async Task BreakInsideAGrapheme_IsTakenAtTheNextGrapheme(string text)
    {
        Font font = CreateFont(text, 12);
        TextOptions options = new(font);
        options.WrappingLength = TextMeasurer.MeasureAdvance("aaaaa", options).Width + 1;

        await Assert.That(Visual(GetLines(text, options))).IsEqualTo("a \u0301|bbbb");
    }

    // The font ligates the line feed and the b after it, so the b, at the mandatory break after the line feed, has no
    // glyph. The break is taken at the c.
    [Test]
    public async Task MandatoryBreakAtACodepointWithNoGlyph_IsTakenAtTheCodepointAfterIt()
    {
        const string text = "a\nbc d";
        Font font = ((IFontMetricsCollection)new FontCollection())
            .AddMetrics(new PairLigatingFakeFontInstance(text, '\n', 'b'), CultureInfo.InvariantCulture)
            .CreateFont(12);

        await Assert.That(Visual(GetLines(text, new TextOptions(font)))).IsEqualTo("a|c d");
    }

    // The layout lays out no variation selector on its own, so text that is only variation selectors has no line, as
    // empty text has none.
    [Test]
    [Arguments("\uFE0F", LayoutMode.HorizontalTopBottom)]
    [Arguments("\uFE0F\uFE0F", LayoutMode.HorizontalTopBottom)]
    [Arguments("\uFE0F", LayoutMode.HorizontalBottomTop)]
    [Arguments("\uFE0F", LayoutMode.VerticalLeftRight)]
    [Arguments("\uFE0F", LayoutMode.VerticalRightLeft)]
    [Arguments("\uFE0F", LayoutMode.VerticalMixedLeftRight)]
    [Arguments("\uFE0F", LayoutMode.VerticalMixedRightLeft)]
    public async Task TextWithNoGlyphToLayOut_LaysOutAsEmptyText(string text, LayoutMode layoutMode)
    {
        Font font = CreateFont("a", 12);
        TextOptions options = new(font) { LayoutMode = layoutMode, TextAlignment = TextAlignment.Center, WrappingLength = 100 };

        await Assert.That(TextLayout.GenerateLayout(text.AsSpan(), options).Count).IsEqualTo(0);
        await Assert.That(TextMeasurer.CountLines(text, options)).IsEqualTo(TextMeasurer.CountLines(string.Empty, options));
        await Assert.That(TextMeasurer.MeasureSize(text, options)).IsEqualTo(TextMeasurer.MeasureSize(string.Empty, options));
    }

    private static readonly string Shy = ((char)0xAD).ToString();

    // The glyphs of each line, in the order the layout draws them, which is left to right.
    private static GlyphLayout[][] GetLines(string text, TextOptions options)
    {
        List<List<GlyphLayout>> lines = [];
        foreach (GlyphLayout glyph in TextLayout.GenerateLayout(text.AsSpan(), options))
        {
            if (glyph.IsStartOfLine)
            {
                lines.Add([]);
            }

            lines[^1].Add(glyph);
        }

        return [.. lines.Select(x => x.ToArray())];
    }

    // Each line in visual order, left to right, with the lines separated by |.
    private static string Visual(GlyphLayout[][] lines)
        => string.Join("|", lines.Select(line => string.Concat(line.Select(x => x.CodePoint.ToString()))));

    private static float X(GlyphLayout[] line, char c) => line.Single(x => x.CodePoint == new CodePoint(c)).PenLocation.X;

    private static string GetVisualOrder(string text, TextOptions options)
    {
        TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> bounds);
        return string.Concat(bounds.ToArray().OrderBy(x => x.Bounds.X).Select(x => x.Codepoint.ToString()));
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
        return d.WithSize(1);
    }

    public static Font CreateFont(string text, float pointSize)
    {
        var fc = (IFontMetricsCollection)new FontCollection();
        Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
        return d.WithSize(pointSize);
    }
}
