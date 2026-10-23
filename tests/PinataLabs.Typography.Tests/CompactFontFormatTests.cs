// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace PinataLabs.Typography.Tests
{
    public class CompactFontFormatTests
    {
        // https://github.com/adobe-fonts/fdarray-test/
        [Test]
        [Arguments("\u0041", 66)]
        [Arguments("\u211D", 30)]
        [Arguments("\u24EA", 235)]
        public void FDSelectFormat0_Works(string testStr, int expectedGlyphIndex)
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.FDArrayTest257File).CreateFont(8);
            var renderer = new ColorGlyphRenderer();

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            GlyphRendererParameters glyphKey = Assert.Single(renderer.GlyphKeys);
            Assert.Equal(expectedGlyphIndex, glyphKey.GlyphId);
        }
    }
}
