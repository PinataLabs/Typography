// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The address of one delta in an Item Variation Store: the index of an ItemVariationData subtable and the index of a
/// row in it. A GPOS <c>VariationIndex</c> device table and a FeatureVariations condition hold one.
/// A <see langword="default"/> value is the absence of an index, which the OpenType specification writes as 0xFFFFFFFF.
/// </summary>
internal readonly struct VariationIndex
{
    // The complement of the 32-bit index, so that default(VariationIndex) is the index 0xFFFFFFFF that means "none".
    private readonly uint complement;

    /// <summary>
    /// Initializes a new instance of the <see cref="VariationIndex"/> struct.
    /// </summary>
    /// <param name="outer">The index of the ItemVariationData subtable.</param>
    /// <param name="inner">The index of the row in the subtable.</param>
    public VariationIndex(ushort outer, ushort inner) => this.complement = ~(((uint)outer << 16) | inner);

    /// <summary>
    /// Gets a value indicating whether this is the absence of an index.
    /// </summary>
    public bool IsNone => this.complement == 0;

    /// <summary>
    /// Gets the index of the ItemVariationData subtable.
    /// </summary>
    public int Outer => (int)(~this.complement >> 16);

    /// <summary>
    /// Gets the index of the row in the subtable.
    /// </summary>
    public int Inner => (int)(~this.complement & 0xFFFF);
}
