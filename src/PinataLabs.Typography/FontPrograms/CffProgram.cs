// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// A CFF font program in an OpenType file: the 'CFF ' table, and the 'hmtx' advances when the file has them.
/// </summary>
internal sealed class CffProgram
{
    private readonly SfntAdvances advances;
    private readonly ReadOnlyMemory<byte> cmap;

    private CffProgram(CffFont font, SfntOptionalTables tables)
    {
        this.Font = font;
        this.cmap = tables.Cmap;
        this.advances = tables.Advances;
        this.MissingTables = tables.Missing;
        this.MalformedTables = tables.Malformed;
    }

    public CffFont Font { get; }

    public OptionalFontTables MissingTables { get; }

    public OptionalFontTables MalformedTables { get; }

    /// <summary>
    /// Loads the program from an OpenType file.
    /// </summary>
    /// <exception cref="InvalidFontFileException">The file has no 'CFF ' table, or the table is malformed.</exception>
    /// <exception cref="FontProgramTooLargeException">The program does not fit under the cap of the budget.</exception>
    public static CffProgram Load(SfntDirectory directory, ref LoadBudget budget)
    {
        if (!directory.TryGetTable(SfntDirectory.Cff, out ReadOnlyMemory<byte> cff))
        {
            throw new InvalidFontFileException(directory.TryGetTable(SfntDirectory.Cff2, out _)
                ? "A font program with a 'CFF2' table is not supported."
                : "The font has no 'CFF ' table.");
        }

        return new CffProgram(CffParser.Load(cff, ref budget), SfntOptionalTables.Read(directory));
    }

    /// <summary>
    /// Gets a value indicating whether the data starts like a bare CFF 1 font: major version 1, a header of at least
    /// 4 bytes and an offset size of 1 to 4. The first byte of every sfnt tag, of a Type 1 font ('%', 0x80)
    /// and of a font collection or WOFF file is not 1, so no other format that <see cref="FontProgram"/> reads matches.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <returns><see langword="true"/> if the data has a CFF 1 header.</returns>
    public static bool IsBareCff(ReadOnlySpan<byte> data)
        => data.Length >= 4 && data[0] == 1 && data[2] >= 4 && data[3] is >= 1 and <= 4;

    /// <summary>
    /// Gets a value indicating whether the data starts like a bare CFF2 font, which is not supported.
    /// </summary>
    /// <param name="data">The font data.</param>
    /// <returns><see langword="true"/> if the data has a CFF2 header.</returns>
    public static bool IsBareCff2(ReadOnlySpan<byte> data)
        => data.Length >= 5 && data[0] == 2 && data[1] == 0 && data[2] == 5;

    /// <summary>
    /// Loads the program from the bytes of a bare CFF font, as a PDF embeds it in a <c>/FontFile3</c> stream.
    /// It has no 'hmtx' table, so each advance comes from the charstring of the glyph.
    /// </summary>
    /// <exception cref="InvalidFontFileException">The data is malformed.</exception>
    /// <exception cref="FontProgramTooLargeException">The program does not fit under the cap of the budget.</exception>
    public static CffProgram LoadBare(ReadOnlyMemory<byte> data, ref LoadBudget budget)
        => new(CffParser.Load(data, ref budget), default);

    public bool TryGetGlyphIdFromCmap(int platformId, int encodingId, int code, out ushort glyphId)
    {
        if (CmapReader.TryGetSubtable(this.cmap.Span, platformId, encodingId, out ReadOnlySpan<byte> subtable))
        {
            return CmapReader.TryMap(subtable, code, this.Font.GlyphCount, out glyphId);
        }

        glyphId = 0;
        return false;
    }

    public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth)
    {
        if (this.advances.IsEmpty)
        {
            return this.Font.TryGetAdvanceWidth(glyphId, out advanceWidth);
        }

        advanceWidth = 0;
        return glyphId < this.Font.GlyphCount && this.advances.TryGet(glyphId, out advanceWidth);
    }
}
