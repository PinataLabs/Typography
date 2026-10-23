// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_180
    {
        [Fact]
        public void CorrectlySetsHeightMetrics()
        {
            // Whitney-book has invalid hhea values.
            Font font = new FontCollection().Add(TestFonts.WhitneyBookFile).CreateFont(25);

            FontRectangle size = TextMeasurer.MeasureSize("H", new TextOptions(font));

            Assert.Equal(16, size.Width, 1);
            Assert.Equal(21, size.Height, 1);
        }
    }
}
