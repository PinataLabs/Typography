// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// The 'glyf' and 'loca' tables of a TrueType font that keep the glyphs of a closure at their glyph IDs. A glyph that is dropped
/// has no data, so its two offsets in 'loca' are the same. A kept glyph is copied as it is, so a composite glyph still names the glyph IDs of
/// its components, and the components are kept with it. 'loca' keeps the format of the font, short or long.
/// </summary>
internal sealed class TrueTypeSubset
{
    // A glyph starts with its contour count, which is negative for a composite glyph, and its bounding box.
    private const int GlyphHeaderSize = 10;

    private readonly TrueTypeGlyphData glyphs;
    private readonly bool[] kept;

    // The boundary that each glyph starts on in 'glyf': 4 bytes for long offsets, which the OpenType specification recommends, else 2 bytes, which short offsets need.
    private readonly int alignment;

    private TrueTypeSubset(TrueTypeGlyphData glyphs, bool[] kept)
    {
        this.glyphs = glyphs;
        this.kept = kept;
        this.alignment = glyphs.HasLongOffsets ? 4 : 2;

        long glyfLength = 0;
        long dataLength = 0;
        for (int i = 0; i < kept.Length; i++)
        {
            glyfLength += this.GetPaddedLength(i);
            dataLength += this.GetLength(i);
        }

        // The glyphs of a font have consecutive ranges of 'glyf', but the range of a glyph that is dropped is never read, so
        // 'loca' can send many kept glyphs to the same bytes. 'loca' cannot do that for the subset, where every glyph starts where
        // the one before it ends. Kept glyphs that need more bytes than the table has share them, and copying each would
        // make a small font large.
        if (dataLength > glyphs.GlyfLength)
        {
            throw new InvalidFontFileException("The kept glyphs of the font share data in 'glyf', and 'loca' of a subset cannot share it.");
        }

        // A short 'loca' holds the offset divided by two in 16 bits. A font that has short offsets cannot have more glyf data than that.
        if (glyfLength > (glyphs.HasLongOffsets ? int.MaxValue : 2L * ushort.MaxValue))
        {
            throw new InvalidFontFileException("The 'glyf' table of the subset is larger than 'loca' can address.");
        }

        this.Glyf = new GlyfTable(this, (int)glyfLength);
        this.Loca = new LocaTable(this, (kept.Length + 1) * (glyphs.HasLongOffsets ? 4 : 2));
    }

    /// <summary>
    /// Gets the 'glyf' table.
    /// </summary>
    public ISubsetTable Glyf { get; }

    /// <summary>
    /// Gets the 'loca' table.
    /// </summary>
    public ISubsetTable Loca { get; }

    /// <summary>
    /// Finds the closure of glyphs, and lays out the 'glyf' and 'loca' tables that keep it. Glyph 0 is always in it.
    /// </summary>
    /// <param name="directory">The table directory of the font.</param>
    /// <param name="glyphIds">The glyphs to keep.</param>
    /// <param name="budget">The budget that the arrays are charged to.</param>
    /// <returns>The new tables.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A glyph ID is not a glyph of the font.</exception>
    /// <exception cref="InvalidFontFileException">A table is malformed, or the data of a kept glyph or of one that a kept glyph needs is.</exception>
    /// <exception cref="FontProgramTooLargeException">The arrays do not fit under the cap of the budget.</exception>
    public static TrueTypeSubset Create(SfntDirectory directory, IReadOnlyCollection<ushort> glyphIds, ref LoadBudget budget)
    {
        TrueTypeGlyphData glyphs = TrueTypeProgram.Load(directory).Glyphs;
        int glyphCount = glyphs.GlyphCount;

        // A glyph is added to the stack once, so the glyph count is enough room.
        budget.Reserve(LoadBudget.SizeOf<bool>(glyphCount) + LoadBudget.SizeOf<ushort>(glyphCount));
        bool[] kept = budget.Allocate<bool>(glyphCount);
        ushort[] pending = budget.Allocate<ushort>(glyphCount);
        int pendingCount = 0;

        Add(0, kept, pending, ref pendingCount);
        foreach (ushort glyphId in glyphIds)
        {
            if (glyphId >= glyphCount)
            {
                throw new ArgumentOutOfRangeException(nameof(glyphIds), glyphId, $"The font has {glyphCount} glyphs, and glyph ID {glyphId} is not one of them.");
            }

            Add(glyphId, kept, pending, ref pendingCount);
        }

        // The components of a composite glyph are glyphs too, and so are the components of those.
        // A glyph that is already kept adds nothing, so a cycle of composites ends.
        while (pendingCount > 0)
        {
            AddComponents(glyphs, pending[--pendingCount], kept, pending, ref pendingCount);
        }

        return new TrueTypeSubset(glyphs, kept);
    }

    private static void Add(int glyphId, bool[] kept, ushort[] pending, ref int pendingCount)
    {
        if (!kept[glyphId])
        {
            kept[glyphId] = true;
            pending[pendingCount++] = (ushort)glyphId;
        }
    }

    private static void AddComponents(TrueTypeGlyphData glyphs, int glyphId, bool[] kept, ushort[] pending, ref int pendingCount)
    {
        if (!glyphs.TryGetGlyphData(glyphId, out ReadOnlyMemory<byte> data))
        {
            throw new InvalidFontFileException($"The location of TrueType glyph {glyphId} is out of range.");
        }

        ReadOnlySpan<byte> glyph = data.Span;
        if (glyph.Length < GlyphHeaderSize || BinaryPrimitives.ReadInt16BigEndian(glyph) >= 0)
        {
            return;
        }

        TrueTypeGlyphDecoder.ComponentReader reader = new(glyph);
        while (reader.TryRead(out TrueTypeGlyphDecoder.Component component))
        {
            if (component.GlyphId >= kept.Length)
            {
                throw new InvalidFontFileException($"TrueType glyph {glyphId} has glyph {component.GlyphId} as a component, and the font has {kept.Length} glyphs.");
            }

            Add(component.GlyphId, kept, pending, ref pendingCount);
        }

        if (reader.Failed)
        {
            throw new InvalidFontFileException($"The components of TrueType glyph {glyphId} run past the end of its data.");
        }
    }

    // The length of a kept glyph, and 0 for a dropped glyph.
    private int GetLength(int glyphId)
        => this.kept[glyphId] && this.glyphs.TryGetGlyphData(glyphId, out ReadOnlyMemory<byte> data) ? data.Length : 0;

    // The length of a kept glyph rounded up to the alignment, and 0 for a dropped glyph.
    private int GetPaddedLength(int glyphId)
        => (this.GetLength(glyphId) + this.alignment - 1) & -this.alignment;

    private void WriteGlyf(Span<byte> destination)
    {
        int position = 0;
        for (int i = 0; i < this.kept.Length; i++)
        {
            if (this.kept[i] && this.glyphs.TryGetGlyphData(i, out ReadOnlyMemory<byte> data))
            {
                data.Span.CopyTo(destination.Slice(position));
            }

            position += this.GetPaddedLength(i);
        }
    }

    private void WriteLoca(Span<byte> destination)
    {
        int position = 0;
        for (int i = 0; i <= this.kept.Length; i++)
        {
            if (this.glyphs.HasLongOffsets)
            {
                BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(i * 4), (uint)position);
            }
            else
            {
                BinaryPrimitives.WriteUInt16BigEndian(destination.Slice(i * 2), (ushort)(position >> 1));
            }

            if (i < this.kept.Length)
            {
                position += this.GetPaddedLength(i);
            }
        }
    }

    private sealed class GlyfTable : ISubsetTable
    {
        private readonly TrueTypeSubset owner;

        public GlyfTable(TrueTypeSubset owner, int length)
        {
            this.owner = owner;
            this.Length = length;
        }

        public int Length { get; }

        public void Write(Span<byte> destination) => this.owner.WriteGlyf(destination);
    }

    private sealed class LocaTable : ISubsetTable
    {
        private readonly TrueTypeSubset owner;

        public LocaTable(TrueTypeSubset owner, int length)
        {
            this.owner = owner;
            this.Length = length;
        }

        public int Length { get; }

        public void Write(Span<byte> destination) => this.owner.WriteLoca(destination);
    }
}
