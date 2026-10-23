// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_203
{
    [Test]
    public async Task CanParseVersion1Font()
    {
        var font = FontDescription.LoadDescription(TestFonts.Version1Font);
        await Assert.That(font).IsNotNull();
    }
}
