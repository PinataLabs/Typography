// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_105
{
    private const int NoBreakSpace = 0x00A0;
    private const int Space = 0x0020;

    [Test]
    [MethodDataSource(nameof(FontsAndOrders))]
    public async Task GlyphSharedBySpacesReportsTheCodePointOfTheText(string path, int first, int second)
    {
        Font font = CreateFont(path);
        font.FontMetrics.TryGetGlyphId(new CodePoint(first), out ushort firstId);
        font.FontMetrics.TryGetGlyphId(new CodePoint(second), out ushort secondId);
        await Assert.That(firstId).IsEqualTo(secondId);

        await AssertReportsCodePoint(font, first);
        await AssertReportsCodePoint(font, second);
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task MissingCharactersReportTheCodePointOfTheText(string path)
    {
        const int han = 0x4E00;
        const int thai = 0x0E01;
        Font font = CreateFont(path);
        await Assert.That(font.FontMetrics.TryGetGlyphId(new CodePoint(han), out _)).IsFalse();
        await Assert.That(font.FontMetrics.TryGetGlyphId(new CodePoint(thai), out _)).IsFalse();

        await AssertReportsCodePoint(font, han);
        await AssertReportsCodePoint(font, thai);
    }

    [Test]
    [MethodDataSource(nameof(FontsAndOrders))]
    public async Task GlyphMetricsReportTheRequestedCodePoint(string path, int first, int second)
    {
        Font font = CreateFont(path);

        font.FontMetrics.TryGetGlyphMetrics(new CodePoint(first), TextAttributes.None, TextDecorations.None, LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out IReadOnlyList<GlyphMetrics>? firstMetrics);
        font.FontMetrics.TryGetGlyphMetrics(new CodePoint(second), TextAttributes.None, TextDecorations.None, LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out IReadOnlyList<GlyphMetrics>? secondMetrics);

        await Assert.That(firstMetrics![0].CodePoint.Value).IsEqualTo(first);
        await Assert.That(secondMetrics![0].CodePoint.Value).IsEqualTo(second);
    }

    [Test]
    [MethodDataSource(nameof(FontsAndOrders))]
    public async Task GlyphsReportTheRequestedCodePoint(string path, int first, int second)
    {
        Font font = CreateFont(path);

        font.TryGetGlyphs(new CodePoint(first), out IReadOnlyList<Glyph>? firstGlyphs);
        font.TryGetGlyphs(new CodePoint(second), out IReadOnlyList<Glyph>? secondGlyphs);

        await Assert.That(firstGlyphs![0].GlyphMetrics.CodePoint.Value).IsEqualTo(first);
        await Assert.That(secondGlyphs![0].GlyphMetrics.CodePoint.Value).IsEqualTo(second);
    }

    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task NoBreakSpaceBeforeSolidusDoesNotBreakAfterSpaceWasLaidOut(string path)
    {
        const string text = "aa bb/cc";

        IReadOnlyList<float> fresh = LineOrigins(CreateFont(path), text);

        Font font = CreateFont(path);
        TextLayout.GenerateLayout(" ".AsSpan(), new TextOptions(font));
        IReadOnlyList<float> afterSpace = LineOrigins(font, text);

        // The solidus line break keeps "aa bb/" together and moves only "cc".
        await Assert.That(fresh.Distinct().Count()).IsEqualTo(2);
        await Assert.That(fresh.Take(6).Distinct().Count()).IsEqualTo(1);
        await Assert.That(afterSpace).IsEquivalentTo(fresh);
    }

    // The font keeps one set of metrics for a glyph, whatever the number of code points that ask for it. A font
    // that kept a set for each code point would grow with every missing character that a caller probes.
    [Test]
    [MethodDataSource(nameof(Fonts))]
    public async Task MetricsOfMissingCodePointsAreNotKeptForEachCodePoint(string path)
    {
        const int count = 2000;
        Font font = CreateFont(path);
        WeakReference[] probed = ProbeMissingCodePoints(font, count);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // The glyph for missing characters has one set of metrics that the font keeps.
        await Assert.That(probed.Length).IsEqualTo(count);
        await Assert.That(probed.Count(reference => reference.IsAlive)).IsLessThanOrEqualTo(1);
        GC.KeepAlive(font);
    }

    public static IEnumerable<string> Fonts()
    {
        yield return TestFonts.TimesNewRomanFile;
        yield return TestFonts.SourceSans3VariableCff2File;
    }

    public static IEnumerable<(string Path, int First, int Second)> FontsAndOrders()
    {
        foreach (string path in Fonts())
        {
            yield return (path, NoBreakSpace, Space);
            yield return (path, Space, NoBreakSpace);
        }
    }

    // Each font comes from a new collection so no earlier test has filled its glyph cache.
    private static Font CreateFont(string path) => new FontCollection().Add(path).CreateFont(12);

    // The metrics are out of reach when this returns, so only the font can keep them alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ProbeMissingCodePoints(Font font, int count)
    {
        List<WeakReference> probed = [];
        for (int value = 0x20000; probed.Count < count; value++)
        {
            CodePoint codePoint = new(value);
            if (font.FontMetrics.TryGetGlyphId(codePoint, out _))
            {
                continue;
            }

            font.FontMetrics.TryGetGlyphMetrics(codePoint, TextAttributes.None, TextDecorations.None, LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out IReadOnlyList<GlyphMetrics>? metrics);
            if (metrics![0].CodePoint != codePoint)
            {
                throw new InvalidOperationException($"U+{value:X} reports U+{metrics[0].CodePoint.Value:X}.");
            }

            probed.Add(new WeakReference(metrics[0]));
        }

        return probed.ToArray();
    }

    private static float[] LineOrigins(Font font, string text)
    {
        TextOptions measure = new(font);
        float wrap = TextMeasurer.MeasureAdvance("aa bb/c", measure).Width;
        TextOptions options = new(font) { WrappingLength = wrap };
        return TextLayout.GenerateLayout(text.AsSpan(), options).Select(g => g.PenLocation.Y).ToArray();
    }

    private static async Task AssertReportsCodePoint(Font font, int codePoint)
    {
        string text = char.ConvertFromUtf32(codePoint);
        TextOptions options = new(font);

        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        await Assert.That(layout.Select(g => g.CodePoint.Value)).IsEquivalentTo([codePoint]);

        TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> bounds);
        int[] measured = bounds.ToArray().Select(b => b.Codepoint.Value).ToArray();
        await Assert.That(measured).IsEquivalentTo([codePoint]);
    }
}
