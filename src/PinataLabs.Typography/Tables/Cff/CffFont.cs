// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// One font of a CFF table. It keeps the table bytes and reads a glyph when the glyph is requested.
/// </summary>
internal sealed class CffFont
{
    private readonly ReadOnlyMemory<byte> data;
    private readonly CffIndex globalSubrs;
    private readonly CffIndex charStrings;
    private readonly CffFontDict topPrivate;
    private readonly CffFontDict[] fontDicts;
    private readonly int fdSelectOffset;

    internal CffFont(
        ReadOnlyMemory<byte> data,
        CffIndex globalSubrs,
        CffIndex charStrings,
        CffFontDict topPrivate,
        CffFontDict[] fontDicts,
        int fdSelectOffset)
    {
        this.data = data;
        this.globalSubrs = globalSubrs;
        this.charStrings = charStrings;
        this.topPrivate = topPrivate;
        this.fontDicts = fontDicts;
        this.fdSelectOffset = fdSelectOffset;
    }

    public int GlyphCount => this.charStrings.Count;

    /// <summary>
    /// Gets what the evaluation of one glyph needs.
    /// </summary>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="glyph">The glyph data.</param>
    /// <returns><see langword="false"/> if the glyph ID is out of range or the data of the glyph is malformed.</returns>
    public bool TryGetGlyph(int glyphId, out CffGlyphData glyph)
    {
        glyph = default;
        if (!this.charStrings.TryGet(glyphId, out ReadOnlyMemory<byte> charString))
        {
            return false;
        }

        CffIndex localSubrs = this.topPrivate.LocalSubrs;
        if (this.fdSelectOffset != 0 && this.data.Span[this.fdSelectOffset] == 3)
        {
            if (!this.TryGetFontDictIndex(glyphId, out int fontDict) || fontDict >= this.fontDicts.Length)
            {
                return false;
            }

            localSubrs = this.fontDicts[fontDict].LocalSubrs;
        }

        glyph = new CffGlyphData((ushort)glyphId, this.globalSubrs, localSubrs, this.topPrivate.NominalWidthX, charString);
        return true;
    }

    /// <summary>
    /// Checks that an FDSelect lies within the table, so that a glyph lookup can read it unchecked.
    /// </summary>
    /// <exception cref="InvalidFontFileException">The FDSelect is malformed.</exception>
    internal static void ValidateFdSelect(ReadOnlySpan<byte> data, int offset, int glyphCount)
    {
        if ((uint)offset >= (uint)data.Length)
        {
            throw new InvalidFontFileException("The CFF FDSelect is out of range.");
        }

        long length = data[offset] switch
        {
            0 => 1 + (long)glyphCount,
            3 when offset + 3 <= data.Length => 3 + (3L * BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 1))) + 2,
            _ => throw new InvalidFontFileException("Only CFF FDSelect formats 0 and 3 are supported."),
        };

        if (offset + length > data.Length)
        {
            throw new InvalidFontFileException("The CFF FDSelect is truncated.");
        }
    }

    private bool TryGetFontDictIndex(int glyphId, out int fontDict)
    {
        ReadOnlySpan<byte> select = this.data.Span.Slice(this.fdSelectOffset);
        if (select[0] == 0)
        {
            fontDict = select[1 + glyphId];
            return true;
        }

        // Format 3: Range3 { Card16 first; Card8 fd } [nRanges], then a Card16 sentinel.
        // Find the last range whose first glyph is not after the glyph.
        int rangeCount = BinaryPrimitives.ReadUInt16BigEndian(select.Slice(1));
        ReadOnlySpan<byte> ranges = select.Slice(3);
        int low = 0;
        int high = rangeCount - 1;
        int found = -1;
        while (low <= high)
        {
            int middle = (low + high) >> 1;
            if (BinaryPrimitives.ReadUInt16BigEndian(ranges.Slice(middle * 3)) <= glyphId)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        if (found < 0 || glyphId >= BinaryPrimitives.ReadUInt16BigEndian(ranges.Slice(rangeCount * 3)))
        {
            fontDict = 0;
            return false;
        }

        fontDict = ranges[(found * 3) + 2];
        return true;
    }
}
