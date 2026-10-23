// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_337
    {
        [Test]
        public async Task CanShapeCompositeGlyphs()
        {
            Font font = new FontCollection().Add(TestFonts.DFKaiSBFile).CreateFont(1024);
            ColorGlyphRenderer renderer = new();
            TextOptions options = new(font)
            {
                HintingMode = HintingMode.Standard
            };

            TextRenderer.RenderTextTo(renderer, "標楷體輸出", options);

            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(5);
            await Assert.That(renderer.GlyphRects.Count).IsEqualTo(5);

            var expected = new FontRectangle[]
            {
                new FontRectangle(0, -101.99994F, 1024, 1024),
                new FontRectangle(1024, -101.99994F, 1024, 1024),
                new FontRectangle(2048, -101.99994F, 1024, 1024),
                new FontRectangle(3072, -101.99994F, 1024, 1024),
                new FontRectangle(4096, -101.99994F, 1024, 1024)
            };

            for (int i = 0; i < expected.Length; i++)
            {
                {
                    await CompareRectangleExact(expected[i], renderer.GlyphRects[i]);
                }
            }
        }

        private static async Task CompareRectangle(FontRectangle expected, FontRectangle actual, int precision = 4)
        {
            await Assert.That(Math.Round(actual.X, precision)).IsEqualTo(Math.Round(expected.X, precision));
            await Assert.That(Math.Round(actual.Y, precision)).IsEqualTo(Math.Round(expected.Y, precision));
            await Assert.That(Math.Round(actual.Width, precision)).IsEqualTo(Math.Round(expected.Width, precision));
            await Assert.That(Math.Round(actual.Height, precision)).IsEqualTo(Math.Round(expected.Height, precision));
        }

        private static async Task CompareRectangleExact(FontRectangle expected, FontRectangle actual)
        {
            await Assert.That(actual.X).IsEqualTo(expected.X);
            await Assert.That(actual.Y).IsEqualTo(expected.Y);
            await Assert.That(actual.Width).IsEqualTo(expected.Width);
            await Assert.That(actual.Height).IsEqualTo(expected.Height);
        }
    }
}
