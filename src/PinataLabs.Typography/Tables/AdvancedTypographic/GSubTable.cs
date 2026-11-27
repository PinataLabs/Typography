// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using PinataLabs.Typography.Tables.AdvancedTypographic.GSub;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The Glyph Substitution (GSUB) table provides data for substitution of glyphs for appropriate rendering of scripts,
/// such as cursively-connecting forms in Arabic script, or for advanced typographic effects, such as ligatures.
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gsub"/>
/// </summary>
internal class GSubTable : Table
{
    internal const string TableName = "GSUB";

    private readonly FeatureVariationsTable? featureVariations;

    public GSubTable(ScriptList? scriptList, FeatureListTable featureList, LookupListTable lookupList, FeatureVariationsTable? featureVariations = null)
    {
        this.ScriptList = scriptList;
        this.FeatureList = featureList;
        this.LookupList = lookupList;
        this.featureVariations = featureVariations;
    }

    public ScriptList? ScriptList { get; }

    public FeatureListTable FeatureList { get; }

    public LookupListTable LookupList { get; }

    /// <summary>
    /// Creates the table that stands for the GSUB table of a font that has none. The shaper of each script still
    /// runs on it, because a shaper also composes, decomposes and reorders glyphs without a lookup, as in HarfBuzz.
    /// </summary>
    /// <returns>A table with no script, feature or lookup.</returns>
    public static GSubTable CreateEmpty() => new(null, FeatureListTable.CreateEmpty(), LookupListTable.CreateEmpty());

    public static GSubTable? Load(FontReader fontReader)
    {
        if (!fontReader.TryGetReaderAtTablePosition(TableName, out BigEndianBinaryReader? binaryReader))
        {
            return null;
        }

        GSubTable table;
        uint featureVariationsOffset;
        using (binaryReader)
        {
            table = Load(binaryReader, out featureVariationsOffset);
        }

        // The FeatureVariations table checks its own offsets, so it reads from the bytes of the table.
        FeatureVariationsTable? featureVariations = FeatureVariationsTable.Load(fontReader, TableName, featureVariationsOffset, table.FeatureList.FeatureTables.Length);
        return featureVariations is null ? table : new GSubTable(table.ScriptList, table.FeatureList, table.LookupList, featureVariations);
    }

    /// <summary>
    /// Gets the table as it substitutes glyphs for an instance of a variable font: the feature tables of the first
    /// FeatureVariations record whose conditions hold for the coordinates stand in for those of the FeatureList.
    /// </summary>
    /// <param name="coordinates">The normalised coordinates of the instance as F2Dot14 values, in <c>fvar</c> order.</param>
    /// <param name="scalars">The scalars of the GDEF Item Variation Store for the instance.</param>
    /// <returns>This table when no record holds, or else a table that shares its scripts and lookups.</returns>
    public GSubTable ForCoordinates(ReadOnlySpan<short> coordinates, in ItemVariationScalars scalars)
    {
        FeatureListTable? featureList = this.featureVariations?.Select(this.FeatureList, coordinates, scalars);
        return featureList is null ? this : new GSubTable(this.ScriptList, featureList, this.LookupList);
    }

    internal static GSubTable Load(BigEndianBinaryReader reader) => Load(reader, out _);

    private static GSubTable Load(BigEndianBinaryReader reader, out uint featureVariationsOffset)
    {
        // GSUB Header, Version 1.0
        // +----------+-------------------+-----------------------------------------------------------+
        // | Type     | Name              | Description                                               |
        // +==========+===================+===========================================================+
        // | uint16   | majorVersion      | Major version of the GSUB table, = 1                      |
        // +----------+-------------------+-----------------------------------------------------------+
        // | uint16   | minorVersion      | Minor version of the GSUB table, = 0                      |
        // +----------+-------------------+-----------------------------------------------------------+
        // | Offset16 | scriptListOffset  | Offset to ScriptList table, from beginning of GSUB table  |
        // +----------+-------------------+-----------------------------------------------------------+
        // | Offset16 | featureListOffset | Offset to FeatureList table, from beginning of GSUB table |
        // +----------+-------------------+-----------------------------------------------------------+
        // | Offset16 | lookupListOffset  | Offset to LookupList table, from beginning of GSUB table  |
        // +----------+-------------------+-----------------------------------------------------------+

        // GSUB Header, Version 1.1
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Type     | Name                    | Description                                                                   |
        // +==========+=========================+===============================================================================+
        // | uint16   | majorVersion            | Major version of the GSUB table, = 1                                          |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | uint16   | minorVersion            | Minor version of the GSUB table, = 1                                          |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset16 | scriptListOffset        | Offset to ScriptList table, from beginning of GSUB table                      |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset16 | featureListOffset       | Offset to FeatureList table, from beginning of GSUB table                     |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset16 | lookupListOffset        | Offset to LookupList table, from beginning of GSUB table                      |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset32 | featureVariationsOffset | Offset to FeatureVariations table, from beginning of GSUB table (may be NULL) |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        ushort majorVersion = reader.ReadUInt16();
        ushort minorVersion = reader.ReadUInt16();

        ushort scriptListOffset = reader.ReadOffset16();
        ushort featureListOffset = reader.ReadOffset16();
        ushort lookupListOffset = reader.ReadOffset16();
        featureVariationsOffset = (minorVersion == 1) ? reader.ReadOffset32() : 0;

        // TODO: Optimization. Allow only reading the scriptList.
        var scriptList = ScriptList.Load(reader, scriptListOffset);

        var featureList = FeatureListTable.Load(reader, featureListOffset);

        var lookupList = LookupListTable.Load(reader, lookupListOffset);

        return new GSubTable(scriptList, featureList, lookupList);
    }

    public void ApplySubstitution(FontMetrics fontMetrics, GlyphSubstitutionCollection collection)
    {
        // Set max constraints to prevent OutOfMemoryException or infinite loops from attacks.
        int maxCount = AdvancedTypographicUtils.GetMaxAllowableShapingCollectionCount(collection.Count);
        int maxOperationsCount = AdvancedTypographicUtils.GetMaxAllowableShapingOperationsCount(collection.Count);
        int currentOperations = 0;

        // Shapers must query the font that is actually being shaped, not the font of the text run.
        collection.FontMetrics = fontMetrics;

        for (int i = 0; i < collection.Count; i++)
        {
            // Choose a shaper based on the script.
            // This determines which features to apply to which glyphs.
            ScriptClass current = CodePoint.GetScriptClass(collection[i].CodePoint);

            int index = i;
            int count = 1;
            while (i < collection.Count - 1)
            {
                // We want to assign the same feature lookups to individual sections of the text rather
                // than the text as a whole to ensure that different language shapers do not interfere
                // with each other when the text contains multiple languages.
                ScriptClass next = CodePoint.GetScriptClass(collection[i + 1].CodePoint);
                if (next != current &&
                    current is not ScriptClass.Common and not ScriptClass.Unknown and not ScriptClass.Inherited &&
                    next is not ScriptClass.Common and not ScriptClass.Unknown and not ScriptClass.Inherited)
                {
                    break;
                }

                if (current is ScriptClass.Common or ScriptClass.Unknown or ScriptClass.Inherited)
                {
                    current = next;
                }

                i++;
                count++;

                if (i >= maxCount)
                {
                    break;
                }
            }

            BaseShaper shaper = ShaperFactory.Create(current, this.GetScriptTag(current), collection.TextOptions);

            // Plan substitution features for each glyph.
            // Shapers can adjust the count during initialization and feature processing so we must capture
            // the current count to allow resetting indexes and processing counts.
            ResolveVerticalForms(fontMetrics, this, collection, index, count, current);
            int collectionCount = collection.Count;
            shaper.Plan(collection, index, count);
            int delta = collection.Count - collectionCount;
            i += delta;
            count += delta;

            IReadOnlyList<ShapingStage> stages = shaper.GetShapingStages();
            int requiredFeatureStage = LookupFeatures.GetRequiredFeatureStage(this.ScriptList, this.FeatureList, current, stages);
            SkippingGlyphIterator iterator = new(fontMetrics, collection, index, default);
            for (int s = 0; s < stages.Count; s++)
            {
                ShapingStage stage = stages[s];
                collectionCount = collection.Count;
                stage.PreProcessFeatures(collection, index, count);

                // Account for substitutions changing the length of the collection.
                delta = collection.Count - collectionCount;
                count += delta;
                i += delta;

                this.ApplyFeatures(
                    fontMetrics,
                    collection,
                    ref iterator,
                    stage.Features,
                    s == requiredFeatureStage,
                    current,
                    index,
                    ref count,
                    ref i,
                    ref collectionCount,
                    maxCount,
                    maxOperationsCount,
                    ref currentOperations);

                collectionCount = collection.Count;
                stage.PostProcessFeatures(collection, index, count);

                // Account for substitutions changing the length of the collection.
                delta = collection.Count - collectionCount;
                count += delta;
                i += delta;
            }
        }
    }

    /// <summary>
    /// Resolves, once per glyph and before any features are planned, whether each <c>Tr</c> (transform-rotate)
    /// glyph in a vertical mixed layout has a vertical form in this font. Glyphs the <c>vert</c> feature
    /// substitutes are laid out upright; the others stay rotated and are shaped like any horizontal glyph.
    /// </summary>
    private static void ResolveVerticalForms(
        FontMetrics fontMetrics,
        GSubTable table,
        GlyphSubstitutionCollection collection,
        int index,
        int count,
        ScriptClass script)
    {
        if (!collection.TextOptions.LayoutMode.IsVerticalMixed())
        {
            return;
        }

        Tag vert = Tag.Parse("vert");
        PlannedFeature[] vertOnly = [new(vert)];
        if (!table.TryGetFeatureLookups(vert, script, out _))
        {
            return;
        }

        for (int i = index; i < index + count; i++)
        {
            GlyphShapingData data = collection[i];
            if (data.HasVerticalForm || CodePoint.GetVerticalOrientationType(data.CodePoint) != VerticalOrientationType.TransformRotate)
            {
                continue;
            }

            // Probe the vert lookups with the glyph on its own.
            GlyphSubstitutionCollection probe = new(collection.TextOptions) { FontMetrics = fontMetrics };
            probe.AddGlyph(data.GlyphId, data.CodePoint, data.Direction, data.TextRun, 0);
            probe.AddShapingFeature(0, new TagEntry(vert, true));

            SkippingGlyphIterator iterator = new(fontMetrics, probe, 0, default);
            int probeCount = 1;
            int probeIndex = 0;
            int probeCollectionCount = 1;
            int operations = 0;
            table.ApplyFeatures(fontMetrics, probe, ref iterator, vertOnly, false, script, 0, ref probeCount, ref probeIndex, ref probeCollectionCount, AdvancedTypographicUtils.GetMaxAllowableShapingCollectionCount(1), AdvancedTypographicUtils.GetMaxAllowableShapingOperationsCount(1), ref operations);
            data.HasVerticalForm = probe[0].GlyphId != data.GlyphId;
        }
    }

    /// <summary>
    /// Applies the lookups of all the given features in LookupList order, as the OpenType specification
    /// requires. Each lookup is applied only to the glyphs that have a feature which references it, and
    /// the lookups of the required feature, when included, to every glyph.
    /// </summary>
    internal void ApplyFeatures(
        FontMetrics fontMetrics,
        GlyphSubstitutionCollection collection,
        ref SkippingGlyphIterator iterator,
        IReadOnlyList<PlannedFeature> features,
        bool withRequiredFeature,
        ScriptClass current,
        int index,
        ref int count,
        ref int i,
        ref int collectionCount,
        int maxCount,
        int maxOperationsCount,
        ref int currentOperations)
    {
        if (!LookupFeatures.TryGetLookups(this.ScriptList, this.FeatureList, this.LookupList.LookupTables.Length, features, current, withRequiredFeature, positioning: false, out List<(ushort Index, LookupFeatures Features)>? lookups))
        {
            return;
        }

        foreach ((ushort lookupIndex, LookupFeatures lookupFeatures) in lookups)
        {
            LookupTable lookupTable = this.LookupList.LookupTables[lookupIndex];
            if (lookupTable.IsReverse)
            {
                // A reverse chaining lookup runs from the last glyph to the first, so that the glyph it substitutes
                // can be the lookahead of the glyph before it. It substitutes one glyph for one.
                iterator.Reset(index + count, lookupTable.LookupFlags);
                while (iterator.Increment(-1) >= index)
                {
                    if (currentOperations++ >= maxOperationsCount)
                    {
                        return;
                    }

                    if (lookupFeatures.AnyEnabledOn(collection[iterator.Index].Features))
                    {
                        lookupTable.TrySubstitution(fontMetrics, this, collection, lookupFeatures, iterator.Index, count - (iterator.Index - index));
                    }
                }

                continue;
            }

            iterator.Start(index, lookupTable.LookupFlags);
            while (iterator.Index < index + count)
            {
                if (collection.Count >= maxCount || currentOperations++ >= maxOperationsCount)
                {
                    return;
                }

                if (!lookupFeatures.AnyEnabledOn(collection[iterator.Index].Features))
                {
                    iterator.Next();
                    continue;
                }

                collectionCount = collection.Count;
                lookupTable.TrySubstitution(fontMetrics, this, collection, lookupFeatures, iterator.Index, count - (iterator.Index - index));
                iterator.Next();

                // Account for substitutions changing the length of the collection.
                int delta = collection.Count - collectionCount;
                count += delta;
                i += delta;
            }
        }
    }

    internal bool TryGetFeatureLookups(
        in Tag feature,
        ScriptClass script,
        [NotNullWhen(true)] out List<(ushort Index, LookupFeatures Features)>? value)
        => this.TryGetFeatureLookups([feature], script, out value);

    internal bool TryGetFeatureLookups(
        IReadOnlyList<Tag> stageFeatures,
        ScriptClass script,
        [NotNullWhen(true)] out List<(ushort Index, LookupFeatures Features)>? value)
    {
        PlannedFeature[] features = new PlannedFeature[stageFeatures.Count];
        for (int i = 0; i < features.Length; i++)
        {
            features[i] = new(stageFeatures[i]);
        }

        return LookupFeatures.TryGetLookups(this.ScriptList, this.FeatureList, this.LookupList.LookupTables.Length, features, script, false, positioning: false, out value);
    }

    /// <summary>
    /// Gets the tag of the script that the table has for text of a Unicode script. The shaper of the text
    /// depends on it: see <see cref="ShaperFactory"/>.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <returns>
    /// The tag, which can be <c>DFLT</c>, <c>dflt</c> or <c>latn</c>, or the default tag when no script applies.
    /// </returns>
    public Tag GetScriptTag(ScriptClass script)
        => this.ScriptList?.ChooseScript(script)?.ScriptTag ?? default;
}
