// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using PinataLabs.Typography.Tests.Fakes;
using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_35
    {
        [Test]
        public void RenderingTabAtStartOrLineTooShort()
        {
            Font font = CreateFont("\t x");
            FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle tabWidth = TextMeasurer.MeasureBounds("\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle tabWithXWidth = TextMeasurer.MeasureBounds("\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

            Assert.Equal(tabWidth.Width + xWidth.Width, tabWithXWidth.Width, 2);
        }

        [Test]
        public void Rendering2TabsAtStartOfLineTooShort()
        {
            Font font = CreateFont("\t x");
            FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle tabWidth = TextMeasurer.MeasureBounds("\t\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle tabWithXWidth = TextMeasurer.MeasureBounds("\t\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

            Assert.Equal(tabWidth.Width + xWidth.Width, tabWithXWidth.Width, 2);
        }

        [Test]
        public void TwoTabsAreDoubleWidthOfOneTab()
        {
            Font font = CreateFont("\t x");
            FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle tabWidth = TextMeasurer.MeasureBounds("\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle twoTabWidth = TextMeasurer.MeasureBounds("\t\t", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

            Assert.Equal(twoTabWidth.Width, tabWidth.Width * 2, 2);
        }

        [Test]
        public void TwoTabsAreDoubleWidthOfOneTabMinusXWidth()
        {
            Font font = CreateFont("\t x");
            FontRectangle xWidth = TextMeasurer.MeasureBounds("x", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle tabWidth = TextMeasurer.MeasureBounds("\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });
            FontRectangle twoTabWidth = TextMeasurer.MeasureBounds("\t\tx", new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

            Assert.Equal(twoTabWidth.Width - xWidth.Width, (tabWidth.Width - xWidth.Width) * 2, 2);
        }

        public static Font CreateFont(string text)
        {
            var fc = (IFontMetricsCollection)new FontCollection();
            Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
            return new Font(d, 1);
        }
    }
}
