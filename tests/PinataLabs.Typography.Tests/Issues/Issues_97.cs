// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_97
    {
        [Test]
        public void ShouldNotThrowNullReferenceExceptionWhenReaderCannotBeCreatedForTable()
            => Assert.Throws<InvalidFontTableException>(() => FontDescription.LoadDescription(TestFonts.Issues.Issue97File));
    }
}
