// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace PinataLabs.Typography;

/// <summary>
/// The width of a font face as a percentage of the normal width, from 50 to 200 as the CSS <c>font-stretch</c> property.
/// The default value is <see cref="Normal"/>.
/// </summary>
public readonly struct FontWidth : IEquatable<FontWidth>, IComparable<FontWidth>
{
    private const float MinPercent = 50F;
    private const float MaxPercent = 200F;
    private const float NormalPercent = 100F;

    // The percentage less 100, so that default(FontWidth) is Normal.
    private readonly float offset;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontWidth"/> struct.
    /// </summary>
    /// <param name="percent">The width. A value outside 50 to 200 is clamped to that range.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percent"/> is not a number.</exception>
    public FontWidth(float percent)
    {
        if (float.IsNaN(percent))
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "The width must be a number.");
        }

        this.offset = Math.Clamp(percent, MinPercent, MaxPercent) - NormalPercent;
    }

    /// <summary>
    /// Gets the width 50%, the CSS keyword <c>ultra-condensed</c>.
    /// </summary>
    public static FontWidth UltraCondensed => new(50F);

    /// <summary>
    /// Gets the width 62.5%, the CSS keyword <c>extra-condensed</c>.
    /// </summary>
    public static FontWidth ExtraCondensed => new(62.5F);

    /// <summary>
    /// Gets the width 75%, the CSS keyword <c>condensed</c>.
    /// </summary>
    public static FontWidth Condensed => new(75F);

    /// <summary>
    /// Gets the width 87.5%, the CSS keyword <c>semi-condensed</c>.
    /// </summary>
    public static FontWidth SemiCondensed => new(87.5F);

    /// <summary>
    /// Gets the width 100%, the CSS keyword <c>normal</c>.
    /// </summary>
    public static FontWidth Normal => default;

    /// <summary>
    /// Gets the width 112.5%, the CSS keyword <c>semi-expanded</c>.
    /// </summary>
    public static FontWidth SemiExpanded => new(112.5F);

    /// <summary>
    /// Gets the width 125%, the CSS keyword <c>expanded</c>.
    /// </summary>
    public static FontWidth Expanded => new(125F);

    /// <summary>
    /// Gets the width 150%, the CSS keyword <c>extra-expanded</c>.
    /// </summary>
    public static FontWidth ExtraExpanded => new(150F);

    /// <summary>
    /// Gets the width 200%, the CSS keyword <c>ultra-expanded</c>.
    /// </summary>
    public static FontWidth UltraExpanded => new(200F);

    /// <summary>
    /// Gets the width as a percentage of the normal width, from 50 to 200.
    /// </summary>
    public float Percent => NormalPercent + this.offset;

    /// <summary>
    /// Compares two <see cref="FontWidth"/> values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(FontWidth left, FontWidth right)
        => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="FontWidth"/> values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are unequal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(FontWidth left, FontWidth right)
        => !left.Equals(right);

    /// <inheritdoc/>
    public int CompareTo(FontWidth other)
        => this.offset.CompareTo(other.offset);

    /// <inheritdoc/>
    public bool Equals(FontWidth other)
        => this.offset.Equals(other.offset);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is FontWidth other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => this.offset.GetHashCode();

    /// <inheritdoc/>
    public override string ToString()
        => this.Percent.ToString(CultureInfo.InvariantCulture) + "%";
}
