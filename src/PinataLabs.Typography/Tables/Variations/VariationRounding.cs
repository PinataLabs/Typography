// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// Rounds a varied value that a public type stores as an integer. The caller sums every delta first and rounds once.
/// </summary>
internal static class VariationRounding
{
    /// <summary>
    /// Rounds to the nearest integer, with a half rounding up, as fontTools <c>otRound</c> does. So 2.5 is 3 and -2.5 is -2.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The integer.</returns>
    public static double Round(double value) => Math.Floor(value + 0.5);

    /// <summary>
    /// Rounds a varied value to an <see cref="ushort"/>, clamped to the range of the type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The integer.</returns>
    public static ushort ToUInt16(double value) => (ushort)Math.Clamp(Round(value), ushort.MinValue, ushort.MaxValue);

    /// <summary>
    /// Rounds a varied value to a <see cref="short"/>, clamped to the range of the type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The integer.</returns>
    public static short ToInt16(double value) => (short)Math.Clamp(Round(value), short.MinValue, short.MaxValue);
}
