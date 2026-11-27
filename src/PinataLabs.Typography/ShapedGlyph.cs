// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography;

/// <summary>
/// A glyph that <see cref="TextShaper.Shape"/> returns, with the place of the characters it stands for in the text. The
/// advance and the offsets are in font design units. Divide them by <see cref="FontMetrics.UnitsPerEm"/> of the font and
/// multiply by its size for a length in points.
/// </summary>
public readonly struct ShapedGlyph : IEquatable<ShapedGlyph>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShapedGlyph"/> struct.
    /// </summary>
    /// <param name="glyphId">The ID of the glyph in the font.</param>
    /// <param name="cluster">The UTF-16 index in the text of the first character that the glyph stands for.</param>
    /// <param name="xAdvance">The distance from the pen to the pen of the next glyph, in font design units.</param>
    /// <param name="xOffset">How far to the right of the pen the glyph is drawn, in font design units.</param>
    /// <param name="yOffset">How far above the baseline the glyph is drawn, in font design units.</param>
    public ShapedGlyph(ushort glyphId, int cluster, int xAdvance, int xOffset, int yOffset)
    {
        this.GlyphId = glyphId;
        this.Cluster = cluster;
        this.XAdvance = xAdvance;
        this.XOffset = xOffset;
        this.YOffset = yOffset;
    }

    /// <summary>
    /// Gets the ID of the glyph in the font.
    /// </summary>
    public ushort GlyphId { get; }

    /// <summary>
    /// Gets the UTF-16 index in the text of the first character that the glyph stands for. The glyph stands for the
    /// characters from there up to the cluster of the next glyph in the text, so a ligature of two letters is one
    /// glyph, and the cluster of the glyph after it is two more than its own. A mark has the cluster of its base. The
    /// clusters never decrease from the first character of the text to the last, which in a run from right to left is
    /// from the last glyph to the first.
    /// </summary>
    public int Cluster { get; }

    /// <summary>
    /// Gets the distance from the pen to the pen of the next glyph, in font design units. It is the advance of the
    /// glyph with the adjustments of the font to it, and it is negative when the font pulls the next glyph back past the
    /// start of this one. A glyph that the font classes as a mark has no advance.
    /// </summary>
    public int XAdvance { get; }

    /// <summary>
    /// Gets how far to the right of the pen the glyph is drawn, in font design units. It does not move the pen.
    /// </summary>
    public int XOffset { get; }

    /// <summary>
    /// Gets how far above the baseline the glyph is drawn, in font design units. It does not move the pen.
    /// </summary>
    public int YOffset { get; }

    /// <summary>
    /// Compares two <see cref="ShapedGlyph"/> values for equality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if the two values are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ShapedGlyph left, ShapedGlyph right) => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="ShapedGlyph"/> values for inequality.
    /// </summary>
    /// <param name="left">The value on the left side of the operator.</param>
    /// <param name="right">The value on the right side of the operator.</param>
    /// <returns><see langword="true"/> if the two values are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ShapedGlyph left, ShapedGlyph right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ShapedGlyph other && this.Equals(other);

    /// <inheritdoc/>
    public bool Equals(ShapedGlyph other)
        => this.GlyphId == other.GlyphId
        && this.Cluster == other.Cluster
        && this.XAdvance == other.XAdvance
        && this.XOffset == other.XOffset
        && this.YOffset == other.YOffset;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.GlyphId, this.Cluster, this.XAdvance, this.XOffset, this.YOffset);

    /// <inheritdoc/>
    public override string ToString()
        => FormattableString.Invariant($"{this.GlyphId} cluster {this.Cluster} advance {this.XAdvance} offset {this.XOffset},{this.YOffset}");
}
