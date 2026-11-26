// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'HVAR' or 'VVAR' table of a font with the region scalars of one variation instance. It gives the advance and the
/// leading side bearing of a glyph, which are the left side bearing in 'HVAR' and the top side bearing in 'VVAR'.
/// A <see langword="default"/> value has no table, and gives every value as it was.
/// </summary>
internal readonly struct MetricsVariation
{
    private readonly MetricsVariationTable? table;
    private readonly ItemVariationScalars scalars;

    /// <summary>
    /// Initializes a new instance of the <see cref="MetricsVariation"/> struct.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="scalars">The region scalars of its store for the coordinates of the instance.</param>
    internal MetricsVariation(MetricsVariationTable table, in ItemVariationScalars scalars)
    {
        this.table = table;
        this.scalars = scalars;
    }

    /// <summary>
    /// Gets a value indicating whether there is no table, so that the advances come from elsewhere: for a TrueType
    /// glyph, from the phantom points.
    /// </summary>
    public bool IsEmpty => this.table is null;

    /// <summary>
    /// Gets a value indicating whether the table maps the leading side bearing, which is the left side bearing of 'HVAR'.
    /// </summary>
    public bool HasLeadingBearings => this.table?.HasLeadingBearings == true;

    /// <summary>
    /// Varies the advance of a glyph, which is the advance width in 'HVAR' and the advance height in 'VVAR'.
    /// </summary>
    /// <param name="glyphId">The glyph.</param>
    /// <param name="advance">The advance at the default instance.</param>
    /// <returns>The advance, rounded once after the delta is added.</returns>
    public ushort VaryAdvance(int glyphId, ushort advance)
        => this.table is null ? advance : VariationRounding.ToUInt16(advance + (double)this.table.GetAdvanceDelta(this.scalars, glyphId));

    /// <summary>
    /// Varies the leading side bearing of a glyph, which is the left side bearing in 'HVAR' and the top side bearing
    /// in 'VVAR'. A table without a map for it leaves it as it was.
    /// </summary>
    /// <param name="glyphId">The glyph.</param>
    /// <param name="bearing">The bearing at the default instance.</param>
    /// <returns>The bearing, rounded once after the delta is added.</returns>
    public short VaryLeadingBearing(int glyphId, short bearing)
        => this.table is null || !this.table.HasLeadingBearings
            ? bearing
            : VariationRounding.ToInt16(bearing + (double)this.table.GetLeadingBearingDelta(this.scalars, glyphId));
}
