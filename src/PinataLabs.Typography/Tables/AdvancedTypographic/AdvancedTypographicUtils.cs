// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using PinataLabs.Typography.Tables.AdvancedTypographic.GPos;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

internal static class AdvancedTypographicUtils
{
    // The following properties are used to prevent overflows caused
    // by maliciously crafted fonts.
    // Based on HarfBuzz hb-buffer.hh
    public const int MaxContextLength = 64;
    private const int MaxLengthFactor = 64;
    private const int MaxLengthMinimum = 16384;
    private const int MaxOperationsFactor = 1024;
    private const int MaxOperationsMinimum = 16384;
    private const int MaxShapingCharsLength = 0x3FFFFFFF; // Half int max.

    /// <summary>
    /// Gets a value indicating whether the glyph represented by the codepoint should be interpreted vertically.
    /// </summary>
    /// <param name="codePoint">The codepoint represented by the glyph.</param>
    /// <param name="layoutMode">The layout mode.</param>
    /// <param name="hasVerticalForm">
    /// Whether the glyph was resolved to a vertical form. Only relevant to <c>Tr</c> (transform-rotate) codepoints
    /// in vertical mixed layout, which are upright when the font provides a vertical form and rotated otherwise.
    /// </param>
    /// <returns>The <see cref="bool"/>.</returns>
    public static bool IsVerticalGlyph(CodePoint codePoint, LayoutMode layoutMode, bool hasVerticalForm)
    {
        if (layoutMode.IsVertical())
        {
            return true;
        }

        if (!layoutMode.IsVerticalMixed())
        {
            return false;
        }

        return CodePoint.GetVerticalOrientationType(codePoint) switch
        {
            VerticalOrientationType.Upright or VerticalOrientationType.TransformUpright => true,
            VerticalOrientationType.TransformRotate => hasVerticalForm,
            _ => false,
        };
    }

    public static int GetMaxAllowableShapingCollectionCount(int length)
        => (int)Math.Clamp((long)length * MaxLengthFactor, MaxLengthMinimum, MaxShapingCharsLength);

    public static int GetMaxAllowableShapingOperationsCount(int length)
        => (int)Math.Clamp((long)length * MaxOperationsFactor, MaxOperationsMinimum, MaxShapingCharsLength);

    public static void ApplyLookupList(
        FontMetrics fontMetrics,
        GSubTable table,
        LookupFeatures features,
        LookupFlags lookupFlags,
        SequenceLookupRecord[] records,
        GlyphSubstitutionCollection collection,
        int index,
        int count)
    {
        SkippingGlyphIterator iterator = new(fontMetrics, collection, index, lookupFlags, features.InputIgnorables);
        int currentCount = collection.Count;

        foreach (SequenceLookupRecord lookupRecord in records)
        {
            ushort sequenceIndex = lookupRecord.SequenceIndex;
            ushort lookupIndex = lookupRecord.LookupListIndex;
            GSub.LookupTable lookup = table.LookupList.LookupTables[lookupIndex];

            // As in HarfBuzz, a reverse chaining substitution applies only as a lookup of a feature.
            if (lookup.IsReverse)
            {
                continue;
            }

            iterator.Index = index;
            iterator.Increment(sequenceIndex);
            lookup.TrySubstitution(fontMetrics, table, collection, features, iterator.Index, count - (iterator.Index - index));

            // Account for substitutions changing the length of the collection.
            if (collection.Count != currentCount)
            {
                count -= currentCount - collection.Count;
                currentCount = collection.Count;
            }
        }
    }

    public static void ApplyLookupList(
        FontMetrics fontMetrics,
        GPosTable table,
        LookupFeatures features,
        LookupFlags lookupFlags,
        SequenceLookupRecord[] records,
        GlyphPositioningCollection collection,
        int index,
        int count)
    {
        SkippingGlyphIterator iterator = new(fontMetrics, collection, index, lookupFlags, features.InputIgnorables);
        foreach (SequenceLookupRecord lookupRecord in records)
        {
            ushort sequenceIndex = lookupRecord.SequenceIndex;
            ushort lookupIndex = lookupRecord.LookupListIndex;
            iterator.Index = index;
            iterator.Increment(sequenceIndex);
            LookupTable lookup = table.LookupList.LookupTables[lookupIndex];
            lookup.TryUpdatePosition(fontMetrics, table, collection, features, iterator.Index, count - (iterator.Index - index));
        }
    }

    /// <summary>
    /// Matches the components of a ligature after its first glyph. Each glyph must have a feature of the lookup enabled.
    /// </summary>
    public static bool MatchInputSequence(SkippingGlyphIterator iterator, LookupFeatures features, ushort[] sequence, Span<int> matches)
        => MatchInput(
            iterator,
            features,
            sequence,
            (component, data) => component == data.GlyphId && features.AnyEnabledOn(data.Features),
            matches,
            out _);

    /// <summary>
    /// Matches the input glyphs of a context rule after its first glyph.
    /// </summary>
    public static bool MatchSequence(SkippingGlyphIterator iterator, LookupFeatures features, ushort[] sequence)
        => MatchInput(iterator, features, sequence, MatchGlyph, default, out _);

    /// <summary>
    /// Matches the input classes of a context rule after its first glyph.
    /// </summary>
    public static bool MatchClassSequence(
        SkippingGlyphIterator iterator,
        LookupFeatures features,
        ushort[] sequence,
        ClassDefinitionTable classDefinitionTable)
        => MatchInput(iterator, features, sequence, (component, data) => component == classDefinitionTable.ClassIndexOf(data.GlyphId), default, out _);

    /// <summary>
    /// Matches the input coverages of a context rule, the first of them at the glyph of the iterator.
    /// </summary>
    public static bool MatchCoverageSequence(SkippingGlyphIterator iterator, LookupFeatures features, CoverageTable[] coverageTables)
        => MatchCoverageInput(iterator, features, coverageTables, out _);

    public static bool ApplyChainedSequenceRule(SkippingGlyphIterator iterator, LookupFeatures features, ChainedSequenceRuleTable rule)
        => MatchInput(iterator, features, rule.InputSequence, MatchGlyph, default, out int end)
        && MatchBacktrack(iterator, features, rule.BacktrackSequence, MatchGlyph)
        && MatchLookahead(iterator, features, end, rule.LookaheadSequence, MatchGlyph);

    public static bool ApplyChainedClassSequenceRule(
        SkippingGlyphIterator iterator,
        LookupFeatures features,
        ChainedClassSequenceRuleTable rule,
        ClassDefinitionTable inputClassDefinitionTable,
        ClassDefinitionTable backtrackClassDefinitionTable,
        ClassDefinitionTable lookaheadClassDefinitionTable)
        => MatchInput(iterator, features, rule.InputSequence, (component, data) => component == inputClassDefinitionTable.ClassIndexOf(data.GlyphId), default, out int end)
        && MatchBacktrack(iterator, features, rule.BacktrackSequence, (component, data) => component == backtrackClassDefinitionTable.ClassIndexOf(data.GlyphId))
        && MatchLookahead(iterator, features, end, rule.LookaheadSequence, (component, data) => component == lookaheadClassDefinitionTable.ClassIndexOf(data.GlyphId));

    public static bool CheckAllCoverages(
        FontMetrics fontMetrics,
        LookupFlags lookupFlags,
        LookupFeatures features,
        IGlyphShapingCollection collection,
        int index,
        int count,
        CoverageTable[] input,
        CoverageTable[] backtrack,
        CoverageTable[] lookahead)
    {
        // Check that there are enough context glyphs.
        if (index - backtrack.Length < 0 || input.Length + lookahead.Length > count)
        {
            return false;
        }

        SkippingGlyphIterator iterator = new(fontMetrics, collection, index, lookupFlags);
        return MatchCoverageInput(iterator, features, input, out int end)
            && MatchBacktrack(iterator, features, backtrack, MatchCoverage)
            && MatchLookahead(iterator, features, end, lookahead, MatchCoverage);
    }

    public static bool ApplyAnchor(
        FontMetrics fontMetrics,
        GlyphPositioningCollection collection,
        int index,
        AnchorTable? baseAnchor,
        MarkRecord markRecord,
        int baseGlyphIndex)
    {
        // A base, ligature component or mark2 glyph is not required to define an attachment point
        // for every mark class (the anchor offset is NULL). In that case the subtable does not apply.
        if (baseAnchor is null || markRecord.MarkAnchorTable is null)
        {
            return false;
        }

        GlyphShapingData baseData = collection[baseGlyphIndex];
        AnchorXY baseXY = baseAnchor.GetAnchor(fontMetrics, baseData, collection);

        GlyphShapingData markData = collection[index];
        AnchorXY markXY = markRecord.MarkAnchorTable.GetAnchor(fontMetrics, markData, collection);

        markData.Bounds.X = Round(baseXY.XCoordinate - markXY.XCoordinate);
        markData.Bounds.Y = Round(baseXY.YCoordinate - markXY.YCoordinate);
        markData.MarkAttachment = baseGlyphIndex;
        return true;
    }

    public static void ApplyPosition(
        FontMetrics fontMetrics,
        GlyphPositioningCollection collection,
        int index,
        ValueRecord record)
    {
        GlyphShapingData current = collection[index];
        current.Bounds.Width += record.XAdvance + Round(fontMetrics.GetDeviceDelta(record.XAdvanceVariation));
        current.Bounds.Height += record.YAdvance + Round(fontMetrics.GetDeviceDelta(record.YAdvanceVariation));
        current.Bounds.X += record.XPlacement + Round(fontMetrics.GetDeviceDelta(record.XPlacementVariation));
        current.Bounds.Y += record.YPlacement + Round(fontMetrics.GetDeviceDelta(record.YPlacementVariation));
    }

    /// <summary>
    /// Rounds a value in font units to the whole unit that positioning adds, as HarfBuzz does: halves round up, so that
    /// 2.5 is 3 and -2.5 is -2. Measured against HarfBuzz 15.0.0 with deltas of half a unit.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The rounded value.</returns>
    public static int Round(float value) => (int)MathF.Floor(value + 0.5F);

    public static bool IsMarkGlyph(FontMetrics fontMetrics, ushort glyphId, GlyphShapingData shapingData)
    {
        if (!fontMetrics.TryGetGlyphClass(glyphId, out GlyphClassDef? glyphClass) &&
            !CodePoint.IsMark(shapingData.CodePoint))
        {
            return false;
        }

        if (glyphClass != GlyphClassDef.MarkGlyph)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Gets a value indicating whether the glyph is a mark that takes no advance. As in HarfBuzz, the GDEF table
    /// says so when it has glyph classes. Without them, the glyph of a non-spacing mark is one.
    /// </summary>
    /// <param name="fontMetrics">The font of the glyph.</param>
    /// <param name="shapingData">The glyph.</param>
    /// <returns><see langword="true"/> if the glyph loses its advance.</returns>
    public static bool IsZeroWidthMark(FontMetrics fontMetrics, GlyphShapingData shapingData)
    {
        if (fontMetrics.TryGetGlyphClass(shapingData.GlyphId, out GlyphClassDef? glyphClass))
        {
            return glyphClass == GlyphClassDef.MarkGlyph;
        }

        return CodePoint.GetGeneralCategory(shapingData.UnicodePropertiesCodePoint) == UnicodeCategory.NonSpacingMark
            && !UnicodeUtility.IsDefaultIgnorableCodePoint((uint)shapingData.UnicodePropertiesCodePoint.Value);
    }

    public static GlyphShapingClass GetGlyphShapingClass(FontMetrics fontMetrics, ushort glyphId, GlyphShapingData shapingData)
    {
        bool isMark;
        bool isBase;
        bool isLigature;
        ushort markAttachmentType = 0;
        if (fontMetrics.TryGetGlyphClass(glyphId, out GlyphClassDef? glyphClass))
        {
            isMark = glyphClass == GlyphClassDef.MarkGlyph;
            isBase = glyphClass == GlyphClassDef.BaseGlyph;
            isLigature = glyphClass == GlyphClassDef.LigatureGlyph;
            if (fontMetrics.TryGetMarkAttachmentClass(glyphId, out GlyphClassDef? markAttachmentClass))
            {
                markAttachmentType = (ushort)markAttachmentClass;
            }
        }
        else
        {
            // TODO: We may have to store each codepoint. FontKit checks all.
            isMark = CodePoint.IsMark(shapingData.UnicodePropertiesCodePoint);
            isBase = !isMark;
            isLigature = shapingData.CodePointCount > 1;
        }

        return new GlyphShapingClass(isMark, isBase, isLigature, markAttachmentType);
    }

    private static bool MatchGlyph(ushort component, GlyphShapingData data) => component == data.GlyphId;

    private static bool MatchCoverage(CoverageTable component, GlyphShapingData data) => component.CoverageIndexOf(data.GlyphId) >= 0;

    // The first coverage is that of the glyph of the iterator. As in HarfBuzz, a rule without input matches nothing.
    private static bool MatchCoverageInput(SkippingGlyphIterator iterator, LookupFeatures features, CoverageTable[] coverageTables, out int end)
    {
        end = -1;
        return coverageTables.Length > 0
            && MatchCoverage(coverageTables[0], iterator.Collection[iterator.Index])
            && MatchInput(iterator, features, coverageTables.AsSpan(1), MatchCoverage, default, out end);
    }

    /// <summary>
    /// Matches the glyphs of an input sequence one by one after the glyph of the iterator, which is the first glyph of
    /// the input, and gives the index of the last glyph that matched.
    /// </summary>
    private static bool MatchInput<T>(
        SkippingGlyphIterator iterator,
        LookupFeatures features,
        ReadOnlySpan<T> sequence,
        Func<T, GlyphShapingData, bool> condition,
        Span<int> matches,
        out int end)
    {
        end = -1;
        iterator.Ignorables = features.InputIgnorables;
        iterator.MatchSyllable(features.PerSyllable);
        for (int i = 0; i < sequence.Length; i++)
        {
            if (i >= MaxContextLength || !iterator.TryMatch(1, sequence[i], condition))
            {
                return false;
            }

            if (!matches.IsEmpty)
            {
                matches[i] = iterator.Index;
            }
        }

        end = iterator.Index;
        return true;
    }

    // A backtrack sequence lists its glyphs from the one nearest to the input to the farthest one.
    private static bool MatchBacktrack<T>(SkippingGlyphIterator iterator, LookupFeatures features, T[] sequence, Func<T, GlyphShapingData, bool> condition)
    {
        iterator.Ignorables = features.ContextIgnorables;
        iterator.MatchSyllable(features.PerSyllable);
        foreach (T component in sequence)
        {
            if (!iterator.TryMatch(-1, component, condition))
            {
                return false;
            }
        }

        return true;
    }

    // The lookahead starts after the last glyph of the input, and keeps to the syllable of the first.
    private static bool MatchLookahead<T>(SkippingGlyphIterator iterator, LookupFeatures features, int end, T[] sequence, Func<T, GlyphShapingData, bool> condition)
    {
        iterator.MatchSyllable(features.PerSyllable);
        iterator.Index = end;
        iterator.Ignorables = features.ContextIgnorables;
        foreach (T component in sequence)
        {
            if (!iterator.TryMatch(1, component, condition))
            {
                return false;
            }
        }

        return true;
    }
}
