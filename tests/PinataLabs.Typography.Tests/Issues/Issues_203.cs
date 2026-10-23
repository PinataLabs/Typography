// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_203
    {
        [Fact]
        public void CanParseVersion1Font()
        {
            var font = FontDescription.LoadDescription(TestFonts.Version1Font);
            Assert.NotNull(font);
        }
    }
}
