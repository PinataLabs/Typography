// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The features of a shaping stage that reference one lookup.
/// The lookup applies to a glyph that has any of them enabled, and to every glyph when the
/// required feature of the language system references it. It skips the joiners that none of them keeps.
/// </summary>
internal readonly struct LookupFeatures
{
    private static readonly Tag VertTag = Tag.Parse("vert");

    private readonly Tag[]? tags;
    private readonly int offset;
    private readonly int count;
    private readonly bool required;

    private LookupFeatures(Tag[] tags, int offset, int count, bool required, ManualJoiners manualJoiners, bool perSyllable, bool positioning)
    {
        this.tags = tags;
        this.offset = offset;
        this.count = count;
        this.required = required;
        (this.InputIgnorables, this.ContextIgnorables) = GetSkippableIgnorables(manualJoiners, positioning);

        // As in HarfBuzz, only GSUB matches per syllable.
        this.PerSyllable = perSyllable && !positioning;
    }

    /// <summary>
    /// Gets the default-ignorable glyphs that the lookup may skip between the glyphs of its input.
    /// </summary>
    public SkippableIgnorables InputIgnorables { get; }

    /// <summary>
    /// Gets the default-ignorable glyphs that the lookup may skip in its backtrack and its lookahead.
    /// </summary>
    public SkippableIgnorables ContextIgnorables { get; }

    /// <summary>
    /// Gets a value indicating whether the lookup matches only glyphs of the syllable of the glyph that it applies to.
    /// </summary>
    public bool PerSyllable { get; }

    /// <summary>
    /// Gets the features of a GSUB lookup that a shaper applies to every glyph on its own, outside the features of
    /// the font. As in HarfBuzz, it skips the joiners wherever HarfBuzz skips them by default.
    /// </summary>
    /// <returns>The features.</returns>
    public static LookupFeatures ForShaperSubstitution() => new([], 0, 0, true, ManualJoiners.None, false, false);

    /// <summary>
    /// Gets the features without the ones that the caller turned off. A shaper still plans them, so the actions of its
    /// stages run, but their lookups do not apply.
    /// </summary>
    /// <param name="features">The features, in the order in which the shaper planned them.</param>
    /// <param name="disabled">The tags of the features that the caller turned off.</param>
    /// <returns>The features that stay on.</returns>
    public static IReadOnlyList<PlannedFeature> WithoutDisabled(IReadOnlyList<PlannedFeature> features, IReadOnlyList<Tag> disabled)
    {
        if (disabled.Count == 0)
        {
            return features;
        }

        List<PlannedFeature> enabled = new(features.Count);
        foreach (PlannedFeature feature in features)
        {
            if (!IsDisabled(disabled, feature.Tag))
            {
                enabled.Add(feature);
            }
        }

        return enabled;
    }

    /// <summary>
    /// Gets a value indicating whether the caller turned a feature off.
    /// </summary>
    /// <param name="disabled">The tags of the features that the caller turned off.</param>
    /// <param name="tag">The tag of the feature.</param>
    /// <returns><see langword="true"/> if the feature is off.</returns>
    public static bool IsDisabled(IReadOnlyList<Tag> disabled, Tag tag)
    {
        for (int i = 0; i < disabled.Count; i++)
        {
            if (disabled[i] == tag)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the lookups that the given features reference for a script, in LookupList order, as the
    /// OpenType specification requires them to be applied. Each lookup appears once, with all the
    /// features that reference it.
    /// </summary>
    /// <param name="scriptList">The script list of the GSUB or GPOS table.</param>
    /// <param name="featureList">The feature list of the table.</param>
    /// <param name="lookupCount">The number of lookups in the LookupList of the table.</param>
    /// <param name="features">The features, in the order in which the shaper planned them.</param>
    /// <param name="script">The script of the text.</param>
    /// <param name="language">The tag of the language system of the text, or <see langword="null"/> when no language is requested.</param>
    /// <param name="withRequiredFeature">Whether the lookups of the required feature of the language system are included.</param>
    /// <param name="positioning">Whether the lookups are those of GPOS, which skips more default-ignorable glyphs than GSUB.</param>
    /// <param name="value">The lookups, when the features reference any.</param>
    /// <returns><see langword="true"/> when the features reference a lookup.</returns>
    public static bool TryGetLookups(
        ScriptList? scriptList,
        FeatureListTable featureList,
        int lookupCount,
        IReadOnlyList<PlannedFeature> features,
        ScriptClass script,
        Tag? language,
        bool withRequiredFeature,
        bool positioning,
        [NotNullWhen(true)] out List<(ushort Index, LookupFeatures Features)>? value)
    {
        // A script without a language system has no features but vert.
        LangSysTable? langSysTable = scriptList?.GetLangSys(script, language);
        value = GetLookups(featureList, lookupCount, features, langSysTable, withRequiredFeature, positioning);
        return value.Count > 0;
    }

    /// <summary>
    /// Gets the shaping stage whose lookups include those of the required feature. As in HarfBuzz, this is the
    /// stage that plans a feature with the tag of the required feature, or else the first stage.
    /// </summary>
    /// <param name="scriptList">The script list of the GSUB table.</param>
    /// <param name="featureList">The feature list of the table.</param>
    /// <param name="script">The script of the text.</param>
    /// <param name="language">The tag of the language system of the text, or <see langword="null"/> when no language is requested.</param>
    /// <param name="stages">The shaping stages.</param>
    /// <returns>The index of the stage.</returns>
    public static int GetRequiredFeatureStage(
        ScriptList? scriptList,
        FeatureListTable featureList,
        ScriptClass script,
        Tag? language,
        IReadOnlyList<ShapingStage> stages)
    {
        LangSysTable? langSysTable = scriptList?.GetLangSys(script, language);
        if (langSysTable is null || langSysTable.RequiredFeatureIndex >= featureList.FeatureTables.Length)
        {
            return 0;
        }

        Tag feature = featureList.FeatureTables[langSysTable.RequiredFeatureIndex].FeatureTag;
        for (int i = 0; i < stages.Count; i++)
        {
            if (IndexOf(stages[i].Features, feature) >= 0)
            {
                return i;
            }
        }

        return 0;
    }

    public bool Contains(Tag tag)
    {
        for (int i = 0; i < this.count; i++)
        {
            if (this.tags![this.offset + i] == tag)
            {
                return true;
            }
        }

        return false;
    }

    public bool AnyEnabledOn(List<TagEntry> glyphFeatures)
    {
        if (this.required)
        {
            return true;
        }

        for (int i = 0; i < glyphFeatures.Count; i++)
        {
            TagEntry entry = glyphFeatures[i];
            if (!entry.Enabled)
            {
                continue;
            }

            for (int j = 0; j < this.count; j++)
            {
                if (this.tags![this.offset + j] == entry.Tag)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<(ushort Index, LookupFeatures Features)> GetLookups(
        FeatureListTable featureList,
        int lookupCount,
        IReadOnlyList<PlannedFeature> features,
        LangSysTable? langSysTable,
        bool withRequiredFeature,
        bool positioning)
    {
        List<(ushort Index, int Order, Tag Feature, bool Required)> references = [];
        ushort[] featureIndices = langSysTable?.FeatureIndices ?? [];
        bool hasVert = false;
        for (int i = 0; i < featureIndices.Length; i++)
        {
            // Skip feature indices that fall outside the feature list.
            if (featureIndices[i] >= featureList.FeatureTables.Length)
            {
                continue;
            }

            FeatureTable featureTable = featureList.FeatureTables[featureIndices[i]];
            hasVert |= featureTable.FeatureTag == VertTag;
            int order = IndexOf(features, featureTable.FeatureTag);
            if (order >= 0)
            {
                AddReferences(references, featureTable, lookupCount, order, false);
            }
        }

        // As in HarfBuzz, vertical text gets the first vert feature of the font when the language system lacks one.
        int vertOrder = IndexOf(features, VertTag);
        if (!hasVert && vertOrder >= 0)
        {
            for (int i = 0; i < featureList.FeatureTables.Length; i++)
            {
                if (featureList.FeatureTables[i].FeatureTag == VertTag)
                {
                    AddReferences(references, featureList.FeatureTables[i], lookupCount, vertOrder, false);
                    break;
                }
            }
        }

        // The shaper need not plan the tag of the required feature. Its order is then -1, before every planned feature.
        if (withRequiredFeature && langSysTable is not null && langSysTable.RequiredFeatureIndex < featureList.FeatureTables.Length)
        {
            FeatureTable featureTable = featureList.FeatureTables[langSysTable.RequiredFeatureIndex];
            AddReferences(references, featureTable, lookupCount, IndexOf(features, featureTable.FeatureTag), true);
        }

        // GPOS matches across syllables, and so do the lookups of a stage that plans no feature per syllable.
        List<bool>? perSyllableFlags = !positioning && HasPerSyllableFeature(features) ? GetPerSyllableFlags(references, features) : null;

        // List.Sort is unstable. The plan order of the feature makes the order total,
        // so the result does not depend on the order of the feature records.
        references.Sort((x, y) => x.Index != y.Index ? x.Index - y.Index : x.Order - y.Order);

        List<(ushort Index, LookupFeatures Features)> lookups = [];
        Tag[] tags = new Tag[references.Count];
        int tagCount = 0;
        int end;
        for (int start = 0; start < references.Count; start = end)
        {
            ushort lookupIndex = references[start].Index;
            int tagStart = tagCount;
            bool required = false;

            // As in HarfBuzz, a lookup keeps a joiner when any of its features does, and the required feature keeps none.
            ManualJoiners manualJoiners = ManualJoiners.None;
            for (end = start; end < references.Count && references[end].Index == lookupIndex; end++)
            {
                required |= references[end].Required;
                if (!references[end].Required && references[end].Order >= 0)
                {
                    manualJoiners |= features[references[end].Order].ManualJoiners;
                }

                // Feature records that share a tag, or a feature that lists a lookup twice, repeat a reference.
                if (end == start || references[end].Order != references[end - 1].Order)
                {
                    tags[tagCount++] = references[end].Feature;
                }
            }

            // The flags and the references are both in the order of the lookup index, one flag for each lookup.
            bool perSyllable = perSyllableFlags?[lookups.Count] ?? false;
            lookups.Add(new(lookupIndex, new LookupFeatures(tags, tagStart, tagCount - tagStart, required, manualJoiners, perSyllable, positioning)));
        }

        return lookups;
    }

    private static bool HasPerSyllableFeature(IReadOnlyList<PlannedFeature> features)
    {
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i].PerSyllable)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets, for each lookup of a stage in the order of its index, whether it matches per syllable, as HarfBuzz 15.0.0
    /// decides it. HarfBuzz adds an entry for each lookup of the required feature, and then of each feature in the order
    /// of the tags. It sorts the entries by lookup index with <c>hb_qsort_inline</c>, and merges each lookup into its
    /// first entry, which keeps its per-syllable flag. The required feature does not match per syllable.
    /// </summary>
    /// <param name="references">The references of the lookups of the stage, in the order of the language system.</param>
    /// <param name="features">The features of the stage.</param>
    /// <returns>The flag of each lookup.</returns>
    private static List<bool> GetPerSyllableFlags(List<(ushort Index, int Order, Tag Feature, bool Required)> references, IReadOnlyList<PlannedFeature> features)
    {
        // Array.Sort is unstable. The place of the reference keeps the order of the records and of their lookups.
        int[] order = new int[references.Count];
        for (int i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        Array.Sort(order, (x, y) =>
        {
            (_, _, Tag xFeature, bool xRequired) = references[x];
            (_, _, Tag yFeature, bool yRequired) = references[y];
            if (xRequired != yRequired)
            {
                return xRequired ? -1 : 1;
            }

            return !xRequired && xFeature != yFeature ? xFeature.Value.CompareTo(yFeature.Value) : x - y;
        });

        (ushort Index, bool PerSyllable)[] entries = new (ushort, bool)[order.Length];
        for (int i = 0; i < order.Length; i++)
        {
            (ushort index, int planOrder, _, bool required) = references[order[i]];
            entries[i] = (index, !required && planOrder >= 0 && features[planOrder].PerSyllable);
        }

        SortAsHarfBuzz(entries);

        List<bool> flags = [];
        for (int i = 0; i < entries.Length; i++)
        {
            if (i == 0 || entries[i].Index != entries[i - 1].Index)
            {
                flags.Add(entries[i].PerSyllable);
            }
        }

        return flags;
    }

    /// <summary>
    /// Sorts the entries by lookup index as <c>hb_qsort_inline</c> of HarfBuzz 15.0.0 does: a quicksort that stops at
    /// ranges of 24 entries or fewer, and then one insertion sort. The quicksort is not stable, so this gives entries of
    /// one lookup the order that HarfBuzz gives them.
    /// </summary>
    /// <param name="entries">The entries.</param>
    private static void SortAsHarfBuzz(Span<(ushort Index, bool PerSyllable)> entries)
    {
        PartitionAsHarfBuzz(entries);
        for (int i = 1; i < entries.Length; i++)
        {
            for (int j = i; j > 0 && entries[j - 1].Index > entries[j].Index; j--)
            {
                (entries[j - 1], entries[j]) = (entries[j], entries[j - 1]);
            }
        }
    }

    // The partitioning loop of hb_qsort_loop: a median-of-three pivot and a two-way Hoare partition.
    private static void PartitionAsHarfBuzz(Span<(ushort Index, bool PerSyllable)> entries)
    {
        while (entries.Length > 24)
        {
            int last = entries.Length - 1;
            int mid = entries.Length / 2;
            if (entries[0].Index > entries[mid].Index)
            {
                (entries[0], entries[mid]) = (entries[mid], entries[0]);
            }

            if (entries[mid].Index > entries[last].Index)
            {
                (entries[mid], entries[last]) = (entries[last], entries[mid]);
                if (entries[0].Index > entries[mid].Index)
                {
                    (entries[0], entries[mid]) = (entries[mid], entries[0]);
                }
            }

            (entries[mid], entries[last - 1]) = (entries[last - 1], entries[mid]);
            ushort pivot = entries[last - 1].Index;
            int i = 0;
            int j = last - 1;
            while (true)
            {
                while (entries[++i].Index < pivot)
                {
                }

                while (entries[--j].Index > pivot)
                {
                }

                if (i >= j)
                {
                    break;
                }

                (entries[i], entries[j]) = (entries[j], entries[i]);
            }

            (entries[i], entries[last - 1]) = (entries[last - 1], entries[i]);

            // Recurse on the smaller side, and go on with the larger.
            int right = entries.Length - i - 1;
            if (i < right)
            {
                PartitionAsHarfBuzz(entries.Slice(0, i));
                entries = entries.Slice(i + 1);
            }
            else
            {
                PartitionAsHarfBuzz(entries.Slice(i + 1));
                entries = entries.Slice(0, i);
            }
        }
    }

    /// <summary>
    /// Gets the default-ignorable glyphs that a lookup may skip, as HarfBuzz does: GSUB skips ZWNJ in the backtrack and
    /// the lookahead only, GPOS always skips it, and both skip ZWJ in the backtrack and the lookahead, and in the input
    /// of a lookup that does not keep it. Only GPOS skips the hidden default-ignorable glyphs.
    /// </summary>
    private static (SkippableIgnorables Input, SkippableIgnorables Context) GetSkippableIgnorables(ManualJoiners manualJoiners, bool positioning)
    {
        SkippableIgnorables inputZwj = (manualJoiners & ManualJoiners.Zwj) == 0 ? SkippableIgnorables.Zwj : SkippableIgnorables.None;
        if (positioning)
        {
            const SkippableIgnorables skipped = SkippableIgnorables.Other | SkippableIgnorables.Zwnj | SkippableIgnorables.Hidden;
            return (skipped | inputZwj, skipped | SkippableIgnorables.Zwj);
        }

        SkippableIgnorables contextZwnj = (manualJoiners & ManualJoiners.Zwnj) == 0 ? SkippableIgnorables.Zwnj : SkippableIgnorables.None;
        return (SkippableIgnorables.Other | inputZwj, SkippableIgnorables.Other | SkippableIgnorables.Zwj | contextZwnj);
    }

    private static void AddReferences(
        List<(ushort Index, int Order, Tag Feature, bool Required)> references,
        FeatureTable featureTable,
        int lookupCount,
        int order,
        bool required)
    {
        ushort[] lookupListIndices = featureTable.LookupListIndices;
        for (int i = 0; i < lookupListIndices.Length; i++)
        {
            // Skip lookup indices that fall outside the lookup list.
            if (lookupListIndices[i] < lookupCount)
            {
                references.Add(new(lookupListIndices[i], order, featureTable.FeatureTag, required));
            }
        }
    }

    private static int IndexOf(IReadOnlyList<PlannedFeature> features, Tag tag)
    {
        for (int i = 0; i < features.Count; i++)
        {
            if (features[i].Tag == tag)
            {
                return i;
            }
        }

        return -1;
    }
}
