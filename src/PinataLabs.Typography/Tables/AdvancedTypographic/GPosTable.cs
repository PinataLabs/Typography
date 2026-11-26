// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Tables.AdvancedTypographic.GPos;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The Glyph Positioning table (GPOS) provides precise control over glyph placement for
/// sophisticated text layout and rendering in each script and language system that a font supports.
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gpos"/>
/// </summary>
internal class GPosTable : Table
{
    internal static readonly Tag MarkTag = Tag.Parse("mark");

    internal static readonly Tag KernTag = Tag.Parse("kern");

    private static readonly Tag VKernTag = Tag.Parse("vkrn");

    internal const string TableName = "GPOS";

    private readonly FeatureVariationsTable? featureVariations;

    public GPosTable(ScriptList? scriptList, FeatureListTable featureList, LookupListTable lookupList, FeatureVariationsTable? featureVariations = null)
    {
        this.ScriptList = scriptList;
        this.FeatureList = featureList;
        this.LookupList = lookupList;
        this.featureVariations = featureVariations;
    }

    public ScriptList? ScriptList { get; }

    public FeatureListTable FeatureList { get; }

    public LookupListTable LookupList { get; }

    public static GPosTable? Load(FontReader fontReader)
    {
        if (!fontReader.TryGetReaderAtTablePosition(TableName, out BigEndianBinaryReader? binaryReader))
        {
            return null;
        }

        GPosTable table;
        uint featureVariationsOffset;
        using (binaryReader)
        {
            table = Load(binaryReader, out featureVariationsOffset);
        }

        // The FeatureVariations table checks its own offsets, so it reads from the bytes of the table.
        FeatureVariationsTable? featureVariations = FeatureVariationsTable.Load(fontReader, TableName, featureVariationsOffset, table.FeatureList.FeatureTables.Length);
        return featureVariations is null ? table : new GPosTable(table.ScriptList, table.FeatureList, table.LookupList, featureVariations);
    }

    /// <summary>
    /// Gets the table as it positions glyphs for an instance of a variable font: the feature tables of the first
    /// FeatureVariations record whose conditions hold for the coordinates stand in for those of the FeatureList.
    /// </summary>
    /// <param name="coordinates">The normalised coordinates of the instance as F2Dot14 values, in <c>fvar</c> order.</param>
    /// <param name="scalars">The scalars of the GDEF Item Variation Store for the instance.</param>
    /// <returns>This table when no record holds, or else a table that shares its scripts and lookups.</returns>
    public GPosTable ForCoordinates(ReadOnlySpan<short> coordinates, in ItemVariationScalars scalars)
    {
        FeatureListTable? featureList = this.featureVariations?.Select(this.FeatureList, coordinates, scalars);
        return featureList is null ? this : new GPosTable(this.ScriptList, featureList, this.LookupList);
    }

    internal static GPosTable Load(BigEndianBinaryReader reader) => Load(reader, out _);

    private static GPosTable Load(BigEndianBinaryReader reader, out uint featureVariationsOffset)
    {
        // GPOS Header, Version 1.0
        // +----------+-------------------+-----------------------------------------------------------+
        // | Type     | Name              | Description                                               |
        // +==========+===================+===========================================================+
        // | uint16   | majorVersion      | Major version of the GPOS table, = 1                      |
        // +----------+-------------------+-----------------------------------------------------------+
        // | uint16   | minorVersion      | Minor version of the GPOS table, = 0                      |
        // +----------+-------------------+-----------------------------------------------------------+
        // | Offset16 | scriptListOffset  | Offset to ScriptList table, from beginning of GPOS table  |
        // +----------+-------------------+-----------------------------------------------------------+
        // | Offset16 | featureListOffset | Offset to FeatureList table, from beginning of GPOS table |
        // +----------+-------------------+-----------------------------------------------------------+
        // | Offset16 | lookupListOffset  | Offset to LookupList table, from beginning of GPOS table  |
        // +----------+-------------------+-----------------------------------------------------------+

        // GPOS Header, Version 1.1
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Type     | Name                    | Description                                                                   |
        // +==========+=========================+===============================================================================+
        // | uint16   | majorVersion            | Major version of the GPOS table, = 1                                          |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | uint16   | minorVersion            | Minor version of the GPOS table, = 1                                          |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset16 | scriptListOffset        | Offset to ScriptList table, from beginning of GPOS table                      |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset16 | featureListOffset       | Offset to FeatureList table, from beginning of GPOS table                     |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset16 | lookupListOffset        | Offset to LookupList table, from beginning of GPOS table                      |
        // +----------+-------------------------+-------------------------------------------------------------------------------+
        // | Offset32 | featureVariationsOffset | Offset to FeatureVariations table, from beginning of GPOS table (may be NULL) |
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

        return new GPosTable(scriptList, featureList, lookupList);
    }

    public bool TryUpdatePositions(FontMetrics fontMetrics, GlyphPositioningCollection collection)
    {
        // Set max constraints to prevent OutOfMemoryException or infinite loops from attacks.
        int maxCount = AdvancedTypographicUtils.GetMaxAllowableShapingCollectionCount(collection.Count);
        int maxOperationsCount = AdvancedTypographicUtils.GetMaxAllowableShapingOperationsCount(collection.Count);
        int currentOperations = 0;
        bool maxOperationsReached = false;

        bool updated = false;
        for (int i = 0; i < collection.Count; i++)
        {
            if (!collection.ShouldProcess(fontMetrics, i))
            {
                continue;
            }

            ScriptClass current = CodePoint.GetScriptClass(collection[i].CodePoint);

            int index = i;
            int count = 1;
            while (i < collection.Count - 1)
            {
                // We want to assign the same feature lookups to individual sections of the text rather
                // than the text as a whole to ensure that different language shapers do not interfere
                // with each other when the text contains multiple languages.
                GlyphShapingData nextData = collection[i + 1];
                ScriptClass next = CodePoint.GetScriptClass(nextData.CodePoint);
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

            if (!this.PositionsScript(current))
            {
                if (i >= maxCount)
                {
                    return updated;
                }

                continue;
            }

            // As in HarfBuzz, the script of the GSUB table chooses the shaper for both tables.
            Tag substitutionScript = fontMetrics.GetGSubTable().GetScriptTag(current);
            BaseShaper shaper = ShaperFactory.Create(current, substitutionScript, collection.TextOptions);

            if (shaper.MarkZeroingMode == MarkZeroingMode.PreGPos)
            {
                ZeroMarkAdvances(fontMetrics, collection, index, count);
            }

            // Plan positioning features for each glyph.
            shaper.Plan(collection, index, count);

            // Positioning has no stage boundaries. The shapers pause only between substitutions, and
            // their stage actions act on substitution collections alone, so the lookups of every
            // planned feature are applied together.
            List<Tag> features = [];
            foreach (ShapingStage stage in shaper.GetShapingStages())
            {
                features.AddRange(stage.FeatureTags);
            }

            bool withRequiredFeature = collection.TextOptions.KerningMode != KerningMode.None
                || !this.RequiredFeatureIsKerning(current);

            if (LookupFeatures.TryGetLookups(this.ScriptList, this.FeatureList, this.LookupList.LookupTables.Length, features, current, withRequiredFeature, out List<(ushort Index, LookupFeatures Features)>? lookups))
            {
                SkippingGlyphIterator iterator = new(fontMetrics, collection, index, default);
                foreach ((ushort lookupIndex, LookupFeatures lookupFeatures) in lookups)
                {
                    LookupTable lookupTable = this.LookupList.LookupTables[lookupIndex];
                    iterator.Reset(index, lookupTable.LookupFlags);

                    while (iterator.Index < index + count)
                    {
                        if (currentOperations++ >= maxOperationsCount)
                        {
                            maxOperationsReached = true;
                            goto EndLookups;
                        }

                        if (lookupFeatures.AnyEnabledOn(collection[iterator.Index].Features))
                        {
                            updated |= lookupTable.TryUpdatePosition(fontMetrics, this, collection, lookupFeatures, iterator.Index, count - (iterator.Index - index));
                        }

                        iterator.Next();
                    }
                }
            }

            EndLookups:
            if (shaper.MarkZeroingMode == MarkZeroingMode.PostGpos)
            {
                ZeroMarkAdvances(fontMetrics, collection, index, count);
            }

            FixCursiveAttachment(collection, index, count);
            FixMarkAttachment(collection, index, count);
            UpdatePositions(fontMetrics, collection, index, count);

            if (i >= maxCount || maxOperationsReached)
            {
                return updated;
            }
        }

        return updated;
    }

    /// <summary>
    /// Gets a value indicating whether the table positions text of the script. When it does not, the text is
    /// positioned as in a font without GPOS. See <see cref="ShaperFactory.NeedsOwnGposScript"/>.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <returns><see langword="true"/> if the table positions the text.</returns>
    public bool PositionsScript(ScriptClass script)
        => !ShaperFactory.NeedsOwnGposScript(script) || this.HasScriptOf(script);

    /// <summary>
    /// Gets a value indicating whether the language system that the table uses for the script lists the feature.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="feature">The tag of the feature.</param>
    /// <returns><see langword="true"/> if the language system lists a feature with the tag.</returns>
    public bool HasFeature(ScriptClass script, Tag feature)
    {
        LangSysTable? langSysTable = this.ScriptList?.GetDefaultLangSys(script);
        if (langSysTable is null)
        {
            return false;
        }

        FeatureTable[] features = this.FeatureList.FeatureTables;
        foreach (ushort index in langSysTable.FeatureIndices)
        {
            if (index < features.Length && features[index].FeatureTag == feature)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasScriptOf(ScriptClass script)
    {
        if (this.ScriptList is null)
        {
            return false;
        }

        foreach (Tag tag in UnicodeScriptTagMap.Instance[script])
        {
            if (this.ScriptList.ContainsKey(tag))
            {
                return true;
            }
        }

        return false;
    }

    private static void FixCursiveAttachment(GlyphPositioningCollection collection, int index, int count)
    {
        LayoutMode layoutMode = collection.TextOptions.LayoutMode;
        for (int i = 0; i < count; i++)
        {
            int currentIndex = i + index;
            GlyphShapingData data = collection[currentIndex];
            if (data.CursiveAttachment != -1)
            {
                int j = data.CursiveAttachment + currentIndex;
                if (j > count)
                {
                    return;
                }

                GlyphShapingData cursiveData = collection[j];
                if (!AdvancedTypographicUtils.IsVerticalGlyph(data.CodePoint, layoutMode, data.HasVerticalForm))
                {
                    data.Bounds.Y += cursiveData.Bounds.Y;
                }
                else
                {
                    data.Bounds.X += cursiveData.Bounds.X;
                }
            }
        }
    }

    private static void FixMarkAttachment(GlyphPositioningCollection collection, int index, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int currentIndex = i + index;
            GlyphShapingData data = collection[currentIndex];
            if (data.MarkAttachment != -1)
            {
                int j = data.MarkAttachment;
                GlyphShapingData markData = collection[j];
                data.Bounds.X += markData.Bounds.X;
                data.Bounds.Y += markData.Bounds.Y;

                if (data.Direction == TextDirection.LeftToRight)
                {
                    for (int k = j; k < currentIndex; k++)
                    {
                        markData = collection[k];
                        data.Bounds.X -= markData.Bounds.Width;
                        data.Bounds.Y -= markData.Bounds.Height;
                    }
                }
                else
                {
                    for (int k = j + 1; k < currentIndex + 1; k++)
                    {
                        markData = collection[k];
                        data.Bounds.X += markData.Bounds.Width;
                        data.Bounds.Y += markData.Bounds.Height;
                    }
                }
            }
        }
    }

    private static void ZeroMarkAdvances(FontMetrics fontMetrics, GlyphPositioningCollection collection, int index, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int currentIndex = i + index;
            GlyphShapingData data = collection[currentIndex];
            if (collection.ShouldProcess(fontMetrics, currentIndex) && AdvancedTypographicUtils.IsZeroWidthMark(fontMetrics, data))
            {
                data.Bounds.Width = 0;
                data.Bounds.Height = 0;
            }
        }
    }

    private static void UpdatePositions(FontMetrics fontMetrics, GlyphPositioningCollection collection, int index, int count)
    {
        for (int i = 0; i < count; i++)
        {
            int currentIndex = i + index;
            collection.UpdatePosition(fontMetrics, currentIndex);
        }
    }

    // KerningMode.None promises no kerning. HarfBuzz still applies a required kern feature, but here the caller's
    // explicit request wins, so a font that makes kern or vkrn its required feature is not kerned then.
    private bool RequiredFeatureIsKerning(ScriptClass script)
    {
        LangSysTable? langSysTable = this.ScriptList?.GetDefaultLangSys(script);
        if (langSysTable is null || langSysTable.RequiredFeatureIndex >= this.FeatureList.FeatureTables.Length)
        {
            return false;
        }

        Tag tag = this.FeatureList.FeatureTables[langSysTable.RequiredFeatureIndex].FeatureTag;
        return tag == KernTag || tag == VKernTag;
    }
}
