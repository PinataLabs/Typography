// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

public class CompactFontFormatTests
{
    // https://github.com/adobe-fonts/fdarray-test/
    [Test]
    [Arguments("\u0041", 66)]
    [Arguments("\u211D", 30)]
    [Arguments("\u24EA", 235)]
    public async Task FDSelectFormat0_Works(string testStr, int expectedGlyphIndex)
    {
        // arrange
        Font font = new FontCollection().Add(TestFonts.FDArrayTest257File).CreateFont(8);
        var renderer = new ColorGlyphRenderer();

        // act
        TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

        // assert
        GlyphRendererParameters glyphKey = await Assert.That(renderer.GlyphKeys).HasSingleItem();
        await Assert.That(glyphKey.GlyphId).IsEqualTo(expectedGlyphIndex);
    }

    [Test]
    public async Task EmptyGlyphHasZeroBounds()
    {
        Font font = new FontCollection().Add(TestFonts.GetPath("SourceSansPro-Regular.otf")).CreateFont(12);

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            new CodePoint(' '),
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics>? metrics)).IsTrue();

        await Assert.That(metrics![0].Width).IsEqualTo((ushort)0);
        await Assert.That(metrics[0].Height).IsEqualTo((ushort)0);
    }
}
