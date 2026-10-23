// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_97
    {
        [Fact]
        public void ShouldNotThrowNullReferenceExceptionWhenReaderCannotBeCreatedForTable()
            => Assert.Throws<InvalidFontTableException>(() => FontDescription.LoadDescription(TestFonts.Issues.Issue97File));
    }
}
