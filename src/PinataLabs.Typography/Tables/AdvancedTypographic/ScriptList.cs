// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// OpenType Layout fonts may contain one or more groups of glyphs used to render various scripts,
/// which are enumerated in a ScriptList table. Both the GSUB and GPOS tables define
/// Script List tables (ScriptList):
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/chapter2#slTbl_sRec"/>
/// </summary>
internal sealed class ScriptList : Dictionary<Tag, ScriptListTable>
{
    private static readonly Tag DfltScriptTag = Tag.Parse("DFLT");

    private static readonly Tag LowercaseDfltScriptTag = Tag.Parse("dflt");

    private static readonly Tag LatnScriptTag = Tag.Parse("latn");

    public static ScriptList? Load(BigEndianBinaryReader reader, long offset)
    {
        // ScriptListTable
        // +--------------+----------------------------+-------------------------------------------------------------+
        // | Type         | Name                       | Description                                                 |
        // +==============+============================+=============================================================+
        // | uint16       | scriptCount                | Number of ScriptRecords                                     |
        // +--------------+----------------------------+-------------------------------------------------------------+
        // | ScriptRecord | scriptRecords[scriptCount] | Array of ScriptRecords, listed alphabetically by script tag |
        // +--------------+----------------------------+-------------------------------------------------------------+
        reader.Seek(offset, SeekOrigin.Begin);

        ushort scriptCount = reader.ReadUInt16();

        // Read records (tags and table offsets)
        var scriptTags = new Tag[scriptCount];
        ushort[] scriptOffsets = new ushort[scriptCount];

        for (int i = 0; i < scriptTags.Length; i++)
        {
            scriptTags[i] = reader.ReadUInt32();
            scriptOffsets[i] = reader.ReadUInt16();
        }

        if (scriptCount == 0)
        {
            return null;
        }

        // Read each table and add it to the dictionary
        ScriptList scriptList = [];
        for (int i = 0; i < scriptCount; ++i)
        {
            Tag scriptTag = scriptTags[i];
            var scriptTable = ScriptListTable.Load(scriptTag, reader, offset + scriptOffsets[i]);
            scriptList.Add(scriptTag, scriptTable);
        }

        return scriptList;
    }

    /// <summary>
    /// Gets the script of the table that applies to text of a Unicode script. As in HarfBuzz, a font without
    /// a script for the text gives the <c>DFLT</c> script, else the <c>dflt</c> script, else <c>latn</c>.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <returns>The script, or <see langword="null"/> when none applies.</returns>
    public ScriptListTable? ChooseScript(ScriptClass script)
    {
        ScriptListTable? table;
        ReadOnlySpan<Tag> tags = OpenTypeScriptTags.GetTags(script);
        for (int i = 0; i < tags.Length; i++)
        {
            if (this.TryGetValue(tags[i], out table))
            {
                return table;
            }
        }

        if (this.TryGetValue(DfltScriptTag, out table)
            || this.TryGetValue(LowercaseDfltScriptTag, out table)
            || this.TryGetValue(LatnScriptTag, out table))
        {
            return table;
        }

        return null;
    }

    /// <summary>
    /// Gets the language system that applies to text of a script in a language.
    /// </summary>
    /// <param name="script">The script of the text.</param>
    /// <param name="language">The tag of the language system, or <see langword="null"/> when no language is requested.</param>
    /// <returns>
    /// The language system with the tag, or the one that applies when no language is requested if the script has none
    /// with the tag, or <see langword="null"/> when no script or language system applies.
    /// </returns>
    public LangSysTable? GetLangSys(ScriptClass script, Tag? language)
        => this.ChooseScript(script)?.GetLangSys(language);
}

internal sealed class ScriptListTable
{
    private static readonly Tag DfltTag = Tag.Parse("dflt");

    private ScriptListTable(LangSysTable[] langSysTables, LangSysTable? defaultLang, Tag scriptTag)
    {
        this.LangSysTables = langSysTables;
        this.DefaultLangSysTable = defaultLang;
        this.ScriptTag = scriptTag;
    }

    public Tag ScriptTag { get; }

    public LangSysTable? DefaultLangSysTable { get; }

    public LangSysTable[] LangSysTables { get; }

    /// <summary>
    /// Gets the language system that applies when no language is requested. As in HarfBuzz, this is the
    /// LangSysRecord tagged <c>dflt</c> when the script has one, and otherwise the default LangSys.
    /// </summary>
    /// <returns>The language system, or <see langword="null"/> when the script has neither.</returns>
    public LangSysTable? GetDefaultLangSys()
    {
        for (int i = 0; i < this.LangSysTables.Length; i++)
        {
            if (this.LangSysTables[i].LangSysTag == DfltTag.Value)
            {
                return this.LangSysTables[i];
            }
        }

        return this.DefaultLangSysTable;
    }

    /// <summary>
    /// Gets the language system that applies to text in a language. As in HarfBuzz, a script that has no language
    /// system with the tag of the language gives the one that applies when no language is requested.
    /// </summary>
    /// <param name="language">The tag of the language system, or <see langword="null"/> when no language is requested.</param>
    /// <returns>The language system, or <see langword="null"/> when the script has none to give.</returns>
    public LangSysTable? GetLangSys(Tag? language)
    {
        if (language is Tag tag)
        {
            for (int i = 0; i < this.LangSysTables.Length; i++)
            {
                if (this.LangSysTables[i].LangSysTag == tag.Value)
                {
                    return this.LangSysTables[i];
                }
            }
        }

        return this.GetDefaultLangSys();
    }

    public static ScriptListTable Load(Tag scriptTag, BigEndianBinaryReader reader, long offset)
    {
        // ScriptListTable
        // +---------------+------------------------------+-------------------------------------------------------------------------------+
        // | Type          | Name                         | Description                                                                   |
        // +===============+==============================+===============================================================================+
        // | Offset16      | defaultLangSysOffset         | Offset to default LangSys table, from beginning of Script table — may be NULL |
        // +---------------+------------------------------+-------------------------------------------------------------------------------+
        // | uint16        | langSysCount                 | Number of LangSysRecords for this script — excluding the default LangSys      |
        // +---------------+------------------------------+-------------------------------------------------------------------------------+
        // | LangSysRecord | langSysRecords[langSysCount] | Array of LangSysRecords, listed alphabetically by LangSys tag                 |
        // +---------------+------------------------------+-------------------------------------------------------------------------------+
        reader.Seek(offset, SeekOrigin.Begin);

        ushort defaultLangSysOffset = reader.ReadOffset16();
        ushort langSysCount = reader.ReadUInt16();

        var langSysRecords = new LangSysRecord[langSysCount];
        for (int i = 0; i < langSysRecords.Length; i++)
        {
            // LangSysRecord
            // +----------+---------------+---------------------------------------------------------+
            // | Type     | Name          | Description                                             |
            // +==========+===============+=========================================================+
            // | Tag      | langSysTag    | 4-byte LangSysTag identifier                            |
            // +----------+---------------+---------------------------------------------------------+
            // | Offset16 | langSysOffset | Offset to LangSys table, from beginning of Script table |
            // +----------+---------------+---------------------------------------------------------+
            uint langSysTag = reader.ReadUInt32();
            ushort langSysOffset = reader.ReadOffset16();
            langSysRecords[i] = new LangSysRecord(langSysTag, langSysOffset);
        }

        // Load the default table.
        LangSysTable? defaultLangSysTable = null;
        if (defaultLangSysOffset > 0)
        {
            defaultLangSysTable = LangSysTable.Load(0, reader, offset + defaultLangSysOffset);
        }

        // Load the other table features.
        // We do this last to avoid excessive seeking.
        var langSysTables = new LangSysTable[langSysCount];
        for (int i = 0; i < langSysTables.Length; i++)
        {
            LangSysRecord langSysRecord = langSysRecords[i];
            langSysTables[i] = LangSysTable.Load(langSysRecord.LangSysTag, reader, offset + langSysRecord.LangSysOffset);
        }

        return new ScriptListTable(langSysTables, defaultLangSysTable, scriptTag);
    }

    private readonly struct LangSysRecord
    {
        public LangSysRecord(uint langSysTag, ushort langSysOffset)
        {
            this.LangSysTag = langSysTag;
            this.LangSysOffset = langSysOffset;
        }

        public uint LangSysTag { get; }

        public ushort LangSysOffset { get; }
    }
}

internal sealed class LangSysTable
{
    private LangSysTable(uint langSysTag, ushort requiredFeatureIndex, ushort[] featureIndices)
    {
        this.LangSysTag = langSysTag;
        this.RequiredFeatureIndex = requiredFeatureIndex;
        this.FeatureIndices = featureIndices;
    }

    public uint LangSysTag { get; }

    public ushort RequiredFeatureIndex { get; }

    public ushort[] FeatureIndices { get; } = [];

    public static LangSysTable Load(uint langSysTag, BigEndianBinaryReader reader, long offset)
    {
        // +----------+-----------------------------------+-----------------------------------------------------------------------------------------+
        // | Type     | Name                              | Description                                                                             |
        // +==========+===================================+=========================================================================================+
        // | Offset16 | lookupOrderOffset                 | = NULL(reserved for an offset to a reordering table)                                   |
        // +----------+-----------------------------------+-----------------------------------------------------------------------------------------+
        // | uint16   | requiredFeatureIndex              | Index of a feature required for this language system; if no required features = 0xFFFF  |
        // +----------+-----------------------------------+-----------------------------------------------------------------------------------------+
        // | uint16   | featureIndexCount                 | Number of feature index values for this language system — excludes the required feature |
        // +----------+-----------------------------------+-----------------------------------------------------------------------------------------+
        // | uint16   | featureIndices[featureIndexCount] | Array of indices into the FeatureList, in arbitrary order                               |
        // +----------+-----------------------------------+-----------------------------------------------------------------------------------------+
        reader.Seek(offset, SeekOrigin.Begin);
        ushort lookupOrderOffset = reader.ReadOffset16();
        ushort requiredFeatureIndex = reader.ReadUInt16();
        ushort featureIndexCount = reader.ReadUInt16();

        ushort[] featureIndices = reader.ReadUInt16Array(featureIndexCount);
        return new LangSysTable(langSysTag, requiredFeatureIndex, featureIndices);
    }
}
