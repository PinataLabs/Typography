// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class TextRunTests
{
    private const string Text = "This is a long and Honorificabilitudinitatibus califragilisticexpialidocious";

    [Test]
    [Arguments(-1, -1)]
    [Arguments(0, 0)]
    [Arguments(1, 0)]
    [Arguments(1, 1)]
    [Arguments(3, 2)]
    public async Task ThrowsZeroValue(int start, int end)
    {
        Exception? ex = await Assert.That(() => { _ = new TextRun() { Start = start, End = end }.Slice(Text.AsSpan()); }).Throws<Exception>();
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task SlicesTextCorrectly()
    {
        bool equal = "Honorificabilitudinitatibus".AsSpan().SequenceEqual(new TextRun() { Start = 19, End = 46 }.Slice(Text.AsSpan()));
        await Assert.That(equal).IsTrue();
    }
}
