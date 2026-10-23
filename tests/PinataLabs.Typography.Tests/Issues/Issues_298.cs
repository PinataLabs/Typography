// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_298
    {
        [Test]
        public async Task DoesNotThrowOutOfBounds()
        {
            const string content = "Please enter the text";

            FontCollection fontFamilies = new();

            FontFamily fontFamily = fontFamilies.Add(TestFonts.Issues.Issue298File);

            Font font = fontFamily.CreateFont(16, FontStyle.Regular);

            TextOptions renderOptions = new(font)
            {
                Dpi = 96,
                WrappingLength = 0f,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                KerningMode = KerningMode.Auto
            };

            FontRectangle bounds = TextMeasurer.MeasureSize(content.AsSpan(), renderOptions);
            await Assert.That(bounds).IsNotEqualTo(default);
        }
    }
}
