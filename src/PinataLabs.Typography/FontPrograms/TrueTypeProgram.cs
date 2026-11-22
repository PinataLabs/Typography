// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// A TrueType font program. It keeps views of the tables it needs and reads them when asked,
/// so it holds no array and decodes nothing at load.
/// </summary>
internal sealed class TrueTypeProgram
{
    private const int MacGlyphNameCount = 258;

    private const int PostHeaderSize = 32;

    private readonly ReadOnlyMemory<byte> loca;
    private readonly ReadOnlyMemory<byte> glyf;
    private readonly ReadOnlyMemory<byte> post;
    private readonly ReadOnlyMemory<byte> cmap;
    private readonly SfntAdvances advances;
    private readonly bool longOffsets;

    private TrueTypeProgram(
        ReadOnlyMemory<byte> loca,
        ReadOnlyMemory<byte> glyf,
        ReadOnlyMemory<byte> post,
        ReadOnlyMemory<byte> cmap,
        SfntAdvances advances,
        bool longOffsets,
        int glyphCount,
        int unitsPerEm)
    {
        this.loca = loca;
        this.glyf = glyf;
        this.post = post;
        this.cmap = cmap;
        this.advances = advances;
        this.longOffsets = longOffsets;
        this.GlyphCount = glyphCount;
        this.UnitsPerEm = unitsPerEm;
    }

    public int GlyphCount { get; }

    public int UnitsPerEm { get; }

    /// <summary>
    /// Loads the program from an sfnt file. The 'head', 'maxp', 'loca' and 'glyf' tables are required.
    /// The 'hhea', 'hmtx', 'post' and 'cmap' tables are optional.
    /// </summary>
    /// <exception cref="InvalidFontFileException">A required table is missing or malformed.</exception>
    public static TrueTypeProgram Load(SfntDirectory directory)
    {
        ReadOnlySpan<byte> head = directory.GetTable(SfntDirectory.Head, "head").Span;
        ReadOnlySpan<byte> maxp = directory.GetTable(SfntDirectory.Maxp, "maxp").Span;
        ReadOnlyMemory<byte> loca = directory.GetTable(SfntDirectory.Loca, "loca");
        ReadOnlyMemory<byte> glyf = directory.GetTable(SfntDirectory.Glyf, "glyf");
        if (head.Length < 52 || maxp.Length < 6)
        {
            throw new InvalidFontFileException("The 'head' or 'maxp' table of the font is truncated.");
        }

        int unitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(head.Slice(18));
        if (unitsPerEm == 0)
        {
            throw new InvalidFontFileException("The font has zero units per em.");
        }

        // 'loca' has one more offset than the font has glyphs. A subset can declare more glyphs than it has offsets for.
        bool longOffsets = BinaryPrimitives.ReadInt16BigEndian(head.Slice(50)) != 0;
        int glyphCount = Math.Min(
            BinaryPrimitives.ReadUInt16BigEndian(maxp.Slice(4)),
            (loca.Length / (longOffsets ? 4 : 2)) - 1);

        if (glyphCount < 1)
        {
            throw new InvalidFontFileException("The font has no glyphs.");
        }

        directory.TryGetTable(SfntDirectory.Post, out ReadOnlyMemory<byte> post);
        directory.TryGetTable(SfntDirectory.Cmap, out ReadOnlyMemory<byte> cmap);
        return new TrueTypeProgram(loca, glyf, post, cmap, SfntAdvances.Read(directory), longOffsets, glyphCount, unitsPerEm);
    }

    /// <summary>
    /// Gets the 'glyf' data of a glyph. An empty glyph has no data.
    /// </summary>
    /// <returns><see langword="false"/> if the glyph ID or the location of the glyph is out of range.</returns>
    public bool TryGetGlyphData(int glyphId, out ReadOnlySpan<byte> glyph)
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
            // Short offsets are stored divided by two.
            start = BinaryPrimitives.ReadUInt16BigEndian(offsets.Slice(glyphId * 2)) * 2U;
            end = BinaryPrimitives.ReadUInt16BigEndian(offsets.Slice((glyphId + 1) * 2)) * 2U;
        }

        if (end < start || end > (uint)this.glyf.Length)
        {
            return false;
        }

        glyph = this.glyf.Span.Slice((int)start, (int)(end - start));
        return true;
    }

    public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth)
    {
        advanceWidth = 0;
        return glyphId < this.GlyphCount && this.advances.TryGet(glyphId, out advanceWidth);
    }

    /// <summary>
    /// Gets the name that the 'post' table gives a glyph.
    /// </summary>
    /// <returns>The name, or <see langword="null"/> if the table has none for the glyph.</returns>
    public string? GetGlyphName(ushort glyphId)
    {
        if (glyphId >= this.GlyphCount || !this.TryGetNameIndex(glyphId, out int nameIndex))
        {
            return null;
        }

        if (nameIndex < MacGlyphNameCount)
        {
            return MacGlyphNames.Names[nameIndex];
        }

        // A name index from 258 selects a string of the table by its position.
        ReadOnlySpan<byte> strings = this.GetPostStrings();
        for (int i = MacGlyphNameCount; TryReadPascalString(ref strings, out ReadOnlySpan<byte> name); i++)
        {
            if (i == nameIndex)
            {
                return FontDataReader.ToText(this.post.Slice(this.post.Length - strings.Length - name.Length, name.Length));
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the glyph that the 'post' table gives a name. The lowest glyph ID wins.
    /// </summary>
    public bool TryGetGlyphId(ReadOnlySpan<char> glyphName, out ushort glyphId)
    {
        glyphId = 0;
        int best = int.MaxValue;

        // The name can be a standard name, a string of the table, or both. Find the first glyph that uses each.
        for (int i = 0; i < MacGlyphNameCount; i++)
        {
            if (glyphName.SequenceEqual(MacGlyphNames.Names[i].AsSpan()))
            {
                best = this.FindFirstGlyphWithNameIndex(i, best);
                break;
            }
        }

        ReadOnlySpan<byte> strings = this.GetPostStrings();
        for (int i = MacGlyphNameCount; TryReadPascalString(ref strings, out ReadOnlySpan<byte> name); i++)
        {
            if (NameEquals(name, glyphName))
            {
                best = this.FindFirstGlyphWithNameIndex(i, best);
            }
        }

        if (best == int.MaxValue)
        {
            return false;
        }

        glyphId = (ushort)best;
        return true;
    }

    /// <summary>
    /// Gets the glyph for each of the 256 codes: from the (3,0) 'cmap' subtable by the low byte of its codes,
    /// else from the (1,0) subtable.
    /// </summary>
    /// <param name="glyphIds">Receives 256 glyph IDs, 0 where a code has no glyph.</param>
    /// <returns><see langword="false"/>, with nothing written, if the font has neither subtable in a supported format.</returns>
    public bool TryGetBuiltInEncoding(Span<ushort> glyphIds)
    {
        if (this.TryGetCmapSubtable(3, 0, out ReadOnlySpan<byte> symbol))
        {
            // A symbol font puts its codes in one of three ranges of the Private Use Area, or uses them directly.
            for (int code = 0; code < 256; code++)
            {
                ushort glyphId = this.MapCode(symbol, code);
                for (int range = 0xF000; glyphId == 0 && range <= 0xF200; range += 0x100)
                {
                    glyphId = this.MapCode(symbol, range | code);
                }

                glyphIds[code] = glyphId;
            }

            return true;
        }

        if (this.TryGetCmapSubtable(1, 0, out ReadOnlySpan<byte> mac))
        {
            for (int code = 0; code < 256; code++)
            {
                glyphIds[code] = this.MapCode(mac, code);
            }

            return true;
        }

        return false;
    }

    private static bool NameEquals(ReadOnlySpan<byte> name, ReadOnlySpan<char> other)
    {
        if (name.Length != other.Length)
        {
            return false;
        }

        for (int i = 0; i < name.Length; i++)
        {
            if (name[i] != other[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadPascalString(ref ReadOnlySpan<byte> strings, out ReadOnlySpan<byte> value)
    {
        if (strings.IsEmpty || strings.Length < 1 + strings[0])
        {
            value = default;
            return false;
        }

        value = strings.Slice(1, strings[0]);
        strings = strings.Slice(1 + value.Length);
        return true;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data, int position)
        => position >= 0 && position + 2 <= data.Length
            ? BinaryPrimitives.ReadUInt16BigEndian(data.Slice(position))
            : (ushort)0;

    // Formats 1 and 2 of 'post' name glyphs. Format 1 uses the standard names in order.
    private int GetPostFormat()
    {
        ReadOnlySpan<byte> table = this.post.Span;
        if (table.Length < PostHeaderSize)
        {
            return 0;
        }

        return BinaryPrimitives.ReadUInt32BigEndian(table) switch
        {
            0x00010000 => 1,
            0x00020000 when table.Length >= PostHeaderSize + 2 => 2,
            _ => 0,
        };
    }

    private int GetPostNameCount()
    {
        ReadOnlySpan<byte> table = this.post.Span;
        int count = BinaryPrimitives.ReadUInt16BigEndian(table.Slice(PostHeaderSize));
        return Math.Min(Math.Min(count, this.GlyphCount), (table.Length - PostHeaderSize - 2) / 2);
    }

    private ReadOnlySpan<byte> GetPostStrings()
    {
        if (this.GetPostFormat() != 2)
        {
            return default;
        }

        ReadOnlySpan<byte> table = this.post.Span;
        int start = PostHeaderSize + 2 + (2 * BinaryPrimitives.ReadUInt16BigEndian(table.Slice(PostHeaderSize)));
        return start <= table.Length ? table.Slice(start) : default;
    }

    private bool TryGetNameIndex(int glyphId, out int nameIndex)
    {
        nameIndex = glyphId;
        switch (this.GetPostFormat())
        {
            case 1:
                return glyphId < MacGlyphNameCount;
            case 2 when glyphId < this.GetPostNameCount():
                nameIndex = BinaryPrimitives.ReadUInt16BigEndian(this.post.Span.Slice(PostHeaderSize + 2 + (glyphId * 2)));
                return true;
            default:
                return false;
        }
    }

    private int FindFirstGlyphWithNameIndex(int nameIndex, int best)
    {
        switch (this.GetPostFormat())
        {
            case 1:
                return nameIndex < this.GlyphCount ? Math.Min(best, nameIndex) : best;
            case 2:
                ReadOnlySpan<byte> indices = this.post.Span.Slice(PostHeaderSize + 2);
                int count = Math.Min(this.GetPostNameCount(), best);
                for (int i = 0; i < count; i++)
                {
                    if (BinaryPrimitives.ReadUInt16BigEndian(indices.Slice(i * 2)) == nameIndex)
                    {
                        return i;
                    }
                }

                return best;
            default:
                return best;
        }
    }

    private bool TryGetCmapSubtable(int platformId, int encodingId, out ReadOnlySpan<byte> subtable)
    {
        subtable = default;
        ReadOnlySpan<byte> table = this.cmap.Span;
        int recordCount = ReadUInt16(table, 2);
        for (int i = 0; i < recordCount; i++)
        {
            int record = 4 + (i * 8);
            if (record + 8 > table.Length)
            {
                return false;
            }

            if (ReadUInt16(table, record) == platformId && ReadUInt16(table, record + 2) == encodingId)
            {
                uint offset = BinaryPrimitives.ReadUInt32BigEndian(table.Slice(record + 4));
                if (offset >= (uint)table.Length)
                {
                    return false;
                }

                // Formats 0, 4 and 6 hold 16-bit codes, which is all that these two encodings need.
                subtable = table.Slice((int)offset);
                return ReadUInt16(subtable, 0) is 0 or 4 or 6;
            }
        }

        return false;
    }

    private ushort MapCode(ReadOnlySpan<byte> subtable, int code)
    {
        int glyphId = 0;
        switch (ReadUInt16(subtable, 0))
        {
            case 0:
                // A byte for each of 256 codes.
                glyphId = code < 256 && 6 + code < subtable.Length ? subtable[6 + code] : 0;
                break;

            case 6:
                // A range of codes: firstCode, entryCount, then a glyph ID for each code.
                int index = code - ReadUInt16(subtable, 6);
                glyphId = index >= 0 && index < ReadUInt16(subtable, 8) ? ReadUInt16(subtable, 10 + (index * 2)) : 0;
                break;

            case 4:
                // Segments of codes in four parallel arrays: endCode, a pad, startCode, idDelta, idRangeOffset.
                int segmentBytes = ReadUInt16(subtable, 6);
                for (int segment = 0; segment < segmentBytes; segment += 2)
                {
                    if (ReadUInt16(subtable, 14 + segment) < code)
                    {
                        continue;
                    }

                    int start = ReadUInt16(subtable, 16 + segmentBytes + segment);
                    if (start > code)
                    {
                        break;
                    }

                    int delta = ReadUInt16(subtable, 16 + (2 * segmentBytes) + segment);
                    int rangeOffsetPosition = 16 + (3 * segmentBytes) + segment;
                    int rangeOffset = ReadUInt16(subtable, rangeOffsetPosition);
                    if (rangeOffset == 0)
                    {
                        glyphId = (code + delta) & 0xFFFF;
                    }
                    else
                    {
                        // The offset is from its own position to the glyph ID of the first code of the segment.
                        glyphId = ReadUInt16(subtable, rangeOffsetPosition + rangeOffset + ((code - start) * 2));
                        glyphId = glyphId == 0 ? 0 : (glyphId + delta) & 0xFFFF;
                    }

                    break;
                }

                break;
        }

        return glyphId < this.GlyphCount ? (ushort)glyphId : (ushort)0;
    }
}
