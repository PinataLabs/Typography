// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_269
    {
        [Test]
        public async Task CorrectlySetsMetricsForFontsNotAdheringToSpec()
        {
            // AliceFrancesHMK has invalid subtables.
            Font font = new FontCollection().Add(TestFonts.AliceFrancesHMKRegularFile).CreateFont(25);

            FontRectangle size = TextMeasurer.MeasureSize("H", new TextOptions(font));
            await Assert.That(Math.Round(size.Width, 1)).IsEqualTo(32);
            await Assert.That(Math.Round(size.Height, 1)).IsEqualTo(27);
        }
    }
}
