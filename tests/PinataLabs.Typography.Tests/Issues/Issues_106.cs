// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_106
{
    [Test]
    [MethodDataSource(nameof(FontsAndOrders))]
    public async Task GlyphMetricsReportTheRequestedTextDecorations(string path, int codePoint, ColorFontSupport support, TextDecorations first, TextDecorations second)
    {
        // Each font comes from a new collection so no earlier test has filled its glyph cache.
        Font font = new FontCollection().Add(path).CreateFont(12);

        await Assert.That(Decorations(font, codePoint, support, first)).IsEquivalentTo([first]);
        await Assert.That(Decorations(font, codePoint, support, second)).IsEquivalentTo([second]);
        await Assert.That(Decorations(font, codePoint, support, first)).IsEquivalentTo([first]);
    }

    public static IEnumerable<(string Path, int CodePoint, ColorFontSupport Support, TextDecorations First, TextDecorations Second)> FontsAndOrders()
    {
        (string Path, int CodePoint, ColorFontSupport Support)[] glyphs =
        [
            (TestFonts.TimesNewRomanFile, 'a', ColorFontSupport.None),
            (TestFonts.SourceSans3VariableCff2File, 'a', ColorFontSupport.None),

            // A COLR glyph has one set of metrics for each of its layers.
            (TestFonts.TwemojiMozillaFile, 0x1F600, ColorFontSupport.MicrosoftColrFormat),
        ];

        foreach ((string path, int codePoint, ColorFontSupport support) in glyphs)
        {
            yield return (path, codePoint, support, TextDecorations.Underline, TextDecorations.None);
            yield return (path, codePoint, support, TextDecorations.None, TextDecorations.Underline);
        }
    }

    private static TextDecorations[] Decorations(Font font, int codePoint, ColorFontSupport support, TextDecorations decorations)
    {
        font.FontMetrics.TryGetGlyphMetrics(new CodePoint(codePoint), TextAttributes.None, decorations, LayoutMode.HorizontalTopBottom, support, out IReadOnlyList<GlyphMetrics>? metrics);
        return metrics!.Select(m => m.TextDecorations).Distinct().ToArray();
    }
}
