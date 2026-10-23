// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_203
    {
        [Test]
        public void CanParseVersion1Font()
        {
            var font = FontDescription.LoadDescription(TestFonts.Version1Font);
            Assert.NotNull(font);
        }
    }
}
