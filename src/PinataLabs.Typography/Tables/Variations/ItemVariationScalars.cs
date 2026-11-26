// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The region scalars of an <see cref="ItemVariationStore"/> for one set of normalised coordinates, which is one
/// <see langword="float"/> for each region. A variation instance computes them once and asks them for deltas.
/// A <see langword="default"/> value has no regions, and every delta from it is 0.
/// </summary>
internal readonly struct ItemVariationScalars
{
    private readonly ItemVariationStore store;
    private readonly float[]? scalars;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemVariationScalars"/> struct.
    /// </summary>
    /// <param name="store">The store that the scalars belong to.</param>
    /// <param name="scalars">The scalar of each region of the store.</param>
    internal ItemVariationScalars(ItemVariationStore store, float[] scalars)
    {
        this.store = store;
        this.scalars = scalars;
    }

    /// <summary>
    /// Gets the scalar of each region of the store.
    /// </summary>
    public ReadOnlySpan<float> Regions => this.scalars;

    /// <summary>
    /// Gets the delta of one row of the store for these coordinates.
    /// </summary>
    /// <param name="outer">The index of the ItemVariationData subtable.</param>
    /// <param name="inner">The index of the row in the subtable.</param>
    /// <returns>The delta, or 0 if the store has no such row.</returns>
    public float GetDelta(int outer, int inner) => this.store.GetDelta(outer, inner, this.Regions);

    /// <summary>
    /// Gets the scalars of the regions that an ItemVariationData subtable names, in the order that it names them.
    /// A CFF2 <c>blend</c> takes the subtable of its <c>vsindex</c>.
    /// </summary>
    /// <param name="dataIndex">The index of the subtable.</param>
    /// <param name="destination">Receives the scalars.</param>
    /// <param name="count">The number of scalars that the subtable names.</param>
    /// <returns><see langword="false"/> if the store has no such subtable, or <paramref name="destination"/> is shorter than <paramref name="count"/>.</returns>
    public bool TryGetRegionScalars(int dataIndex, Span<float> destination, out int count)
        => this.store.TryGetRegionScalars(dataIndex, this.Regions, destination, out count);
}
