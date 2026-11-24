// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables.TrueType.Glyphs;

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
    /// Builds the glyph table of text layout from raw 'glyf' records.
    /// </summary>
    public static GlyphTable GlyphTable(params byte[][] glyphs)
    {
        MemoryStream glyf = new();
        byte[] loca = new byte[(glyphs.Length + 1) * 4];
        for (int i = 0; i < glyphs.Length; i++)
        {
            glyf.Write(glyphs[i]);
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan((i + 1) * 4), (uint)glyf.Length);
        }

        return new GlyphTable(new TrueTypeGlyphData(loca, glyf.ToArray(), longOffsets: true, glyphs.Length));
    }

    /// <summary>
    /// Builds the 'glyf' record of a simple glyph without instructions. Its bounding box is the box of its points.
    /// </summary>
    public static byte[] SimpleGlyph(params (short X, short Y, bool OnCurve)[][] contours)
        => SimpleGlyph([], contours);

    /// <summary>
    /// Builds the 'glyf' record of a simple glyph. Its bounding box is the box of its points.
    /// </summary>
    public static byte[] SimpleGlyph(byte[] instructions, params (short X, short Y, bool OnCurve)[][] contours)
    {
        (short X, short Y, bool OnCurve)[] points = [.. contours.SelectMany(x => x)];
        List<byte> glyph = [];
        AddInt16(glyph, contours.Length);
        AddInt16(glyph, points.Min(p => p.X));
        AddInt16(glyph, points.Min(p => p.Y));
        AddInt16(glyph, points.Max(p => p.X));
        AddInt16(glyph, points.Max(p => p.Y));

        int end = -1;
        foreach ((short X, short Y, bool OnCurve)[] contour in contours)
        {
            end += contour.Length;
            AddInt16(glyph, end);
        }

        AddInt16(glyph, instructions.Length);
        glyph.AddRange(instructions);
        glyph.AddRange(points.Select(p => (byte)(p.OnCurve ? 1 : 0)));
        for (int i = 0; i < points.Length; i++)
        {
            AddInt16(glyph, points[i].X - (i == 0 ? 0 : points[i - 1].X));
        }

        for (int i = 0; i < points.Length; i++)
        {
            AddInt16(glyph, points[i].Y - (i == 0 ? 0 : points[i - 1].Y));
        }

        return [.. glyph];
    }

    /// <summary>
    /// Builds one component record of a composite glyph.
    /// </summary>
    /// <param name="glyphId">The glyph of the component.</param>
    /// <param name="flags">The flags. The flags for the transform and for more components are added.</param>
    /// <param name="argument1">The x offset, or the number of a point of the composite.</param>
    /// <param name="argument2">The y offset, or the number of a point of the component.</param>
    /// <param name="transform">Nothing, one scale, an x and a y scale, or a two-by-two matrix.</param>
    public static byte[] Component(ushort glyphId, int flags, int argument1, int argument2, params float[] transform)
    {
        flags |= transform.Length switch { 0 => 0, 1 => 0x0008, 2 => 0x0040, 4 => 0x0080, _ => throw new ArgumentException("Give 0, 1, 2 or 4 values.", nameof(transform)) };
        List<byte> component = [];
        AddInt16(component, flags);
        AddInt16(component, glyphId);
        if ((flags & 0x0001) != 0)
        {
            AddInt16(component, argument1);
            AddInt16(component, argument2);
        }
        else
        {
            component.Add((byte)argument1);
            component.Add((byte)argument2);
        }

        foreach (float value in transform)
        {
            AddInt16(component, (int)MathF.Round(value * 16384));
        }

        return [.. component];
    }

    /// <summary>
    /// Builds the 'glyf' record of a composite glyph from its component records.
    /// </summary>
    public static byte[] CompositeGlyphWithInstructions(byte[] instructions, params byte[][] components)
    {
        List<byte> glyph = [0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0];
        for (int i = 0; i < components.Length; i++)
        {
            byte[] component = [.. components[i]];
            bool last = i == components.Length - 1;
            component[1] |= (byte)(last ? 0 : 0x20);
            component[0] |= (byte)(last && instructions.Length > 0 ? 0x01 : 0);
            glyph.AddRange(component);
        }

        if (instructions.Length > 0)
        {
            AddInt16(glyph, instructions.Length);
            glyph.AddRange(instructions);
        }

        return [.. glyph];
    }

    public static byte[] CompositeGlyph(params byte[][] components)
        => CompositeGlyphWithInstructions([], components);

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

    private static void AddInt16(List<byte> data, int value)
    {
        data.Add((byte)(value >> 8));
        data.Add((byte)value);
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
