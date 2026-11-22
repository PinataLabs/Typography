// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Numerics;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// One font of a CFF table. It keeps the table bytes and reads a glyph when the glyph is requested.
/// </summary>
internal sealed class CffFont
{
    private readonly ReadOnlyMemory<byte> data;
    private readonly CffIndex strings;
    private readonly CffIndex globalSubrs;
    private readonly CffIndex charStrings;

    // For each glyph its SID, or its CID in a CID-keyed font.
    private readonly ushort[] charset;
    private readonly int encodingOffset;
    private readonly CffFontDict[] fontDicts;
    private readonly int fdSelectOffset;

    internal CffFont(
        ReadOnlyMemory<byte> data,
        CffIndex strings,
        CffIndex globalSubrs,
        CffIndex charStrings,
        ushort[] charset,
        bool isCidKeyed,
        Matrix3x2 fontMatrix,
        int encodingOffset,
        CffFontDict[] fontDicts,
        int fdSelectOffset)
    {
        this.data = data;
        this.strings = strings;
        this.globalSubrs = globalSubrs;
        this.charStrings = charStrings;
        this.charset = charset;
        this.IsCidKeyed = isCidKeyed;
        this.FontMatrix = fontMatrix;
        this.encodingOffset = encodingOffset;
        this.fontDicts = fontDicts;
        this.fdSelectOffset = fdSelectOffset;
    }

    public int GlyphCount => this.charStrings.Count;

    /// <summary>
    /// Gets a value indicating whether the font is CID-keyed. Such a font has CIDs in place of glyph names.
    /// </summary>
    public bool IsCidKeyed { get; }

    /// <summary>
    /// Gets the matrix from glyph space to ems. It holds for every glyph: a glyph whose Font DICT has
    /// another matrix is written in the glyph space of this one.
    /// </summary>
    public Matrix3x2 FontMatrix { get; }

    /// <summary>
    /// Gets the advance width that the charstring of a glyph gives, in glyph space.
    /// </summary>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="advanceWidth">The advance width.</param>
    /// <returns><see langword="false"/> if the glyph ID is out of range or the glyph is malformed.</returns>
    public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth)
    {
        advanceWidth = 0;
        return this.TryGetGlyph(glyphId, out CffGlyphData glyph) && glyph.TryGetAdvanceWidth(out advanceWidth);
    }

    /// <summary>
    /// Gets the name of a glyph.
    /// </summary>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns>The name, or <see langword="null"/> if the glyph has none.</returns>
    public string? GetGlyphName(ushort glyphId)
    {
        if (this.IsCidKeyed || glyphId >= this.charset.Length)
        {
            return null;
        }

        int sid = this.charset[glyphId];
        if (sid < CffStandardStrings.Count)
        {
            return CffStandardStrings.GetName(sid);
        }

        if (!this.strings.TryGet(sid - CffStandardStrings.Count, out ReadOnlyMemory<byte> name))
        {
            return null;
        }

        // Strings are single-byte. Each byte is the code point of its character.
        return string.Create(name.Length, name, static (chars, bytes) =>
        {
            ReadOnlySpan<byte> span = bytes.Span;
            for (int i = 0; i < chars.Length; i++)
            {
                chars[i] = (char)span[i];
            }
        });
    }

    /// <summary>
    /// Finds the glyph with the given name. The lowest glyph ID wins.
    /// </summary>
    /// <param name="glyphName">The glyph name, compared by ordinal.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns><see langword="false"/> if no glyph has the name.</returns>
    public bool TryGetGlyphId(ReadOnlySpan<char> glyphName, out ushort glyphId)
    {
        glyphId = 0;
        if (this.IsCidKeyed)
        {
            return false;
        }

        for (int i = 0; i < this.charset.Length; i++)
        {
            if (this.NameEquals(this.charset[i], glyphName))
            {
                glyphId = (ushort)i;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Finds the glyph for a CID. A font that is not CID-keyed maps each CID to the glyph with the same ID.
    /// </summary>
    /// <param name="cid">The CID.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns><see langword="false"/> if the font has no glyph for the CID.</returns>
    public bool TryGetGlyphIdForCid(int cid, out ushort glyphId)
    {
        int index = this.IsCidKeyed
            ? (uint)cid <= ushort.MaxValue ? this.charset.AsSpan().IndexOf((ushort)cid) : -1
            : (uint)cid < (uint)this.GlyphCount ? cid : -1;

        glyphId = index < 0 ? (ushort)0 : (ushort)index;
        return index >= 0;
    }

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

        // A font without an FDSelect has one Font DICT.
        int fontDict = 0;
        if (this.fdSelectOffset != 0 && !this.TryGetFontDictIndex(glyphId, out fontDict))
        {
            return false;
        }

        if (fontDict >= this.fontDicts.Length)
        {
            return false;
        }

        glyph = new CffGlyphData((ushort)glyphId, this.globalSubrs, in this.fontDicts[fontDict], charString);
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

    /// <summary>
    /// Gets the glyph for each of the 256 codes of the encoding that the font carries.
    /// </summary>
    /// <param name="glyphIds">Receives 256 glyph IDs, 0 where a code has no glyph.</param>
    /// <returns>
    /// <see langword="false"/>, with nothing written, if the font is CID-keyed or its encoding is malformed.
    /// </returns>
    public bool TryGetBuiltInEncoding(Span<ushort> glyphIds)
    {
        if (this.IsCidKeyed)
        {
            return false;
        }

        Span<ushort> codes = stackalloc ushort[256];
        codes.Clear();

        // Offsets 0 and 1 name the predefined Standard and Expert encodings.
        if (this.encodingOffset <= 1)
        {
            ushort[] sids = this.encodingOffset == 0 ? CffPredefinedTables.StandardEncoding : CffPredefinedTables.ExpertEncoding;
            for (int code = 0; code < codes.Length; code++)
            {
                codes[code] = this.GetGlyphForSid(sids[code]);
            }
        }
        else if (!this.TryReadCustomEncoding(codes))
        {
            return false;
        }

        codes.CopyTo(glyphIds);
        return true;
    }

    private bool TryReadCustomEncoding(Span<ushort> codes)
    {
        ReadOnlySpan<byte> table = this.data.Span.Slice(this.encodingOffset);
        if (table.Length < 2)
        {
            return false;
        }

        // The codes of glyphs 1, 2, 3 and so on: one code per glyph in format 0, ranges of codes in format 1.
        int count = table[1];
        int position = 2;
        int glyph = 1;
        switch (table[0] & 0x7F)
        {
            case 0:
                if (position + count > table.Length)
                {
                    return false;
                }

                for (int i = 0; i < count; i++, glyph++)
                {
                    this.Encode(codes, table[position++], glyph);
                }

                break;

            case 1:
                if (position + (2 * count) > table.Length)
                {
                    return false;
                }

                for (int i = 0; i < count; i++, position += 2)
                {
                    for (int code = table[position], last = code + table[position + 1]; code <= last; code++, glyph++)
                    {
                        this.Encode(codes, code, glyph);
                    }
                }

                break;

            default:
                return false;
        }

        // The high bit of the format marks supplements: extra codes for glyphs that have more than one.
        if ((table[0] & 0x80) != 0)
        {
            if (position >= table.Length || position + 1 + (3 * table[position]) > table.Length)
            {
                return false;
            }

            int supplements = table[position++];
            for (int i = 0; i < supplements; i++, position += 3)
            {
                codes[table[position]] = this.GetGlyphForSid(BinaryPrimitives.ReadUInt16BigEndian(table.Slice(position + 1)));
            }
        }

        return true;
    }

    private void Encode(Span<ushort> codes, int code, int glyph)
    {
        if (code < codes.Length && glyph < this.GlyphCount)
        {
            codes[code] = (ushort)glyph;
        }
    }

    private ushort GetGlyphForSid(ushort sid)
    {
        int index = this.charset.AsSpan().IndexOf(sid);
        return index < 0 ? (ushort)0 : (ushort)index;
    }

    private bool NameEquals(int sid, ReadOnlySpan<char> name)
    {
        if (sid < CffStandardStrings.Count)
        {
            return name.SequenceEqual(CffStandardStrings.GetName(sid).AsSpan());
        }

        if (!this.strings.TryGet(sid - CffStandardStrings.Count, out ReadOnlyMemory<byte> item) || item.Length != name.Length)
        {
            return false;
        }

        ReadOnlySpan<byte> bytes = item.Span;
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != name[i])
            {
                return false;
            }
        }

        return true;
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
