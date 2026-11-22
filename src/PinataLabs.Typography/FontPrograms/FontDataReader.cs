// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// A big-endian cursor over font data. The Read methods are for load time and throw
/// <see cref="InvalidFontFileException"/> at the end of the data. The Try methods are for glyph time
/// and return <see langword="false"/> instead.
/// </summary>
internal ref struct FontDataReader
{
    private readonly ReadOnlySpan<byte> data;

    public FontDataReader(ReadOnlySpan<byte> data, int position = 0)
    {
        this.data = data;
        this.Position = position;
    }

    public int Position { get; set; }

    /// <summary>
    /// Converts single-byte text, such as a glyph name, to a string. Each byte is the code point of its character.
    /// </summary>
    public static string ToText(ReadOnlyMemory<byte> text)
        => string.Create(text.Length, text, static (chars, bytes) =>
        {
            ReadOnlySpan<byte> span = bytes.Span;
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)span[i];
            }
        });

    public byte ReadByte()
        => this.TryReadByte(out byte value) ? value : throw EndOfData();

    public ushort ReadUInt16()
        => this.TryReadUInt16(out ushort value) ? value : throw EndOfData();

    public short ReadInt16()
        => (short)this.ReadUInt16();

    public uint ReadUInt32()
        => this.TryReadUInt32(out uint value) ? value : throw EndOfData();

    public bool TryReadByte(out byte value)
    {
        if ((uint)this.Position >= (uint)this.data.Length)
        {
            value = 0;
            return false;
        }

        value = this.data[this.Position++];
        return true;
    }

    public bool TryReadSByte(out sbyte value)
    {
        bool result = this.TryReadByte(out byte unsigned);
        value = (sbyte)unsigned;
        return result;
    }

    public bool TryReadUInt16(out ushort value)
    {
        if (!this.Has(2))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt16BigEndian(this.data.Slice(this.Position));
        this.Position += 2;
        return true;
    }

    public bool TryReadInt16(out short value)
    {
        bool result = this.TryReadUInt16(out ushort unsigned);
        value = (short)unsigned;
        return result;
    }

    public bool TryReadUInt32(out uint value)
    {
        if (!this.Has(4))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32BigEndian(this.data.Slice(this.Position));
        this.Position += 4;
        return true;
    }

    public bool TrySkip(int count)
    {
        if (count < 0 || !this.Has(count))
        {
            return false;
        }

        this.Position += count;
        return true;
    }

    private static InvalidFontFileException EndOfData()
        => new("The font data ends before the end of a table.");

    private readonly bool Has(int count)
        => this.Position >= 0 && (long)this.Position + count <= this.data.Length;
}
