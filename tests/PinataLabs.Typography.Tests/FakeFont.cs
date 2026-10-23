// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Fakes;

namespace PinataLabs.Typography.Tests
{
    public class FakeFont
    {
        [Test]
        public async Task TestFontMetricProperties()
        {
            Font fakeFont = CreateFont("A");
            FontMetrics metrics = fakeFont.FontMetrics;
            await Assert.That(metrics.UnitsPerEm).IsEqualTo(30);
            await Assert.That(metrics.HorizontalMetrics.Ascender).IsEqualTo(35);
            await Assert.That(metrics.HorizontalMetrics.Descender).IsEqualTo(8);
            await Assert.That(metrics.HorizontalMetrics.LineGap).IsEqualTo(12);
            await Assert.That(metrics.HorizontalMetrics.LineHeight).IsEqualTo(35 - 8 + 12);

            // Vertical metrics are all ones. Descender is always negative due to the grid orientation.
            await Assert.That(metrics.VerticalMetrics.Ascender).IsEqualTo(1);
            await Assert.That(metrics.VerticalMetrics.Descender).IsEqualTo(-1);
            await Assert.That(metrics.VerticalMetrics.LineGap).IsEqualTo(1);
            await Assert.That(metrics.VerticalMetrics.LineHeight).IsEqualTo(1 - (-1) + 1);
        }

        public static Font CreateFont(string text, string name = "name")
            => CreateFontWithInstance(text, name, out _);

        internal static Font CreateFontWithInstance(string text, string name, out FakeFontInstance instance)
        {
            var fc = (IFontMetricsCollection)new FontCollection();
            instance = FakeFontInstance.CreateFontWithVaryingVerticalFontMetrics(text, name);
            Font d = fc.AddMetrics(instance, CultureInfo.InvariantCulture).CreateFont(12);
            return new Font(d, 1);
        }
    }
}
