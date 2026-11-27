// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// Walks the glyphs of a collection past the glyphs that the lookup flags skip, and past the default-ignorable glyphs
/// that the lookup may skip. A default-ignorable glyph that the lookup may skip is still matched where the lookup asks
/// for it, as in HarfBuzz. The walk stops at the ends of the script run of the collection, as at the ends of a HarfBuzz
/// buffer.
/// </summary>
internal struct SkippingGlyphIterator
{
    private readonly FontMetrics fontMetrics;
    private bool ignoreMarks;
    private bool ignoreBaseGlyphs;
    private bool ignoreLigatures;
    private ushort markAttachmentType;
    private bool useMarkFilteringSet;
    private ushort markFilteringSet;

    // The syllable of the syllabic shaper that a glyph must be in to match, or 0 to match a glyph of any.
    private int syllable;

    public SkippingGlyphIterator(
        FontMetrics fontMetrics,
        IGlyphShapingCollection collection,
        int index,
        LookupFlags lookupFlags,
        SkippableIgnorables ignorables = SkippableIgnorables.None)
    {
        this.fontMetrics = fontMetrics;
        this.Collection = collection;
        this.Ignorables = ignorables;
        this.Reset(index, lookupFlags);
    }

    private enum Skip
    {
        No,
        Yes,
        Maybe,
    }

    public IGlyphShapingCollection Collection { get; }

    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the default-ignorable glyphs that the iterator may skip.
    /// </summary>
    public SkippableIgnorables Ignorables { get; set; }

    public int Next()
    {
        this.Move(1);
        return this.Index;
    }

    public int Increment(int count = 1)
    {
        int direction = count < 0 ? -1 : 1;
        count = Math.Abs(count);
        while (count-- > 0)
        {
            this.Move(direction);
        }

        return this.Index;
    }

    public void Reset(int index, LookupFlags lookupFlags)
    {
        this.Index = index;
        this.ignoreMarks = (lookupFlags & LookupFlags.IgnoreMarks) != 0;
        this.ignoreBaseGlyphs = (lookupFlags & LookupFlags.IgnoreBaseGlyphs) != 0;
        this.ignoreLigatures = (lookupFlags & LookupFlags.IgnoreLigatures) != 0;
        this.markAttachmentType = (ushort)((uint)(lookupFlags & LookupFlags.MarkAttachmentTypeMask) >> 8);
        this.useMarkFilteringSet = (lookupFlags & LookupFlags.UseMarkFilteringSet) != 0;
        this.markFilteringSet = (ushort)((uint)(lookupFlags & LookupFlags.MarkFilteringSetMask) >> 16);
    }

    /// <summary>
    /// Resets the iterator to the first glyph at or after the index that the lookup flags do not skip.
    /// </summary>
    public void Start(int index, LookupFlags lookupFlags)
    {
        this.Reset(index - 1, lookupFlags);
        this.Move(1);
    }

    /// <summary>
    /// Moves to the next glyph in the direction that matches a component of a sequence. A glyph that the lookup flags
    /// skip is passed. A default-ignorable glyph that the iterator may skip is passed when it does not match.
    /// </summary>
    /// <typeparam name="T">The type of the component.</typeparam>
    /// <param name="direction">1 to move forward, or -1 to move back.</param>
    /// <param name="component">The component of the sequence.</param>
    /// <param name="matches">Whether a glyph matches the component.</param>
    /// <returns><see langword="true"/> when the iterator is at a glyph that matches.</returns>
    public bool TryMatch<T>(int direction, T component, Func<T, GlyphShapingData, bool> matches)
    {
        while (true)
        {
            this.Index += direction;
            if (this.Index < this.Collection.RunStart || this.Index >= this.Collection.RunEnd)
            {
                return false;
            }

            Skip skip = this.GetSkip(this.Index);
            if (skip == Skip.Yes)
            {
                continue;
            }

            // As in HarfBuzz, a glyph of another syllable does not match, and the lookup flags still skip it.
            GlyphShapingData data = this.Collection[this.Index];
            if ((this.syllable == 0 || SyllableOf(data) == this.syllable) && matches(component, data))
            {
                return true;
            }

            if (skip == Skip.No)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Keeps the glyphs that the iterator matches to the syllable of the glyph at its index, when the lookup matches per
    /// syllable.
    /// </summary>
    /// <param name="perSyllable">Whether the lookup matches per syllable.</param>
    public void MatchSyllable(bool perSyllable) => this.syllable = perSyllable ? SyllableOf(this.Collection[this.Index]) : 0;

    /// <summary>
    /// Gets a value indicating whether a lookup may skip a glyph as a default-ignorable glyph.
    /// </summary>
    /// <param name="data">The glyph.</param>
    /// <param name="ignorables">The default-ignorable glyphs that the lookup may skip.</param>
    /// <returns><see langword="true"/> if the lookup may skip the glyph.</returns>
    public static bool MaySkip(GlyphShapingData data, SkippableIgnorables ignorables)
    {
        if (ignorables == SkippableIgnorables.None || !data.IsDefaultIgnorable)
        {
            return false;
        }

        SkippableIgnorables kind = data.CodePoint.Value switch
        {
            0x200C => SkippableIgnorables.Zwnj,
            0x200D => SkippableIgnorables.Zwj,
            0x034F when data.IsUnhiddenJoiner => SkippableIgnorables.Other,
            0x034F or (>= 0x180B and <= 0x180D) or 0x180F or (>= 0xE0020 and <= 0xE007F) => SkippableIgnorables.Hidden,
            _ => SkippableIgnorables.Other,
        };

        return (ignorables & kind) != 0;
    }

    // The Indic and Khmer shapers, the universal shaper and the Myanmar shaper each number the syllables of a run, with
    // numbers that GlyphSubstitutionCollection.TakeSyllableNumbers gives no other run.
    private static int SyllableOf(GlyphShapingData data)
        => data.IndicShapingEngineInfo?.Syllable ?? data.UniversalShapingEngineInfo?.Syllable ?? data.MyanmarShapingEngineInfo?.Syllable ?? 0;

    // Without a component to match, a glyph that may be skipped is skipped.
    private void Move(int direction)
    {
        this.Index += direction;
        while (this.Index >= this.Collection.RunStart && this.Index < this.Collection.RunEnd)
        {
            if (this.GetSkip(this.Index) == Skip.No)
            {
                break;
            }

            this.Index += direction;
        }
    }

    private readonly Skip GetSkip(int index)
    {
        GlyphShapingData data = this.Collection[index];
        if (this.ShouldIgnore(data))
        {
            return Skip.Yes;
        }

        return MaySkip(data, this.Ignorables) ? Skip.Maybe : Skip.No;
    }

    private readonly bool ShouldIgnore(GlyphShapingData data)
    {
        GlyphShapingClass shapingClass = AdvancedTypographicUtils.GetGlyphShapingClass(this.fontMetrics, data.GlyphId, data);
        if ((this.ignoreMarks && shapingClass.IsMark) ||
            (this.ignoreBaseGlyphs && shapingClass.IsBase) ||
            (this.ignoreLigatures && shapingClass.IsLigature))
        {
            return true;
        }

        // A mark filtering set takes the place of the mark attachment type.
        if (shapingClass.IsMark && this.useMarkFilteringSet)
        {
            return !this.fontMetrics.IsInMarkGlyphSet(this.markFilteringSet, data.GlyphId);
        }

        return this.markAttachmentType > 0 && shapingClass.IsMark && shapingClass.MarkAttachmentType != this.markAttachmentType;
    }
}
