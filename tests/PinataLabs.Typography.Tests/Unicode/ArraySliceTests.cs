// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
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

    // The slice 3..8 holds 5 items, so a slice of it that does not lie in 0..5 is out of range, though the array holds
    // the items.
    [Test]
    [Arguments(-1, 1)]
    [Arguments(0, 6)]
    [Arguments(5, 1)]
    [Arguments(6, 0)]
    [Arguments(1, -1)]
    public async Task SliceOutsideTheSlice_Throws(int start, int length)
    {
        await Assert.That(() => new ArraySlice<int>(Data, 3, 5).Slice(start, length)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => new ReadOnlyArraySlice<int>(Data, 3, 5).Slice(start, length)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    [Arguments(0, 5)]
    [Arguments(5, 0)]
    [Arguments(2, 3)]
    public async Task SliceInsideTheSlice_HoldsItsItems(int start, int length)
    {
        int[] expected = Data[(3 + start)..(3 + start + length)];

        await Assert.That(new ArraySlice<int>(Data, 3, 5).Slice(start, length).Span.ToArray()).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(new ReadOnlyArraySlice<int>(Data, 3, 5).Slice(start, length).Span.ToArray()).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
