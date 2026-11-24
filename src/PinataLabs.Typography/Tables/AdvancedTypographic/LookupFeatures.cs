// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The features of a shaping stage that reference one lookup.
/// The lookup applies to a glyph that has any of them enabled.
/// </summary>
internal readonly struct LookupFeatures
{
    private readonly Tag[]? tags;
    private readonly int offset;
    private readonly int count;

    public LookupFeatures(Tag[] tags, int offset, int count)
    {
        this.tags = tags;
        this.offset = offset;
        this.count = count;
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
    /// <param name="value">The lookups, when the features reference any.</param>
    /// <returns><see langword="true"/> when the features reference a lookup.</returns>
    public static bool TryGetLookups(
        ScriptList? scriptList,
        FeatureListTable featureList,
        int lookupCount,
        IReadOnlyList<Tag> features,
        ScriptClass script,
        [NotNullWhen(true)] out List<(ushort Index, LookupFeatures Features)>? value)
    {
        // No language is requested, so only one language system applies. A script without one has no features.
        LangSysTable? langSysTable = scriptList?.GetDefaultLangSys(script);
        if (langSysTable is null)
        {
            value = null;
            return false;
        }

        value = GetLookups(featureList, lookupCount, features, langSysTable);
        return value.Count > 0;
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
        IReadOnlyList<Tag> features,
        LangSysTable langSysTable)
    {
        List<(ushort Index, int Order, Tag Feature)> references = [];
        ushort[] featureIndices = langSysTable.FeatureIndices;
        for (int i = 0; i < featureIndices.Length; i++)
        {
            // Skip feature indices that fall outside the feature list.
            if (featureIndices[i] >= featureList.FeatureTables.Length)
            {
                continue;
            }

            FeatureTable featureTable = featureList.FeatureTables[featureIndices[i]];
            Tag feature = featureTable.FeatureTag;

            int order = IndexOf(features, feature);
            if (order < 0)
            {
                continue;
            }

            ushort[] lookupListIndices = featureTable.LookupListIndices;
            for (int j = 0; j < lookupListIndices.Length; j++)
            {
                ushort lookupIndex = lookupListIndices[j];

                // Skip lookup indices that fall outside the lookup list.
                if (lookupIndex >= lookupCount)
                {
                    continue;
                }

                references.Add(new(lookupIndex, order, feature));
            }
        }

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
            for (end = start; end < references.Count && references[end].Index == lookupIndex; end++)
            {
                // Feature records that share a tag, or a feature that lists a lookup twice, repeat a reference.
                if (end == start || references[end].Order != references[end - 1].Order)
                {
                    tags[tagCount++] = references[end].Feature;
                }
            }

            lookups.Add(new(lookupIndex, new LookupFeatures(tags, tagStart, tagCount - tagStart)));
        }

        return lookups;
    }

    private static int IndexOf(IReadOnlyList<Tag> tags, Tag tag)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i] == tag)
            {
                return i;
            }
        }

        return -1;
    }
}
