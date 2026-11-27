// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography;

/// <summary>
/// A feature of the font that a caller of <see cref="TextShaper.Shape"/> turns on or off for a run, such as <c>smcp</c> or
/// <c>liga</c>. It applies to the whole run. A shaper turns on the features that its script needs, and a caller turns off
/// the ones that it does not want. When a run lists a feature more than once, the last entry counts.
/// </summary>
public readonly struct ShapingFeature : IEquatable<ShapingFeature>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShapingFeature"/> struct.
    /// </summary>
    /// <param name="tag">The tag of the feature, such as <c>Tag.Parse("smcp")</c>.</param>
    /// <param name="enabled"><see langword="true"/> to turn the feature on; <see langword="false"/> to turn it off.</param>
    public ShapingFeature(Tag tag, bool enabled)
    {
        this.Tag = tag;
        this.Enabled = enabled;
    }

    /// <summary>
    /// Gets the tag of the feature.
    /// </summary>
    public Tag Tag { get; }

    /// <summary>
    /// Gets a value indicating whether the feature is on.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Compares two <see cref="ShapingFeature"/> values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ShapingFeature left, ShapingFeature right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="ShapingFeature"/> values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if the two values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ShapingFeature left, ShapingFeature right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ShapingFeature other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(ShapingFeature other) => this.Tag.Equals(other.Tag) && this.Enabled == other.Enabled;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.Tag, this.Enabled);

    /// <inheritdoc/>
    public override string ToString() => this.Enabled ? this.Tag.ToString() : "-" + this.Tag;
}
