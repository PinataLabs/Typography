// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_180
{
    [Test]
    public async Task CorrectlySetsHeightMetrics()
    {
        // Whitney-book has invalid hhea values.
        Font font = new FontCollection().Add(TestFonts.WhitneyBookFile).CreateFont(25);

        FontRectangle size = TextMeasurer.MeasureSize("H", new TextOptions(font));

        await Assert.That(Math.Round(size.Width, 1)).IsEqualTo(16);
        await Assert.That(Math.Round(size.Height, 1)).IsEqualTo(21);
    }
}
