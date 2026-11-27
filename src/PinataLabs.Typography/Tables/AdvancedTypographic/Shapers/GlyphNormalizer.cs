// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Collections.Generic;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.Unicode.Resources;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Maps the characters of the text to the glyphs that the font has, before GSUB. It decomposes a character that
/// the font lacks into the canonical parts that the font has, sorts each run of combining marks, and composes
/// a base and a mark into the precomposed character when the font has it. It follows the normalization step of
/// HarfBuzz, with the mode of each shaper in its <see cref="NormalizationProfile"/>.
/// </summary>
internal ref struct GlyphNormalizer
{
    // HarfBuzz leaves a longer run of marks in the order of the text.
    private const int MaxSortedMarks = 32;

    // No canonical decomposition is longer than four characters.
    private const int MaxDecomposition = 4;

    private const int StackItems = 64;

    private static readonly Tag ThaiTag = Tag.Parse("thai");

    // The Arabic modifier marks sort before every other mark once they have moved, the ones below the base first.
    // Inferred: HarfBuzz gives the moved marks a class of their own, and no text shows where it sorts.
    private const byte ModifierBelowOrder = 1;

    private const byte ModifierAboveOrder = 2;

    // The marks that the Arabic shaper moves are below (class 220) or above (class 230) the base.
    private static readonly byte BelowOrder = MarkOrder(new CodePoint(0x0323));

    private static readonly byte AboveOrder = MarkOrder(new CodePoint(0x0301));

    private static readonly byte PatahOrder = MarkOrder(new CodePoint(0x05B7));

    private static readonly byte QamatsOrder = MarkOrder(new CodePoint(0x05B8));

    private static readonly byte ShevaOrder = MarkOrder(new CodePoint(0x05B0));

    private static readonly byte HiriqOrder = MarkOrder(new CodePoint(0x05B4));

    private static readonly byte MetegOrder = MarkOrder(new CodePoint(0x05BD));

    private readonly FontMetrics fontMetrics;

    private readonly GlyphSubstitutionCollection collection;

    private readonly bool hasFallbackFonts;

    // The run of glyphs that one shaper shapes. HarfBuzz normalizes each such run as one buffer.
    private NormalizationProfile profile = NormalizationProfile.Default;

    private int runStart;

    private int runEnd;

    private bool runHasMarks;

    private bool composesPresentationForms;

    private GlyphNormalizer(FontMetrics fontMetrics, GlyphSubstitutionCollection collection, bool hasFallbackFonts)
    {
        this.fontMetrics = fontMetrics;
        this.collection = collection;
        this.hasFallbackFonts = hasFallbackFonts;
    }

    /// <summary>
    /// Normalizes the glyphs of one font run, which the 'cmap' table has just mapped one to one from the text.
    /// </summary>
    /// <param name="fontMetrics">The font that shapes the run.</param>
    /// <param name="collection">The glyphs of the run.</param>
    /// <param name="hasFallbackFonts">
    /// Whether a fallback font can replace the glyphs that this font lacks. A fallback font replaces glyphs offset
    /// by offset. So a mark that this font lacks must keep its offset, and its run of marks is not sorted.
    /// </param>
    public static void Normalize(FontMetrics fontMetrics, GlyphSubstitutionCollection collection, bool hasFallbackFonts)
        => new GlyphNormalizer(fontMetrics, collection, hasFallbackFonts).NormalizeRuns();

    private void NormalizeRuns()
    {
        Item[]? rented = null;
        Span<Item> stack = stackalloc Item[StackItems];

        int index = 0;
        while (index < this.collection.Count)
        {
            this.runStart = index;
            this.runEnd = GetScriptRunEnd(this.collection, index, out ScriptClass script);
            Tag scriptTag = this.fontMetrics.GetGSubTable().GetScriptTag(script);
            this.profile = ShaperFactory.GetNormalizationProfile(script, scriptTag);
            this.composesPresentationForms = this.profile.ComposesPresentationForms && !this.fontMetrics.HasMarkPositioning(script, this.collection.TextOptions);
            if (this.profile.SplitsVowels)
            {
                this.SplitVowels();
            }

            // As in HarfBuzz, this comes before the marks are sorted.
            if (script == ScriptClass.Thai && scriptTag != ThaiTag)
            {
                ThaiPrivateUseForms.Apply(this.fontMetrics, this.collection, this.runStart, this.runEnd);
            }

            // HarfBuzz sorts and composes only in a buffer that has a mark after its first character.
            this.runHasMarks = false;
            for (int i = index + 1; i < this.runEnd && !this.runHasMarks; i++)
            {
                this.runHasMarks = CodePoint.IsMark(this.collection[i].CodePoint);
            }

            while (index < this.runEnd)
            {
                int end = index + 1;
                while (end < this.runEnd && IsMarkOrNonStarter(this.collection[end].CodePoint))
                {
                    end++;
                }

                int count = end - index;
                if (count == 1 && !this.CanChangeAlone(this.collection[index]))
                {
                    index++;
                    continue;
                }

                int capacity = count * MaxDecomposition;
                if (capacity > stack.Length && capacity > (rented?.Length ?? 0))
                {
                    if (rented is not null)
                    {
                        ArrayPool<Item>.Shared.Return(rented);
                    }

                    rented = ArrayPool<Item>.Shared.Rent(capacity);
                }

                int delta = this.NormalizeCluster(index, end, capacity <= stack.Length ? stack : rented);
                index = end + delta;
                this.runEnd += delta;
            }
        }

        if (rented is not null)
        {
            ArrayPool<Item>.Shared.Return(rented);
        }

        this.ReplaceMissingSpaces();
    }

    /// <summary>
    /// Gives a space that the font still lacks the glyph of U+0020, and the no-break hyphen the glyph of the hyphen.
    /// The character stays, so the line breaker still sees a space that does not break.
    /// </summary>
    private readonly void ReplaceMissingSpaces()
    {
        for (int i = 0; i < this.collection.Count; i++)
        {
            GlyphShapingData data = this.collection[i];
            if (data.GlyphId != 0)
            {
                continue;
            }

            SpaceFallbackKind kind = SpaceFallback.GetKind(data.CodePoint);
            if (kind != SpaceFallbackKind.None && this.TryGetGlyph(SpaceFallback.Space, out ushort glyphId))
            {
                data.GlyphId = glyphId;
                data.SpaceFallback = kind;
            }
            else if (data.CodePoint.Value == SpaceFallback.NoBreakHyphen && this.TryGetGlyph(SpaceFallback.Hyphen, out glyphId))
            {
                data.GlyphId = glyphId;
            }
        }
    }

    /// <summary>
    /// Splits each vowel of the run that the shaper splits, before the run is normalized. The mark of the vowel moves
    /// before the marks right before the vowel that the shaper moves it past.
    /// </summary>
    private void SplitVowels()
    {
        Span<Item> items = stackalloc Item[StackItems];
        for (int i = this.runStart; i < this.runEnd; i++)
        {
            if (!this.profile.TrySplitVowel((uint)this.collection[i].CodePoint.Value, out uint mark, out uint rest))
            {
                continue;
            }

            int start = i;
            while (start > this.runStart
                && i - start < StackItems - 2
                && this.profile.MovesSplitMarkBefore((uint)this.collection[start - 1].CodePoint.Value))
            {
                start--;
            }

            if (!this.CanNormalize(start, i + 1))
            {
                continue;
            }

            int count = 0;
            items[count++] = new Item(new CodePoint(mark), this.GetGlyphOrMissing(mark), i - start);
            for (int j = start; j < i; j++)
            {
                GlyphShapingData data = this.collection[j];
                items[count++] = new Item(data.CodePoint, data.GlyphId, j - start);
            }

            items[count++] = new Item(new CodePoint(rest), this.GetGlyphOrMissing(rest), i - start);
            this.collection.ReplaceNormalized(start, i + 1 - start, items.Slice(0, count));
            this.runEnd++;
            i++;
        }
    }

    private readonly ushort GetGlyphOrMissing(uint codePoint)
        => this.TryGetGlyph(codePoint, out ushort glyphId) ? glyphId : (ushort)0;

    // A character with no mark after it changes only when the font lacks it, or when the shaper decomposes everything.
    private readonly bool CanChangeAlone(GlyphShapingData data)
        => data.GlyphId == 0 || this.profile.Mode == NormalizationMode.ComposedDiacriticsNoShortCircuit;

    /// <summary>
    /// Normalizes one character and the marks that follow it.
    /// </summary>
    /// <returns>The change in the number of glyphs.</returns>
    private readonly int NormalizeCluster(int start, int end, Span<Item> items)
    {
        if (!this.CanNormalize(start, end))
        {
            return 0;
        }

        bool changed = false;
        int count = 0;
        Span<Item> parts = stackalloc Item[MaxDecomposition];
        for (int i = start; i < end; i++)
        {
            GlyphShapingData data = this.collection[i];
            bool hasMark = (i > this.runStart && CodePoint.IsMark(data.CodePoint))
                || (i + 1 < this.runEnd && CodePoint.IsMark(this.collection[i + 1].CodePoint));

            bool keepsGlyph = this.profile.Mode == NormalizationMode.None
                || (this.profile.Mode == NormalizationMode.ComposedDiacritics && !hasMark);

            int decomposed = keepsGlyph && data.GlyphId != 0
                ? 0
                : this.Decompose((uint)data.CodePoint.Value, keepsGlyph, parts, 0);

            if (decomposed == 0)
            {
                items[count++] = new Item(data.CodePoint, data.GlyphId, i - start);
                continue;
            }

            changed = true;
            for (int j = 0; j < decomposed; j++)
            {
                items[count++] = new Item(parts[j].CodePoint, parts[j].GlyphId, i - start);
            }
        }

        if (this.runHasMarks)
        {
            items = items.Slice(0, count);
            changed |= this.SortMarks(items);
            if (this.profile.Mode != NormalizationMode.None)
            {
                int composed = this.Compose(items);
                changed |= composed != count;
                count = composed;
            }
        }

        if (!changed)
        {
            return 0;
        }

        this.collection.ReplaceNormalized(start, end - start, items.Slice(0, count));
        return count - (end - start);
    }

    private readonly bool CanNormalize(int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            GlyphShapingData data = this.collection.GetGlyphShapingData(i, out int offset);

            // A variation selector fixes the glyph of its base. When the 'cmap' table has the variation sequence,
            // the selector is not in the collection, and the next glyph is more than one offset away.
            if (CodePoint.IsVariationSelector(data.CodePoint)
                || (i + 1 < this.collection.Count && OffsetAt(this.collection, i + 1) > offset + 1))
            {
                return false;
            }
        }

        return this.collection.IsUnresolved(start, end);
    }

    /// <summary>
    /// Writes the parts of the character that the font has glyphs for. A step stops at the first part that the font
    /// has when <paramref name="shortest"/> is set, and goes as deep as the font allows otherwise.
    /// </summary>
    /// <returns>The number of parts, or 0 when the font lacks a part.</returns>
    private readonly int Decompose(uint codePoint, bool shortest, Span<Item> parts, int depth)
    {
        if (depth == MaxDecomposition - 1 || !this.profile.TryDecompose(codePoint, out uint first, out uint second))
        {
            return 0;
        }

        ushort secondGlyph = 0;
        if (second != 0 && !this.TryGetGlyph(second, out secondGlyph))
        {
            return 0;
        }

        bool hasFirst = this.TryGetGlyph(first, out ushort firstGlyph);
        int count = shortest && hasFirst ? 0 : this.Decompose(first, shortest, parts, depth + 1);
        if (count == 0)
        {
            if (!hasFirst)
            {
                return 0;
            }

            parts[count++] = new Item(new CodePoint(first), firstGlyph, 0);
        }

        if (second != 0)
        {
            parts[count++] = new Item(new CodePoint(second), secondGlyph, 0);
        }

        return count;
    }

    private readonly bool SortMarks(Span<Item> items)
    {
        bool changed = false;
        int i = 0;
        while (i < items.Length)
        {
            if (items[i].Order == 0)
            {
                i++;
                continue;
            }

            int end = i;
            bool canMove = true;
            while (end < items.Length && items[end].Order != 0)
            {
                canMove &= !this.hasFallbackFonts || items[end].GlyphId != 0;
                end++;
            }

            if (canMove && end - i <= MaxSortedMarks)
            {
                Span<Item> run = items.Slice(i, end - i);
                changed |= SortByOrder(run);
                changed |= this.profile.MarkReordering switch
                {
                    MarkReordering.ArabicModifierMarks => MoveModifierMarks(run),
                    MarkReordering.HebrewMeteg => MoveMeteg(run),
                    _ => false,
                };
            }

            i = end;
        }

        return changed;
    }

    /// <summary>
    /// Composes each mark with the starter before it when no mark between them blocks it, and the font has the
    /// composed character.
    /// </summary>
    /// <returns>The number of items left.</returns>
    private readonly int Compose(Span<Item> items)
    {
        int starter = 0;
        int count = 1;
        for (int i = 1; i < items.Length; i++)
        {
            Item item = items[i];
            if (CodePoint.IsMark(item.CodePoint)
                && (starter == count - 1 || items[count - 1].Order < item.Order)
                && this.profile.TryCompose((uint)items[starter].CodePoint.Value, (uint)item.CodePoint.Value, this.composesPresentationForms, out uint composed)
                && this.TryGetGlyph(composed, out ushort glyphId))
            {
                items[starter] = new Item(new CodePoint(composed), glyphId, items[starter].Source);
                continue;
            }

            items[count++] = item;
            if (item.Order == 0)
            {
                starter = count - 1;
            }
        }

        return count;
    }

    private readonly bool TryGetGlyph(uint codePoint, out ushort glyphId)
        => this.fontMetrics.TryGetGlyphId(new CodePoint(codePoint), out glyphId) && glyphId != 0;

    private static bool SortByOrder(Span<Item> run)
    {
        bool changed = false;
        for (int i = 1; i < run.Length; i++)
        {
            Item item = run[i];
            int j = i;
            while (j > 0 && run[j - 1].Order > item.Order)
            {
                run[j] = run[j - 1];
                j--;
            }

            run[j] = item;
            changed |= j != i;
        }

        return changed;
    }

    /// <summary>
    /// Moves the modifier marks that lead the marks below the base, and then those that lead the marks above it,
    /// to the start of the sorted run. A hamza above then comes before the vowel mark that it shares a base with.
    /// </summary>
    private static bool MoveModifierMarks(Span<Item> run)
    {
        bool changed = false;
        int start = 0;
        MoveLeadingModifierMarks(run, BelowOrder, ModifierBelowOrder, ref start, ref changed);
        MoveLeadingModifierMarks(run, AboveOrder, ModifierAboveOrder, ref start, ref changed);
        return changed;
    }

    /// <summary>
    /// Swaps the first sheva or hiriq that follows a patah or qamats with the meteg or mark below the base after it.
    /// </summary>
    private static bool MoveMeteg(Span<Item> run)
    {
        for (int i = 2; i < run.Length; i++)
        {
            byte vowel = run[i - 2].Order;
            byte point = run[i - 1].Order;
            byte last = run[i].Order;
            if ((vowel == PatahOrder || vowel == QamatsOrder)
                && (point == ShevaOrder || point == HiriqOrder)
                && (last == MetegOrder || last == BelowOrder))
            {
                (run[i - 1], run[i]) = (run[i], run[i - 1]);
                return true;
            }
        }

        return false;
    }

    private static void MoveLeadingModifierMarks(Span<Item> run, byte order, byte movedOrder, ref int start, ref bool changed)
    {
        int first = start;
        while (first < run.Length && run[first].Order < order)
        {
            first++;
        }

        int end = first;
        while (end < run.Length && run[end].Order == order && run[end].IsArabicModifier)
        {
            end++;
        }

        for (int i = first; i < end; i++)
        {
            Item item = run[i];
            item.Order = movedOrder;
            for (int j = i; j > start; j--)
            {
                run[j] = run[j - 1];
            }

            run[start] = item;
            changed |= i != start;
            start++;
        }
    }

    /// <summary>
    /// Gets the end of the run of glyphs that GSUB shapes with one shaper. Characters of no script of their own
    /// join the script before or after them. A mark stays with its base, whatever its script.
    /// </summary>
    private static int GetScriptRunEnd(GlyphSubstitutionCollection collection, int start, out ScriptClass script)
    {
        script = collection.TextOptions.GetScriptClass(collection[start].CodePoint);
        int end = start + 1;
        while (end < collection.Count)
        {
            CodePoint codePoint = collection[end].CodePoint;
            ScriptClass next = collection.TextOptions.GetScriptClass(codePoint);
            if (next != script && HasOwnScript(script) && HasOwnScript(next) && !CodePoint.IsMark(codePoint))
            {
                break;
            }

            if (!HasOwnScript(script))
            {
                script = next;
            }

            end++;
        }

        return end;
    }

    private static bool HasOwnScript(ScriptClass script)
        => script is not ScriptClass.Common and not ScriptClass.Unknown and not ScriptClass.Inherited;

    private static bool IsMarkOrNonStarter(CodePoint codePoint)
        => CodePoint.IsMark(codePoint) || MarkOrder(codePoint) != 0;

    private static byte MarkOrder(CodePoint codePoint)
        => (byte)UnicodeData.GetCombiningClassData((uint)codePoint.Value);

    private static int OffsetAt(GlyphSubstitutionCollection collection, int index)
    {
        collection.GetGlyphShapingData(index, out int offset);
        return offset;
    }

    /// <summary>
    /// One glyph of a cluster while it is normalized.
    /// </summary>
    internal struct Item
    {
        public Item(CodePoint codePoint, ushort glyphId, int source)
        {
            uint data = UnicodeData.GetCombiningClassData((uint)codePoint.Value);
            this.CodePoint = codePoint;
            this.GlyphId = glyphId;
            this.Source = source;
            this.Order = (byte)data;
            this.IsArabicModifier = (data & CanonicalMappingData.ArabicModifierMark) != 0;
        }

        public CodePoint CodePoint { get; }

        public ushort GlyphId { get; }

        /// <summary>
        /// Gets the index, in the cluster, of the glyph of the character that this glyph comes from.
        /// </summary>
        public int Source { get; }

        /// <summary>
        /// Gets or sets the place of the character in the order that marks are sorted by. It is 0 for a starter.
        /// </summary>
        public byte Order { get; set; }

        public bool IsArabicModifier { get; }
    }
}
