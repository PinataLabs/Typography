// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// The 'loca' and 'glyf' tables of a TrueType font. It keeps views of the two tables and finds
/// the data of a glyph when asked, so it holds no array and decodes nothing.
/// </summary>
internal sealed class TrueTypeGlyphData
{
    private readonly ReadOnlyMemory<byte> loca;
    private readonly ReadOnlyMemory<byte> glyf;
    private readonly bool longOffsets;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrueTypeGlyphData"/> class.
    /// </summary>
    /// <param name="loca">The 'loca' table.</param>
    /// <param name="glyf">The 'glyf' table.</param>
    /// <param name="longOffsets">Whether 'loca' holds 32-bit offsets. Otherwise it holds 16-bit offsets divided by two.</param>
    /// <param name="glyphCount">The number of glyphs that the font declares.</param>
    public TrueTypeGlyphData(ReadOnlyMemory<byte> loca, ReadOnlyMemory<byte> glyf, bool longOffsets, int glyphCount)
    {
        this.loca = loca;
        this.glyf = glyf;
        this.longOffsets = longOffsets;

        // 'loca' has one more offset than the font has glyphs. A subset can declare more glyphs than it has offsets for.
        this.GlyphCount = Math.Max(0, Math.Min(glyphCount, (loca.Length / (longOffsets ? 4 : 2)) - 1));
    }

    /// <summary>
    /// Gets the number of glyphs that have an offset in 'loca'.
    /// </summary>
    public int GlyphCount { get; }

    /// <summary>
    /// Gets the length of the 'glyf' table.
    /// </summary>
    public int GlyfLength => this.glyf.Length;

    /// <summary>
    /// Gets a value indicating whether 'loca' holds 32-bit offsets. Otherwise it holds 16-bit offsets divided by two.
    /// </summary>
    public bool HasLongOffsets => this.longOffsets;

    /// <summary>
    /// Gets the 'glyf' data of a glyph. An empty glyph has no data.
    /// </summary>
    /// <returns><see langword="false"/> if the glyph ID or the location of the glyph is out of range.</returns>
    public bool TryGetGlyphData(int glyphId, out ReadOnlyMemory<byte> glyph)
    {
        glyph = default;
        if ((uint)glyphId >= (uint)this.GlyphCount)
        {
            return false;
        }

        ReadOnlySpan<byte> offsets = this.loca.Span;
        uint start;
        uint end;
        if (this.longOffsets)
        {
            start = BinaryPrimitives.ReadUInt32BigEndian(offsets.Slice(glyphId * 4));
            end = BinaryPrimitives.ReadUInt32BigEndian(offsets.Slice((glyphId + 1) * 4));
        }
        else
        {
            start = BinaryPrimitives.ReadUInt16BigEndian(offsets.Slice(glyphId * 2)) * 2U;
            end = BinaryPrimitives.ReadUInt16BigEndian(offsets.Slice((glyphId + 1) * 2)) * 2U;
        }

        if (end < start || end > (uint)this.glyf.Length)
        {
            return false;
        }

        glyph = this.glyf.Slice((int)start, (int)(end - start));
        return true;
    }
}
