// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using Xunit;

namespace PinataLabs.Typography.Tests
{
    public class TextRunTests
    {
        private const string Text = "This is a long and Honorificabilitudinitatibus califragilisticexpialidocious";

        [Test]
        [Arguments(-1, -1)]
        [Arguments(0, 0)]
        [Arguments(1, 0)]
        [Arguments(1, 1)]
        [Arguments(3, 2)]
        public void ThrowsZeroValue(int start, int end)
        {
            Exception ex = Assert.ThrowsAny<Exception>(() => new TextRun() { Start = start, End = end }.Slice(Text.AsSpan()));
            Assert.NotNull(ex);
        }

        [Test]
        public void SlicesTextCorrectly()
        {
            ReadOnlySpan<char> slice = new TextRun() { Start = 19, End = 46 }.Slice(Text.AsSpan());
            Assert.True("Honorificabilitudinitatibus".AsSpan().SequenceEqual(slice));
        }
    }
}
