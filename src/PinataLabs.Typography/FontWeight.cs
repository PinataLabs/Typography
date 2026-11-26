// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace PinataLabs.Typography;

/// <summary>
/// The weight of a font face, from 1 to 1000 as the CSS <c>font-weight</c> property.
/// The default value is <see cref="Normal"/>.
/// </summary>
public readonly struct FontWeight : IEquatable<FontWeight>, IComparable<FontWeight>
{
    private const float MinValue = 1F;
    private const float MaxValue = 1000F;
    private const float NormalValue = 400F;

    // The value less 400, so that default(FontWeight) is Normal.
    private readonly float offset;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontWeight"/> struct.
    /// </summary>
    /// <param name="value">The weight. A value outside 1 to 1000 is clamped to that range.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not a number.</exception>
    public FontWeight(float value)
    {
        if (float.IsNaN(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "The weight must be a number.");
        }

        this.offset = Math.Clamp(value, MinValue, MaxValue) - NormalValue;
    }

    /// <summary>
    /// Gets the weight 100.
    /// </summary>
    public static FontWeight Thin => new(100F);

    /// <summary>
    /// Gets the weight 200.
    /// </summary>
    public static FontWeight ExtraLight => new(200F);

    /// <summary>
    /// Gets the weight 300.
    /// </summary>
    public static FontWeight Light => new(300F);

    /// <summary>
    /// Gets the weight 400, the CSS keyword <c>normal</c>.
    /// </summary>
    public static FontWeight Normal => default;

    /// <summary>
    /// Gets the weight 500.
    /// </summary>
    public static FontWeight Medium => new(500F);

    /// <summary>
    /// Gets the weight 600.
    /// </summary>
    public static FontWeight SemiBold => new(600F);

    /// <summary>
    /// Gets the weight 700, the CSS keyword <c>bold</c>.
    /// </summary>
    public static FontWeight Bold => new(700F);

    /// <summary>
    /// Gets the weight 800.
    /// </summary>
    public static FontWeight ExtraBold => new(800F);

    /// <summary>
    /// Gets the weight 900.
    /// </summary>
    public static FontWeight Black => new(900F);

    /// <summary>
    /// Gets the weight, from 1 to 1000.
    /// </summary>
    public float Value => NormalValue + this.offset;

    /// <summary>
    /// Compares two <see cref="FontWeight"/> values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(FontWeight left, FontWeight right)
        => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="FontWeight"/> values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are unequal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(FontWeight left, FontWeight right)
        => !left.Equals(right);

    /// <inheritdoc/>
    public int CompareTo(FontWeight other)
        => this.offset.CompareTo(other.offset);

    /// <inheritdoc/>
    public bool Equals(FontWeight other)
        => this.offset.Equals(other.offset);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is FontWeight other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => this.offset.GetHashCode();

    /// <inheritdoc/>
    public override string ToString()
        => this.Value.ToString(CultureInfo.InvariantCulture);
}
