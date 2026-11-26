// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace PinataLabs.Typography;

/// <summary>
/// The slope of a font face: normal, italic, or oblique at an angle, as the CSS <c>font-style</c> property.
/// The default value is <see cref="Normal"/>.
/// </summary>
public readonly struct FontSlope : IEquatable<FontSlope>
{
    private const float MaxAngle = 90F;
    private const float DefaultObliqueAngle = 14F;

    private FontSlope(FontSlopeKind kind, float obliqueAngle)
    {
        this.Kind = kind;
        this.ObliqueAngle = obliqueAngle;
    }

    /// <summary>
    /// Gets the upright slope, the CSS keyword <c>normal</c>.
    /// </summary>
    public static FontSlope Normal => default;

    /// <summary>
    /// Gets the italic slope, the CSS keyword <c>italic</c>.
    /// </summary>
    public static FontSlope Italic => new(FontSlopeKind.Italic, 0F);

    /// <summary>
    /// Gets the kind of slope.
    /// </summary>
    public FontSlopeKind Kind { get; }

    /// <summary>
    /// Gets the angle in degrees that an oblique slope leans to the right, from -90 to 90.
    /// It is 0 when <see cref="Kind"/> is not <see cref="FontSlopeKind.Oblique"/>.
    /// </summary>
    public float ObliqueAngle { get; }

    /// <summary>
    /// Compares two <see cref="FontSlope"/> values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(FontSlope left, FontSlope right)
        => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="FontSlope"/> values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are unequal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(FontSlope left, FontSlope right)
        => !left.Equals(right);

    /// <summary>
    /// Creates an oblique slope, as the CSS <c>oblique</c> keyword with an angle.
    /// </summary>
    /// <param name="degrees">
    /// The angle in degrees that the glyphs lean to the right. A value outside -90 to 90 is clamped to that range.
    /// </param>
    /// <returns>The oblique <see cref="FontSlope"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="degrees"/> is not a number.</exception>
    public static FontSlope Oblique(float degrees = DefaultObliqueAngle)
    {
        if (float.IsNaN(degrees))
        {
            throw new ArgumentOutOfRangeException(nameof(degrees), "The angle must be a number.");
        }

        return new(FontSlopeKind.Oblique, Math.Clamp(degrees, -MaxAngle, MaxAngle));
    }

    /// <inheritdoc/>
    public bool Equals(FontSlope other)
        => this.Kind == other.Kind && this.ObliqueAngle == other.ObliqueAngle;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is FontSlope other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.Kind, this.ObliqueAngle);

    /// <inheritdoc/>
    public override string ToString()
        => this.Kind switch
        {
            FontSlopeKind.Italic => "italic",
            FontSlopeKind.Oblique => "oblique " + this.ObliqueAngle.ToString(CultureInfo.InvariantCulture) + "deg",
            _ => "normal",
        };
}
