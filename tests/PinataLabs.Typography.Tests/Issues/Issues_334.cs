// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_334
{
    [Test]
    public async Task DoesNotSkewCompositeGlyph()
    {
        FontFamily family = new FontCollection().Add(TestFonts.SumanaRegularFile);
        family.TryGetMetrics(FontStyle.Regular, out FontMetrics? metrics);

        Font font = family.CreateFont(metrics!.UnitsPerEm);
        ColorGlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "(", new TextOptions(font));

        await Assert.That(renderer.GlyphRects).HasSingleItem();

        FontRectangle expected = new(31, 190.5F, 264, 1047);
        await Assert.That(renderer.GlyphRects[0]).IsEqualTo(expected);
    }
}
