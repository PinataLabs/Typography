// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Unicode;

public class ArraySliceTests
{
    private static readonly int[] Data = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9];

    [Test]
    public async Task SliceOfAnOffsetSliceStartsAtTheSliceStart()
    {
        ArraySlice<int> slice = new ArraySlice<int>(Data, 3, 5).Slice(1, 3);

        await Assert.That(slice.Span.ToArray()).IsEquivalentTo(new[] { 4, 5, 6 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ReadOnlySliceOfAnOffsetSliceStartsAtTheSliceStart()
    {
        ReadOnlyArraySlice<int> slice = new ReadOnlyArraySlice<int>(Data, 3, 5).Slice(1, 3);

        await Assert.That(slice.Span.ToArray()).IsEquivalentTo(new[] { 4, 5, 6 }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
