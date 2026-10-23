// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Fakes;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_35
{
    [Test]
    public async Task RenderingTabAtStartOrLineTooShort()
    {
        Font font = CreateFont("\t x");
        FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle tabWidth = TextMeasurer.MeasureBounds("\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle tabWithXWidth = TextMeasurer.MeasureBounds("\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

        await Assert.That(Math.Round(tabWithXWidth.Width, 2)).IsEqualTo(Math.Round(tabWidth.Width + xWidth.Width, 2));
    }

    [Test]
    public async Task Rendering2TabsAtStartOfLineTooShort()
    {
        Font font = CreateFont("\t x");
        FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle tabWidth = TextMeasurer.MeasureBounds("\t\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle tabWithXWidth = TextMeasurer.MeasureBounds("\t\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

        await Assert.That(Math.Round(tabWithXWidth.Width, 2)).IsEqualTo(Math.Round(tabWidth.Width + xWidth.Width, 2));
    }

    [Test]
    public async Task TwoTabsAreDoubleWidthOfOneTab()
    {
        Font font = CreateFont("\t x");
        FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle tabWidth = TextMeasurer.MeasureBounds("\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle twoTabWidth = TextMeasurer.MeasureBounds("\t\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

        await Assert.That(Math.Round(tabWidth.Width * 2, 2)).IsEqualTo(Math.Round(twoTabWidth.Width, 2));
    }

    [Test]
    public async Task TwoTabsAreDoubleWidthOfOneTabMinusXWidth()
    {
        Font font = CreateFont("\t x");
        FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle tabWidth = TextMeasurer.MeasureBounds("\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
        FontRectangle twoTabWidth = TextMeasurer.MeasureBounds("\t\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

        await Assert.That(Math.Round((tabWidth.Width - xWidth.Width) * 2, 2)).IsEqualTo(Math.Round(twoTabWidth.Width - xWidth.Width, 2));
    }

    public static Font CreateFont(string text)
    {
        var fc = (IFontMetricsCollection)new FontCollection();
        Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
        return new Font(d, 1);
    }
}
