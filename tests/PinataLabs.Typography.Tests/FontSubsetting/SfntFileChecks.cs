// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace PinataLabs.Typography.Tests.FontSubsetting;

/// <summary>
/// Checks the structure of an sfnt file as OpenType requires it, apart from the loader of the library: the table directory, the
/// checksum of each table, and the checksum of the file that 'head' balances.
/// </summary>
internal static class SfntFileChecks
{
    /// <summary>
    /// Gets the tags of the tables of an sfnt file, in the order of its directory.
    /// </summary>
    public static List<string> GetTags(byte[] font)
    {
        List<string> tags = [];
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            tags.Add(Encoding.ASCII.GetString(font, 12 + (i * 16), 4));
        }

        return tags;
    }

    /// <summary>
    /// Describes every rule of the file structure that the font breaks.
    /// </summary>
    public static List<string> Verify(byte[] font)
    {
        List<string> problems = [];
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        if (font.Length % 4 != 0)
        {
            problems.Add($"the file is {font.Length} bytes, which is not a multiple of 4");
        }

        int searchRange = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(6));
        int entrySelector = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(8));
        int rangeShift = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(10));
        if (searchRange != 16 * (1 << entrySelector) || (1 << entrySelector) > count || (2 << entrySelector) <= count || rangeShift != (16 * count) - searchRange)
        {
            problems.Add($"the search fields {searchRange}, {entrySelector} and {rangeShift} do not fit {count} tables");
        }

        string previous = string.Empty;
        int headOffset = -1;
        for (int i = 0; i < count; i++)
        {
            int record = 12 + (i * 16);
            string tag = Encoding.ASCII.GetString(font, record, 4);
            uint checksum = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 4));
            long offset = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 8));
            long length = BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + 12));
            if (string.CompareOrdinal(tag, previous) <= 0)
            {
                problems.Add($"'{tag}' is not after '{previous}' in the directory");
            }

            previous = tag;
            if (offset % 4 != 0 || offset + length > font.Length)
            {
                problems.Add($"'{tag}' at {offset}, {length} bytes, is not aligned or is past the end of the file");
                continue;
            }

            byte[] contents = new byte[(length + 3) & ~3L];
            Array.Copy(font, offset, contents, 0, length);
            if (tag == "head")
            {
                headOffset = (int)offset;
                contents.AsSpan(8, 4).Clear();
            }

            if (Sum(contents) != checksum)
            {
                problems.Add($"the checksum of '{tag}' is {checksum:X8}, and the data gives {Sum(contents):X8}");
            }
        }

        if (headOffset >= 0 && Sum(font) != 0xB1B0AFBA)
        {
            problems.Add($"the checksum of the file is {Sum(font):X8}, and 'head' should make it B1B0AFBA");
        }

        return problems;
    }

    private static uint Sum(byte[] data)
    {
        uint sum = 0;
        for (int i = 0; i + 3 < data.Length; i += 4)
        {
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(i));
        }

        return sum;
    }
}
