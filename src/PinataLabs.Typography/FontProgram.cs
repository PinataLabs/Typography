// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography;

/// <summary>
/// A font program: the glyph outlines of one font, read by glyph ID without text layout.
/// </summary>
/// <remarks>
/// <para>
/// Use this type to draw the glyphs of a font that another format selects and positions, such as a
/// font embedded in a PDF file. It gives the outline of a glyph, the matrix from glyph space to ems,
/// glyph lookup by name and by CID, and the encoding that the font carries.
/// </para>
/// <para>
/// A font program keeps the data that it was loaded from and reads that data on each call.
/// It holds no state that changes after load, so one instance can serve many threads at once.
/// </para>
/// </remarks>
public sealed class FontProgram
{
    // A TrueTypeProgram or a CffProgram. Each member selects on the type, because a generic virtual
    // method with a struct type argument does not work with every ahead-of-time compiler.
    private readonly object program;

    private FontProgram(TrueTypeProgram program)
    {
        this.program = program;
        this.Format = FontProgramFormat.TrueType;
        this.GlyphCount = program.GlyphCount;
        this.FontMatrix = Matrix3x2.CreateScale(1F / program.UnitsPerEm);
    }

    private FontProgram(CffProgram program)
    {
        this.program = program;
        this.Format = FontProgramFormat.Cff;
        this.GlyphCount = program.Font.GlyphCount;
        this.FontMatrix = program.Font.FontMatrix;
        this.IsCidKeyed = program.Font.IsCidKeyed;
    }

    /// <summary>
    /// Gets the format of the outlines.
    /// </summary>
    public FontProgramFormat Format { get; }

    /// <summary>
    /// Gets a value indicating whether the program is a CID-keyed CFF font.
    /// Such a program identifies its glyphs by CID and has no glyph names.
    /// </summary>
    public bool IsCidKeyed { get; }

    /// <summary>
    /// Gets the number of glyphs. Glyph IDs run from 0 to one less than this number.
    /// </summary>
    public int GlyphCount { get; }

    /// <summary>
    /// Gets the matrix that maps glyph space to ems. It holds for every glyph of the program.
    /// For TrueType it scales by one over the units per em.
    /// For CFF it is the FontMatrix of the Top DICT, which by default scales by 0.001.
    /// </summary>
    public Matrix3x2 FontMatrix { get; }

    /// <summary>
    /// Loads a font program. The format is detected from the data.
    /// </summary>
    /// <remarks>
    /// The supported formats are an sfnt file with TrueType outlines in a 'glyf' table, and an
    /// OpenType file with a 'CFF ' table. WOFF, WOFF2, font collections and CFF2 are not supported.
    /// </remarks>
    /// <param name="data">
    /// The font file. The program keeps this memory and reads it on each call. It does not copy it.
    /// Do not change or reuse the memory while the program is in use.
    /// </param>
    /// <returns>The <see cref="FontProgram"/>.</returns>
    /// <exception cref="InvalidFontFileException">The data is malformed or is not in a supported format.</exception>
    public static FontProgram Load(ReadOnlyMemory<byte> data)
    {
        SfntDirectory directory = SfntDirectory.Read(data);
        return directory.Version switch
        {
            SfntDirectory.TrueTypeVersion or SfntDirectory.AppleTrueTypeVersion => new FontProgram(TrueTypeProgram.Load(directory)),
            SfntDirectory.OpenTypeCffVersion => new FontProgram(CffProgram.Load(directory)),
            0x74746366 => throw new InvalidFontFileException("A font collection is not a supported font program."),
            0x774F4646 or 0x774F4632 => throw new InvalidFontFileException("A WOFF or WOFF2 file is not a supported font program."),
            _ => throw new InvalidFontFileException("The data is not a supported font program."),
        };
    }

    /// <summary>
    /// Writes the unhinted outline of a glyph to a sink, in glyph space with y pointing up.
    /// </summary>
    /// <remarks>
    /// Each contour is a <see cref="IGlyphOutlineSink.MoveTo"/>, then segments, then a
    /// <see cref="IGlyphOutlineSink.ClosePath"/>. A glyph without an outline, such as a space, writes nothing.
    /// This method does not throw for malformed glyph data.
    /// </remarks>
    /// <typeparam name="TSink">The type of the sink.</typeparam>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="sink">The sink that receives the outline.</param>
    /// <returns>
    /// <see langword="true"/> if the outline was written; <see langword="false"/> if the glyph ID is out of
    /// range or the glyph data is malformed. After <see langword="false"/> the sink can hold a part of the outline.
    /// </returns>
    public bool TryWriteOutline<TSink>(ushort glyphId, ref TSink sink)
        where TSink : IGlyphOutlineSink
        => this.program switch
        {
            TrueTypeProgram trueType => TrueTypeGlyphDecoder.TryWriteOutline(trueType, glyphId, ref sink),
            CffProgram cff => cff.Font.TryWriteOutline(glyphId, ref sink),
            _ => false,
        };

    /// <summary>
    /// Gets the advance width of a glyph in glyph space.
    /// The width comes from the 'hmtx' table, or from the charstring of a CFF glyph when the font has no 'hmtx' table.
    /// </summary>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="advanceWidth">The advance width.</param>
    /// <returns>
    /// <see langword="true"/> if the program has a width for the glyph; <see langword="false"/> if the glyph ID
    /// is out of range, the program has no widths, or the glyph data is malformed.
    /// </returns>
    public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth)
    {
        advanceWidth = 0;
        return this.program switch
        {
            TrueTypeProgram trueType => trueType.TryGetAdvanceWidth(glyphId, out advanceWidth),
            CffProgram cff => cff.TryGetAdvanceWidth(glyphId, out advanceWidth),
            _ => false,
        };
    }

    /// <summary>
    /// Finds the glyph with the given name. The comparison is ordinal.
    /// When more than one glyph has the name, the result is the lowest glyph ID.
    /// </summary>
    /// <param name="glyphName">The glyph name.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns>
    /// <see langword="true"/> if a glyph has the name; otherwise <see langword="false"/>.
    /// A program without glyph names, such as a CID-keyed program, always returns <see langword="false"/>.
    /// </returns>
    public bool TryGetGlyphId(ReadOnlySpan<char> glyphName, out ushort glyphId)
    {
        glyphId = 0;
        return this.program switch
        {
            TrueTypeProgram trueType => trueType.TryGetGlyphId(glyphName, out glyphId),
            CffProgram cff => cff.Font.TryGetGlyphId(glyphName, out glyphId),
            _ => false,
        };
    }

    /// <summary>
    /// Gets the name of a glyph.
    /// </summary>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns>The name, or <see langword="null"/> if the glyph has no name or the glyph ID is out of range.</returns>
    public string? GetGlyphName(ushort glyphId)
        => this.program switch
        {
            TrueTypeProgram trueType => trueType.GetGlyphName(glyphId),
            CffProgram cff => cff.Font.GetGlyphName(glyphId),
            _ => null,
        };

    /// <summary>
    /// Finds the glyph for a character identifier (CID).
    /// A CID-keyed program maps the CID through its charset.
    /// Any other program maps a CID to the glyph with the same ID.
    /// </summary>
    /// <param name="cid">The CID.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns><see langword="true"/> if the program has a glyph for the CID; otherwise <see langword="false"/>.</returns>
    public bool TryGetGlyphIdForCid(int cid, out ushort glyphId)
    {
        if (this.program is CffProgram cff)
        {
            return cff.Font.TryGetGlyphIdForCid(cid, out glyphId);
        }

        bool found = (uint)cid < (uint)this.GlyphCount;
        glyphId = found ? (ushort)cid : (ushort)0;
        return found;
    }

    /// <summary>
    /// Gets the glyph for each of the 256 character codes under the encoding that the program carries.
    /// </summary>
    /// <remarks>
    /// A CFF program uses its Standard, Expert or custom encoding.
    /// A TrueType program uses its (3,0) 'cmap' subtable by the low byte of each code, or else its (1,0) subtable.
    /// </remarks>
    /// <param name="glyphIds">
    /// Receives the glyph ID for each code in its first 256 elements, 0 where a code has no glyph.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the program has an encoding. <see langword="false"/>, with nothing written,
    /// if it has none: a CID-keyed program, or a TrueType program without either subtable.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="glyphIds"/> has fewer than 256 elements.</exception>
    public bool TryGetBuiltInEncoding(Span<ushort> glyphIds)
    {
        if (glyphIds.Length < 256)
        {
            throw new ArgumentException("The span must have at least 256 elements.", nameof(glyphIds));
        }

        return this.program switch
        {
            TrueTypeProgram trueType => trueType.TryGetBuiltInEncoding(glyphIds),
            CffProgram cff => cff.Font.TryGetBuiltInEncoding(glyphIds),
            _ => false,
        };
    }
}
