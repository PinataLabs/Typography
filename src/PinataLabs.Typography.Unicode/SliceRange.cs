// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography;

/// <summary>
/// Checks the range of a slice taken from another slice.
/// </summary>
internal static class SliceRange
{
    /// <summary>
    /// Throws when <paramref name="start"/> or the end of the range falls outside a parent slice of <paramref name="parentLength"/> items.
    /// </summary>
    /// <param name="start">The start of the range, relative to the parent slice.</param>
    /// <param name="length">The length of the range.</param>
    /// <param name="parentLength">The length of the parent slice.</param>
    public static void Check(int start, int length, int parentLength)
    {
        if ((uint)start > (uint)parentLength || (uint)length > (uint)(parentLength - start))
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
    }
}
