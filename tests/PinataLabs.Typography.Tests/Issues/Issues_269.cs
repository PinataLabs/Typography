// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_269
    {
        [Test]
        public void CorrectlySetsMetricsForFontsNotAdheringToSpec()
        {
            // AliceFrancesHMK has invalid subtables.
            Font font = new FontCollection().Add(TestFonts.AliceFrancesHMKRegularFile).CreateFont(25);

            FontRectangle size = TextMeasurer.MeasureSize("H", new TextOptions(font));
            Assert.Equal(32, size.Width, 1);
            Assert.Equal(27, size.Height, 1);
        }
    }
}
