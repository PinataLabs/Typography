// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_97
{
    [Test]
    public async Task ShouldNotThrowNullReferenceExceptionWhenReaderCannotBeCreatedForTable()
    {
        await Assert.That(() => FontDescription.LoadDescription(TestFonts.Issues.Issue97File)).ThrowsExactly<InvalidFontTableException>();
    }
}
