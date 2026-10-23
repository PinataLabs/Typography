// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_96
{
    [Test]
    public async Task ShouldNotThrowArgumentExceptionWhenFontContainsDuplicateTables()
    {
        await Assert.That(() => FontDescription.LoadDescription(TestFonts.Issues.Issue96File)).ThrowsExactly<EndOfStreamException>();
    }
}
