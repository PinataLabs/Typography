// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// Writes an sfnt file from its tables. It writes the tables in the order of their tags, each starting on a 4-byte boundary, and it
/// sets the checksum of each table and the 'head' checkSumAdjustment, so a reader that checks them accepts the file.
/// </summary>
internal static class SfntWriter
{
    private const uint Head = 0x68656164;

    private const int HeaderSize = 12;
    private const int RecordSize = 16;
    private const int CheckSumAdjustmentOffset = 8;
    private const uint ChecksumMagic = 0xB1B0AFBA;

    /// <summary>
    /// Gets the length of the file.
    /// </summary>
    /// <param name="tables">The tables, in ascending order of their tags, one for each tag.</param>
    /// <returns>The length in bytes.</returns>
    /// <exception cref="InvalidFontFileException">The file would be larger than an array can be.</exception>
    public static int GetLength(ReadOnlySpan<SfntTableSource> tables)
    {
        long length = HeaderSize + ((long)tables.Length * RecordSize);
        foreach (SfntTableSource table in tables)
        {
            length += Pad(table.Length);
        }

        return length <= int.MaxValue ? (int)length : throw new InvalidFontFileException("The subset font would be larger than 2 GB.");
    }

    /// <summary>
    /// Writes the file.
    /// </summary>
    /// <param name="version">The sfnt version.</param>
    /// <param name="tables">The tables, in ascending order of their tags, one for each tag.</param>
    /// <param name="destination">The destination, which is exactly <see cref="GetLength"/> bytes long.</param>
    public static void Write(uint version, ReadOnlySpan<SfntTableSource> tables, Span<byte> destination)
    {
        destination.Clear();
        BinaryPrimitives.WriteUInt32BigEndian(destination, version);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(4), (ushort)tables.Length);

        // The search fields let a reader find a table by binary search.
        int entrySelector = 0;
        while ((2 << entrySelector) <= tables.Length)
        {
            entrySelector++;
        }

        int searchRange = 16 << entrySelector;
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(6), (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(8), (ushort)entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(10), (ushort)((tables.Length * 16) - searchRange));

        int position = HeaderSize + (tables.Length * RecordSize);
        int headPosition = -1;
        for (int i = 0; i < tables.Length; i++)
        {
            SfntTableSource table = tables[i];
            Span<byte> contents = destination.Slice(position, table.Length);
            table.Write(contents);
            if (table.Tag == Head && contents.Length >= CheckSumAdjustmentOffset + 4)
            {
                // The checksum of 'head' counts its checkSumAdjustment as 0.
                headPosition = position;
                contents.Slice(CheckSumAdjustmentOffset, 4).Clear();
            }

            Span<byte> record = destination.Slice(HeaderSize + (i * RecordSize), RecordSize);
            BinaryPrimitives.WriteUInt32BigEndian(record, table.Tag);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(4), CheckSum(destination.Slice(position, Pad(table.Length))));
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(8), (uint)position);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(12), (uint)table.Length);
            position += Pad(table.Length);
        }

        if (headPosition >= 0)
        {
            BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(headPosition + CheckSumAdjustmentOffset), ChecksumMagic - CheckSum(destination));
        }
    }

    private static int Pad(int length)
        => (int)(((long)length + 3) & ~3L);

    // The sum of the 32-bit words of the data. The length is a multiple of 4.
    private static uint CheckSum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        for (int i = 0; i < data.Length; i += 4)
        {
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i));
        }

        return sum;
    }
}
