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
    public class Issues_33
    {
        [Test]
        [Arguments("\naaaabbbbccccddddeeee\n\t\t\t3 tabs\n\t\t\t\t\t5 tabs", 580, 70)] // newlines aren't directly measured but it is used for offsetting
        [Arguments("\n\tHelloworld", 310, 10)]
        [Arguments("\tHelloworld", 310, 10)]
        [Arguments("  Helloworld", 340, 10)]
        [Arguments("Hell owor ld\t", 390, 10)]
        [Arguments("Helloworld  ", 360, 10)]
        public async Task WhiteSpaceAtStartOfLineNotMeasured(string text, float width, float height)
        {
            Font font = CreateFont(text);
            FontRectangle size = TextMeasurer.MeasureBounds(text, new TextOptions(font) { Dpi = font.FontMetrics.ScaleFactor });

            await Assert.That(Math.Round(size.Height, 2)).IsEqualTo(Math.Round(height, 2));
            await Assert.That(Math.Round(size.Width, 2)).IsEqualTo(Math.Round(width, 2));
        }

        public static Font CreateFont(string text)
        {
            var fc = (IFontMetricsCollection)new FontCollection();
            Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
            return new Font(d, 1);
        }
    }
}
