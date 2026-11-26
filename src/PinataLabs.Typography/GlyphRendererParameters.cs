// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// The combined set of properties that uniquely identify the glyph that is to be rendered
/// at a particular size and dpi.
/// </summary>
[DebuggerDisplay("GlyphId = {GlyphId}, CodePoint = {CodePoint}, PointSize = {PointSize}, Dpi = {Dpi}")]
public readonly struct GlyphRendererParameters : IEquatable<GlyphRendererParameters>
{
    internal GlyphRendererParameters(
        GlyphMetrics metrics,
        TextRun textRun,
        float pointSize,
        float dpi,
        GlyphLayoutMode layoutMode)
    {
        this.FontMetrics = metrics.FontMetrics.Owner;
        this.GlyphId = metrics.GlyphId;
        this.PointSize = pointSize;
        this.Dpi = dpi;
        this.GlyphType = metrics.GlyphType;
        this.GlyphColor = metrics.GlyphColor ?? default;
        this.TextRun = textRun;
        this.CodePoint = metrics.CodePoint;
        this.LayoutMode = layoutMode;
    }

    /// <summary>
    /// Gets the metrics that own this glyph, as <see cref="Font.FontMetrics"/> and <see cref="FontFamily.Faces"/> give them.
    /// Two glyphs belong to the same font only when they have
    /// the same <see cref="FontMetrics"/> object: the comparison is by reference, so two fonts that have
    /// the same name but come from different files differ.
    /// </summary>
    public FontMetrics FontMetrics { get; }

    /// <summary>
    /// Gets the color details of this glyph.
    /// </summary>
    public GlyphColor GlyphColor { get; }

    /// <summary>
    /// Gets the type of this glyph.
    /// </summary>
    public GlyphType GlyphType { get; }

    /// <summary>
    /// Gets the id of the glyph within the font tables.
    /// </summary>
    public ushort GlyphId { get; }

    /// <summary>
    /// Gets the codepoint represented by this glyph.
    /// </summary>
    public CodePoint CodePoint { get; }

    /// <summary>
    /// Gets the rendered point size.
    /// </summary>
    public float PointSize { get; }

    /// <summary>
    /// Gets the dots-per-inch the glyph is to be rendered at.
    /// </summary>
    public float Dpi { get; }

    /// <summary>
    /// Gets the layout mode applied to the glyph.
    /// </summary>
    public GlyphLayoutMode LayoutMode { get; }

    /// <summary>
    /// Gets the text run that this glyph belongs to.
    /// </summary>
    public TextRun TextRun { get; }

    /// <summary>
    /// Compares two <see cref="GlyphRendererParameters"/> objects for equality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="GlyphRendererParameters"/> on the left side of the operand.
    /// </param>
    /// <param name="right">
    /// The <see cref="GlyphRendererParameters"/> on the right side of the operand.
    /// </param>
    /// <returns>
    /// True if the current left is equal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    public static bool operator ==(GlyphRendererParameters left, GlyphRendererParameters right)
        => left.Equals(right);

    /// <summary>
    /// Compares two <see cref="GlyphRendererParameters"/> objects for inequality.
    /// </summary>
    /// <param name="left">
    /// The <see cref="GlyphRendererParameters"/> on the left side of the operand.
    /// </param>
    /// <param name="right">
    /// The <see cref="GlyphRendererParameters"/> on the right side of the operand.
    /// </param>
    /// <returns>
    /// True if the current left is unequal to the <paramref name="right"/> parameter; otherwise, false.
    /// </returns>
    public static bool operator !=(GlyphRendererParameters left, GlyphRendererParameters right)
        => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(GlyphRendererParameters other)
        => other.PointSize == this.PointSize
        && other.Dpi == this.Dpi
        && other.GlyphId == this.GlyphId
        && other.GlyphType == this.GlyphType
        && other.TextRun.TextAttributes == this.TextRun.TextAttributes
        && other.TextRun.TextDecorations == this.TextRun.TextDecorations
        && other.LayoutMode == this.LayoutMode
        && other.GlyphColor.Equals(this.GlyphColor)
        && ReferenceEquals(other.FontMetrics, this.FontMetrics);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is GlyphRendererParameters parameters && this.Equals(parameters);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int a = HashCode.Combine(
            RuntimeHelpers.GetHashCode(this.FontMetrics),
            this.PointSize,
            this.GlyphId,
            this.GlyphType,
            this.GlyphColor);

        int b = HashCode.Combine(
            this.Dpi,
            this.TextRun.TextAttributes,
            this.TextRun.TextDecorations,
            this.LayoutMode);

        return HashCode.Combine(a, b);
    }
}
