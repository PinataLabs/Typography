// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Rewrites the table directory of an sfnt file with tables removed or replaced, the way a PDF producer
/// strips a subset. It copies every other table byte for byte.
/// </summary>
internal static class SfntRewriter
{
    private const int HeaderSize = 12;
    private const int RecordSize = 16;

    public static byte[] Remove(byte[] font, params string[] tags)
        => Rewrite(font, tags.ToDictionary(tag => tag, _ => (byte[]?)null));

    /// <summary>
    /// Replaces the contents of a table. A table that the font does not have is added.
    /// </summary>
    public static byte[] Replace(byte[] font, string tag, byte[] contents)
        => Rewrite(font, new Dictionary<string, byte[]?> { [tag] = contents });

    /// <summary>
    /// Returns the contents of a table, or null when the font has none.
    /// </summary>
    public static byte[]? GetTable(byte[] font, string tag)
    {
        foreach ((string recordTag, byte[] contents) in ReadTables(font))
        {
            if (recordTag == tag)
            {
                return contents;
            }
        }

        return null;
    }

    /// <summary>
    /// Replaces the contents of tables, or removes a table when its value is null.
    /// </summary>
    public static byte[] Rewrite(byte[] font, IReadOnlyDictionary<string, byte[]?> changes)
    {
        SortedDictionary<string, byte[]> tables = new(StringComparer.Ordinal);
        foreach ((string tag, byte[] contents) in ReadTables(font))
        {
            tables[tag] = contents;
        }

        foreach ((string tag, byte[]? contents) in changes)
        {
            if (contents is null)
            {
                tables.Remove(tag);
            }
            else
            {
                tables[tag] = contents;
            }
        }

        int tableCount = tables.Count;
        int position = HeaderSize + (tableCount * RecordSize);
        byte[] output = new byte[position + tables.Sum(t => (t.Value.Length + 3) & ~3)];
        font.AsSpan(0, 4).CopyTo(output);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)tableCount);
        int entrySelector = (int)Math.Log2(Math.Max(1, tableCount));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6), (ushort)((1 << entrySelector) * 16));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8), (ushort)entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10), (ushort)((tableCount * 16) - ((1 << entrySelector) * 16)));

        int index = 0;
        foreach ((string tag, byte[] contents) in tables)
        {
            Span<byte> record = output.AsSpan(HeaderSize + (index++ * RecordSize), RecordSize);
            Encoding.ASCII.GetBytes(tag, record);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(8), (uint)position);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(12), (uint)contents.Length);
            contents.CopyTo(output, position);
            position += (contents.Length + 3) & ~3;
        }

        return output;
    }

    private static IEnumerable<(string Tag, byte[] Contents)> ReadTables(byte[] font)
    {
        int count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4));
        for (int i = 0; i < count; i++)
        {
            ReadOnlySpan<byte> record = font.AsSpan(HeaderSize + (i * RecordSize), RecordSize);
            string tag = Encoding.ASCII.GetString(record.Slice(0, 4));
            int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(8));
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(record.Slice(12));
            yield return (tag, font.AsSpan(offset, length).ToArray());
        }
    }
}
