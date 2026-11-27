// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.Unicode.Resources;

namespace PinataLabs.Typography;

/// <summary>
/// Contains supplementary data that allows the shaping of glyphs.
/// </summary>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
internal class GlyphShapingData
{
    private CodePoint codePoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphShapingData"/> class.
    /// </summary>
    public GlyphShapingData(TextRun textRun) => this.TextRun = textRun;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphShapingData"/> class.
    /// </summary>
    /// <param name="data">The data to copy properties from.</param>
    /// <param name="clearFeatures">Whether to clear features.</param>
    public GlyphShapingData(GlyphShapingData data, bool clearFeatures = false)
    {
        this.GlyphId = data.GlyphId;
        this.CodePoint = data.CodePoint;
        this.UnicodePropertiesCodePoint = data.UnicodePropertiesCodePoint;
        this.SubstitutedGlyphClass = data.SubstitutedGlyphClass;
        this.IsOtherLetter = data.IsOtherLetter;
        this.CodePointCount = data.CodePointCount;
        this.SourceOffset = data.SourceOffset;
        this.Cluster = data.Cluster;
        this.Direction = data.Direction;
        this.TextRun = data.TextRun;
        this.LigatureId = data.LigatureId;
        this.IsLigated = data.IsLigated;
        this.LigatureComponent = data.LigatureComponent;
        this.MarkAttachment = data.MarkAttachment;
        this.CursiveAttachment = data.CursiveAttachment;
        this.IsSubstituted = data.IsSubstituted;
        this.IsDecomposed = data.IsDecomposed;
        this.IsMultiplied = data.IsMultiplied;
        this.IsUnhiddenJoiner = data.IsUnhiddenJoiner;
        this.HasVerticalForm = data.HasVerticalForm;
        this.SpaceFallback = data.SpaceFallback;
        this.JamoFeature = data.JamoFeature;
        if (data.UniversalShapingEngineInfo != null)
        {
            this.UniversalShapingEngineInfo = new(
                data.UniversalShapingEngineInfo.Category,
                data.UniversalShapingEngineInfo.SyllableType,
                data.UniversalShapingEngineInfo.Syllable);
        }

        if (data.IndicShapingEngineInfo != null)
        {
            this.IndicShapingEngineInfo = new(
                data.IndicShapingEngineInfo.Category,
                data.IndicShapingEngineInfo.Position,
                data.IndicShapingEngineInfo.SyllableType,
                data.IndicShapingEngineInfo.Syllable);
        }

        if (data.MyanmarShapingEngineInfo != null)
        {
            this.MyanmarShapingEngineInfo = new(
                data.MyanmarShapingEngineInfo.Category,
                data.MyanmarShapingEngineInfo.Position,
                data.MyanmarShapingEngineInfo.SyllableType,
                data.MyanmarShapingEngineInfo.Syllable);
        }

        if (!clearFeatures)
        {
            this.Features = [with(data.Features)];
        }

        this.Bounds = data.Bounds;
    }

    /// <summary>
    /// Gets or sets the glyph id.
    /// </summary>
    public ushort GlyphId { get; set; }

    /// <summary>
    /// Gets or sets the leading codepoint. Setting it gives the glyph the Unicode properties of the codepoint.
    /// </summary>
    public CodePoint CodePoint
    {
        get => this.codePoint;
        set
        {
            this.codePoint = value;
            this.UnicodePropertiesCodePoint = value;
        }
    }

    /// <summary>
    /// Gets or sets the codepoint whose Unicode properties the glyph has: the general category that makes it a mark,
    /// and the combining class that sorts it. It is <see cref="CodePoint"/>, except for the dotted circle that breaks an
    /// invalid vowel sequence. As in HarfBuzz, that circle has the properties of the character after it.
    /// </summary>
    public CodePoint UnicodePropertiesCodePoint { get; set; }

    /// <summary>
    /// Gets or sets the class that GSUB gave the glyph, which a font without glyph classes takes in place of the class of
    /// its character. As in HarfBuzz, a ligature of components that are not all marks is a ligature glyph, even when its
    /// first component is a mark.
    /// </summary>
    public GlyphClassDef? SubstitutedGlyphClass { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the glyph has the general category Lo where the character whose properties
    /// it has is Mn. As in HarfBuzz, a ligature of components that are not all marks takes it in any font, and the glyphs
    /// that a substitution makes of the ligature keep it, whatever class they get.
    /// </summary>
    public bool IsOtherLetter { get; set; }

    /// <summary>
    /// Gets or sets the codepoint count represented by this glyph.
    /// </summary>
    public int CodePointCount { get; set; } = 1;

    /// <summary>
    /// Gets a value indicating whether a shaper inserted the glyph, as a dotted circle, so that it stands for no
    /// character of the text.
    /// </summary>
    public bool IsInserted => this.CodePointCount == 0;

    /// <summary>
    /// Gets or sets the offset, within the input codepoint collection, of the character that the glyph comes from. It
    /// moves with the glyph when a shaper reorders the glyphs, and a glyph that is made from another keeps it. So a
    /// fallback font finds its own glyphs of the character, wherever the shaper put the glyph.
    /// </summary>
    public int SourceOffset { get; set; }

    /// <summary>
    /// Gets or sets the cluster of the glyph, as HarfBuzz keeps it at its default cluster level: the UTF-16 index in
    /// the text of the first character that the glyph stands for. A glyph that a ligature, a reordering or a mark
    /// joins shares the lowest cluster of the glyphs it joins, so the clusters of a run never decrease.
    /// </summary>
    public int Cluster { get; set; }

    /// <summary>
    /// Gets or sets the text direction.
    /// </summary>
    public TextDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the text run this glyph belongs to.
    /// </summary>
    public TextRun TextRun { get; set; }

    /// <summary>
    /// Gets or sets the id of any ligature this glyph is a member of.
    /// </summary>
    public int LigatureId { get; set; } = 0;

    /// <summary>
    /// Gets or sets a value indicating whether the glyph is ligated.
    /// </summary>
    public bool IsLigated { get; set; } = false;

    /// <summary>
    /// Gets or sets the ligature component index of the glyph.
    /// </summary>
    public int LigatureComponent { get; set; } = -1;

    /// <summary>
    /// Gets or sets the index of any mark attachment.
    /// </summary>
    public int MarkAttachment { get; set; } = -1;

    /// <summary>
    /// Gets or sets the offset from the glyph to the glyph that it is cursively attached to, or 0 when it is not attached.
    /// </summary>
    public int CursiveAttachment { get; set; }

    /// <summary>
    /// Gets or sets the collection of features.
    /// </summary>
    public List<TagEntry> Features { get; set; } = [];

    /// <summary>
    /// Gets or sets the shaping bounds.
    /// </summary>
    public GlyphShapingBounds Bounds { get; set; } = new(0, 0, 0, 0);

    /// <summary>
    /// Gets or sets a value indicating whether this glyph is the result of a substitution.
    /// </summary>
    public bool IsSubstituted { get; set; }

    /// <summary>
    /// Gets a value indicating whether the glyph is of a default-ignorable character that GSUB did not substitute. As in
    /// HarfBuzz, a lookup can skip such a glyph between the glyphs it matches, and the glyph takes no room after shaping.
    /// </summary>
    public bool IsDefaultIgnorable => !this.IsSubstituted && UnicodeUtility.IsHiddenAfterShaping((uint)this.CodePoint.Value);

    /// <summary>
    /// Gets or sets a value indicating whether the glyph is of a COMBINING GRAPHEME JOINER that keeps no mark from moving
    /// past another. As in HarfBuzz, a lookup may then skip it as any other default-ignorable glyph, in GSUB too.
    /// </summary>
    public bool IsUnhiddenJoiner { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this glyph is the result of a decomposition substitution
    /// </summary>
    public bool IsDecomposed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the glyph is one of the glyphs that a multiple substitution made of one.
    /// As in HarfBuzz, a ligature that the glyph then forms is not.
    /// </summary>
    public bool IsMultiplied { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this glyph was resolved to a vertical form (the font's
    /// <c>vert</c> substitution or a cmap vertical presentation form). It is only set for <c>Tr</c>
    /// (transform-rotate) codepoints in vertical mixed layout; such glyphs are laid out upright when set
    /// and rotated, with horizontal features and metrics, otherwise.
    /// </summary>
    public bool HasVerticalForm { get; set; }

    /// <summary>
    /// Gets or sets the space that this glyph of U+0020 stands for, when the font lacks that space.
    /// </summary>
    public SpaceFallbackKind SpaceFallback { get; set; }

    /// <summary>
    /// Gets or sets the jamo feature, <c>ljmo</c>, <c>vjmo</c> or <c>tjmo</c>, that the Hangul shaper gives the glyph of a
    /// jamo in a syllable that it does not compose. GSUB and GPOS apply only that one of the three to the glyph.
    /// </summary>
    public Tag? JamoFeature { get; set; }

    /// <summary>
    /// Gets or sets the universal shaping information.
    /// </summary>
    public UniversalShapingEngineInfo? UniversalShapingEngineInfo { get; set; }

    /// <summary>
    /// Gets or sets the Indic shaping information.
    /// </summary>
    public IndicShapingEngineInfo? IndicShapingEngineInfo { get; set; }

    /// <summary>
    /// Gets or sets the Myanmar shaping information.
    /// </summary>
    public MyanmarShapingEngineInfo? MyanmarShapingEngineInfo { get; set; }

    private string DebuggerDisplay
        => FormattableString
        .Invariant($" {this.GlyphId} : {this.CodePoint.ToDebuggerDisplay()} : {CodePoint.GetScriptClass(this.CodePoint)} : {this.Direction} : {this.TextRun.TextAttributes} : {this.LigatureId} : {this.LigatureComponent} : {this.IsDecomposed}");

    internal string ToDebuggerDisplay() => this.DebuggerDisplay;
}

/// <summary>
/// Represents information required for universal shaping.
/// </summary>
internal class UniversalShapingEngineInfo
{
    public UniversalShapingEngineInfo(string category, string syllableType, int syllable)
    {
        this.Category = category;
        this.SyllableType = syllableType;
        this.Syllable = syllable;
    }

    public string Category { get; set; }

    public string SyllableType { get; }

    public int Syllable { get; }
}

/// <summary>
/// Represents information required for Myanmar shaping.
/// </summary>
internal class MyanmarShapingEngineInfo
{
    public MyanmarShapingEngineInfo(
        MyanmarShaper.Categories category,
        MyanmarShaper.Positions position,
        string syllableType,
        int syllable)
    {
        this.Category = category;
        this.Position = position;
        this.SyllableType = syllableType;
        this.Syllable = syllable;
    }

    public MyanmarShaper.Categories Category { get; set; }

    public MyanmarShaper.Positions Position { get; set; }

    public string SyllableType { get; }

    public int Syllable { get; }
}

internal class IndicShapingEngineInfo
{
    public IndicShapingEngineInfo(
        IndicShapingData.Categories category,
        IndicShapingData.Positions position,
        string syllableType,
        int syllable)
    {
        this.Category = category;
        this.Position = position;
        this.SyllableType = syllableType;
        this.Syllable = syllable;
    }

    public IndicShapingData.Categories Category { get; set; }

    public IndicShapingData.Positions Position { get; set; }

    public string SyllableType { get; }

    public int Syllable { get; }
}
