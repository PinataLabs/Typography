// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Fakes;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_32
{
    [Test]
    public async Task TabWidth0CausesBadTabRendering()
    {
        string text = "Hello\tworld";
        Font font = CreateFont(text);
        FontRectangle size = TextMeasurer.MeasureSize(text, new TextOptions(font)
        {
            Dpi = font.FontMetrics.ScaleFactor,
            TabWidth = 0
        });

        // tab width of 0 should make tabs not render at all
        await Assert.That(Math.Round(size.Height, 4)).IsEqualTo(10);
        await Assert.That(Math.Round(size.Width, 4)).IsEqualTo(320);
    }

    public static Font CreateFont(string text)
    {
        var fc = (IFontMetricsCollection)new FontCollection();
        Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
        return new Font(d, 1);
    }
}
