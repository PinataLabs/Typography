// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// What a set of CFF glyphs needs from the font: the glyphs themselves, the seac components that they draw, and the
/// subroutines that their charstrings call. It runs each glyph with the evaluation engine that draws outlines, so
/// the subroutines that it finds are the ones that drawing the glyph calls, with the bias and the nesting of the engine.
/// </summary>
internal sealed class CffClosure : ICffCallObserver
{
    // Glyphs found and not yet run. A glyph is added once, so the glyph count is enough room.
    private readonly ushort[] pending;
    private int pendingCount;

    // The Font DICT of the glyph that is running, which owns the local subroutines that it calls.
    private int fontDict;

    private CffClosure(bool[] glyphs, bool[] globalSubrs, bool[][] localSubrs, ushort[] pending)
    {
        this.Glyphs = glyphs;
        this.GlobalSubrs = globalSubrs;
        this.LocalSubrs = localSubrs;
        this.pending = pending;
    }

    /// <summary>
    /// Gets, for each glyph ID, whether the glyph is kept.
    /// </summary>
    public bool[] Glyphs { get; }

    /// <summary>
    /// Gets, for each global subroutine, whether a kept glyph calls it.
    /// </summary>
    public bool[] GlobalSubrs { get; }

    /// <summary>
    /// Gets, for each Font DICT, whether a kept glyph calls each of its local subroutines. Font DICTs that share one local Subrs INDEX
    /// share one array, which a call from a glyph of any of them marks.
    /// </summary>
    public bool[][] LocalSubrs { get; }

    /// <summary>
    /// Finds the closure of glyphs. Glyph 0 is always in it.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="map">The parts of the table, which say which Font DICTs share a local Subrs INDEX.</param>
    /// <param name="glyphIds">The glyphs to keep.</param>
    /// <param name="budget">The budget that the arrays are charged to.</param>
    /// <returns>The closure.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A glyph ID is not a glyph of the font.</exception>
    /// <exception cref="InvalidFontFileException">The charstring of a kept glyph is malformed.</exception>
    /// <exception cref="FontProgramTooLargeException">The arrays do not fit under the cap of the budget.</exception>
    public static CffClosure Compute(CffFont font, CffTableMap map, IReadOnlyCollection<ushort> glyphIds, ref LoadBudget budget)
    {
        int glyphCount = font.GlyphCount;
        int globalCount = font.GlobalSubrs.Count;
        long size = LoadBudget.SizeOf<bool>(glyphCount)
            + LoadBudget.SizeOf<ushort>(glyphCount)
            + LoadBudget.SizeOf<bool>(globalCount)
            + LoadBudget.SizeOf<bool[]>(font.FontDictCount);
        int[] owners = map.SubrsOwners;
        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] == i)
            {
                size += LoadBudget.SizeOf<bool>(font.GetLocalSubrs(i).Count);
            }
        }

        budget.Reserve(size);
        bool[][] localSubrs = budget.Allocate<bool[]>(font.FontDictCount);
        for (int i = 0; i < localSubrs.Length; i++)
        {
            // The owner of a Font DICT comes before it, so its array is there to share.
            localSubrs[i] = owners[i] < 0 ? [] : owners[i] == i ? budget.Allocate<bool>(font.GetLocalSubrs(i).Count) : localSubrs[owners[i]];
        }

        CffClosure closure = new(budget.Allocate<bool>(glyphCount), budget.Allocate<bool>(globalCount), localSubrs, budget.Allocate<ushort>(glyphCount));
        closure.Add(0);
        foreach (ushort glyphId in glyphIds)
        {
            if (glyphId >= glyphCount)
            {
                throw new ArgumentOutOfRangeException(nameof(glyphIds), glyphId, $"The font has {glyphCount} glyphs, and glyph ID {glyphId} is not one of them.");
            }

            closure.Add(glyphId);
        }

        while (closure.pendingCount > 0)
        {
            int glyphId = closure.pending[--closure.pendingCount];
            if (!font.TryGetFontDict(glyphId, out closure.fontDict) || !font.TryObserveCalls(glyphId, closure))
            {
                throw new InvalidFontFileException($"The charstring of CFF glyph {glyphId} is malformed.");
            }
        }

        return closure;
    }

    void ICffCallObserver.SubrCalled(bool isGlobal, int index)
        => (isGlobal ? this.GlobalSubrs : this.LocalSubrs[this.fontDict])[index] = true;

    void ICffCallObserver.SeacComponentUsed(int glyphId)
        => this.Add(glyphId);

    private void Add(int glyphId)
    {
        if (!this.Glyphs[glyphId])
        {
            this.Glyphs[glyphId] = true;
            this.pending[this.pendingCount++] = (ushort)glyphId;
        }
    }
}
