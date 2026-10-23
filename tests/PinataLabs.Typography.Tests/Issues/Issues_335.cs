// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_335
    {
        [Test]
        [Arguments(TextAlignment.Start)]
        [Arguments(TextAlignment.Center)]
        [Arguments(TextAlignment.End)]
        public async Task HorizontalAlignmentWorksForSingleLineText(TextAlignment alignment)
        {
            FontFamily family = new FontCollection().Add(TestFonts.OpenSansFile);
            family.TryGetMetrics(FontStyle.Regular, out FontMetrics metrics);

            TextOptions options = new(family.CreateFont(metrics.UnitsPerEm))
            {
                LineSpacing = 1.5F,
                WrappingLength = 10000,
                TextAlignment = alignment,
            };

            string text = "abc123";

            FontRectangle singleBounds = TextMeasurer.MeasureBounds(text, options);

            text = "abc123\nabc123";

            FontRectangle multipleBounds = TextMeasurer.MeasureBounds(text, options);

            await Assert.That(singleBounds.Location).IsEqualTo(multipleBounds.Location);
        }

        [Test]
        [Arguments(TextAlignment.Start)]
        [Arguments(TextAlignment.Center)]
        [Arguments(TextAlignment.End)]
        public async Task VerticalAlignmentWorksForSingleLineText(TextAlignment alignment)
        {
            FontFamily family = new FontCollection().Add(TestFonts.OpenSansFile);
            family.TryGetMetrics(FontStyle.Regular, out FontMetrics metrics);

            TextOptions options = new(family.CreateFont(metrics.UnitsPerEm))
            {
                LineSpacing = 1.5F,
                WrappingLength = 10000,
                TextAlignment = alignment,
                LayoutMode = LayoutMode.VerticalLeftRight
            };

            string text = "abc123";

            FontRectangle singleBounds = TextMeasurer.MeasureBounds(text, options);

            text = "abc123\nabc123";

            FontRectangle multipleBounds = TextMeasurer.MeasureBounds(text, options);

            await Assert.That(singleBounds.Location).IsEqualTo(multipleBounds.Location);
        }
    }
}
