// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// A tuple variation store as a view over the bytes: the variation data of one glyph in 'gvar', or the data of 'cvar'.
/// Both use the layout of the common table formats:
/// https://learn.microsoft.com/typography/opentype/spec/otvarcommonformats#tuple-variation-store
/// The store checks its header and the headers of its tuples when it is read, and decodes the point numbers and the
/// packed deltas of the tuples that an instance uses when it is asked for them. A <see langword="default"/> store has no
/// tuples.
/// </summary>
internal readonly struct TupleVariationStore
{
    // The tuple variation count and the offset of the serialized data.
    private const int HeaderSize = 4;

    private const int SharedPointNumbersFlag = 0x8000;
    private const int CountMask = 0x0FFF;

    private const int EmbeddedPeakTupleFlag = 0x8000;
    private const int IntermediateRegionFlag = 0x4000;
    private const int PrivatePointNumbersFlag = 0x2000;
    private const int TupleIndexMask = 0x0FFF;

    private const int PointsAreWordsFlag = 0x80;
    private const int PointRunCountMask = 0x7F;

    private const int DeltasAreZeroFlag = 0x80;
    private const int DeltasAreWordsFlag = 0x40;
    private const int DeltaRunCountMask = 0x3F;

    // The data that every offset in the store counts from. For 'gvar' it is the data of the glyph, and for 'cvar'
    // it is the table, which has a version before the header.
    private readonly ReadOnlyMemory<byte> data;
    private readonly ReadOnlyMemory<byte> sharedTuples;
    private readonly int axisCount;
    private readonly int tupleCount;
    private readonly int headersPosition;
    private readonly int serializedPosition;
    private readonly bool hasSharedPointNumbers;

    private TupleVariationStore(
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte> sharedTuples,
        int axisCount,
        int tupleCount,
        int headersPosition,
        int serializedPosition,
        bool hasSharedPointNumbers)
    {
        this.data = data;
        this.sharedTuples = sharedTuples;
        this.axisCount = axisCount;
        this.tupleCount = tupleCount;
        this.headersPosition = headersPosition;
        this.serializedPosition = serializedPosition;
        this.hasSharedPointNumbers = hasSharedPointNumbers;
    }

    /// <summary>
    /// Gets the number of tuples.
    /// </summary>
    public int TupleCount => this.tupleCount;

    /// <summary>
    /// Reads the header of a store and the header of each tuple, and checks that the serialized data of the tuples lies
    /// within the data.
    /// </summary>
    /// <param name="data">The data that the offsets of the store count from.</param>
    /// <param name="position">The position of the tuple variation count in <paramref name="data"/>. It is 0 for the
    /// data of a glyph in 'gvar', and 4 for 'cvar', which has a version in front.</param>
    /// <param name="axisCount">The number of axes of the font, which is the number of coordinates of a peak tuple.</param>
    /// <param name="sharedTuples">The shared peak tuples of 'gvar' as F2Dot14 values, <paramref name="axisCount"/> for each
    /// tuple. 'cvar' has none, and every tuple of it holds its own peak.</param>
    /// <param name="store">The store.</param>
    /// <returns><see langword="false"/> if a header is truncated, or the data of the tuples does not fit.</returns>
    public static bool TryRead(ReadOnlyMemory<byte> data, int position, int axisCount, ReadOnlyMemory<byte> sharedTuples, out TupleVariationStore store)
    {
        store = default;
        ReadOnlySpan<byte> span = data.Span;
        FontDataReader reader = new(span, position);
        if (axisCount <= 0
            || !reader.TryReadUInt16(out ushort countField)
            || !reader.TryReadUInt16(out ushort dataOffset))
        {
            return false;
        }

        int tupleCount = countField & CountMask;
        int headersPosition = reader.Position;
        long dataSize = 0;
        for (int i = 0; i < tupleCount; i++)
        {
            if (!TryReadHeader(ref reader, axisCount, out TupleHeader header))
            {
                return false;
            }

            dataSize += header.DataSize;
        }

        // The tuples with no data and the tuples that have it all lie behind the headers.
        if (dataOffset < reader.Position || dataOffset + dataSize > span.Length)
        {
            return false;
        }

        store = new TupleVariationStore(data, sharedTuples, axisCount, tupleCount, headersPosition, dataOffset, (countField & SharedPointNumbersFlag) != 0);
        return true;
    }

    /// <summary>
    /// Starts reading the tuples that an instance uses: the tuples whose scalar is not 0. A tuple is decoded when the
    /// reader gives it, so a store holds no more memory than one tuple takes.
    /// </summary>
    /// <param name="coordinates">The F2Dot14 coordinates of the instance, in <c>fvar</c> order. An axis with no
    /// coordinate is at 0.</param>
    /// <param name="pointCount">The number of points that a tuple with no point numbers covers: the points of the glyph,
    /// with its four phantom points, or the control values of 'cvar'.</param>
    /// <param name="hasY">Whether a tuple has a second run of deltas, for the y coordinates of 'gvar'. 'cvar' has one.</param>
    /// <returns>The reader.</returns>
    public Reader CreateReader(ReadOnlySpan<short> coordinates, int pointCount, bool hasY)
        => new(this, coordinates, pointCount, hasY);

    // A header has the size of the tuple data, the flags with the shared tuple index, then the peak tuple and the
    // region start and end tuples when the flags say so. The tuples are F2Dot14 values, one for each axis.
    private static bool TryReadHeader(ref FontDataReader reader, int axisCount, out TupleHeader header)
    {
        header = default;
        if (!reader.TryReadUInt16(out ushort dataSize) || !reader.TryReadUInt16(out ushort flags))
        {
            return false;
        }

        int peakPosition = -1;
        int startPosition = -1;
        int tupleSize = axisCount * 2;
        if ((flags & EmbeddedPeakTupleFlag) != 0)
        {
            peakPosition = reader.Position;
            if (!reader.TrySkip(tupleSize))
            {
                return false;
            }
        }

        if ((flags & IntermediateRegionFlag) != 0)
        {
            startPosition = reader.Position;
            if (!reader.TrySkip(2 * tupleSize))
            {
                return false;
            }
        }

        header = new TupleHeader(dataSize, flags, peakPosition, startPosition);
        return true;
    }

    // Reads the point numbers of a tuple. A count of 0 means every point, which is null here. The numbers are runs of
    // bytes or words, and each is the distance from the one before it.
    private static bool TryReadPointNumbers(ref FontDataReader reader, int dataEnd, out ushort[]? points)
    {
        points = null;
        if (!reader.TryReadByte(out byte first))
        {
            return false;
        }

        if (first == 0)
        {
            return true;
        }

        int count = first;
        if (first >= 0x80)
        {
            if (!reader.TryReadByte(out byte second))
            {
                return false;
            }

            count = ((first & 0x7F) << 8) | second;
        }

        // Each number takes at least one byte, so a count above the bytes that are left is malformed, whatever it says.
        if (count > dataEnd - reader.Position)
        {
            return false;
        }

        ushort[] numbers = new ushort[count];
        int current = 0;
        for (int i = 0; i < count;)
        {
            if (!reader.TryReadByte(out byte control))
            {
                return false;
            }

            int run = (control & PointRunCountMask) + 1;
            if (run > count - i)
            {
                return false;
            }

            bool words = (control & PointsAreWordsFlag) != 0;
            for (int end = i + run; i < end; i++)
            {
                int distance;
                if (words)
                {
                    if (!reader.TryReadUInt16(out ushort word))
                    {
                        return false;
                    }

                    distance = word;
                }
                else
                {
                    if (!reader.TryReadByte(out byte value))
                    {
                        return false;
                    }

                    distance = value;
                }

                current += distance;
                if (current > ushort.MaxValue)
                {
                    return false;
                }

                numbers[i] = (ushort)current;
            }
        }

        points = numbers;
        return true;
    }

    // Reads the deltas, which are runs of zeros, bytes or words.
    private static bool TryReadDeltas(ref FontDataReader reader, short[] deltas)
    {
        for (int i = 0; i < deltas.Length;)
        {
            if (!reader.TryReadByte(out byte control))
            {
                return false;
            }

            int run = (control & DeltaRunCountMask) + 1;
            if (run > deltas.Length - i)
            {
                return false;
            }

            if ((control & DeltasAreZeroFlag) != 0)
            {
                i += run;
            }
            else if ((control & DeltasAreWordsFlag) != 0)
            {
                for (int end = i + run; i < end; i++)
                {
                    if (!reader.TryReadInt16(out deltas[i]))
                    {
                        return false;
                    }
                }
            }
            else
            {
                for (int end = i + run; i < end; i++)
                {
                    if (!reader.TryReadSByte(out sbyte value))
                    {
                        return false;
                    }

                    deltas[i] = value;
                }
            }
        }

        return true;
    }

    private static bool TryReadTuple(
        ref FontDataReader reader,
        int end,
        in TupleHeader header,
        ushort[]? sharedPoints,
        int pointCount,
        bool hasY,
        float scalar,
        out TupleVariation variation)
    {
        variation = default;
        ushort[]? points = sharedPoints;
        if ((header.Flags & PrivatePointNumbersFlag) != 0 && !TryReadPointNumbers(ref reader, end, out points))
        {
            return false;
        }

        int count = points?.Length ?? pointCount;
        short[] deltaX = new short[count];
        short[]? deltaY = hasY ? new short[count] : null;
        if (!TryReadDeltas(ref reader, deltaX) || (deltaY is not null && !TryReadDeltas(ref reader, deltaY)))
        {
            return false;
        }

        variation = new TupleVariation(scalar, points, deltaX, deltaY);
        return true;
    }

    // The scalar of a tuple is the product of the scalars of its axes. The peak is in the header or one of the shared
    // tuples, and a region without start and end tuples runs from 0 to the peak.
    private readonly bool TryGetScalar(ReadOnlySpan<byte> span, in TupleHeader header, ReadOnlySpan<short> coordinates, out float scalar)
    {
        scalar = 1;
        ReadOnlySpan<byte> peaks;
        if (header.PeakPosition >= 0)
        {
            peaks = span.Slice(header.PeakPosition, this.axisCount * 2);
        }
        else
        {
            int index = header.Flags & TupleIndexMask;
            if ((index + 1L) * this.axisCount * 2 > this.sharedTuples.Length)
            {
                return false;
            }

            peaks = this.sharedTuples.Span.Slice(index * this.axisCount * 2, this.axisCount * 2);
        }

        for (int axis = 0; axis < this.axisCount; axis++)
        {
            int peak = BinaryPrimitives.ReadInt16BigEndian(peaks.Slice(axis * 2));
            int start = Math.Min(peak, 0);
            int end = Math.Max(peak, 0);
            if (header.StartPosition >= 0)
            {
                start = BinaryPrimitives.ReadInt16BigEndian(span.Slice(header.StartPosition + (axis * 2)));
                end = BinaryPrimitives.ReadInt16BigEndian(span.Slice(header.StartPosition + (this.axisCount * 2) + (axis * 2)));
            }

            float axisScalar = VariationScalar.Get(start, peak, end, axis < coordinates.Length ? coordinates[axis] : 0);
            if (axisScalar == 0)
            {
                scalar = 0;
                return true;
            }

            scalar *= axisScalar;
        }

        return true;
    }

    private readonly record struct TupleHeader(int DataSize, int Flags, int PeakPosition, int StartPosition);

    /// <summary>
    /// Reads the tuples of a store that an instance uses, one at a time, in the order of the store.
    /// </summary>
    internal ref struct Reader
    {
        private readonly TupleVariationStore store;
        private readonly ReadOnlySpan<short> coordinates;
        private readonly int pointCount;
        private readonly bool hasY;
        private int index;
        private int headerPosition;
        private int dataPosition;
        private ushort[]? sharedPoints;

        internal Reader(TupleVariationStore store, ReadOnlySpan<short> coordinates, int pointCount, bool hasY)
        {
            this.store = store;
            this.coordinates = coordinates;
            this.pointCount = pointCount;
            this.hasY = hasY;
            this.index = 0;
            this.headerPosition = store.headersPosition;
            this.dataPosition = store.serializedPosition;
            this.sharedPoints = null;
            this.Failed = false;

            // The shared point numbers sit in front of the data of the first tuple.
            if (store.tupleCount > 0 && store.hasSharedPointNumbers)
            {
                FontDataReader pointReader = new(store.data.Span, this.dataPosition);
                this.Failed = !TryReadPointNumbers(ref pointReader, store.data.Length, out this.sharedPoints);
                this.dataPosition = pointReader.Position;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the data of a tuple that the instance uses is malformed. The reader gives no
        /// more tuples after it.
        /// </summary>
        public bool Failed { get; private set; }

        /// <summary>
        /// Reads the next tuple that the instance uses.
        /// </summary>
        /// <param name="variation">The tuple.</param>
        /// <returns><see langword="false"/> after the last tuple, or when the data is malformed, which <see cref="Failed"/> tells.</returns>
        public bool TryRead(out TupleVariation variation)
        {
            variation = default;
            ReadOnlySpan<byte> span = this.store.data.Span;
            FontDataReader headers = new(span, this.headerPosition);
            while (!this.Failed && this.index < this.store.tupleCount)
            {
                if (!TryReadHeader(ref headers, this.store.axisCount, out TupleHeader header))
                {
                    this.Failed = true;
                    return false;
                }

                // The shared point numbers push the data of the tuples on, so the sizes can run past the data.
                int end = this.dataPosition + header.DataSize;
                if (end > span.Length || !this.store.TryGetScalar(span, in header, this.coordinates, out float scalar))
                {
                    this.Failed = true;
                    return false;
                }

                int position = this.dataPosition;
                this.index++;
                this.headerPosition = headers.Position;
                this.dataPosition = end;
                if (scalar == 0)
                {
                    continue;
                }

                FontDataReader tupleReader = new(span.Slice(0, end), position);
                this.Failed = !TryReadTuple(ref tupleReader, end, in header, this.sharedPoints, this.pointCount, this.hasY, scalar, out variation);
                return !this.Failed;
            }

            return false;
        }
    }
}

/// <summary>
/// One tuple of a <see cref="TupleVariationStore"/> that an instance uses: the scalar of its region, the points that it
/// has deltas for, and the deltas, which are not scaled yet.
/// </summary>
/// <param name="Scalar">How much of the deltas the instance takes, above 0 and at most 1.</param>
/// <param name="Points">The numbers of the points that have a delta, in increasing order. It is <see langword="null"/> for
/// a tuple that has a delta for every point.</param>
/// <param name="DeltaX">The delta of each point, which is the x delta in 'gvar'.</param>
/// <param name="DeltaY">The y delta of each point in 'gvar', and <see langword="null"/> in 'cvar'.</param>
internal readonly record struct TupleVariation(float Scalar, ushort[]? Points, short[] DeltaX, short[]? DeltaY);
