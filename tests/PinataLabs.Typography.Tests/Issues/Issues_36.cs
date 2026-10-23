// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Fakes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_36
    {
        [Test]
        [Arguments(1)]
        [Arguments(2)]
        [Arguments(3)]
        [Arguments(4)]
        [Arguments(5)]
        public async Task TextWidthForTabOnlyTextShouldBeSingleTabWidthMultipliedByTabCount(int tabCount)
        {
            Font font = CreateFont("\t x");

            FontRectangle tabWidth = TextMeasurer.MeasureBounds("\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            string tabString = string.Empty.PadRight(tabCount, '\t');
            FontRectangle tabCountWidth = TextMeasurer.MeasureBounds(tabString, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

            await Assert.That(Math.Round(tabCountWidth.Width, 2)).IsEqualTo(Math.Round(tabWidth.Width * tabCount, 2));
        }

        [Test]
        [Arguments(1)]
        [Arguments(2)]
        [Arguments(3)]
        [Arguments(4)]
        [Arguments(5)]
        public async Task TextWidthForTabOnlyTextShouldBeSingleTabWidthMultipliedByTabCountMinusX(int tabCount)
        {
            Font font = CreateFont("\t x");

            FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle tabWidth = TextMeasurer.MeasureBounds("\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            string tabString = "x".PadLeft(tabCount + 1, '\t');
            FontRectangle tabCountWidth = TextMeasurer.MeasureBounds(tabString, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

            float singleTabWidth = tabWidth.Width - xWidth.Width;
            float finalTabWidth = tabCountWidth.Width - xWidth.Width;
            await Assert.That(Math.Round(finalTabWidth, 2)).IsEqualTo(Math.Round(singleTabWidth * tabCount, 2));
        }

        public static Font CreateFont(string text)
        {
            var fc = (IFontMetricsCollection)new FontCollection();
            Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
            return new Font(d, 1);
        }
    }
}
