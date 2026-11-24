// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// A Type 1 font program: PFB, PFA, or the stream of a PDF <c>/FontFile</c>.
/// </summary>
/// <remarks>
/// <para>
/// Glyph 0 is <c>.notdef</c>. The other glyphs follow in the order of the <c>/CharStrings</c> dictionary.
/// A font program without <c>.notdef</c> gets an empty one.
/// </para>
/// <para>
/// The program owns one buffer, the decrypted private part with its charstrings decrypted in place, and
/// arrays of offsets into it. It does not keep the data it was loaded from, and it caches nothing after the load.
/// </para>
/// </remarks>
internal sealed class Type1Program
{
    /// <summary>
    /// The name offset of a <c>.notdef</c> glyph that the font program does not define.
    /// </summary>
    internal const int SynthesizedName = -1;

    private const string Notdef = ".notdef";

    private readonly byte[] data;
    private readonly Type1CharStringTable glyphs;
    private readonly Type1CharStringTable subrs;
    private readonly int[] nameOffsets;
    private readonly byte[] nameLengths;

    // The glyph IDs sorted by name in byte order, then by ID, so a search finds the lowest ID of a name.
    private readonly ushort[] nameIndex;
    private readonly ushort[]? encoding;

    internal Type1Program(
        byte[] data,
        in Type1Header header,
        ReadOnlySpan<byte> clearText,
        Type1CharStringTable glyphs,
        int[] nameOffsets,
        byte[] nameLengths,
        Type1CharStringTable subrs,
        ref LoadBudget budget)
    {
        this.data = data;
        this.glyphs = glyphs;
        this.subrs = subrs;
        this.nameOffsets = nameOffsets;
        this.nameLengths = nameLengths;
        this.nameIndex = this.BuildNameIndex(ref budget);
        this.FontMatrix = header.FontMatrix;
        this.FontBBox = header.FontBBox;
        this.PaintType = header.PaintType;
        this.StrokeWidth = header.StrokeWidth;
        this.encoding = this.ResolveEncoding(in header, clearText, ref budget);
    }

    /// <summary>
    /// Gets the matrix from glyph space to ems. 0.001 scale when the font program gives none.
    /// </summary>
    public Matrix3x2 FontMatrix { get; }

    public Bounds FontBBox { get; }

    /// <summary>
    /// Gets the <c>/PaintType</c>: 0 for filled glyphs, 2 for stroked ones.
    /// </summary>
    public int PaintType { get; }

    /// <summary>
    /// Gets the <c>/StrokeWidth</c> in glyph space units, 0 when the font gives none.
    /// </summary>
    public float StrokeWidth { get; }

    public int GlyphCount => this.glyphs.Count;

    /// <summary>
    /// Tells whether data starts like a Type 1 font program: a PFB segment, or a PostScript <c>%!</c> comment
    /// after any PostScript white space.
    /// </summary>
    /// <param name="data">The font file.</param>
    /// <returns><see langword="true"/> if <see cref="Load"/> should be tried.</returns>
    public static bool IsType1(ReadOnlySpan<byte> data)
    {
        if (Type1Eexec.IsPfb(data))
        {
            return true;
        }

        int start = 0;
        while (start < data.Length && Type1Tokenizer.IsWhiteSpace(data[start]))
        {
            start++;
        }

        return data.Slice(start).StartsWith("%!"u8);
    }

    /// <summary>
    /// Loads a Type 1 font program from PFB, PFA or PDF <c>/FontFile</c> data.
    /// </summary>
    /// <param name="data">The font file. The program does not keep it.</param>
    /// <param name="budget">
    /// The budget that every array the program keeps is charged to, before it is allocated.
    /// The data is not charged, because the program does not keep it.
    /// </param>
    /// <returns>The font program.</returns>
    /// <exception cref="InvalidFontFileException">The data is not a Type 1 font program, or is malformed.</exception>
    /// <exception cref="FontProgramTooLargeException">The program does not fit under the cap of the budget.</exception>
    public static Type1Program Load(ReadOnlyMemory<byte> data, ref LoadBudget budget)
    {
        ReadOnlySpan<byte> file = data.Span;
        byte[]? privatePart = null;
        ReadOnlySpan<byte> clearText = file;
        if (Type1Eexec.IsPfb(file))
        {
            clearText = Type1Eexec.GetPfbClearText(file, out int binaryPosition);
            privatePart = Type1Eexec.DecryptPfbBinary(file, binaryPosition, ref budget);
        }

        Type1Header header = new(stackalloc int[256], stackalloc int[256]);
        Type1Parser.ParseHeader(clearText, ref header);
        if (privatePart is null)
        {
            int start = header.EexecEnd;
            if (start <= 0 || start >= clearText.Length)
            {
                throw new InvalidFontFileException("The data is not a Type 1 font program: it has no eexec section.");
            }

            privatePart = Type1Eexec.DecryptSection(clearText.Slice(start), ref budget);
        }

        return Type1Parser.ParsePrivate(privatePart, in header, clearText, ref budget);
    }

    /// <summary>
    /// Writes the unhinted outline of a glyph in glyph space, y up. Each contour is MoveTo, segments, ClosePath.
    /// </summary>
    /// <typeparam name="TSink">The type of the sink.</typeparam>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="sink">The sink.</param>
    /// <returns>
    /// <see langword="false"/> for an out-of-range ID or malformed glyph data. The sink may then hold a partial outline.
    /// </returns>
    public bool TryWriteOutline<TSink>(ushort glyphId, ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (this.IsSynthesizedNotdef(glyphId))
        {
            return true;
        }

        if (!this.glyphs.TryGet(this.data, glyphId, out ReadOnlySpan<byte> charString))
        {
            return false;
        }

        Span<double> scratch = stackalloc double[Type1CharStringEngine.ScratchLength];
        Type1CharStringEngine engine = new(this, scratch);
        return engine.TryWriteOutline(charString, ref sink);
    }

    /// <summary>
    /// Gets the advance width that the glyph's <c>hsbw</c> or <c>sbw</c> gives, in glyph space.
    /// </summary>
    /// <param name="glyphId">The glyph ID.</param>
    /// <param name="advanceWidth">The advance width.</param>
    /// <returns><see langword="false"/> for an out-of-range ID or a glyph that sets no width.</returns>
    public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth)
    {
        advanceWidth = 0;
        if (this.IsSynthesizedNotdef(glyphId))
        {
            return true;
        }

        if (!this.glyphs.TryGet(this.data, glyphId, out ReadOnlySpan<byte> charString))
        {
            return false;
        }

        Span<double> scratch = stackalloc double[Type1CharStringEngine.ScratchLength];
        Type1CharStringEngine engine = new(this, scratch);
        return engine.TryGetAdvanceWidth(charString, out advanceWidth);
    }

    /// <summary>
    /// Finds a glyph by name. The lowest ID wins when the font program defines a name twice.
    /// </summary>
    /// <param name="glyphName">The glyph name.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns><see langword="true"/> if the font program has the glyph.</returns>
    public bool TryGetGlyphId(ReadOnlySpan<char> glyphName, out ushort glyphId)
    {
        int low = 0;
        int high = this.nameIndex.Length;
        while (low < high)
        {
            int middle = (int)((uint)(low + high) >> 1);
            if (CompareName(this.GetName(this.nameIndex[middle]), glyphName) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        if (low < this.nameIndex.Length && CompareName(this.GetName(this.nameIndex[low]), glyphName) == 0)
        {
            glyphId = this.nameIndex[low];
            return true;
        }

        glyphId = 0;
        return false;
    }

    public string? GetGlyphName(ushort glyphId)
    {
        if (glyphId >= this.nameOffsets.Length)
        {
            return null;
        }

        return this.IsSynthesizedNotdef(glyphId) ? Notdef : DecodeName(this.GetName(glyphId));
    }

    /// <summary>
    /// Fills <paramref name="glyphIds"/> with the glyph for each code under the font program's own encoding, 0 where unencoded.
    /// </summary>
    /// <param name="glyphIds">At least 256 glyph IDs.</param>
    /// <returns><see langword="false"/>, with nothing written, when the font program defines no encoding.</returns>
    public bool TryGetBuiltInEncoding(Span<ushort> glyphIds)
    {
        if (glyphIds.Length < 256)
        {
            throw new ArgumentException("The span must hold 256 glyph IDs.", nameof(glyphIds));
        }

        if (this.encoding is null)
        {
            return false;
        }

        this.encoding.CopyTo(glyphIds);
        return true;
    }

    // PostScript names are bytes. Each byte maps to the character with the same value.
    internal static string DecodeName(ReadOnlySpan<byte> name)
    {
        Span<char> characters = name.Length <= 256 ? stackalloc char[name.Length] : new char[name.Length];
        for (int i = 0; i < name.Length; i++)
        {
            characters[i] = (char)name[i];
        }

        return new string(characters);
    }

    internal bool TryGetSubr(int index, out ReadOnlySpan<byte> charString)
        => this.subrs.TryGet(this.data, index, out charString);

    /// <summary>
    /// Gets the charstring of the glyph that StandardEncoding gives a code, which is how <c>seac</c> names its parts.
    /// </summary>
    /// <param name="code">The StandardEncoding code.</param>
    /// <param name="charString">The charstring.</param>
    /// <returns><see langword="true"/> if the font program has the glyph.</returns>
    internal bool TryGetStandardEncodingCharString(int code, out ReadOnlySpan<byte> charString)
    {
        charString = default;
        return (uint)code < 256
            && this.TryGetGlyphId(StandardEncodingName(code), out ushort glyphId)
            && this.glyphs.TryGet(this.data, glyphId, out charString);
    }

    // Adobe StandardEncoding is the same in Type 1 and CFF, so the CFF table of string IDs serves both.
    private static string StandardEncodingName(int code)
        => CffStandardStrings.GetName(CffPredefinedTables.StandardEncoding[code]);

    // Orders a name of bytes against a name of characters as if each byte were the character with the same value.
    private static int CompareName(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> name)
    {
        int length = Math.Min(bytes.Length, name.Length);
        for (int i = 0; i < length; i++)
        {
            int difference = bytes[i] - name[i];
            if (difference != 0)
            {
                return difference;
            }
        }

        return bytes.Length - name.Length;
    }

    private bool IsSynthesizedNotdef(int glyphId) => glyphId == 0 && this.nameOffsets[0] == SynthesizedName;

    private ReadOnlySpan<byte> GetName(int glyphId)
        => this.IsSynthesizedNotdef(glyphId) ? ".notdef"u8 : this.data.AsSpan(this.nameOffsets[glyphId], this.nameLengths[glyphId]);

    private ushort[] BuildNameIndex(ref LoadBudget budget)
    {
        ushort[] index = budget.Allocate<ushort>(this.nameOffsets.Length);
        for (int i = 0; i < index.Length; i++)
        {
            index[i] = (ushort)i;
        }

        Array.Sort(index, (x, y) =>
        {
            int order = this.GetName(x).SequenceCompareTo(this.GetName(y));
            return order != 0 ? order : x.CompareTo(y);
        });
        return index;
    }

    private ushort[]? ResolveEncoding(in Type1Header header, ReadOnlySpan<byte> clearText, ref LoadBudget budget)
    {
        if (header.EncodingKind == Type1EncodingKind.None)
        {
            return null;
        }

        ushort[] glyphIds = budget.Allocate<ushort>(256);
        Span<char> name = stackalloc char[byte.MaxValue];
        for (int code = 0; code < glyphIds.Length; code++)
        {
            ushort glyphId;
            if (header.EncodingKind == Type1EncodingKind.Standard)
            {
                this.TryGetGlyphId(StandardEncodingName(code), out glyphId);
            }
            else
            {
                // No glyph has a name longer than 255 bytes.
                int length = header.EncodingNameLengths[code];
                if (length is 0 or > byte.MaxValue)
                {
                    continue;
                }

                ReadOnlySpan<byte> bytes = clearText.Slice(header.EncodingNameStarts[code], length);
                for (int i = 0; i < length; i++)
                {
                    name[i] = (char)bytes[i];
                }

                this.TryGetGlyphId(name.Slice(0, length), out glyphId);
            }

            glyphIds[code] = glyphId;
        }

        return glyphIds;
    }
}
