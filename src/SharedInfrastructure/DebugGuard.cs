// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;

namespace PinataLabs.Typography;

/// <summary>
/// Provides methods to protect against invalid parameters for a DEBUG build.
/// </summary>
[DebuggerStepThrough]
internal static class DebugGuard
{
    /// <summary>
    /// Ensures that the value is not null.
    /// </summary>
    /// <param name="value">The target object, which cannot be null.</param>
    /// <param name="parameterName">The name of the parameter that is to be checked.</param>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    [Conditional("DEBUG")]
    public static void NotNull<TValue>(TValue value, string parameterName)
        where TValue : class
        => ArgumentNullException.ThrowIfNull(value, parameterName);

    /// <summary>
    /// Verifies that the specified value is less than or equal to a maximum value
    /// and throws an exception if it is not.
    /// </summary>
    /// <param name="value">The target value, which should be validated.</param>
    /// <param name="max">The maximum value.</param>
    /// <param name="parameterName">The name of the parameter that is to be checked.</param>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is greater than the maximum value.
    /// </exception>
    [Conditional("DEBUG")]
    public static void MustBeLessThanOrEqualTo<TValue>(TValue value, TValue max, string parameterName)
        where TValue : IComparable<TValue>
        => ArgumentOutOfRangeException.ThrowIfGreaterThan(value, max, parameterName);

    /// <summary>
    /// Verifies that the specified value is greater than or equal to a minimum value
    /// and throws an exception if it is not.
    /// </summary>
    /// <param name="value">The target value, which should be validated.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="parameterName">The name of the parameter that is to be checked.</param>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is less than the minimum value.
    /// </exception>
    [Conditional("DEBUG")]
    public static void MustBeGreaterThanOrEqualTo<TValue>(TValue value, TValue min, string parameterName)
        where TValue : IComparable<TValue>
        => ArgumentOutOfRangeException.ThrowIfLessThan(value, min, parameterName);

    /// <summary>
    /// Verifies that the specified value is greater than or equal to a minimum value and less than
    /// or equal to a maximum value and throws an exception if it is not.
    /// </summary>
    /// <param name="value">The target value, which should be validated.</param>
    /// <param name="min">The minimum value.</param>
    /// <param name="max">The maximum value.</param>
    /// <param name="parameterName">The name of the parameter that is to be checked.</param>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is less than the minimum value or greater than the maximum value.
    /// </exception>
    [Conditional("DEBUG")]
    public static void MustBeBetweenOrEqualTo<TValue>(TValue value, TValue min, TValue max, string parameterName)
        where TValue : IComparable<TValue>
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, min, parameterName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, max, parameterName);
    }
}
