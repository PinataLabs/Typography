// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Fakes;

internal class FakeGlyphSource
{
    public FakeGlyphSource(CodePoint codePoint, ushort index)
    {
        this.CodePoint = codePoint;
        this.Index = index;
    }

    public CodePoint CodePoint { get; }

    public ushort Index { get; }
}
