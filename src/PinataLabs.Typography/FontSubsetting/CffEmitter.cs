// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// Writes the parts of a CFF table in order. With an empty destination it writes nothing and only counts, so one routine
/// that emits the whole table also measures it.
/// </summary>
internal ref struct CffEmitter
{
    private readonly Span<byte> destination;
    private readonly bool write;

    // The INDEX that is being written: the size of its offsets, where the next offset goes,
    // and the offset of the next object, which counts from 1.
    private int offSize;
    private long nextOffsetPosition;
    private long nextObjectOffset;

    public CffEmitter(Span<byte> destination)
    {
        this.destination = destination;
        this.write = !destination.IsEmpty;
        this.Position = 0;
        this.offSize = 0;
        this.nextOffsetPosition = 0;
        this.nextObjectOffset = 0;
    }

    /// <summary>
    /// Gets the position of the next byte to write. The count is 64 bits, so measuring a table that is too large to write does not overflow.
    /// Only a position below 2 GB is a place that a writer can use.
    /// </summary>
    public long Position { get; private set; }

    /// <summary>
    /// Writes bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public void Write(ReadOnlySpan<byte> bytes)
    {
        if (this.write)
        {
            bytes.CopyTo(this.destination.Slice((int)this.Position));
        }

        this.Position += bytes.Length;
    }

    /// <summary>
    /// Starts an INDEX with its count and its offsets, which the objects that follow fill in.
    /// </summary>
    /// <param name="count">The number of objects, which <see cref="AddObject"/> and <see cref="AddDict"/> add.</param>
    /// <param name="dataLength">The total length of the objects.</param>
    public void BeginIndex(int count, long dataLength)
    {
        if (count == 0)
        {
            this.Position += 2;
            return;
        }

        this.offSize = GetOffSize(dataLength);
        if (this.write)
        {
            Span<byte> header = this.destination.Slice((int)this.Position);
            BinaryPrimitives.WriteUInt16BigEndian(header, (ushort)count);
            header[2] = (byte)this.offSize;
            WriteOffset(header.Slice(3), this.offSize, 1);
        }

        this.nextOffsetPosition = this.Position + 3 + this.offSize;
        this.nextObjectOffset = 1;
        this.Position += 3 + ((count + 1L) * this.offSize);
    }

    /// <summary>
    /// Adds an object to the INDEX.
    /// </summary>
    /// <param name="bytes">The object.</param>
    public void AddObject(ReadOnlySpan<byte> bytes)
    {
        this.Write(bytes);
        this.EndObject(bytes.Length);
    }

    /// <summary>
    /// Adds a DICT to the INDEX, with new offsets.
    /// </summary>
    /// <param name="source">The DICT.</param>
    /// <param name="kind">The kind of the DICT.</param>
    /// <param name="offsets">The new values.</param>
    public void AddDict(ReadOnlySpan<byte> source, CffDictKind kind, in CffDictOffsets offsets)
    {
        int length = CffDictRewriter.Rewrite(source, kind, in offsets, this.write ? this.destination.Slice((int)this.Position) : default);
        this.Position += length;
        this.EndObject(length);
    }

    /// <summary>
    /// Writes a DICT with new offsets.
    /// </summary>
    /// <param name="source">The DICT.</param>
    /// <param name="kind">The kind of the DICT.</param>
    /// <param name="offsets">The new values.</param>
    /// <returns>The length of the DICT.</returns>
    public int WriteDict(ReadOnlySpan<byte> source, CffDictKind kind, in CffDictOffsets offsets)
    {
        int length = CffDictRewriter.Rewrite(source, kind, in offsets, this.write ? this.destination.Slice((int)this.Position) : default);
        this.Position += length;
        return length;
    }

    private static int GetOffSize(long dataLength)
        => dataLength < 0xFF ? 1 : dataLength < 0xFFFF ? 2 : dataLength < 0xFFFFFF ? 3 : 4;

    private static void WriteOffset(Span<byte> destination, int offSize, long value)
    {
        for (int i = offSize - 1; i >= 0; i--)
        {
            destination[i] = (byte)value;
            value >>= 8;
        }
    }

    private void EndObject(int length)
    {
        this.nextObjectOffset += length;
        if (this.write)
        {
            WriteOffset(this.destination.Slice((int)this.nextOffsetPosition), this.offSize, this.nextObjectOffset);
        }

        this.nextOffsetPosition += this.offSize;
    }
}
