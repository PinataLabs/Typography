// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// A DeltaSetIndexMap as a view over the table bytes. It maps an index, such as a glyph ID, to the outer and inner
/// index of a delta in an <see cref="ItemVariationStore"/>. HVAR, VVAR and the COLR table use it, in formats 0 and 1:
/// https://learn.microsoft.com/typography/opentype/spec/otvarcommonformats#associating-target-items-to-variation-data
/// A <see langword="default"/> map has no entries.
/// </summary>
internal readonly struct DeltaSetIndexMap
{
    private const int InnerIndexBitCountMask = 0x0F;
    private const int MapEntrySizeMask = 0x30;

    // The entries, after the header.
    private readonly ReadOnlyMemory<byte> entries;
    private readonly int entrySize;
    private readonly int innerIndexBitCount;

    private DeltaSetIndexMap(ReadOnlyMemory<byte> entries, int count, int entrySize, int innerIndexBitCount)
    {
        this.entries = entries;
        this.Count = count;
        this.entrySize = entrySize;
        this.innerIndexBitCount = innerIndexBitCount;
    }

    /// <summary>
    /// Gets the number of entries.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Reads a map and checks that its entries lie within the table.
    /// </summary>
    /// <param name="table">The table that holds the map.</param>
    /// <param name="offset">The position of the map in <paramref name="table"/>.</param>
    /// <param name="map">The map.</param>
    /// <returns><see langword="false"/> if the map is malformed. The caller then treats it as absent.</returns>
    public static bool TryRead(ReadOnlyMemory<byte> table, int offset, out DeltaSetIndexMap map)
    {
        map = default;
        if ((uint)offset > (uint)table.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> span = table.Span.Slice(offset);
        if (span.Length < 4)
        {
            return false;
        }

        // Format 0 counts the entries in 16 bits, and format 1 in 32 bits.
        int headerSize;
        uint count;
        switch (span[0])
        {
            case 0:
                headerSize = 4;
                count = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(2));
                break;

            case 1 when span.Length >= 6:
                headerSize = 6;
                count = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(2));
                break;

            default:
                return false;
        }

        int entryFormat = span[1];
        int entrySize = ((entryFormat & MapEntrySizeMask) >> 4) + 1;
        if ((long)count * entrySize > span.Length - headerSize)
        {
            return false;
        }

        map = new DeltaSetIndexMap(table.Slice(offset + headerSize, (int)count * entrySize), (int)count, entrySize, (entryFormat & InnerIndexBitCountMask) + 1);
        return true;
    }

    /// <summary>
    /// Gets the outer and inner index of a delta. An index past the last entry takes the last entry.
    /// </summary>
    /// <param name="index">The index to map, such as a glyph ID.</param>
    /// <param name="outer">The index of the ItemVariationData subtable.</param>
    /// <param name="inner">The index of the row in the subtable.</param>
    /// <returns><see langword="false"/> if the index is negative or the map has no entries.</returns>
    public bool TryGet(int index, out int outer, out int inner)
    {
        outer = 0;
        inner = 0;
        if (index < 0 || this.Count == 0)
        {
            return false;
        }

        ReadOnlySpan<byte> entry = this.entries.Span.Slice(Math.Min(index, this.Count - 1) * this.entrySize, this.entrySize);
        uint value = 0;
        foreach (byte b in entry)
        {
            value = (value << 8) | b;
        }

        inner = (int)(value & ((1U << this.innerIndexBitCount) - 1));
        outer = (int)(value >> this.innerIndexBitCount);
        return true;
    }
}
