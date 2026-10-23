// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_27
{
    [Test]
    public async Task ThrowsMeasuringWhitespace()
    {
        // wendy one returns wrong points for 'o'
        Font font = new FontCollection().Add(TestFonts.WendyOneFile).CreateFont(12);
        FontRectangle size = TextMeasurer.MeasureBounds("          ", new TextOptions(new Font(font, 30)));

        await Assert.That(Math.Round(size.Width, 1)).IsEqualTo(60);
        await Assert.That(Math.Round(size.Height, 1)).IsEqualTo(0);
    }
}
