// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_96
    {
        [Test]
        public void ShouldNotThrowArgumentExceptionWhenFontContainsDuplicateTables()
            => Assert.Throws<EndOfStreamException>(() => FontDescription.LoadDescription(TestFonts.Issues.Issue96File));
    }
}
