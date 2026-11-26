// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// An Item Variation Store as a view over the table bytes. It serves HVAR, VVAR, MVAR, GDEF and the CFF2
/// variation store, which all use the same layout:
/// https://learn.microsoft.com/typography/opentype/spec/otvarcommonformats#item-variation-store
/// The store checks the headers of its VariationRegionList and its ItemVariationData subtables when it is read.
/// It copies no delta rows, and it reads each delta from the bytes when asked. A <see langword="default"/> store
/// has no regions and no data, and every delta from it is 0.
/// </summary>
internal readonly struct ItemVariationStore
{
    private const int HeaderSize = 8;
    private const int RegionListHeaderSize = 4;
    private const int RegionAxisSize = 6;
    private const int DataHeaderSize = 6;
    private const int LongWordsFlag = 0x8000;
    private const int WordDeltaCountMask = 0x7FFF;

    // The store, from its format field. The offsets inside it are relative to that field.
    private readonly ReadOnlyMemory<byte> data;
    private readonly int regionListOffset;

    private ItemVariationStore(ReadOnlyMemory<byte> data, int regionListOffset, int axisCount, int regionCount, int dataCount)
    {
        this.data = data;
        this.regionListOffset = regionListOffset;
        this.AxisCount = axisCount;
        this.RegionCount = regionCount;
        this.DataCount = dataCount;
    }

    /// <summary>
    /// Gets the number of axes that each region of the store has coordinates for.
    /// </summary>
    public int AxisCount { get; }

    /// <summary>
    /// Gets the number of regions in the VariationRegionList.
    /// </summary>
    public int RegionCount { get; }

    /// <summary>
    /// Gets the number of ItemVariationData subtables.
    /// </summary>
    public int DataCount { get; }

    /// <summary>
    /// Reads a store that a table points to, as the 'HVAR', 'VVAR', 'MVAR' and 'GDEF' tables do.
    /// </summary>
    /// <param name="table">The table that holds the store.</param>
    /// <param name="offset">The position of the store in <paramref name="table"/>.</param>
    /// <param name="store">The store.</param>
    /// <returns><see langword="false"/> if the store is malformed. The caller then treats it as absent.</returns>
    public static bool TryRead(ReadOnlyMemory<byte> table, int offset, out ItemVariationStore store)
    {
        if ((uint)offset > (uint)table.Length)
        {
            store = default;
            return false;
        }

        return TryReadStore(table.Slice(offset), out store);
    }

    /// <summary>
    /// Reads the variation store of a CFF2 table. It has a 16-bit length in front of the store, and the
    /// ItemVariationData subtables have a region index list and no delta rows.
    /// </summary>
    /// <param name="table">The CFF2 table.</param>
    /// <param name="offset">The position of the length field in <paramref name="table"/>.</param>
    /// <param name="store">The store.</param>
    /// <returns><see langword="false"/> if the store is malformed. The caller then treats it as absent.</returns>
    public static bool TryReadCff2(ReadOnlyMemory<byte> table, int offset, out ItemVariationStore store)
    {
        store = default;
        if ((uint)offset > (uint)table.Length || table.Length - offset < 2)
        {
            return false;
        }

        int length = BinaryPrimitives.ReadUInt16BigEndian(table.Span.Slice(offset));
        return table.Length - offset - 2 >= length && TryReadStore(table.Slice(offset + 2, length), out store);
    }

    /// <summary>
    /// Gets the size of an ItemVariationData subtable.
    /// </summary>
    /// <param name="dataIndex">The index of the subtable, which is the outer index of a delta, or the <c>vsindex</c> of CFF2.</param>
    /// <param name="itemCount">The number of delta rows.</param>
    /// <param name="regionIndexCount">The number of regions that each row has a delta for.</param>
    /// <returns><see langword="false"/> if the store has no such subtable.</returns>
    public bool TryGetDataCounts(int dataIndex, out int itemCount, out int regionIndexCount)
    {
        if (!this.TryGetData(dataIndex, out DataHeader header))
        {
            itemCount = 0;
            regionIndexCount = 0;
            return false;
        }

        itemCount = header.ItemCount;
        regionIndexCount = header.RegionIndexCount;
        return true;
    }

    /// <summary>
    /// Computes the scalar of every region for a set of normalised coordinates, with the algorithm of the Font Variations
    /// Overview, "Algorithm for Interpolation of Instance Values".
    /// </summary>
    /// <param name="coordinates">The normalised coordinates as F2Dot14 values, one for each axis of the font in
    /// <c>fvar</c> order. An axis with no coordinate is at 0, and a coordinate for an axis that the store does not have is unused.</param>
    /// <returns>The scalars, which answer deltas for these coordinates.</returns>
    public ItemVariationScalars CreateScalars(ReadOnlySpan<short> coordinates)
    {
        if (this.RegionCount == 0)
        {
            return new ItemVariationScalars(this, []);
        }

        float[] scalars = new float[this.RegionCount];
        ReadOnlySpan<byte> regions = this.data.Span.Slice(this.regionListOffset + RegionListHeaderSize);
        int regionSize = this.AxisCount * RegionAxisSize;
        for (int i = 0; i < scalars.Length; i++)
        {
            scalars[i] = GetScalar(regions.Slice(i * regionSize, regionSize), coordinates);
        }

        return new ItemVariationScalars(this, scalars);
    }

    /// <summary>
    /// Gets the scalars of the regions that an ItemVariationData subtable names, in the order that it names them.
    /// A CFF2 <c>blend</c> multiplies its deltas with these.
    /// </summary>
    /// <param name="dataIndex">The index of the subtable.</param>
    /// <param name="regionScalars">The scalar of each region of the store.</param>
    /// <param name="destination">Receives the scalars.</param>
    /// <param name="count">The number of scalars that the subtable names.</param>
    /// <returns><see langword="false"/> if the store has no such subtable, or <paramref name="destination"/> is shorter than <paramref name="count"/>.</returns>
    public bool TryGetRegionScalars(int dataIndex, ReadOnlySpan<float> regionScalars, Span<float> destination, out int count)
    {
        if (!this.TryGetData(dataIndex, out DataHeader header) || destination.Length < header.RegionIndexCount)
        {
            count = 0;
            return false;
        }

        ReadOnlySpan<byte> indexes = this.data.Span.Slice(header.RegionIndexesPosition, 2 * header.RegionIndexCount);
        for (int i = 0; i < header.RegionIndexCount; i++)
        {
            destination[i] = GetRegionScalar(regionScalars, BinaryPrimitives.ReadUInt16BigEndian(indexes.Slice(2 * i)));
        }

        count = header.RegionIndexCount;
        return true;
    }

    /// <summary>
    /// Sums the deltas of one row, each scaled by the scalar of its region.
    /// </summary>
    /// <param name="outer">The index of the ItemVariationData subtable.</param>
    /// <param name="inner">The index of the row in the subtable.</param>
    /// <param name="regionScalars">The scalar of each region of the store.</param>
    /// <returns>The delta, or 0 if the store has no such row.</returns>
    public float GetDelta(int outer, int inner, ReadOnlySpan<float> regionScalars)
    {
        if (!this.TryGetData(outer, out DataHeader header) || (uint)inner >= (uint)header.ItemCount)
        {
            return 0;
        }

        ReadOnlySpan<byte> span = this.data.Span;
        ReadOnlySpan<byte> indexes = span.Slice(header.RegionIndexesPosition, 2 * header.RegionIndexCount);
        ReadOnlySpan<byte> row = span.Slice(header.RowsPosition + (inner * header.RowSize), header.RowSize);
        int wordSize = header.LongWords ? 4 : 2;
        int byteSize = header.LongWords ? 2 : 1;
        float delta = 0;
        int position = 0;
        for (int i = 0; i < header.RegionIndexCount; i++)
        {
            int size = i < header.WordDeltaCount ? wordSize : byteSize;
            int value = size switch
            {
                4 => BinaryPrimitives.ReadInt32BigEndian(row.Slice(position)),
                2 => BinaryPrimitives.ReadInt16BigEndian(row.Slice(position)),
                _ => (sbyte)row[position],
            };

            position += size;
            delta += GetRegionScalar(regionScalars, BinaryPrimitives.ReadUInt16BigEndian(indexes.Slice(2 * i))) * value;
        }

        return delta;
    }

    // A region index past the region list names no region, so it contributes nothing.
    private static float GetRegionScalar(ReadOnlySpan<float> regionScalars, int regionIndex)
        => regionIndex < regionScalars.Length ? regionScalars[regionIndex] : 0;

    private static bool TryReadStore(ReadOnlyMemory<byte> store, out ItemVariationStore result)
    {
        result = default;
        ReadOnlySpan<byte> span = store.Span;
        if (span.Length < HeaderSize || BinaryPrimitives.ReadUInt16BigEndian(span) != 1)
        {
            return false;
        }

        uint regionListOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(2));
        int dataCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6));
        if (HeaderSize + (4L * dataCount) > span.Length
            || regionListOffset == 0
            || (long)regionListOffset + RegionListHeaderSize > span.Length)
        {
            return false;
        }

        int regionList = (int)regionListOffset;
        int axisCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(regionList));
        int regionCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(regionList + 2));

        // Regions with no axes would let a few bytes of input ask for 4 bytes for each of 65535 regions.
        if ((axisCount == 0 && regionCount > 0)
            || regionList + RegionListHeaderSize + ((long)axisCount * regionCount * RegionAxisSize) > span.Length)
        {
            return false;
        }

        ItemVariationStore candidate = new(store, regionList, axisCount, regionCount, dataCount);
        for (int i = 0; i < dataCount; i++)
        {
            if (!candidate.TryGetData(i, out _))
            {
                return false;
            }
        }

        result = candidate;
        return true;
    }

    // The scalar of one region, from its axis coordinates: start, peak and end of each axis, as F2Dot14 values.
    private static float GetScalar(ReadOnlySpan<byte> region, ReadOnlySpan<short> coordinates)
    {
        float scalar = 1;
        for (int axis = 0; axis * RegionAxisSize < region.Length; axis++)
        {
            ReadOnlySpan<byte> axisCoordinates = region.Slice(axis * RegionAxisSize, RegionAxisSize);
            int start = BinaryPrimitives.ReadInt16BigEndian(axisCoordinates);
            int peak = BinaryPrimitives.ReadInt16BigEndian(axisCoordinates.Slice(2));
            int end = BinaryPrimitives.ReadInt16BigEndian(axisCoordinates.Slice(4));

            // An axis with no peak, a peak outside its range, or a range that spans 0 does not take part.
            if (peak == 0 || start > peak || peak > end || (start < 0 && end > 0))
            {
                continue;
            }

            int coordinate = axis < coordinates.Length ? coordinates[axis] : 0;
            if (coordinate == peak)
            {
                continue;
            }

            if (coordinate <= start || end <= coordinate)
            {
                return 0;
            }

            scalar *= coordinate < peak
                ? (float)(coordinate - start) / (peak - start)
                : (float)(end - coordinate) / (end - peak);
        }

        return scalar;
    }

    // A subtable with a null offset has no rows and no regions.
    private bool TryGetData(int dataIndex, out DataHeader header)
    {
        header = default;
        if ((uint)dataIndex >= (uint)this.DataCount)
        {
            return false;
        }

        ReadOnlySpan<byte> span = this.data.Span;
        uint offset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(HeaderSize + (4 * dataIndex)));
        if (offset == 0)
        {
            return true;
        }

        if ((long)offset + DataHeaderSize > span.Length)
        {
            return false;
        }

        int position = (int)offset;
        int itemCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(position));
        int wordDeltaCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(position + 2));
        int regionIndexCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(position + 4));
        bool longWords = (wordDeltaCount & LongWordsFlag) != 0;
        wordDeltaCount &= WordDeltaCountMask;
        if (wordDeltaCount > regionIndexCount)
        {
            return false;
        }

        header = new DataHeader(position, itemCount, regionIndexCount, wordDeltaCount, longWords);
        long end = header.RowsPosition + ((long)itemCount * header.RowSize);
        if (end > span.Length)
        {
            header = default;
            return false;
        }

        return true;
    }

    private readonly record struct DataHeader(int Position, int ItemCount, int RegionIndexCount, int WordDeltaCount, bool LongWords)
    {
        public int RegionIndexesPosition => this.Position + DataHeaderSize;

        public int RowsPosition => this.RegionIndexesPosition + (2 * this.RegionIndexCount);

        // The first WordDeltaCount deltas of a row are words, and the rest are bytes. LONG_WORDS widens both.
        public int RowSize => this.LongWords
            ? (4 * this.WordDeltaCount) + (2 * (this.RegionIndexCount - this.WordDeltaCount))
            : (2 * this.WordDeltaCount) + (this.RegionIndexCount - this.WordDeltaCount);
    }
}
