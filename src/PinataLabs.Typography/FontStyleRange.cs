// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography;

/// <summary>
/// The styles that a font face covers: a range of weights, a range of widths and the slopes that it has.
/// A static face covers one weight, one width and one slope. A variable face covers the range of its <c>wght</c>
/// and <c>wdth</c> axes, and the slopes that its <c>ital</c> and <c>slnt</c> axes reach.
/// <see cref="FontFamily.MatchFace(FontStyle)"/> matches a requested <see cref="FontStyle"/> against these ranges.
/// </summary>
public sealed class FontStyleRange
{
    private const float DefaultObliqueAngle = 14F;

    internal FontStyleRange(
        FontWeight minWeight,
        FontWeight maxWeight,
        FontWidth minWidth,
        FontWidth maxWidth,
        bool hasNormal,
        bool hasItalic,
        bool hasOblique,
        float minObliqueAngle,
        float maxObliqueAngle)
    {
        this.MinWeight = minWeight;
        this.MaxWeight = maxWeight;
        this.MinWidth = minWidth;
        this.MaxWidth = maxWidth;
        this.HasNormal = hasNormal;
        this.HasItalic = hasItalic;
        this.HasOblique = hasOblique;
        this.MinObliqueAngle = hasOblique ? minObliqueAngle : 0F;
        this.MaxObliqueAngle = hasOblique ? maxObliqueAngle : 0F;
    }

    /// <summary>
    /// Gets the lightest weight that the face covers.
    /// </summary>
    public FontWeight MinWeight { get; }

    /// <summary>
    /// Gets the heaviest weight that the face covers.
    /// </summary>
    public FontWeight MaxWeight { get; }

    /// <summary>
    /// Gets the narrowest width that the face covers.
    /// </summary>
    public FontWidth MinWidth { get; }

    /// <summary>
    /// Gets the widest width that the face covers.
    /// </summary>
    public FontWidth MaxWidth { get; }

    /// <summary>
    /// Gets a value indicating whether the face has upright glyphs.
    /// </summary>
    public bool HasNormal { get; }

    /// <summary>
    /// Gets a value indicating whether the face has italic glyphs.
    /// </summary>
    public bool HasItalic { get; }

    /// <summary>
    /// Gets a value indicating whether the face has oblique glyphs.
    /// <see cref="MinObliqueAngle"/> and <see cref="MaxObliqueAngle"/> give their angles.
    /// </summary>
    public bool HasOblique { get; }

    /// <summary>
    /// Gets the smallest angle in degrees that the oblique glyphs of the face lean to the right.
    /// It is 0 when <see cref="HasOblique"/> is <see langword="false"/>.
    /// </summary>
    public float MinObliqueAngle { get; }

    /// <summary>
    /// Gets the largest angle in degrees that the oblique glyphs of the face lean to the right.
    /// It is 0 when <see cref="HasOblique"/> is <see langword="false"/>.
    /// </summary>
    public float MaxObliqueAngle { get; }

    /// <summary>
    /// Gets the oblique angles of the face as one closed interval, where upright glyphs have the angle 0.
    /// The interval is empty, and its start is greater than its end, for a face that is only italic.
    /// </summary>
    internal (float Start, float End) ObliqueInterval
    {
        get
        {
            float start = float.PositiveInfinity;
            float end = float.NegativeInfinity;
            if (this.HasNormal)
            {
                start = 0F;
                end = 0F;
            }

            if (this.HasOblique)
            {
                start = Math.Min(start, this.MinObliqueAngle);
                end = Math.Max(end, this.MaxObliqueAngle);
            }

            return (start, end);
        }
    }

    /// <summary>
    /// Determines whether the face covers a style.
    /// </summary>
    /// <param name="style">The style to look for.</param>
    /// <returns>
    /// <see langword="true"/> if the weight, width and slope of <paramref name="style"/> are all in the range;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool Contains(FontStyle style)
    {
        float weight = style.Weight.Value;
        float width = style.Width.Percent;
        if (weight < this.MinWeight.Value || weight > this.MaxWeight.Value
            || width < this.MinWidth.Percent || width > this.MaxWidth.Percent)
        {
            return false;
        }

        FontSlope slope = style.Slope;
        if (slope.Kind == FontSlopeKind.Italic)
        {
            return this.HasItalic;
        }

        float angle = slope.Kind == FontSlopeKind.Oblique ? slope.ObliqueAngle : 0F;
        (float start, float end) = this.ObliqueInterval;
        return angle >= start && angle <= end;
    }

    /// <summary>
    /// Creates the range of a static face.
    /// </summary>
    /// <param name="style">The weight, width and slope of the face.</param>
    /// <returns>The <see cref="FontStyleRange"/> that covers exactly <paramref name="style"/>.</returns>
    internal static FontStyleRange FromStyle(FontStyle style)
    {
        FontSlope slope = style.Slope;
        bool isOblique = slope.Kind == FontSlopeKind.Oblique && slope.ObliqueAngle != 0F;

        // An oblique slope of 0 degrees is upright.
        return new FontStyleRange(
            style.Weight,
            style.Weight,
            style.Width,
            style.Width,
            hasNormal: slope.Kind == FontSlopeKind.Normal || (slope.Kind == FontSlopeKind.Oblique && slope.ObliqueAngle == 0F),
            hasItalic: slope.Kind == FontSlopeKind.Italic,
            hasOblique: isOblique,
            minObliqueAngle: slope.ObliqueAngle,
            maxObliqueAngle: slope.ObliqueAngle);
    }

    /// <summary>
    /// Reads the range of a static face from its tables.
    /// </summary>
    /// <param name="os2">The OS/2 table, if the font has one.</param>
    /// <param name="head">The head table, if the font has one.</param>
    /// <param name="italicAngle">The italic angle of the post table, in degrees counter-clockwise from the vertical.</param>
    /// <param name="fvar">The fvar table, if the font is a variable font. Its axes widen the range of the default instance.</param>
    /// <returns>The <see cref="FontStyleRange"/> of the face.</returns>
    internal static FontStyleRange FromTables(OS2Table? os2, HeadTable? head, float italicAngle, FvarTable? fvar = null)
    {
        FontWeight weight = FontWeight.Normal;
        FontWidth width = FontWidth.Normal;
        FontSlope slope = FontSlope.Normal;

        if (os2 != null)
        {
            // A weight class of 1 to 9 is the legacy scale, where 4 is regular and 7 is bold, as in fontconfig and WPF.
            // A weight class of 0 is not a weight, so the BOLD bit says whether the face is bold.
            if (os2.WeightClass is >= 1 and <= 9)
            {
                weight = new FontWeight(os2.WeightClass * 100);
            }
            else if (os2.WeightClass != 0)
            {
                weight = new FontWeight(os2.WeightClass);
            }
            else if (os2.FontStyle.HasFlag(OS2Table.FontStyleSelection.BOLD))
            {
                weight = FontWeight.Bold;
            }

            width = WidthFromClass(os2.WidthClass);

            if (os2.FontStyle.HasFlag(OS2Table.FontStyleSelection.ITALIC))
            {
                slope = FontSlope.Italic;
            }
            else if (os2.Version >= 4 && os2.FontStyle.HasFlag(OS2Table.FontStyleSelection.OBLIQUE))
            {
                // The italic angle is counter-clockwise, and the oblique angle of CSS is clockwise.
                slope = FontSlope.Oblique(italicAngle != 0F ? -italicAngle : DefaultObliqueAngle);
            }
        }
        else if (head != null)
        {
            if (head.MacStyle.HasFlag(HeadTable.HeadMacStyle.Bold))
            {
                weight = FontWeight.Bold;
            }

            if (head.MacStyle.HasFlag(HeadTable.HeadMacStyle.Italic))
            {
                slope = FontSlope.Italic;
            }
        }

        FontStyleRange range = FromStyle(new FontStyle(weight, width, slope));
        return fvar is null ? range : range.WithAxes(fvar);
    }

    // The range of a variable face. The wght and wdth axes give the weights and the widths. The ital axis adds upright
    // faces where it reaches 0 and italic faces where it reaches 1. The slnt axis is an angle in degrees
    // counter-clockwise from the vertical, and the oblique angle of CSS is clockwise, so the oblique angles run from the
    // negated maximum to the negated minimum. Where a part has no axis, the part of the default instance stays, as for
    // a static face.
    private FontStyleRange WithAxes(FvarTable fvar)
    {
        FontWeight minWeight = this.MinWeight;
        FontWeight maxWeight = this.MaxWeight;
        if (fvar.TryGetAxis(FvarTable.WeightTag, out FontVariationAxis? weightAxis))
        {
            minWeight = new FontWeight(weightAxis.MinValue);
            maxWeight = new FontWeight(weightAxis.MaxValue);
        }

        FontWidth minWidth = this.MinWidth;
        FontWidth maxWidth = this.MaxWidth;
        if (fvar.TryGetAxis(FvarTable.WidthTag, out FontVariationAxis? widthAxis))
        {
            minWidth = new FontWidth(widthAxis.MinValue);
            maxWidth = new FontWidth(widthAxis.MaxValue);
        }

        bool hasNormal = this.HasNormal;
        bool hasItalic = this.HasItalic;
        bool hasOblique = this.HasOblique;
        float minAngle = this.MinObliqueAngle;
        float maxAngle = this.MaxObliqueAngle;
        if (fvar.TryGetAxis(FvarTable.ItalicTag, out FontVariationAxis? italicAxis))
        {
            hasNormal |= italicAxis.MinValue <= 0F && italicAxis.MaxValue >= 0F;
            hasItalic |= italicAxis.MinValue <= 1F && italicAxis.MaxValue >= 1F;
        }

        if (fvar.TryGetAxis(FvarTable.SlantTag, out FontVariationAxis? slantAxis))
        {
            float low = -slantAxis.MaxValue;
            float high = -slantAxis.MinValue;
            hasNormal |= low <= 0F && high >= 0F;
            if (low != 0F || high != 0F)
            {
                minAngle = hasOblique ? Math.Min(minAngle, low) : low;
                maxAngle = hasOblique ? Math.Max(maxAngle, high) : high;
                hasOblique = true;
            }
        }

        return new FontStyleRange(minWeight, maxWeight, minWidth, maxWidth, hasNormal, hasItalic, hasOblique, minAngle, maxAngle);
    }

    // usWidthClass as a percentage, from the table in the OS/2 chapter of the OpenType specification.
    // A value outside 1 to 9 is not a width class, so it gives the normal width.
    private static FontWidth WidthFromClass(ushort widthClass)
        => widthClass switch
        {
            1 => FontWidth.UltraCondensed,
            2 => FontWidth.ExtraCondensed,
            3 => FontWidth.Condensed,
            4 => FontWidth.SemiCondensed,
            6 => FontWidth.SemiExpanded,
            7 => FontWidth.Expanded,
            8 => FontWidth.ExtraExpanded,
            9 => FontWidth.UltraExpanded,
            _ => FontWidth.Normal,
        };
}
