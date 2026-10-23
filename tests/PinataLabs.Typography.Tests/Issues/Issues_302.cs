// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_302
    {
#if OS_WINDOWS
        [Test]
        public async Task DoesNotThrowOutOfBounds()
        {
            const string content = "فِيلْمٌ";
            FontFamily fontFamily = SystemFonts.Get("Arial");
            Font font = fontFamily.CreateFont(16, FontStyle.Regular);
            TextOptions renderOptions = new(font);

            await Assert.That(TextMeasurer.TryMeasureCharacterBounds(content, renderOptions, out ReadOnlySpan<GlyphBounds> _)).IsTrue();
        }
#endif
    }
}
