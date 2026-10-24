// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// Tests for fonts with a 'CFF2' outline table, rendered at their default instance.
/// </summary>
public class Cff2FontTests
{
    // Reference values were obtained with fontTools (TTFont + BoundsPen over getGlyphSet(), which
    // evaluates CFF2 charstrings at the default location, wght=200) for SourceSans3VF-Upright.otf:
    //   gs[name].draw(BoundsPen(gs)); font['hmtx'][name]
    // Arguments are: character, glyph id, advance width, xMin, yMin, xMax, yMax (font units, 1000 upem).
    // H, a, u, e and t use 'blend' in their charstrings or subroutines. "Å" does not.
    [Test]
    [Arguments('H', 9, 634, 100, 0, 534, 660)]
    [Arguments('a', 28, 486, 60, -12, 404, 490)]
    [Arguments('m', 40, 806, 96, 0, 718, 490)]
    [Arguments('b', 29, 538, 96, -12, 486, 722)]
    [Arguments('u', 48, 524, 88, -12, 428, 478)]
    [Arguments('r', 45, 306, 96, 0, 310, 490)]
    [Arguments('g', 34, 480, 54, -234, 470, 490)]
    [Arguments('e', 32, 478, 54, -12, 440, 490)]
    [Arguments('f', 33, 252, 30, 0, 276, 734)]
    [Arguments('o', 42, 532, 54, -12, 478, 490)]
    [Arguments('n', 41, 526, 96, 0, 438, 490)]
    [Arguments('s', 46, 400, 34, -12, 364, 490)]
    [Arguments('t', 47, 302, 30, -8, 294, 638)]
    [Arguments('i', 36, 222, 78, 0, 142, 670)]
    [Arguments('v', 49, 422, 12, 0, 410, 478)]
    [Arguments('Å', 59, 520, 10, 0, 510, 868)]
    public async Task DefaultInstanceGlyphMetricsMatchReference(
        char c, int glyphId, int advance, int xMin, int yMin, int xMax, int yMax)
    {
        Font font = new FontCollection().Add(TestFonts.SourceSans3VariableCff2File).CreateFont(12);
        CodePoint codePoint = new(c);

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            codePoint,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics>? metrics)).IsTrue();

        GlyphMetrics glyph = metrics![0];
        await Assert.That(glyph.GlyphId).IsEqualTo((ushort)glyphId);
        await Assert.That(glyph.AdvanceWidth).IsEqualTo((ushort)advance);
        await Assert.That(glyph.Width).IsEqualTo((ushort)(xMax - xMin));
        await Assert.That(glyph.Height).IsEqualTo((ushort)(yMax - yMin));
        await Assert.That(glyph.LeftSideBearing).IsEqualTo((short)xMin);
    }

    [Test]
    public async Task RendersNonEmptyOutlinesForText()
    {
        Font font = new FontCollection().Add(TestFonts.SourceSans3VariableCff2File).CreateFont(100);
        GlyphRenderer renderer = new();

        TextRenderer.RenderTextTo(renderer, "Hamburgefonstiv", new TextOptions(font));

        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(15);
        await Assert.That(renderer.ControlPoints.Count).IsGreaterThan(100);
    }

    [Test]
    [Arguments("H", 634)]
    [Arguments("a", 486)]
    [Arguments("m", 806)]
    [Arguments("u", 524)]
    [Arguments("g", 480)]
    [Arguments("v", 422)]
    public async Task MeasuredAdvanceMatchesHmtx(string text, int hmtxAdvance)
    {
        // At 1000 upem and 72 dpi, pixels equal font units.
        Font font = new FontCollection().Add(TestFonts.SourceSans3VariableCff2File).CreateFont(1000);
        TextOptions options = new(font) { Dpi = 72 };

        FontRectangle advance = TextMeasurer.MeasureAdvance(text, options);

        await Assert.That(advance.Width).IsEqualTo(hmtxAdvance).Within(0.01F);
    }
}
