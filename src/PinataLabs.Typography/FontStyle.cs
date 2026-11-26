// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography;

/// <summary>
/// The style that a caller asks of a font: a weight, a width and a slope, as the CSS <c>font-weight</c>,
/// <c>font-stretch</c> and <c>font-style</c> properties give them.
/// The default value is <see cref="Regular"/>.
/// </summary>
public readonly struct FontStyle : IEquatable<FontStyle>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontStyle"/> struct.
    /// </summary>
    /// <param name="weight">The weight.</param>
    /// <param name="width">The width.</param>
    /// <param name="slope">The slope.</param>
    public FontStyle(FontWeight weight, FontWidth width, FontSlope slope)
    {
        this.Weight = weight;
        this.Width = width;
        this.Slope = slope;
    }

    /// <summary>
    /// Gets the style with weight 400, width 100% and a normal slope.
    /// </summary>
    public static FontStyle Regular => default;

    /// <summary>
    /// Gets the style with weight 700, width 100% and a normal slope.
    /// </summary>
    public static FontStyle Bold => new(FontWeight.Bold, FontWidth.Normal, FontSlope.Normal);

    /// <summary>
    /// Gets the style with weight 400, width 100% and an italic slope.
    /// </summary>
    public static FontStyle Italic => new(FontWeight.Normal, FontWidth.Normal, FontSlope.Italic);

    /// <summary>
    /// Gets the style with weight 700, width 100% and an italic slope.
    /// </summary>
    public static FontStyle BoldItalic => new(FontWeight.Bold, FontWidth.Normal, FontSlope.Italic);

    /// <summary>
    /// Gets the weight.
    /// </summary>
    public FontWeight Weight { get; }

    /// <summary>
    /// Gets the width.
    /// </summary>
    public FontWidth Width { get; }

    /// <summary>
    /// Gets the slope.
    /// </summary>
    public FontSlope Slope { get; }

    /// <summary>
    /// Compares two <see cref="FontStyle"/> values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(FontStyle left, FontStyle right)
        => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="FontStyle"/> values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operand.</param>
    /// <param name="right">The value on the right side of the operand.</param>
    /// <returns><see langword="true"/> if the values are unequal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(FontStyle left, FontStyle right)
        => !left.Equals(right);

    /// <summary>
    /// Creates a style with another weight and the same width and slope.
    /// </summary>
    /// <param name="weight">The weight of the new style.</param>
    /// <returns>The new <see cref="FontStyle"/>.</returns>
    public FontStyle WithWeight(FontWeight weight)
        => new(weight, this.Width, this.Slope);

    /// <summary>
    /// Creates a style with another width and the same weight and slope.
    /// </summary>
    /// <param name="width">The width of the new style.</param>
    /// <returns>The new <see cref="FontStyle"/>.</returns>
    public FontStyle WithWidth(FontWidth width)
        => new(this.Weight, width, this.Slope);

    /// <summary>
    /// Creates a style with another slope and the same weight and width.
    /// </summary>
    /// <param name="slope">The slope of the new style.</param>
    /// <returns>The new <see cref="FontStyle"/>.</returns>
    public FontStyle WithSlope(FontSlope slope)
        => new(this.Weight, this.Width, slope);

    /// <inheritdoc/>
    public bool Equals(FontStyle other)
        => this.Weight == other.Weight && this.Width == other.Width && this.Slope == other.Slope;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is FontStyle other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.Weight, this.Width, this.Slope);

    /// <inheritdoc/>
    public override string ToString()
        => $"{this.Weight} {this.Width} {this.Slope}";
}
