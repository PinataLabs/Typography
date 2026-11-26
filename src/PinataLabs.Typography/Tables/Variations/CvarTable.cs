// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'cvar' table: deltas for the values of the Control Value Table at the tuples of a variable TrueType font, so that
/// the hinting instructions of an instance see the values that suit its weight and width.
/// https://learn.microsoft.com/typography/opentype/spec/cvar
/// </summary>
/// <remarks>
/// A table that is malformed, or that has the wrong version, counts as absent, and so does one that has data that cannot be
/// read for an instance: the instance then uses the values of the 'cvt ' table.
/// </remarks>
internal sealed class CvarTable : Table
{
    internal const string TableName = "cvar";

    // The major and minor version come before the tuple variation count and the offset of the data.
    private const int HeaderSize = 8;
    private const int TupleVariationStorePosition = 4;

    private readonly ReadOnlyMemory<byte> data;
    private readonly int axisCount;

    private CvarTable(ReadOnlyMemory<byte> data, int axisCount)
    {
        this.data = data;
        this.axisCount = axisCount;
    }

    /// <summary>
    /// Loads the table.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none, it has no axes, or the table is malformed.</returns>
    public static CvarTable? Load(FontReader reader)
    {
        byte[]? data = reader.TryReadTableBytes(TableName);
        FvarTable? fvar = data is null ? null : reader.TryGetTable<FvarTable>();
        return data is null || fvar is null ? null : Parse(data, fvar.Axes.Count);
    }

    /// <summary>
    /// Reads the header of a 'cvar' table.
    /// </summary>
    /// <param name="data">The bytes of the table.</param>
    /// <param name="axisCount">The number of axes of the font.</param>
    /// <returns>The table, or <see langword="null"/> when the header is malformed.</returns>
    public static CvarTable? Parse(ReadOnlyMemory<byte> data, int axisCount)
    {
        ReadOnlySpan<byte> span = data.Span;
        return span.Length >= HeaderSize
            && BinaryPrimitives.ReadUInt16BigEndian(span) == 1
            && axisCount > 0
            && TupleVariationStore.TryRead(data, TupleVariationStorePosition, axisCount, default, out _)
            ? new CvarTable(data, axisCount)
            : null;
    }

    /// <summary>
    /// Gives the control values of an instance, which are the values of the 'cvt ' table with the deltas added.
    /// </summary>
    /// <param name="controlValues">The values of the 'cvt ' table.</param>
    /// <param name="coordinates">The F2Dot14 coordinates of the instance, in <c>fvar</c> order.</param>
    /// <returns>The values of the instance, or <see langword="null"/> when the data for the instance is malformed.</returns>
    public short[]? CreateControlValues(short[] controlValues, ReadOnlySpan<short> coordinates)
    {
        if (!TupleVariationStore.TryRead(this.data, TupleVariationStorePosition, this.axisCount, default, out TupleVariationStore store))
        {
            return null;
        }

        // The deltas of every tuple are summed before the value rounds, as the specification says for the control values.
        double[] sums = new double[controlValues.Length];
        TupleVariationStore.Reader reader = store.CreateReader(coordinates, controlValues.Length, hasY: false);
        while (reader.TryRead(out TupleVariation variation))
        {
            for (int i = 0; i < variation.DeltaX.Length; i++)
            {
                int index = variation.Points is null ? i : variation.Points[i];

                // A delta for a value that the table does not have is ignored.
                if (index < sums.Length)
                {
                    sums[index] += variation.Scalar * (double)variation.DeltaX[i];
                }
            }
        }

        if (reader.Failed)
        {
            return null;
        }

        short[] result = new short[controlValues.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (short)Math.Clamp(controlValues[i] + Math.Floor(sums[i] + 0.5), short.MinValue, short.MaxValue);
        }

        return result;
    }
}
