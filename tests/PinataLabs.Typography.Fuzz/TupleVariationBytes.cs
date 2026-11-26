// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Writes the binary layouts of the tuple variation stores of 'gvar' and 'cvar', so a test can build exactly the data
/// it needs.
/// </summary>
internal static class TupleVariationBytes
{
    private const int EmbeddedPeak = 0x8000;
    private const int Intermediate = 0x4000;
    private const int PrivatePoints = 0x2000;
    private const int SharedPointNumbers = 0x8000;

    /// <summary>
    /// One tuple to write. It has its peak in its header, or the index of a shared tuple. A tuple with start and end tuples has an
    /// intermediate region. A tuple with point numbers has its own, and the other tuples use the shared ones, or take every point.
    /// </summary>
    internal sealed record TupleData(
        short[]? Peak,
        short[]? Start,
        short[]? End,
        int[]? Points,
        int[] DeltaX,
        int[]? DeltaY,
        int SharedIndex = 0);

    /// <summary>
    /// Writes the variation data of one glyph of 'gvar'.
    /// </summary>
    /// <param name="sharedPoints">The point numbers that tuples with none of their own use. A <see langword="null"/> value writes none.</param>
    /// <param name="tuples">The tuples.</param>
    public static byte[] Glyph(int[]? sharedPoints, params TupleData[] tuples)
        => Write(0, sharedPoints, tuples);

    /// <summary>
    /// Writes a 'cvar' table: a version, then the tuple variation data, whose offsets count from the start of the table.
    /// </summary>
    /// <param name="sharedPoints">The point numbers that tuples with none of their own use. A <see langword="null"/> value writes none.</param>
    /// <param name="tuples">The tuples, each with an embedded peak.</param>
    public static byte[] Cvar(int[]? sharedPoints, params TupleData[] tuples)
        => Write(4, sharedPoints, tuples);

    /// <summary>
    /// Writes a 'gvar' table.
    /// </summary>
    /// <param name="axisCount">The number of axes of the font.</param>
    /// <param name="sharedTuples">The shared peak tuples.</param>
    /// <param name="glyphs">The variation data of each glyph, which is empty for a glyph that has none.</param>
    /// <param name="longOffsets">Whether the offsets are 32 bits.</param>
    public static byte[] Gvar(int axisCount, short[][] sharedTuples, byte[][] glyphs, bool longOffsets = false)
    {
        List<byte> bytes = [];
        int offsetSize = longOffsets ? 4 : 2;
        int sharedOffset = 20 + ((glyphs.Length + 1) * offsetSize);
        int sharedSize = sharedTuples.Length * axisCount * 2;
        int dataOffset = sharedOffset + sharedSize;

        UInt16(bytes, 1);
        UInt16(bytes, 0);
        UInt16(bytes, (ushort)axisCount);
        UInt16(bytes, (ushort)sharedTuples.Length);
        UInt32(bytes, (uint)sharedOffset);
        UInt16(bytes, (ushort)glyphs.Length);
        UInt16(bytes, (ushort)(longOffsets ? 1 : 0));
        UInt32(bytes, (uint)dataOffset);

        // A short offset counts in words, so the data of each glyph is padded to an even size.
        byte[][] padded = [.. glyphs.Select(glyph => glyph.Length % 2 == 0 || longOffsets ? glyph : [.. glyph, 0])];
        int position = 0;
        for (int i = 0; i <= glyphs.Length; i++)
        {
            if (longOffsets)
            {
                UInt32(bytes, (uint)position);
            }
            else
            {
                UInt16(bytes, (ushort)(position / 2));
            }

            position += i < glyphs.Length ? padded[i].Length : 0;
        }

        foreach (short[] tuple in sharedTuples)
        {
            foreach (short coordinate in tuple)
            {
                UInt16(bytes, (ushort)coordinate);
            }
        }

        foreach (byte[] glyph in padded)
        {
            bytes.AddRange(glyph);
        }

        return [.. bytes];
    }

    /// <summary>
    /// Writes point numbers as the data of a tuple does.
    /// </summary>
    /// <param name="points">The numbers, in increasing order. An empty array means every point.</param>
    public static byte[] PointNumbers(int[] points)
    {
        List<byte> bytes = [];
        WritePointNumbers(bytes, points);
        return [.. bytes];
    }

    /// <summary>
    /// Writes deltas as the data of a tuple does, in runs of zeros, bytes and words.
    /// </summary>
    public static byte[] Deltas(int[] deltas)
    {
        List<byte> bytes = [];
        WriteDeltas(bytes, deltas);
        return [.. bytes];
    }

    private static byte[] Write(int prefix, int[]? sharedPoints, TupleData[] tuples)
    {
        List<byte> serialized = [];
        if (sharedPoints is not null)
        {
            WritePointNumbers(serialized, sharedPoints);
        }

        List<byte> headers = [];
        foreach (TupleData tuple in tuples)
        {
            List<byte> data = [];
            if (tuple.Points is not null)
            {
                WritePointNumbers(data, tuple.Points);
            }

            WriteDeltas(data, tuple.DeltaX);
            if (tuple.DeltaY is not null)
            {
                WriteDeltas(data, tuple.DeltaY);
            }

            int flags = (tuple.Peak is null ? tuple.SharedIndex : EmbeddedPeak)
                | (tuple.Start is null ? 0 : Intermediate)
                | (tuple.Points is null ? 0 : PrivatePoints);
            UInt16(headers, (ushort)data.Count);
            UInt16(headers, (ushort)flags);
            foreach (short coordinate in tuple.Peak ?? [])
            {
                UInt16(headers, (ushort)coordinate);
            }

            foreach (short coordinate in (tuple.Start ?? []).Concat(tuple.End ?? []))
            {
                UInt16(headers, (ushort)coordinate);
            }

            serialized.AddRange(data);
        }

        List<byte> bytes = [];
        for (int i = 0; i < prefix; i += 2)
        {
            UInt16(bytes, (ushort)(i == 0 ? 1 : 0));
        }

        UInt16(bytes, (ushort)(tuples.Length | (sharedPoints is null ? 0 : SharedPointNumbers)));
        UInt16(bytes, (ushort)(prefix + 4 + headers.Count));
        bytes.AddRange(headers);
        bytes.AddRange(serialized);
        return [.. bytes];
    }

    private static void WritePointNumbers(List<byte> bytes, int[] points)
    {
        if (points.Length < 0x80)
        {
            bytes.Add((byte)points.Length);
        }
        else
        {
            bytes.Add((byte)(0x80 | (points.Length >> 8)));
            bytes.Add((byte)points.Length);
        }

        int previous = 0;
        for (int start = 0; start < points.Length; start += 0x80)
        {
            int[] run = points.Skip(start).Take(0x80).ToArray();
            int[] distances = new int[run.Length];
            for (int i = 0; i < run.Length; i++)
            {
                distances[i] = run[i] - previous;
                previous = run[i];
            }

            bool words = distances.Any(distance => distance > byte.MaxValue);
            bytes.Add((byte)((words ? 0x80 : 0) | (run.Length - 1)));
            foreach (int distance in distances)
            {
                if (words)
                {
                    UInt16(bytes, (ushort)distance);
                }
                else
                {
                    bytes.Add((byte)distance);
                }
            }
        }
    }

    private static void WriteDeltas(List<byte> bytes, int[] deltas)
    {
        for (int start = 0; start < deltas.Length; start += 0x40)
        {
            int[] run = deltas.Skip(start).Take(0x40).ToArray();
            if (run.All(delta => delta == 0))
            {
                bytes.Add((byte)(0x80 | (run.Length - 1)));
            }
            else if (run.All(delta => delta is >= sbyte.MinValue and <= sbyte.MaxValue))
            {
                bytes.Add((byte)(run.Length - 1));
                bytes.AddRange(run.Select(delta => (byte)(sbyte)delta));
            }
            else
            {
                bytes.Add((byte)(0x40 | (run.Length - 1)));
                foreach (int delta in run)
                {
                    UInt16(bytes, (ushort)delta);
                }
            }
        }
    }

    private static void UInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }

    private static void UInt32(List<byte> bytes, uint value)
    {
        UInt16(bytes, (ushort)(value >> 16));
        UInt16(bytes, (ushort)value);
    }
}
