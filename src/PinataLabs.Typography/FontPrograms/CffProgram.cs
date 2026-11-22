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

    private CffProgram(CffFont font, SfntAdvances advances)
    {
        this.Font = font;
        this.advances = advances;
    }

    public CffFont Font { get; }

    /// <summary>
    /// Loads the program from an OpenType file.
    /// </summary>
    /// <exception cref="InvalidFontFileException">The file has no 'CFF ' table, or the table is malformed.</exception>
    public static CffProgram Load(SfntDirectory directory)
    {
        if (!directory.TryGetTable(SfntDirectory.Cff, out ReadOnlyMemory<byte> cff))
        {
            throw new InvalidFontFileException(directory.TryGetTable(SfntDirectory.Cff2, out _)
                ? "A font program with a 'CFF2' table is not supported."
                : "The font has no 'CFF ' table.");
        }

        return new CffProgram(CffParser.Load(cff), SfntAdvances.Read(directory));
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
