// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

internal struct SkippingGlyphIterator
{
    private readonly FontMetrics fontMetrics;
    private bool ignoreMarks;
    private bool ignoreBaseGlyphs;
    private bool ignoreLigatures;
    private ushort markAttachmentType;
    private bool useMarkFilteringSet;
    private ushort markFilteringSet;

    public SkippingGlyphIterator(
        FontMetrics fontMetrics,
        IGlyphShapingCollection collection,
        int index,
        LookupFlags lookupFlags)
    {
        this.fontMetrics = fontMetrics;
        this.Collection = collection;
        this.Reset(index, lookupFlags);
    }

    public IGlyphShapingCollection Collection { get; }

    public int Index { get; set; }

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

    private void Move(int direction)
    {
        this.Index += direction;
        while (this.Index >= 0 && this.Index < this.Collection.Count)
        {
            if (!this.ShouldIgnore(this.Index))
            {
                break;
            }

            this.Index += direction;
        }
    }

    private readonly bool ShouldIgnore(int index)
    {
        GlyphShapingData data = this.Collection[index];
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
