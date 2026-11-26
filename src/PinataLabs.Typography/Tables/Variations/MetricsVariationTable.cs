// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// What the 'HVAR' and 'VVAR' tables have in common: an Item Variation Store, and delta set index maps that give each
/// glyph its delta for the advance and for the side bearing in the direction of the table.
/// https://learn.microsoft.com/typography/opentype/spec/hvar
/// https://learn.microsoft.com/typography/opentype/spec/vvar
/// </summary>
/// <remarks>
/// A table that is truncated, has another major version, or has a store or a map that is malformed is absent, so
/// that a table that is read always answers. A map of no entries is no map. The right side bearing and bottom side bearing maps, and the vertical
/// origin map of 'VVAR', are not read: <see cref="GlyphMetrics.RightSideBearing"/> and
/// <see cref="GlyphMetrics.BottomSideBearing"/> follow from the advance, the other bearing and the size of the glyph.
/// </remarks>
internal abstract class MetricsVariationTable : Table
{
    private const int MajorVersion = 1;
    private const int StoreOffsetPosition = 4;
    private const int AdvanceMapOffsetPosition = 8;
    private const int LeadingBearingMapOffsetPosition = 12;

    private readonly ItemVariationStore store;
    private readonly DeltaSetIndexMap? advanceMap;
    private readonly DeltaSetIndexMap? leadingBearingMap;

    private protected MetricsVariationTable(in ItemVariationStore store, DeltaSetIndexMap? advanceMap, DeltaSetIndexMap? leadingBearingMap)
    {
        this.store = store;
        this.advanceMap = advanceMap;
        this.leadingBearingMap = leadingBearingMap;
    }

    /// <summary>
    /// Gets a value indicating whether the table maps the left side bearing, or the top side bearing for 'VVAR'.
    /// </summary>
    internal bool HasLeadingBearings => this.leadingBearingMap is not null;

    /// <summary>
    /// Computes the region scalars of the table for the coordinates of an instance.
    /// </summary>
    /// <param name="coordinates">The normalised F2Dot14 coordinates of the instance, in <c>fvar</c> order.</param>
    /// <returns>What gives the deltas of the table at these coordinates.</returns>
    public MetricsVariation CreateVariation(ReadOnlySpan<short> coordinates)
        => new(this, this.store.CreateScalars(coordinates));

    /// <summary>
    /// Gets the delta of the advance of a glyph. Without a map, the glyph index is the inner index and the outer index is 0.
    /// </summary>
    /// <param name="scalars">The region scalars of the instance.</param>
    /// <param name="glyphId">The glyph.</param>
    /// <returns>The delta, which is 0 for a glyph that the map or the store lacks.</returns>
    internal float GetAdvanceDelta(in ItemVariationScalars scalars, int glyphId)
    {
        if (this.advanceMap is not DeltaSetIndexMap map)
        {
            return scalars.GetDelta(0, glyphId);
        }

        return map.TryGet(glyphId, out int outer, out int inner) ? scalars.GetDelta(outer, inner) : 0;
    }

    /// <summary>
    /// Gets the delta of the left side bearing of a glyph, or of its top side bearing in 'VVAR'.
    /// </summary>
    /// <param name="scalars">The region scalars of the instance.</param>
    /// <param name="glyphId">The glyph.</param>
    /// <returns>The delta, which is 0 for a glyph that the map or the store lacks, and when the table has no such map.</returns>
    internal float GetLeadingBearingDelta(in ItemVariationScalars scalars, int glyphId)
        => this.leadingBearingMap is DeltaSetIndexMap map && map.TryGet(glyphId, out int outer, out int inner)
            ? scalars.GetDelta(outer, inner)
            : 0;

    /// <summary>
    /// Reads the part of the table that 'HVAR' and 'VVAR' share, which is their header up to the mappings of the bearings.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="headerSize">The size of the header of the table.</param>
    /// <param name="store">The store.</param>
    /// <param name="advanceMap">The advance map, or <see langword="null"/> when the table has none.</param>
    /// <param name="leadingBearingMap">The map of the left or top side bearing, or <see langword="null"/> when the table has none.</param>
    /// <returns><see langword="false"/> if the table is malformed.</returns>
    private protected static bool TryRead(
        ReadOnlyMemory<byte> table,
        int headerSize,
        out ItemVariationStore store,
        out DeltaSetIndexMap? advanceMap,
        out DeltaSetIndexMap? leadingBearingMap)
    {
        store = default;
        advanceMap = null;
        leadingBearingMap = null;
        ReadOnlySpan<byte> span = table.Span;
        if (span.Length < headerSize || BinaryPrimitives.ReadUInt16BigEndian(span) != MajorVersion)
        {
            return false;
        }

        // A store is required, and each map is optional, with the offset 0 for a map that the table lacks.
        return TryReadOffset(span, StoreOffsetPosition, out int storeOffset)
            && storeOffset != 0
            && ItemVariationStore.TryRead(table, storeOffset, out store)
            && TryReadMap(table, AdvanceMapOffsetPosition, out advanceMap)
            && TryReadMap(table, LeadingBearingMapOffsetPosition, out leadingBearingMap);
    }

    private static bool TryReadOffset(ReadOnlySpan<byte> span, int position, out int offset)
    {
        uint value = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(position));
        offset = (int)value;
        return value <= int.MaxValue;
    }

    private static bool TryReadMap(ReadOnlyMemory<byte> table, int position, out DeltaSetIndexMap? map)
    {
        map = null;
        if (!TryReadOffset(table.Span, position, out int offset))
        {
            return false;
        }

        if (offset == 0)
        {
            return true;
        }

        if (!DeltaSetIndexMap.TryRead(table, offset, out DeltaSetIndexMap read))
        {
            return false;
        }

        // HarfBuzz reads a map of no entries as it reads no map: the advance takes the glyph index as its inner index, and no bearing
        // is mapped.
        map = read.Count == 0 ? null : read;
        return true;
    }
}
