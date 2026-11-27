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

    /// <summary>
    /// Applies the nested lookups of a context rule at the input glyphs that it matched, as HarfBuzz does in apply_lookup.
    /// A record whose sequence index is past the input, or past the run, is skipped. A nested lookup that adds or removes
    /// glyphs moves the input glyphs after it.
    /// </summary>
    /// <param name="fontMetrics">The font.</param>
    /// <param name="table">The GSUB table.</param>
    /// <param name="features">The features of the lookup of the rule.</param>
    /// <param name="records">The records of the rule.</param>
    /// <param name="collection">The glyphs.</param>
    /// <param name="positions">The index of each input glyph, the first at 0, with room for <see cref="MaxContextLength"/>.</param>
    /// <param name="count">The number of input glyphs.</param>
    public static void ApplyLookupList(
        FontMetrics fontMetrics,
        GSubTable table,
        LookupFeatures features,
        SequenceLookupRecord[] records,
        GlyphSubstitutionCollection collection,
        Span<int> positions,
        int count)
    {
        int end = positions[count - 1] + 1;
        foreach (SequenceLookupRecord lookupRecord in records)
        {
            // An earlier lookup that removed glyphs can leave a record past the input, or past the run.
            int sequenceIndex = lookupRecord.SequenceIndex;
            if (sequenceIndex >= count || positions[sequenceIndex] >= collection.RunEnd)
            {
                continue;
            }

            // As in HarfBuzz, a reverse chaining substitution applies only as a lookup of a feature.
            GSub.LookupTable lookup = table.LookupList.LookupTables[lookupRecord.LookupListIndex];
            if (lookup.IsReverse)
            {
                continue;
            }

            int position = positions[sequenceIndex];
            int length = collection.Count;
            lookup.TrySubstitution(fontMetrics, table, collection, features, position, collection.RunEnd - position);
            int delta = collection.Count - length;
            if (delta == 0)
            {
                continue;
            }

            // As in HarfBuzz, glyphs that a lookup adds follow its position, and glyphs that it removes are the input
            // glyphs after it. The end of the input does not move before the position.
            end += delta;
            if (end < position)
            {
                delta += position - end;
                end = position;
            }

            int next = sequenceIndex + 1;
            if (delta > 0)
            {
                if (count + delta > MaxContextLength)
                {
                    break;
                }
            }
            else
            {
                delta = Math.Max(delta, next - count);
                next -= delta;
            }

            positions[next..count].CopyTo(positions[(next + delta)..]);
            next += delta;
            count += delta;
            for (int j = sequenceIndex + 1; j < next; j++)
            {
                positions[j] = positions[j - 1] + 1;
            }

            for (; next < count; next++)
            {
                positions[next] += delta;
            }
        }
    }

    /// <summary>
    /// Applies the nested lookups of a context rule at the input glyphs that it matched, as HarfBuzz does. A record
    /// whose sequence index is past the input is skipped. Positioning neither adds nor removes glyphs.
    /// </summary>
    /// <param name="fontMetrics">The font.</param>
    /// <param name="table">The GPOS table.</param>
    /// <param name="features">The features of the lookup of the rule.</param>
    /// <param name="records">The records of the rule.</param>
    /// <param name="collection">The glyphs.</param>
    /// <param name="positions">The index of each input glyph, the first at 0.</param>
    /// <param name="count">The number of input glyphs.</param>
    public static void ApplyLookupList(
        FontMetrics fontMetrics,
        GPosTable table,
        LookupFeatures features,
        SequenceLookupRecord[] records,
        GlyphPositioningCollection collection,
        ReadOnlySpan<int> positions,
        int count)
    {
        foreach (SequenceLookupRecord lookupRecord in records)
        {
            if (lookupRecord.SequenceIndex >= count)
            {
                continue;
            }

            int position = positions[lookupRecord.SequenceIndex];
            LookupTable lookup = table.LookupList.LookupTables[lookupRecord.LookupListIndex];
            lookup.TryUpdatePosition(fontMetrics, table, collection, features, position, collection.RunEnd - position);
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

    // The matchers of context rules below write the index of each input glyph into positions, the first glyph at 0, for
    // ApplyLookupList. The input of a rule is its sequence and the glyph of the iterator before it.

    /// <summary>
    /// Matches the input glyphs of a context rule after its first glyph.
    /// </summary>
    public static bool MatchSequence(SkippingGlyphIterator iterator, LookupFeatures features, ushort[] sequence, Span<int> positions)
        => MatchContextInput(iterator, features, sequence, MatchGlyph, positions, out _);

    /// <summary>
    /// Matches the input classes of a context rule after its first glyph.
    /// </summary>
    public static bool MatchClassSequence(
        SkippingGlyphIterator iterator,
        LookupFeatures features,
        ushort[] sequence,
        ClassDefinitionTable classDefinitionTable,
        Span<int> positions)
        => MatchContextInput(iterator, features, sequence, (component, data) => component == classDefinitionTable.ClassIndexOf(data.GlyphId), positions, out _);

    /// <summary>
    /// Matches the input coverages of a context rule, the first of them at the glyph of the iterator.
    /// </summary>
    public static bool MatchCoverageSequence(SkippingGlyphIterator iterator, LookupFeatures features, CoverageTable[] coverageTables, Span<int> positions)
        => MatchCoverageInput(iterator, features, coverageTables, positions, out _);

    public static bool ApplyChainedSequenceRule(SkippingGlyphIterator iterator, LookupFeatures features, ChainedSequenceRuleTable rule, Span<int> positions)
        => MatchContextInput(iterator, features, rule.InputSequence, MatchGlyph, positions, out int end)
        && MatchBacktrack(iterator, features, rule.BacktrackSequence, MatchGlyph)
        && MatchLookahead(iterator, features, end, rule.LookaheadSequence, MatchGlyph);

    public static bool ApplyChainedClassSequenceRule(
        SkippingGlyphIterator iterator,
        LookupFeatures features,
        ChainedClassSequenceRuleTable rule,
        ClassDefinitionTable inputClassDefinitionTable,
        ClassDefinitionTable backtrackClassDefinitionTable,
        ClassDefinitionTable lookaheadClassDefinitionTable,
        Span<int> positions)
        => MatchContextInput(iterator, features, rule.InputSequence, (component, data) => component == inputClassDefinitionTable.ClassIndexOf(data.GlyphId), positions, out int end)
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
        CoverageTable[] lookahead,
        Span<int> positions)
    {
        // Check that there are enough context glyphs in the run.
        if (index - backtrack.Length < collection.RunStart || input.Length + lookahead.Length > count)
        {
            return false;
        }

        SkippingGlyphIterator iterator = new(fontMetrics, collection, index, lookupFlags);
        return MatchCoverageInput(iterator, features, input, positions, out int end)
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

    /// <summary>
    /// Gets a value indicating whether the glyph is a mark: one that mark positioning attaches past, and that takes no
    /// advance. As in HarfBuzz, the GDEF table says so when it has glyph classes. Without them, the glyph has the class
    /// that <see cref="GetSynthesizedGlyphClass"/> gives.
    /// </summary>
    /// <param name="fontMetrics">The font of the glyph.</param>
    /// <param name="shapingData">The glyph.</param>
    /// <returns><see langword="true"/> if the glyph is a mark.</returns>
    public static bool IsMarkGlyph(FontMetrics fontMetrics, GlyphShapingData shapingData)
        => !shapingData.IsUnclassified && (fontMetrics.TryGetGlyphClass(shapingData.GlyphId, out GlyphClassDef? glyphClass)
            ? glyphClass == GlyphClassDef.MarkGlyph
            : GetSynthesizedGlyphClass(shapingData) == GlyphClassDef.MarkGlyph);

    /// <summary>
    /// Gets the number of components of the glyph, as in HarfBuzz (_hb_glyph_info_get_lig_num_comps): those of the
    /// ligature that formed it, while it is that ligature and has the ligature class, and one for any other glyph,
    /// whatever characters it holds.
    /// </summary>
    /// <param name="fontMetrics">The font of the glyph.</param>
    /// <param name="shapingData">The glyph.</param>
    /// <returns>The number of components.</returns>
    public static int GetLigatureComponentCount(FontMetrics fontMetrics, GlyphShapingData shapingData)
        => shapingData.LigatureId > 0
            && shapingData.LigatureComponent == -1
            && GetGlyphShapingClass(fontMetrics, shapingData.GlyphId, shapingData).IsLigature
            ? shapingData.LigatureComponentCount
            : 1;

    public static GlyphShapingClass GetGlyphShapingClass(FontMetrics fontMetrics, ushort glyphId, GlyphShapingData shapingData)
    {
        if (shapingData.IsUnclassified)
        {
            return default;
        }

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
            GlyphClassDef synthesized = GetSynthesizedGlyphClass(shapingData);
            isMark = synthesized == GlyphClassDef.MarkGlyph;
            isBase = synthesized == GlyphClassDef.BaseGlyph;
            isLigature = synthesized == GlyphClassDef.LigatureGlyph;
        }

        return new GlyphShapingClass(isMark, isBase, isLigature, markAttachmentType);
    }

    /// <summary>
    /// Gets the class that HarfBuzz gives a glyph in a font without glyph classes. It is the class that GSUB gave the
    /// glyph, if any. Otherwise, as in hb_synthesize_glyph_classes, the glyph of a nonspacing mark that is not default
    /// ignorable is a mark, and any other glyph is a base. So a spacing or enclosing mark, and the combining grapheme
    /// joiner, are bases.
    /// </summary>
    private static GlyphClassDef GetSynthesizedGlyphClass(GlyphShapingData shapingData)
        => shapingData.SubstitutedGlyphClass ?? (CodePoint.GetGeneralCategory(shapingData.UnicodePropertiesCodePoint) == UnicodeCategory.NonSpacingMark
            && !UnicodeUtility.IsDefaultIgnorableCodePoint((uint)shapingData.UnicodePropertiesCodePoint.Value)
            ? GlyphClassDef.MarkGlyph
            : GlyphClassDef.BaseGlyph);

    private static bool MatchGlyph(ushort component, GlyphShapingData data) => component == data.GlyphId;

    private static bool MatchCoverage(CoverageTable component, GlyphShapingData data) => component.CoverageIndexOf(data.GlyphId) >= 0;

    // The first coverage is that of the glyph of the iterator. As in HarfBuzz, a rule without input matches nothing.
    private static bool MatchCoverageInput(SkippingGlyphIterator iterator, LookupFeatures features, CoverageTable[] coverageTables, Span<int> positions, out int end)
    {
        end = -1;
        return coverageTables.Length > 0
            && MatchCoverage(coverageTables[0], iterator.Collection[iterator.Index])
            && MatchContextInput(iterator, features, coverageTables.AsSpan(1), MatchCoverage, positions, out end);
    }

    // Matches the input of a context rule after its first glyph, the glyph of the iterator.
    private static bool MatchContextInput<T>(
        SkippingGlyphIterator iterator,
        LookupFeatures features,
        ReadOnlySpan<T> sequence,
        Func<T, GlyphShapingData, bool> condition,
        Span<int> positions,
        out int end)
    {
        positions[0] = iterator.Index;
        return MatchInput(iterator, features, sequence, condition, positions[1..], out end);
    }

    /// <summary>
    /// Matches the glyphs of an input sequence one by one after the glyph of the iterator, which is the first glyph of
    /// the input. It writes the index of each glyph that matched into matches, and gives the index of the last.
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
            // As in HarfBuzz, an input of more than MaxContextLength glyphs, the first of them included, matches nothing.
            if (i + 1 >= MaxContextLength || !iterator.TryMatch(1, sequence[i], condition))
            {
                return false;
            }

            matches[i] = iterator.Index;
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
