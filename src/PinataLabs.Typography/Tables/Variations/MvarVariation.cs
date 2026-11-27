// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'MVAR' table of a font with the region scalars of one variation instance. It varies the global metrics that
/// the instance reads from other tables. A <see langword="default"/> value has no table, and gives every value as it was.
/// </summary>
internal readonly struct MvarVariation
{
    // The tags of the metrics that the library reads.
    internal static readonly Tag HorizontalAscender = Tag.Parse("hasc");
    internal static readonly Tag HorizontalDescender = Tag.Parse("hdsc");
    internal static readonly Tag HorizontalLineGap = Tag.Parse("hlgp");
    internal static readonly Tag HorizontalClippingAscent = Tag.Parse("hcla");
    internal static readonly Tag HorizontalClippingDescent = Tag.Parse("hcld");
    internal static readonly Tag VerticalAscender = Tag.Parse("vasc");
    internal static readonly Tag VerticalDescender = Tag.Parse("vdsc");
    internal static readonly Tag VerticalLineGap = Tag.Parse("vlgp");
    internal static readonly Tag SubscriptXSize = Tag.Parse("sbxs");
    internal static readonly Tag SubscriptYSize = Tag.Parse("sbys");
    internal static readonly Tag SubscriptXOffset = Tag.Parse("sbxo");
    internal static readonly Tag SubscriptYOffset = Tag.Parse("sbyo");
    internal static readonly Tag SuperscriptXSize = Tag.Parse("spxs");
    internal static readonly Tag SuperscriptYSize = Tag.Parse("spys");
    internal static readonly Tag SuperscriptXOffset = Tag.Parse("spxo");
    internal static readonly Tag SuperscriptYOffset = Tag.Parse("spyo");
    internal static readonly Tag StrikeoutSize = Tag.Parse("strs");
    internal static readonly Tag StrikeoutPosition = Tag.Parse("stro");
    internal static readonly Tag CapHeight = Tag.Parse("cpht");
    internal static readonly Tag XHeight = Tag.Parse("xhgt");
    internal static readonly Tag UnderlineThickness = Tag.Parse("unds");
    internal static readonly Tag UnderlinePosition = Tag.Parse("undo");

    private readonly MvarTable? table;
    private readonly ItemVariationScalars scalars;

    /// <summary>
    /// Initializes a new instance of the <see cref="MvarVariation"/> struct.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="scalars">The region scalars of its store for the coordinates of the instance.</param>
    internal MvarVariation(MvarTable table, in ItemVariationScalars scalars)
    {
        this.table = table;
        this.scalars = scalars;
    }

    /// <summary>
    /// Varies a metric.
    /// </summary>
    /// <param name="tag">The tag of the metric.</param>
    /// <param name="value">The value at the default instance.</param>
    /// <returns>The value, rounded once after the delta is added. It is <paramref name="value"/> when the table has no record for the tag.</returns>
    public short Apply(Tag tag, short value)
        => this.table is null ? value : VariationRounding.ToInt16(value + (double)this.table.GetDelta(this.scalars, tag));

    /// <summary>
    /// Varies a metric that the font stores wider than a <see cref="short"/>, as the win ascent and descent are
    /// <see cref="ushort"/>. The caller clamps once, after it does the rest of its arithmetic.
    /// </summary>
    /// <param name="tag">The tag of the metric.</param>
    /// <param name="value">The value at the default instance.</param>
    /// <returns>The value, rounded once after the delta is added. It is <paramref name="value"/> when the table has no record for the tag.</returns>
    public int ApplyWide(Tag tag, int value)
        => this.table is null
            ? value
            : (int)Math.Clamp(VariationRounding.Round(value + (double)this.table.GetDelta(this.scalars, tag)), int.MinValue, int.MaxValue);
}
