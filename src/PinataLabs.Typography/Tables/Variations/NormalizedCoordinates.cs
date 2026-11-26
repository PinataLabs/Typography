// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The position of a variation instance on every axis of its font: one F2Dot14 <see langword="short"/> for each
/// <c>fvar</c> axis, in <c>fvar</c> order, after the <c>avar</c> mapping. Two requests that normalise to the same
/// values are the same instance. Equality is exact, so the value works as a dictionary key.
/// A <see langword="default"/> value has no axes, which is the coordinates of a font that has no <c>fvar</c> table.
/// </summary>
internal readonly struct NormalizedCoordinates : IEquatable<NormalizedCoordinates>
{
    private readonly short[]? values;

    /// <summary>
    /// Initializes a new instance of the <see cref="NormalizedCoordinates"/> struct.
    /// </summary>
    /// <param name="values">The F2Dot14 coordinate of each axis. The value keeps the array, so the caller must not change it.</param>
    public NormalizedCoordinates(short[] values) => this.values = values;

    /// <summary>
    /// Gets the F2Dot14 coordinate of each axis, in <c>fvar</c> order.
    /// </summary>
    public ReadOnlySpan<short> Values => this.values;

    /// <summary>
    /// Gets a value indicating whether every coordinate is 0, which is the default instance of the font.
    /// </summary>
    public bool IsDefault
    {
        get
        {
            foreach (short value in this.Values)
            {
                if (value != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Compares two values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if every coordinate is equal.</returns>
    public static bool operator ==(NormalizedCoordinates left, NormalizedCoordinates right) => left.Equals(right);

    /// <summary>
    /// Compares two values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if a coordinate differs.</returns>
    public static bool operator !=(NormalizedCoordinates left, NormalizedCoordinates right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NormalizedCoordinates other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(NormalizedCoordinates other) => this.Values.SequenceEqual(other.Values);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (short value in this.Values)
        {
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() => "[" + string.Join(", ", this.Values.ToArray()) + "]";
}
