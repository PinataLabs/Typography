// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// A CFF table that keeps the glyphs of a closure at their glyph IDs. The charstring of a glyph that is dropped is a lone
/// <c>endchar</c>, and the body of a subroutine that no kept glyph calls is a lone <c>return</c>. The glyphs and the
/// subroutines keep their positions, so no charstring changes and no bias changes.
/// </summary>
/// <remarks>
/// The table is laid out again from the Top DICT outwards, so it holds nothing that the Top DICT does not reach.
/// The charset, the encoding, the FDSelect and the String INDEX are copied as they are, so glyph names,
/// SIDs, CIDs and the Font DICT of each glyph stay the same.
/// </remarks>
internal sealed class CffSubset : ISubsetTable
{
    // The charstring of a dropped glyph, and the body of a subroutine that no kept glyph calls.
    private static readonly byte[] EndChar = [0x0E];
    private static readonly byte[] Return = [0x0B];

    private readonly CffFont font;
    private readonly CffClosure closure;
    private readonly CffTableMap map;

    private readonly long globalSubrsLength;
    private readonly long charStringsLength;

    // For each Font DICT, the length of the data of its local Subrs INDEX.
    private readonly long[] localSubrsLengths;

    // For each Font DICT, the length and the offset of its Private DICT, as the layout places it.
    private readonly int[] privates;

    // Where the layout placed each part. A pass over the layout sets them, and the next pass writes them into the DICTs.
    private int charsetOffset;
    private int encodingOffset;
    private int fdSelectOffset;
    private int charStringsOffset;
    private int fdArrayOffset;

    private CffSubset(CffFont font, CffClosure closure, CffTableMap map, ref LoadBudget budget)
    {
        this.font = font;
        this.closure = closure;
        this.map = map;

        int fontDictCount = font.FontDictCount;
        this.localSubrsLengths = budget.Allocate<long>(fontDictCount);
        this.privates = budget.Allocate<int>(2 * fontDictCount);

        this.globalSubrsLength = GetSubrsLength(font.GlobalSubrs, closure.GlobalSubrs);
        for (int i = 0; i < fontDictCount; i++)
        {
            this.localSubrsLengths[i] = map.FontDicts[i].HasSubrs ? GetSubrsLength(font.GetLocalSubrs(i), closure.LocalSubrs[i]) : 0;
        }

        for (int i = 0; i < font.GlyphCount; i++)
        {
            this.charStringsLength += closure.Glyphs[i] ? GetLength(font.CharStrings, i) : EndChar.Length;
        }

        // The first pass finds where each part goes, and the length of the table.
        this.Length = this.Emit(default);
    }

    /// <summary>
    /// Gets the length of the table in bytes.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Finds what a set of glyphs needs from a CFF table, and lays out the table that keeps only that.
    /// </summary>
    /// <param name="table">The CFF table.</param>
    /// <param name="glyphIds">The glyphs to keep.</param>
    /// <param name="budget">The budget that the arrays are charged to.</param>
    /// <returns>The new table.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A glyph ID is not a glyph of the font.</exception>
    /// <exception cref="InvalidFontFileException">The table is malformed, or the charstring of a kept glyph is.</exception>
    /// <exception cref="FontProgramTooLargeException">The arrays do not fit under the cap of the budget.</exception>
    public static CffSubset Create(ReadOnlyMemory<byte> table, IReadOnlyCollection<ushort> glyphIds, ref LoadBudget budget)
    {
        CffFont font = CffParser.Load(table, ref budget);
        CffClosure closure = CffClosure.Compute(font, glyphIds, ref budget);
        CffTableMap map = CffTableMap.Read(table, font, ref budget);
        return new CffSubset(font, closure, map, ref budget);
    }

    /// <inheritdoc />
    public void Write(Span<byte> destination)
    {
        int length = this.Emit(destination);
        Debug.Assert(length == this.Length, "The second pass over the layout gave another length.");
    }

    private static long GetSubrsLength(CffIndex subrs, bool[] used)
    {
        long length = 0;
        for (int i = 0; i < subrs.Count; i++)
        {
            length += used[i] ? GetLength(subrs, i) : Return.Length;
        }

        return length;
    }

    private static int GetLength(CffIndex index, int i)
        => index.TryGetRange(i, out _, out int length) ? length : throw new InvalidFontFileException("An object of a CFF INDEX is out of range.");

    private static void WriteSubrs(ref CffEmitter emitter, CffIndex subrs, bool[] used, long dataLength)
    {
        emitter.BeginIndex(subrs.Count, dataLength);
        for (int i = 0; i < subrs.Count; i++)
        {
            ReadOnlyMemory<byte> subr = default;
            emitter.AddObject(used[i] && subrs.TryGet(i, out subr) ? subr.Span : Return);
        }
    }

    // Writes the table, or measures it when the destination is empty.
    private int Emit(Span<byte> destination)
    {
        CffEmitter emitter = new(destination);
        emitter.Write(this.map.Header.Span);

        // The first font only: the Name INDEX and the Top DICT INDEX each hold one object.
        if (this.map.Name.IsEmpty)
        {
            emitter.BeginIndex(0, 0);
        }
        else
        {
            emitter.BeginIndex(1, this.map.Name.Length);
            emitter.AddObject(this.map.Name.Span);
        }

        CffDictOffsets top = this.GetTopOffsets();
        emitter.BeginIndex(1, CffDictRewriter.Rewrite(this.map.TopDict.Span, CffDictKind.Top, in top, default));
        emitter.AddDict(this.map.TopDict.Span, CffDictKind.Top, in top);

        emitter.Write(this.map.Strings.Span);
        WriteSubrs(ref emitter, this.font.GlobalSubrs, this.closure.GlobalSubrs, this.globalSubrsLength);

        this.charsetOffset = emitter.Position;
        emitter.Write(this.map.CharsetData.Span);
        this.fdSelectOffset = emitter.Position;
        emitter.Write(this.map.FdSelect.Span);
        this.encodingOffset = emitter.Position;
        emitter.Write(this.map.EncodingData.Span);

        this.charStringsOffset = emitter.Position;
        emitter.BeginIndex(this.font.GlyphCount, this.charStringsLength);
        for (int i = 0; i < this.font.GlyphCount; i++)
        {
            ReadOnlyMemory<byte> charString = default;
            emitter.AddObject(this.closure.Glyphs[i] && this.font.CharStrings.TryGet(i, out charString) ? charString.Span : EndChar);
        }

        if (this.map.HasFdArray)
        {
            this.fdArrayOffset = emitter.Position;
            long fontDictsLength = 0;
            for (int i = 0; i < this.map.FontDicts.Length; i++)
            {
                CffDictOffsets offsets = this.GetFontDictOffsets(i);
                fontDictsLength += CffDictRewriter.Rewrite(this.map.FontDicts[i].Dict.Span, CffDictKind.FontDict, in offsets, default);
            }

            emitter.BeginIndex(this.map.FontDicts.Length, fontDictsLength);
            for (int i = 0; i < this.map.FontDicts.Length; i++)
            {
                CffDictOffsets offsets = this.GetFontDictOffsets(i);
                emitter.AddDict(this.map.FontDicts[i].Dict.Span, CffDictKind.FontDict, in offsets);
            }
        }

        // Each Private DICT is followed by its local Subrs INDEX, which its Subrs operator names by the length of the DICT.
        for (int i = 0; i < this.map.FontDicts.Length; i++)
        {
            CffTableMap.FontDictMap fontDict = this.map.FontDicts[i];
            if (fontDict.Private.IsEmpty)
            {
                this.privates[2 * i] = 0;
                this.privates[(2 * i) + 1] = 0;
                continue;
            }

            int offset = emitter.Position;
            CffDictOffsets measure = default;
            int length = CffDictRewriter.Rewrite(fontDict.Private.Span, CffDictKind.Private, in measure, default);
            CffDictOffsets offsets = new() { Subrs = length };
            emitter.WriteDict(fontDict.Private.Span, CffDictKind.Private, in offsets);
            this.privates[2 * i] = length;
            this.privates[(2 * i) + 1] = offset;
            if (fontDict.HasSubrs)
            {
                WriteSubrs(ref emitter, this.font.GetLocalSubrs(i), this.closure.LocalSubrs[i], this.localSubrsLengths[i]);
            }
        }

        return emitter.Position;
    }

    private CffDictOffsets GetTopOffsets()
        => new()
        {
            Charset = this.map.Charset < 3 ? this.map.Charset : this.charsetOffset,
            Encoding = this.map.Encoding < 2 ? this.map.Encoding : this.encodingOffset,
            CharStrings = this.charStringsOffset,
            FdArray = this.map.HasFdArray ? this.fdArrayOffset : 0,
            FdSelect = this.map.FdSelect.IsEmpty ? 0 : this.fdSelectOffset,

            // A font with an FDArray takes its Private DICTs from the Font DICTs and ignores the one of the Top DICT.
            PrivateSize = this.map.HasFdArray ? 0 : this.privates[0],
            PrivateOffset = this.map.HasFdArray ? 0 : this.privates[1],
        };

    private CffDictOffsets GetFontDictOffsets(int fontDict)
        => new() { PrivateSize = this.privates[2 * fontDict], PrivateOffset = this.privates[(2 * fontDict) + 1] };
}
