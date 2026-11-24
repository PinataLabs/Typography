// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.FontPrograms.Type1;

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
/// A TrueType or CFF font program keeps the data that it was loaded from and reads that data on each call.
/// A Type 1 font program keeps a decrypted copy of the part that holds its glyphs, and not the data.
/// A font program holds no state that changes after load, so one instance can serve many threads at once.
/// </para>
/// </remarks>
public sealed class FontProgram
{
    // A TrueTypeProgram, a CffProgram or a Type1Program. Each member selects on the type, because a generic virtual
    // method with a struct type argument does not work with every ahead-of-time compiler.
    private readonly object program;

    private FontProgram(TrueTypeProgram program, long retainedByteCount)
    {
        this.RetainedByteCount = retainedByteCount;
        this.program = program;
        this.Format = FontProgramFormat.TrueType;
        this.GlyphCount = program.GlyphCount;
        this.FontMatrix = Matrix3x2.CreateScale(1F / program.UnitsPerEm);
        this.MissingOptionalTables = program.MissingTables;
        this.MalformedOptionalTables = program.MalformedTables;
    }

    private FontProgram(CffProgram program, long retainedByteCount)
    {
        this.RetainedByteCount = retainedByteCount;
        this.program = program;
        this.Format = FontProgramFormat.Cff;
        this.GlyphCount = program.Font.GlyphCount;
        this.FontMatrix = program.Font.FontMatrix;
        this.IsCidKeyed = program.Font.IsCidKeyed;
        this.MissingOptionalTables = program.MissingTables;
        this.MalformedOptionalTables = program.MalformedTables;
    }

    private FontProgram(Type1Program program, long retainedByteCount)
    {
        this.RetainedByteCount = retainedByteCount;
        this.program = program;
        this.Format = FontProgramFormat.Type1;
        this.GlyphCount = program.GlyphCount;
        this.FontMatrix = program.FontMatrix;
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
    /// For CFF it is the FontMatrix of the Top DICT, and for Type 1 the <c>/FontMatrix</c> of the font.
    /// Both scale by 0.001 by default.
    /// </summary>
    public Matrix3x2 FontMatrix { get; }

    /// <summary>
    /// Gets the optional tables that the font does not have, for diagnostics. The value is fixed at load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A font subset, such as one embedded in a PDF file, often leaves out tables that reading outlines does not need.
    /// <see cref="Load(ReadOnlyMemory{byte})"/> accepts such a font. A table that is missing is treated as absent,
    /// so for example a TrueType program without a 'post' table has no glyph names, and one without 'hhea' and 'hmtx' has no advance widths.
    /// </para>
    /// <para>
    /// This property and <see cref="MalformedOptionalTables"/> never have a table in common. A Type 1 program has no sfnt tables
    /// and reports <see cref="OptionalFontTables.None"/>. A table that the font has but that cannot be used is in
    /// <see cref="MalformedOptionalTables"/>.
    /// </para>
    /// </remarks>
    public OptionalFontTables MissingOptionalTables { get; }

    /// <summary>
    /// Gets the optional tables that the font has but that were ignored because they are too damaged to use,
    /// for diagnostics. The value is fixed at load.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A table is malformed when its header is truncated, or when the structure that reading it needs does not fit in its data.
    /// A malformed table is treated as absent and never fails the load.
    /// The 'name' and 'OS/2' tables are never read, so the check for them covers only the header size.
    /// The 'hhea' table is malformed when it is shorter than 36 bytes or says the font has no metrics,
    /// and 'hmtx' when it is shorter than one metric. A font has advance widths only when both are usable.
    /// The 'post' table is malformed when it is shorter than its header or has a version that the OpenType specification does not define.
    /// The 'cmap' table is malformed when its version is not 0 or its list of encoding records does not fit.
    /// The check does not read the data that those tables hold, so a damaged entry inside a usable table makes a lookup fail, as before.
    /// </para>
    /// <para>
    /// The 'head', 'maxp', 'loca' and 'glyf' tables of TrueType, and the 'CFF ' table of an OpenType file, are required.
    /// A font without one, or with one that is malformed, fails to load.
    /// </para>
    /// </remarks>
    public OptionalFontTables MalformedOptionalTables { get; }

    /// <summary>
    /// Gets the bytes that this font program keeps reachable. The value is fixed at load and never changes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is the length of the data that was passed to <c>Load</c> when the program keeps that data,
    /// plus, for each array that the program allocated, 24 bytes for the array header plus the size of its elements.
    /// A TrueType program allocates no array. A CFF program allocates one array of 2 bytes per glyph
    /// for its charset and one array of Font DICT records, one record for each Font DICT.
    /// A Type 1 program allocates the decrypted private part of the font, which holds the glyphs, two arrays of
    /// 4 bytes per glyph and two of 4 bytes per subroutine index, up to the highest that the font defines, for where
    /// they lie in it, one array of 4 bytes, one of
    /// 1 byte and one of 2 bytes per glyph for the glyph names and their sorted order, and one array of 256 glyph IDs
    /// for its encoding when it has one.
    /// The program makes no other allocation that outlives <c>Load</c>, and it never caches.
    /// </para>
    /// <para>
    /// A TrueType or CFF program keeps the memory that it was given, so it counts the length of that memory.
    /// When the memory is a slice of a larger array, the whole array stays alive and only the slice is counted.
    /// A Type 1 program does not keep the memory, so it does not count it.
    /// </para>
    /// <para>
    /// The size of each element, and of a Font DICT record, is what <c>Unsafe.SizeOf</c> gives on the running runtime.
    /// The array header is the 24 bytes of 64-bit CoreCLR. Another runtime, such as Mono, IL2CPP or a 32-bit process,
    /// may use a few bytes more or less for each array. A TrueType or CFF program has at most two arrays and a Type 1
    /// program at most nine.
    /// </para>
    /// <para>
    /// The objects of the program itself are not counted: the <see cref="FontProgram"/>, its TrueType, CFF or Type 1
    /// program, and the parsed CFF font. They are a fixed amount for each font whatever its size.
    /// Measured on 64-bit CoreCLR, a load allocated under 320 bytes for them, whatever the font.
    /// A caller that bounds the memory of many fonts can add 1024 bytes for each font.
    /// </para>
    /// </remarks>
    public long RetainedByteCount { get; }

    /// <summary>
    /// Loads a font program. The format is detected from the data.
    /// </summary>
    /// <remarks>
    /// The supported formats are an sfnt file with TrueType outlines in a 'glyf' table, an
    /// OpenType file with a 'CFF ' table, and a Type 1 font as a PFB file, a PFA file or the stream of a
    /// PDF <c>/FontFile</c>. WOFF, WOFF2, font collections and CFF2 are not supported.
    /// A Type 1 font is found from its data alone, so the <c>/Length1</c>, <c>/Length2</c> and <c>/Length3</c>
    /// of a PDF stream are not needed.
    /// </remarks>
    /// <param name="data">
    /// The font file. A TrueType or CFF program keeps this memory and reads it on each call. It does not copy it.
    /// Do not change or reuse the memory while the program is in use. A Type 1 program does not keep the memory
    /// after <c>Load</c> returns.
    /// </param>
    /// <returns>The <see cref="FontProgram"/>.</returns>
    /// <exception cref="InvalidFontFileException">The data is malformed or is not in a supported format.</exception>
    public static FontProgram Load(ReadOnlyMemory<byte> data)
        => LoadCore(data, null);

    /// <summary>
    /// Loads a font program and refuses one that would retain more than the limit of the options.
    /// </summary>
    /// <remarks>
    /// The limit is checked before each allocation and before any parsing, so a font that is too large
    /// is refused before it is parsed. See <see cref="Load(ReadOnlyMemory{byte})"/> for the formats.
    /// </remarks>
    /// <param name="data">The font file. See <see cref="Load(ReadOnlyMemory{byte})"/>.</param>
    /// <param name="options">The options.</param>
    /// <returns>The <see cref="FontProgram"/>.</returns>
    /// <exception cref="InvalidFontFileException">The data is malformed or is not in a supported format.</exception>
    /// <exception cref="FontProgramTooLargeException">
    /// The program would retain more than <see cref="FontProgramOptions.MaxRetainedBytes"/>.
    /// This type derives from <see cref="InvalidFontFileException"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public static FontProgram Load(ReadOnlyMemory<byte> data, FontProgramOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return LoadCore(data, options.MaxRetainedBytes);
    }

    private static FontProgram LoadCore(ReadOnlyMemory<byte> data, long? maxRetainedBytes)
    {
        if (Type1Program.IsType1(data.Span))
        {
            return LoadType1(data, maxRetainedBytes);
        }

        LoadBudget budget = new(data.Length, maxRetainedBytes);
        SfntDirectory directory = SfntDirectory.Read(data);
        return directory.Version switch
        {
            SfntDirectory.TrueTypeVersion or SfntDirectory.AppleTrueTypeVersion => new FontProgram(TrueTypeProgram.Load(directory), budget.RetainedBytes),
            SfntDirectory.OpenTypeCffVersion => LoadCff(directory, ref budget),
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
            Type1Program type1 => type1.TryWriteOutline(glyphId, ref sink),
            _ => false,
        };

    private static FontProgram LoadCff(SfntDirectory directory, ref LoadBudget budget)
    {
        CffProgram program = CffProgram.Load(directory, ref budget);
        return new FontProgram(program, budget.RetainedBytes);
    }

    private static FontProgram LoadType1(ReadOnlyMemory<byte> data, long? maxRetainedBytes)
    {
        LoadBudget budget = new(0, maxRetainedBytes);
        Type1Program program = Type1Program.Load(data, ref budget);
        return new FontProgram(program, budget.RetainedBytes);
    }

    /// <summary>
    /// Gets the advance width of a glyph in glyph space.
    /// The width comes from the 'hmtx' table, or from the charstring of a CFF glyph when the font has no 'hmtx' table.
    /// For Type 1 it comes from the <c>hsbw</c> or <c>sbw</c> of the charstring.
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
            Type1Program type1 => type1.TryGetAdvanceWidth(glyphId, out advanceWidth),
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
            Type1Program type1 => type1.TryGetGlyphId(glyphName, out glyphId),
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
            Type1Program type1 => type1.GetGlyphName(glyphId),
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
    /// A Type 1 program uses its <c>/Encoding</c>: StandardEncoding, or the array of glyph names that it defines.
    /// </remarks>
    /// <param name="glyphIds">
    /// Receives the glyph ID for each code in its first 256 elements, 0 where a code has no glyph.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the program has an encoding. <see langword="false"/>, with nothing written,
    /// if it has none: a CID-keyed program, a TrueType program without either subtable, or a Type 1 program
    /// without an <c>/Encoding</c>.
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
            Type1Program type1 => type1.TryGetBuiltInEncoding(glyphIds),
            _ => false,
        };
    }
}
