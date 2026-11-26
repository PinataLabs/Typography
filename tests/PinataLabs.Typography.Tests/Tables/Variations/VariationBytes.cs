// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Writes the binary layouts of the common variation formats, so a test can build exactly the data it needs.
/// </summary>
internal static class VariationBytes
{
    /// <summary>
    /// One ItemVariationData subtable to write: the regions that it names, how many of the leading deltas of each row
    /// are words, and the rows.
    /// </summary>
    internal readonly record struct ItemData(int[] RegionIndexes, int WordDeltaCount, int[][] Rows, bool LongWords = false);

    /// <summary>
    /// Converts a coordinate in -2 to 2 to F2Dot14.
    /// </summary>
    public static short F2Dot14(double value) => (short)Math.Round(value * 16384);

    /// <summary>
    /// Writes an Item Variation Store.
    /// </summary>
    /// <param name="axisCount">The number of axes of each region.</param>
    /// <param name="regions">The regions, each as the start, peak and end of every axis in turn.</param>
    /// <param name="data">The ItemVariationData subtables. A <see langword="null"/> one is written as a null offset.</param>
    public static byte[] Store(int axisCount, short[][] regions, params ItemData?[] data)
    {
        List<byte> bytes = [];
        int regionListOffset = 8 + (4 * data.Length);
        int regionListSize = 4 + (regions.Sum(region => region.Length) * 2);

        UInt16(bytes, 1);
        UInt32(bytes, (uint)regionListOffset);
        UInt16(bytes, (ushort)data.Length);

        // The offsets of the subtables follow the region list, one after the other.
        int next = regionListOffset + regionListSize;
        foreach (ItemData? item in data)
        {
            UInt32(bytes, item is null ? 0 : (uint)next);
            next += item is null ? 0 : ItemSize(item.Value);
        }

        UInt16(bytes, (ushort)axisCount);
        UInt16(bytes, (ushort)regions.Length);
        foreach (short coordinate in regions.SelectMany(region => region))
        {
            UInt16(bytes, (ushort)coordinate);
        }

        foreach (ItemData? item in data)
        {
            if (item is not null)
            {
                WriteItem(bytes, item.Value);
            }
        }

        return [.. bytes];
    }

    /// <summary>
    /// Writes a CFF2 variation store: a 16-bit length, then a store whose subtables have no rows.
    /// </summary>
    public static byte[] Cff2Store(int axisCount, short[][] regions, params int[][] regionIndexes)
    {
        byte[] store = Store(axisCount, regions, [.. regionIndexes.Select(indexes => (ItemData?)new ItemData(indexes, 0, []))]);
        List<byte> bytes = [];
        UInt16(bytes, (ushort)store.Length);
        bytes.AddRange(store);
        return [.. bytes];
    }

    /// <summary>
    /// Writes a DeltaSetIndexMap.
    /// </summary>
    /// <param name="format">0 for a 16-bit entry count, 1 for a 32-bit one.</param>
    /// <param name="entrySize">The size of an entry in bytes, 1 to 4.</param>
    /// <param name="innerBitCount">The number of low bits of an entry that hold the inner index, 1 to 16.</param>
    /// <param name="entries">The outer and inner index of each entry.</param>
    public static byte[] Map(int format, int entrySize, int innerBitCount, params (int Outer, int Inner)[] entries)
    {
        List<byte> bytes = [(byte)format, (byte)(((entrySize - 1) << 4) | (innerBitCount - 1))];
        if (format == 0)
        {
            UInt16(bytes, (ushort)entries.Length);
        }
        else
        {
            UInt32(bytes, (uint)entries.Length);
        }

        foreach ((int outer, int inner) in entries)
        {
            uint value = ((uint)outer << innerBitCount) | (uint)inner;
            for (int i = entrySize - 1; i >= 0; i--)
            {
                bytes.Add((byte)(value >> (8 * i)));
            }
        }

        return [.. bytes];
    }

    public static void UInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    public static void UInt32(List<byte> bytes, uint value)
    {
        UInt16(bytes, (ushort)(value >> 16));
        UInt16(bytes, (ushort)value);
    }

    private static int ItemSize(ItemData item)
    {
        int words = item.WordDeltaCount;
        int bytes = item.RegionIndexes.Length - words;
        int rowSize = item.LongWords ? (4 * words) + (2 * bytes) : (2 * words) + bytes;
        return 6 + (2 * item.RegionIndexes.Length) + (item.Rows.Length * rowSize);
    }

    private static void WriteItem(List<byte> bytes, ItemData item)
    {
        UInt16(bytes, (ushort)item.Rows.Length);
        UInt16(bytes, (ushort)(item.WordDeltaCount | (item.LongWords ? 0x8000 : 0)));
        UInt16(bytes, (ushort)item.RegionIndexes.Length);
        foreach (int index in item.RegionIndexes)
        {
            UInt16(bytes, (ushort)index);
        }

        foreach (int[] row in item.Rows)
        {
            for (int i = 0; i < row.Length; i++)
            {
                int size = i < item.WordDeltaCount ? (item.LongWords ? 4 : 2) : (item.LongWords ? 2 : 1);
                for (int shift = size - 1; shift >= 0; shift--)
                {
                    bytes.Add((byte)(row[i] >> (8 * shift)));
                }
            }
        }
    }
}
