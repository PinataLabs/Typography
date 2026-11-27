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

    // For each Font DICT that owns its local Subrs INDEX (see CffTableMap.SubrsOwners), the length of the data of the INDEX.
    private readonly long[] localSubrsLengths;

    // For each Font DICT that owns its Private DICT, the length of the DICT and its offset, as the layout places it.
    private readonly int[] privateLengths;
    private readonly int[] privateOffsets;

    // For each Font DICT that owns its local Subrs INDEX, the offset of the INDEX, as the layout places it.
    private readonly int[] subrsOffsets;

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
        this.privateLengths = budget.Allocate<int>(fontDictCount);
        this.privateOffsets = budget.Allocate<int>(fontDictCount);
        this.subrsOffsets = budget.Allocate<int>(fontDictCount);

        this.globalSubrsLength = GetSubrsLength(font.GlobalSubrs, closure.GlobalSubrs);
        for (int i = 0; i < fontDictCount; i++)
        {
            this.localSubrsLengths[i] = map.SubrsOwners[i] == i ? GetSubrsLength(font.GetLocalSubrs(i), closure.LocalSubrs[i]) : 0;
        }

        for (int i = 0; i < font.GlyphCount; i++)
        {
            this.charStringsLength += closure.Glyphs[i] ? GetLength(font.CharStrings, i) : EndChar.Length;
        }

        // The first pass finds where each part goes, and the length of the table. Font DICTs that share a Private DICT or a local Subrs INDEX
        // share one copy, so the table is the size of the parts that the font has. A font that is built so that its parts overlap
        // can still be larger than the font, so the length is counted in 64 bits.
        long length = this.Emit(default);
        this.Length = length <= int.MaxValue ? (int)length : throw new InvalidFontFileException("The CFF table of the subset would be larger than 2 GB.");
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
        CffTableMap map = CffTableMap.Read(table, font, ref budget);
        CffClosure closure = CffClosure.Compute(font, map, glyphIds, ref budget);
        return new CffSubset(font, closure, map, ref budget);
    }

    /// <inheritdoc />
    public void Write(Span<byte> destination)
    {
        long length = this.Emit(destination);
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
    private long Emit(Span<byte> destination)
    {
        CffEmitter emitter = new(destination);
        emitter.Write(this.map.Header.Span);

        // The first font only: the Name INDEX and the Top DICT INDEX each hold one object. The name can have no bytes.
        if (this.map.HasName)
        {
            emitter.BeginIndex(1, this.map.Name.Length);
            emitter.AddObject(this.map.Name.Span);
        }
        else
        {
            emitter.BeginIndex(0, 0);
        }

        CffDictOffsets top = this.GetTopOffsets();
        emitter.BeginIndex(1, CffDictRewriter.Rewrite(this.map.TopDict.Span, CffDictKind.Top, in top, default));
        emitter.AddDict(this.map.TopDict.Span, CffDictKind.Top, in top);

        emitter.Write(this.map.Strings.Span);
        WriteSubrs(ref emitter, this.font.GlobalSubrs, this.closure.GlobalSubrs, this.globalSubrsLength);

        this.charsetOffset = (int)emitter.Position;
        emitter.Write(this.map.CharsetData.Span);
        this.fdSelectOffset = (int)emitter.Position;
        emitter.Write(this.map.FdSelect.Span);
        this.encodingOffset = (int)emitter.Position;
        emitter.Write(this.map.EncodingData.Span);

        this.charStringsOffset = (int)emitter.Position;
        emitter.BeginIndex(this.font.GlyphCount, this.charStringsLength);
        for (int i = 0; i < this.font.GlyphCount; i++)
        {
            ReadOnlyMemory<byte> charString = default;
            emitter.AddObject(this.closure.Glyphs[i] && this.font.CharStrings.TryGet(i, out charString) ? charString.Span : EndChar);
        }

        if (this.map.HasFdArray)
        {
            this.fdArrayOffset = (int)emitter.Position;
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

        // Each Private DICT that a Font DICT names, and each local Subrs INDEX that a Private DICT names, is written once, however many
        // Font DICTs name it. The Subrs operator of a Private DICT gives the distance to the INDEX, which is always ahead of the DICT.
        for (int i = 0; i < this.map.FontDicts.Length; i++)
        {
            if (this.map.PrivateOwners[i] != i)
            {
                continue;
            }

            ReadOnlySpan<byte> privateDict = this.map.FontDicts[i].Private.Span;
            int offset = (int)emitter.Position;
            int subrsOwner = this.map.SubrsOwners[i];
            CffDictOffsets offsets = new() { Subrs = subrsOwner < 0 ? 0 : this.subrsOffsets[subrsOwner] - offset };
            this.privateLengths[i] = emitter.WriteDict(privateDict, CffDictKind.Private, in offsets);
            this.privateOffsets[i] = offset;
        }

        for (int i = 0; i < this.map.FontDicts.Length; i++)
        {
            if (this.map.SubrsOwners[i] == i)
            {
                this.subrsOffsets[i] = (int)emitter.Position;
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
            PrivateSize = this.map.HasFdArray ? 0 : this.GetPrivateLength(0),
            PrivateOffset = this.map.HasFdArray ? 0 : this.GetPrivateOffset(0),
        };

    private CffDictOffsets GetFontDictOffsets(int fontDict)
        => new() { PrivateSize = this.GetPrivateLength(fontDict), PrivateOffset = this.GetPrivateOffset(fontDict) };

    // A Font DICT that has no Private DICT names none. One that shares a Private DICT names the copy that its owner has.
    private int GetPrivateLength(int fontDict)
        => this.map.PrivateOwners[fontDict] < 0 ? 0 : this.privateLengths[this.map.PrivateOwners[fontDict]];

    private int GetPrivateOffset(int fontDict)
        => this.map.PrivateOwners[fontDict] < 0 ? 0 : this.privateOffsets[this.map.PrivateOwners[fontDict]];
}
