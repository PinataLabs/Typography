// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Numerics;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'gvar' table: for each glyph of a TrueType font, the deltas of its points at the tuples of the font, which move the
/// points of the glyph away from its default outline. It sums the deltas for one instance and interpolates the points that
/// a tuple leaves out.
/// https://learn.microsoft.com/typography/opentype/spec/gvar
/// </summary>
/// <remarks>
/// A table whose header is malformed, or that has a different number of axes than 'fvar', counts as absent. A glyph whose
/// variation data is malformed is a glyph that <see cref="TryGetDeltas"/> cannot answer for, and the caller draws it at the
/// default instance.
/// </remarks>
internal sealed class GvarTable : Table
{
    internal const string TableName = "gvar";

    // Two versions, the axis count, the shared tuple count, the offset of the shared tuples, the glyph count, the flags
    // and the offset of the glyph variation data, which come before the array of offsets.
    private const int HeaderSize = 20;
    private const int LongOffsetsFlag = 1;

    private readonly ReadOnlyMemory<byte> data;
    private readonly ReadOnlyMemory<byte> sharedTuples;
    private readonly int axisCount;
    private readonly int glyphCount;
    private readonly bool longOffsets;
    private readonly uint glyphDataOffset;

    private GvarTable(ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> sharedTuples, int axisCount, int glyphCount, bool longOffsets, uint glyphDataOffset)
    {
        this.data = data;
        this.sharedTuples = sharedTuples;
        this.axisCount = axisCount;
        this.glyphCount = glyphCount;
        this.longOffsets = longOffsets;
        this.glyphDataOffset = glyphDataOffset;
    }

    /// <summary>
    /// Loads the table.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none, it has no axes, or the table is malformed.</returns>
    public static GvarTable? Load(FontReader reader)
    {
        byte[]? data = reader.TryReadTableBytes(TableName);
        FvarTable? fvar = data is null ? null : reader.TryGetTable<FvarTable>();
        return data is null || fvar is null ? null : Parse(data, fvar.Axes.Count);
    }

    /// <summary>
    /// Reads the header of a 'gvar' table.
    /// </summary>
    /// <param name="data">The bytes of the table.</param>
    /// <param name="axisCount">The number of axes of the font.</param>
    /// <returns>The table, or <see langword="null"/> when its header is malformed or it has another number of axes.</returns>
    public static GvarTable? Parse(ReadOnlyMemory<byte> data, int axisCount)
    {
        ReadOnlySpan<byte> span = data.Span;
        if (span.Length < HeaderSize || BinaryPrimitives.ReadUInt16BigEndian(span) != 1 || BinaryPrimitives.ReadUInt16BigEndian(span.Slice(4)) != axisCount)
        {
            return null;
        }

        int sharedTupleCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6));
        uint sharedTuplesOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(8));
        int glyphCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(12));
        bool longOffsets = (BinaryPrimitives.ReadUInt16BigEndian(span.Slice(14)) & LongOffsetsFlag) != 0;
        uint glyphDataOffset = BinaryPrimitives.ReadUInt32BigEndian(span.Slice(16));

        long sharedTuplesSize = (long)sharedTupleCount * axisCount * 2;
        long offsetsSize = (glyphCount + 1L) * (longOffsets ? 4 : 2);
        if (sharedTuplesOffset + sharedTuplesSize > span.Length || HeaderSize + offsetsSize > span.Length || glyphDataOffset > span.Length)
        {
            return null;
        }

        return new GvarTable(data, data.Slice((int)sharedTuplesOffset, (int)sharedTuplesSize), axisCount, glyphCount, longOffsets, glyphDataOffset);
    }

    /// <summary>
    /// Sums the deltas of a glyph for an instance, with the points that its tuples leave out interpolated.
    /// </summary>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="coordinates">The F2Dot14 coordinates of the instance, in <c>fvar</c> order.</param>
    /// <param name="original">The points of the glyph before variation, followed by its four phantom points. A composite
    /// glyph has one point for each component, which is its offset.</param>
    /// <param name="endPoints">The number of the last point of each contour. A composite glyph has none.</param>
    /// <param name="deltas">Receives a delta for each of the points, as long as <paramref name="original"/>.</param>
    /// <returns><see langword="false"/> if the variation data of the glyph is malformed. A glyph that the table has no data for has the delta 0 for every point.</returns>
    public bool TryGetDeltas(int glyphId, ReadOnlySpan<short> coordinates, ReadOnlySpan<Vector2> original, ReadOnlySpan<ushort> endPoints, Span<Vector2> deltas)
    {
        deltas.Clear();
        if (!this.TryGetStore(glyphId, out TupleVariationStore store, out bool hasData))
        {
            return false;
        }

        if (!hasData)
        {
            return true;
        }

        int pointCount = original.Length;

        // Every tuple with deltas for some of the points interpolates in a copy of its own, and the instance takes a scalar of it.
        Vector2[] tupleDeltas = new Vector2[pointCount];
        bool[] touched = new bool[pointCount];
        TupleVariationStore.Reader reader = store.CreateReader(coordinates, pointCount, hasY: true);
        while (reader.TryRead(out TupleVariation variation))
        {
            if (variation.Points is null)
            {
                for (int i = 0; i < pointCount; i++)
                {
                    tupleDeltas[i] = new Vector2(variation.DeltaX[i], variation.DeltaY![i]);
                }
            }
            else
            {
                Array.Clear(tupleDeltas, 0, pointCount);
                Array.Clear(touched, 0, pointCount);
                for (int i = 0; i < variation.Points.Length; i++)
                {
                    int point = variation.Points[i];
                    if (point < pointCount)
                    {
                        tupleDeltas[point] = new Vector2(variation.DeltaX[i], variation.DeltaY![i]);
                        touched[point] = true;
                    }
                }

                UntouchedPointInterpolation.Apply(original, endPoints, touched, tupleDeltas);
            }

            for (int i = 0; i < pointCount; i++)
            {
                deltas[i] += variation.Scalar * tupleDeltas[i];
            }
        }

        if (reader.Failed)
        {
            deltas.Clear();
            return false;
        }

        return true;
    }

    // A glyph that the table has no data for has none of its own, which is not the same as data that is malformed.
    private bool TryGetStore(int glyphId, out TupleVariationStore store, out bool hasData)
    {
        store = default;
        hasData = false;
        if ((uint)glyphId >= (uint)this.glyphCount)
        {
            return true;
        }

        ReadOnlySpan<byte> offsets = this.data.Span.Slice(HeaderSize);
        long start;
        long end;
        if (this.longOffsets)
        {
            start = BinaryPrimitives.ReadUInt32BigEndian(offsets.Slice(glyphId * 4));
            end = BinaryPrimitives.ReadUInt32BigEndian(offsets.Slice((glyphId + 1) * 4));
        }
        else
        {
            start = BinaryPrimitives.ReadUInt16BigEndian(offsets.Slice(glyphId * 2)) * 2L;
            end = BinaryPrimitives.ReadUInt16BigEndian(offsets.Slice((glyphId + 1) * 2)) * 2L;
        }

        if (end < start || this.glyphDataOffset + end > this.data.Length)
        {
            return false;
        }

        if (end == start)
        {
            return true;
        }

        hasData = true;
        return TupleVariationStore.TryRead(this.data.Slice((int)(this.glyphDataOffset + start), (int)(end - start)), 0, this.axisCount, this.sharedTuples, out store);
    }
}
