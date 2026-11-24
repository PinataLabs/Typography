// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// Reads a 'cmap' table in place. Every method is bounds-checked, never throws, and treats data that
/// does not hold together as a code with no glyph.
/// </summary>
internal static class CmapReader
{
    private const int HeaderSize = 4;
    private const int RecordSize = 8;

    /// <summary>
    /// Finds the first encoding record for a platform and encoding. Its subtable runs from its offset to the end of the table.
    /// </summary>
    /// <returns><see langword="false"/> if there is no such record, its offset is outside the table, or its format is not supported.</returns>
    public static bool TryGetSubtable(ReadOnlySpan<byte> cmap, int platformId, int encodingId, out ReadOnlySpan<byte> subtable)
    {
        subtable = default;
        int recordCount = ReadUInt16(cmap, 2);
        for (int i = 0; i < recordCount; i++)
        {
            int record = HeaderSize + (i * RecordSize);
            if (record + RecordSize > cmap.Length)
            {
                return false;
            }

            if (ReadUInt16(cmap, record) != platformId || ReadUInt16(cmap, record + 2) != encodingId)
            {
                continue;
            }

            uint offset = BinaryPrimitives.ReadUInt32BigEndian(cmap.Slice(record + 4));
            if (offset >= (uint)cmap.Length)
            {
                return false;
            }

            subtable = cmap.Slice((int)offset);
            return ReadUInt16(subtable, 0) is 0 or 2 or 4 or 6 or 10 or 12 or 13;
        }

        return false;
    }

    /// <summary>
    /// Maps a code through a subtable that <see cref="TryGetSubtable"/> returned.
    /// </summary>
    /// <returns><see langword="false"/> if the code has no glyph, or the glyph is 0 or past the glyph count.</returns>
    public static bool TryMap(ReadOnlySpan<byte> subtable, int code, int glyphCount, out ushort glyphId)
    {
        glyphId = 0;
        if (code < 0)
        {
            return false;
        }

        long glyph = ReadUInt16(subtable, 0) switch
        {
            0 => Format0(subtable, code),
            2 => Format2(subtable, code),
            4 => Format4(subtable, code),
            6 => Format6(subtable, code),
            10 => Format10(subtable, code),
            12 => Format12(subtable, code, constant: false),
            13 => Format12(subtable, code, constant: true),
            _ => 0,
        };

        if (glyph <= 0 || glyph >= glyphCount || glyph > ushort.MaxValue)
        {
            return false;
        }

        glyphId = (ushort)glyph;
        return true;
    }

    // A byte for each of 256 codes.
    private static long Format0(ReadOnlySpan<byte> subtable, int code)
        => code < 256 && 6 + code < subtable.Length ? subtable[6 + code] : 0;

    // Mixed one and two byte codes. 256 sub-header keys, then 8-byte sub-headers.
    private static long Format2(ReadOnlySpan<byte> subtable, int code)
    {
        if (code > 0xFFFF)
        {
            return 0;
        }

        int high = code >> 8;
        int low = code & 0xFF;
        int key = code < 256 ? ReadUInt16(subtable, 6 + (code * 2)) : ReadUInt16(subtable, 6 + (high * 2));

        // Key 0 is the sub-header for one-byte codes. A byte that starts a two-byte code has no one-byte glyph.
        if ((code < 256) != (key == 0))
        {
            return 0;
        }

        int header = 518 + key;
        int firstCode = ReadUInt16(subtable, header);
        int entryCount = ReadUInt16(subtable, header + 2);
        int idDelta = (short)ReadUInt16(subtable, header + 4);
        int rangeOffsetPosition = header + 6;
        int index = low - firstCode;
        if (index < 0 || index >= entryCount)
        {
            return 0;
        }

        int glyph = ReadUInt16(subtable, rangeOffsetPosition + ReadUInt16(subtable, rangeOffsetPosition) + (index * 2));
        return glyph == 0 ? 0 : (glyph + idDelta) & 0xFFFF;
    }

    // Segments of codes in four parallel arrays: endCode, a pad, startCode, idDelta, idRangeOffset.
    private static long Format4(ReadOnlySpan<byte> subtable, int code)
    {
        int segmentBytes = ReadUInt16(subtable, 6);
        if (code > 0xFFFF || segmentBytes == 0 || (segmentBytes & 1) != 0 || 16 + (4L * segmentBytes) > subtable.Length)
        {
            return 0;
        }

        // The first segment whose end code is at least the code. End codes are sorted.
        int low = 0;
        int high = (segmentBytes / 2) - 1;
        while (low < high)
        {
            int middle = (low + high) >> 1;
            if (ReadUInt16(subtable, 14 + (middle * 2)) < code)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        int segment = low * 2;
        if (ReadUInt16(subtable, 14 + segment) < code)
        {
            return 0;
        }

        int start = ReadUInt16(subtable, 16 + segmentBytes + segment);
        if (start > code)
        {
            return 0;
        }

        int delta = ReadUInt16(subtable, 16 + (2 * segmentBytes) + segment);
        int rangeOffsetPosition = 16 + (3 * segmentBytes) + segment;
        int rangeOffset = ReadUInt16(subtable, rangeOffsetPosition);
        if (rangeOffset == 0)
        {
            return (code + delta) & 0xFFFF;
        }

        // The offset is from its own position to the glyph ID of the first code of the segment.
        int glyph = ReadUInt16(subtable, rangeOffsetPosition + rangeOffset + ((code - start) * 2));
        return glyph == 0 ? 0 : (glyph + delta) & 0xFFFF;
    }

    // A range of codes: firstCode, entryCount, then a glyph ID for each code.
    private static long Format6(ReadOnlySpan<byte> subtable, int code)
    {
        int index = code - ReadUInt16(subtable, 6);
        return index >= 0 && index < ReadUInt16(subtable, 8) ? ReadUInt16(subtable, 10 + (index * 2)) : 0;
    }

    // 32-bit codes in one range: startCharCode, numChars, then a glyph ID for each code.
    private static long Format10(ReadOnlySpan<byte> subtable, int code)
    {
        long index = code - ReadUInt32(subtable, 12);
        return index >= 0 && index < ReadUInt32(subtable, 16) ? ReadUInt16(subtable, 20 + (index * 2)) : 0;
    }

    // Groups of 32-bit codes, sorted by start: start, end, glyph of start. Format 13 gives every code of a group the same glyph.
    private static long Format12(ReadOnlySpan<byte> subtable, int code, bool constant)
    {
        long groupCount = ReadUInt32(subtable, 12);
        if (groupCount == 0 || 16 + (groupCount * 12) > subtable.Length)
        {
            return 0;
        }

        long low = 0;
        long high = groupCount - 1;
        while (low < high)
        {
            long middle = (low + high) >> 1;
            if (ReadUInt32(subtable, 16 + (middle * 12) + 4) < code)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        long group = 16 + (low * 12);
        long start = ReadUInt32(subtable, group);
        if (code < start || code > ReadUInt32(subtable, group + 4))
        {
            return 0;
        }

        long glyph = ReadUInt32(subtable, group + 8);
        return constant ? glyph : glyph + (code - start);
    }

    private static int ReadUInt16(ReadOnlySpan<byte> data, long position)
        => position >= 0 && position + 2 <= data.Length
            ? BinaryPrimitives.ReadUInt16BigEndian(data.Slice((int)position))
            : 0;

    private static long ReadUInt32(ReadOnlySpan<byte> data, long position)
        => position >= 0 && position + 4 <= data.Length
            ? BinaryPrimitives.ReadUInt32BigEndian(data.Slice((int)position))
            : 0;
}
