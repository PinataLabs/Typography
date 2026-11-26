// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'avar' table: one segment map for each axis of the 'fvar' table, which maps a normalised coordinate to
/// another normalised coordinate, so that the weight of a variable font can change in steps that look even.
/// https://learn.microsoft.com/typography/opentype/spec/avar
/// </summary>
/// <remarks>
/// The table reads the segment maps of a version 1 table. A version 2 table has the same segment maps in front of its
/// extra data, which the table ignores. A table that is truncated or has segment maps that are not in increasing
/// order is malformed, and <see cref="Load(FontReader)"/> gives <see langword="null"/> for it, as if the font had none.
/// </remarks>
internal sealed class AvarTable : Table
{
    internal const string TableName = "avar";

    private const int HeaderSize = 8;
    private const int MapEntrySize = 4;
    private const double F2Dot14Scale = 16384D;

    // One map for each axis. A map holds the (from, to) pairs in increasing order of from.
    private readonly (double From, double To)[][] maps;

    private AvarTable((double From, double To)[][] maps) => this.maps = maps;

    /// <summary>
    /// Gets the number of axes that the table has segment maps for.
    /// </summary>
    public int AxisCount => this.maps.Length;

    /// <summary>
    /// Loads the table, which is the segment maps of 'avar'.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none or it is malformed.</returns>
    public static AvarTable? Load(FontReader reader)
    {
        byte[]? data = reader.TryReadTableBytes(TableName);
        return data is null ? null : Parse(data);
    }

    /// <summary>
    /// Reads the segment maps of an 'avar' table.
    /// </summary>
    /// <param name="data">The bytes of the table.</param>
    /// <returns>The table, or <see langword="null"/> when it is malformed.</returns>
    public static AvarTable? Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderSize)
        {
            return null;
        }

        ushort majorVersion = BinaryPrimitives.ReadUInt16BigEndian(data);
        int axisCount = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(6));
        if (majorVersion is not (1 or 2))
        {
            return null;
        }

        (double From, double To)[][] maps = new (double From, double To)[axisCount][];
        int position = HeaderSize;
        for (int axis = 0; axis < axisCount; axis++)
        {
            if (data.Length - position < 2)
            {
                return null;
            }

            int count = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(position));
            position += 2;
            if (data.Length - position < count * MapEntrySize)
            {
                return null;
            }

            List<(double From, double To)> map = new(count);
            for (int i = 0; i < count; i++, position += MapEntrySize)
            {
                double from = BinaryPrimitives.ReadInt16BigEndian(data.Slice(position)) / F2Dot14Scale;
                double to = BinaryPrimitives.ReadInt16BigEndian(data.Slice(position + 2)) / F2Dot14Scale;

                // The maps must not run backwards. When two entries start at the same value, the last one counts.
                if (map.Count > 0 && (from < map[^1].From || to < map[^1].To))
                {
                    return null;
                }

#pragma warning disable S1244 // A repeated from-value is an exact duplicate in the font data.
                if (map.Count > 0 && from == map[^1].From)
#pragma warning restore S1244
                {
                    map[^1] = (from, to);
                }
                else
                {
                    map.Add((from, to));
                }
            }

            // A map that has entries must pin the ends and the default of the axis, or the table is malformed.
            if (map.Count > 0 && (!map.Contains((-1D, -1D)) || !map.Contains((0D, 0D)) || !map.Contains((1D, 1D))))
            {
                return null;
            }

            maps[axis] = map.ToArray();
        }

        return new AvarTable(maps);
    }

    /// <summary>
    /// Maps a normalised coordinate of an axis, as fontTools <c>piecewiseLinearMap</c> does: a value on an entry gives
    /// its target, a value between two entries interpolates, and a value beyond the first or the last entry keeps its
    /// distance from it.
    /// </summary>
    /// <param name="axis">The index of the axis in the 'fvar' table.</param>
    /// <param name="value">The coordinate after normalisation, from -1 to 1.</param>
    /// <returns>The mapped coordinate.</returns>
    public double Map(int axis, double value)
    {
        (double From, double To)[] map = this.maps[axis];
        if (map.Length == 0)
        {
            return value;
        }

        (double From, double To) first = map[0];
        (double From, double To) last = map[^1];
        if (value < first.From)
        {
            return value + first.To - first.From;
        }

        if (value > last.From)
        {
            return value + last.To - last.From;
        }

        for (int i = 0; i < map.Length; i++)
        {
            (double From, double To) end = map[i];
#pragma warning disable S1244 // An exact segment end maps to its exact target.
            if (value == end.From)
#pragma warning restore S1244
            {
                return end.To;
            }

            if (value < end.From)
            {
                (double From, double To) start = map[i - 1];
                return start.To + ((end.To - start.To) * (value - start.From) / (end.From - start.From));
            }
        }

        return value;
    }
}
