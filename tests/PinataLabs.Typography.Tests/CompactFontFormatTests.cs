// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests
{
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
    }
}
