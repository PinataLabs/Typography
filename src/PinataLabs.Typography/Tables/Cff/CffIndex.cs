// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// A CFF INDEX as a view over the table bytes. It copies nothing, and it reads the offset of an
/// object when the object is requested.
/// </summary>
internal readonly struct CffIndex
{
    private readonly ReadOnlyMemory<byte> data;
    private readonly int offsetArray;
    private readonly int offSize;

    private CffIndex(ReadOnlyMemory<byte> data, int count, int offSize, int offsetArray)
    {
        this.data = data;
        this.Count = count;
        this.offSize = offSize;
        this.offsetArray = offsetArray;
    }

    /// <summary>
    /// Gets the number of objects in the INDEX.
    /// </summary>
    public int Count { get; }

    // Offsets are relative to the byte before the object data.
    private long DataStart => this.offsetArray + (((long)this.Count + 1) * this.offSize) - 1;

    /// <summary>
    /// Reads the header of an INDEX and checks that the INDEX lies within the data.
    /// </summary>
    /// <param name="data">The table that contains the INDEX.</param>
    /// <param name="position">The position of the INDEX in <paramref name="data"/>.</param>
    /// <param name="countSize">The size of the count field in bytes: 2 for CFF, 4 for CFF2.</param>
    /// <param name="end">The position of the first byte after the INDEX.</param>
    /// <returns>The <see cref="CffIndex"/>.</returns>
    /// <exception cref="InvalidFontFileException">The INDEX is malformed.</exception>
    public static CffIndex Read(ReadOnlyMemory<byte> data, int position, int countSize, out int end)
    {
        ReadOnlySpan<byte> span = data.Span;
        if (position < 0 || (long)position + countSize > span.Length)
        {
            throw new InvalidFontFileException("A CFF INDEX is out of range.");
        }

        uint count = countSize == 2
            ? BinaryPrimitives.ReadUInt16BigEndian(span.Slice(position))
            : BinaryPrimitives.ReadUInt32BigEndian(span.Slice(position));

        int cursor = position + countSize;
        if (count == 0)
        {
            end = cursor;
            return default;
        }

        if (cursor >= span.Length)
        {
            throw new InvalidFontFileException("A CFF INDEX is truncated.");
        }

        int offSize = span[cursor++];
        long offsetsLength = ((long)count + 1) * offSize;
        if (offSize is < 1 or > 4 || cursor + offsetsLength > span.Length)
        {
            throw new InvalidFontFileException("A CFF INDEX has an invalid offset array.");
        }

        CffIndex index = new(data, (int)count, offSize, cursor);
        long last = index.DataStart + index.ReadOffset(span, (int)count);
        if (last <= index.DataStart || last > span.Length)
        {
            throw new InvalidFontFileException("A CFF INDEX has invalid object data.");
        }

        end = (int)last;
        return index;
    }

    /// <summary>
    /// Gets the object at the given index.
    /// </summary>
    /// <param name="index">The index of the object.</param>
    /// <param name="item">The bytes of the object.</param>
    /// <returns><see langword="false"/> if the index or the offsets of the object are out of range.</returns>
    public bool TryGet(int index, out ReadOnlyMemory<byte> item)
    {
        if (this.TryGetRange(index, out int start, out int length))
        {
            item = this.data.Slice(start, length);
            return true;
        }

        item = default;
        return false;
    }

    /// <summary>
    /// Gets the position and the length of the object at the given index.
    /// </summary>
    /// <param name="index">The index of the object.</param>
    /// <param name="start">The position of the object in the table.</param>
    /// <param name="length">The length of the object in bytes.</param>
    /// <returns><see langword="false"/> if the index or the offsets of the object are out of range.</returns>
    public bool TryGetRange(int index, out int start, out int length)
    {
        start = 0;
        length = 0;
        if ((uint)index >= (uint)this.Count)
        {
            return false;
        }

        ReadOnlySpan<byte> span = this.data.Span;
        uint first = this.ReadOffset(span, index);
        uint next = this.ReadOffset(span, index + 1);
        if (first < 1 || next < first || this.DataStart + next > span.Length)
        {
            return false;
        }

        start = (int)(this.DataStart + first);
        length = (int)(next - first);
        return true;
    }

    private uint ReadOffset(ReadOnlySpan<byte> span, int index)
    {
        int position = this.offsetArray + (index * this.offSize);
        uint value = 0;
        for (int i = 0; i < this.offSize; i++)
        {
            value = (value << 8) | span[position + i];
        }

        return value;
    }
}
