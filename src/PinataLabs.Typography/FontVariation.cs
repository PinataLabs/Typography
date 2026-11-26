// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography;

/// <summary>
/// A value for one axis of a variable font, in the user coordinates of the axis: 650 for <c>wght</c> is a weight
/// of 650. The rules follow the CSS <c>font-variation-settings</c> property: a font ignores a variation whose axis it
/// does not have, it clamps the value to the range of the axis, and the last value for an axis wins.
/// </summary>
public readonly struct FontVariation : IEquatable<FontVariation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontVariation"/> struct.
    /// </summary>
    /// <param name="axis">The tag of the axis, such as <c>Tag.Parse("wght")</c>.</param>
    /// <param name="value">The value in the user coordinates of the axis. A value that is not a number gives the default of the axis.</param>
    public FontVariation(Tag axis, float value)
    {
        this.Axis = axis;
        this.Value = value;
    }

    /// <summary>
    /// Gets the tag of the axis.
    /// </summary>
    public Tag Axis { get; }

    /// <summary>
    /// Gets the value in the user coordinates of the axis.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Compares two <see cref="FontVariation"/> values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(FontVariation left, FontVariation right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="FontVariation"/> values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if the two values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(FontVariation left, FontVariation right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FontVariation other && this.Equals(other);

    /// <inheritdoc/>
#pragma warning disable S1244 // Value equality must be exact to agree with GetHashCode.
    public bool Equals(FontVariation other) => this.Axis.Equals(other.Axis) && this.Value.Equals(other.Value);
#pragma warning restore S1244

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Axis, this.Value);

    /// <inheritdoc/>
    public override string ToString() => this.Axis + "=" + this.Value.ToString(CultureInfo.InvariantCulture);
}
