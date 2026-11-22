// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Builds the smallest TrueType file that <see cref="FontProgram"/> loads: 'head', 'maxp', 'loca' and 'glyf',
/// and a 'post' table when one is given. Glyphs are raw 'glyf' records.
/// </summary>
internal static class TrueTypeBuilder
{
    public static byte[] Build(IReadOnlyList<byte[]> glyphs, byte[]? post = null)
    {
        byte[] head = new byte[54];
        BinaryPrimitives.WriteUInt16BigEndian(head.AsSpan(18), 1000);
        BinaryPrimitives.WriteInt16BigEndian(head.AsSpan(50), 1);

        byte[] maxp = new byte[6];
        BinaryPrimitives.WriteUInt32BigEndian(maxp, 0x00005000);
        BinaryPrimitives.WriteUInt16BigEndian(maxp.AsSpan(4), checked((ushort)glyphs.Count));

        MemoryStream glyf = new();
        byte[] loca = new byte[(glyphs.Count + 1) * 4];
        for (int i = 0; i < glyphs.Count; i++)
        {
            glyf.Write(glyphs[i]);
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan((i + 1) * 4), (uint)glyf.Length);
        }

        List<(string Tag, byte[] Data)> tables = [("glyf", glyf.ToArray()), ("head", head), ("loca", loca), ("maxp", maxp)];
        if (post is not null)
        {
            tables.Add(("post", post));
        }

        return Assemble(tables);
    }

    /// <summary>
    /// Builds a format 2 'post' table.
    /// </summary>
    /// <param name="nameIndices">The name index of each glyph.</param>
    /// <param name="strings">The strings, which name indices from 258 select in order.</param>
    public static byte[] Post(IReadOnlyList<ushort> nameIndices, IEnumerable<string> strings)
    {
        MemoryStream post = new();
        byte[] header = new byte[34];
        BinaryPrimitives.WriteUInt32BigEndian(header, 0x00020000);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(32), checked((ushort)nameIndices.Count));
        post.Write(header);
        foreach (ushort index in nameIndices)
        {
            post.WriteByte((byte)(index >> 8));
            post.WriteByte((byte)index);
        }

        foreach (string value in strings)
        {
            post.WriteByte(checked((byte)value.Length));
            post.Write(Encoding.ASCII.GetBytes(value));
        }

        return post.ToArray();
    }

    private static byte[] Assemble(List<(string Tag, byte[] Data)> tables)
    {
        MemoryStream file = new();
        byte[] header = new byte[12 + (16 * tables.Count)];
        BinaryPrimitives.WriteUInt32BigEndian(header, 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)tables.Count);
        file.Write(header);

        for (int i = 0; i < tables.Count; i++)
        {
            Span<byte> record = header.AsSpan(12 + (16 * i), 16);
            Encoding.ASCII.GetBytes(tables[i].Tag, record);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(8), (uint)file.Length);
            BinaryPrimitives.WriteUInt32BigEndian(record.Slice(12), (uint)tables[i].Data.Length);
            file.Write(tables[i].Data);
            while (file.Length % 4 != 0)
            {
                file.WriteByte(0);
            }
        }

        byte[] data = file.ToArray();
        header.CopyTo(data, 0);
        return data;
    }
}
