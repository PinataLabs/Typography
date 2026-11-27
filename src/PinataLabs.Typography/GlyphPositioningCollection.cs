// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// Represents a collection of glyph metrics that are mapped to input codepoints.
/// </summary>
internal sealed class GlyphPositioningCollection : IGlyphShapingCollection
{
    /// <summary>
    /// Contains a map the index of a map within the collection, non-sequential codepoint offsets, and their glyph ids, point size, and mtrics.
    /// </summary>
    private readonly List<GlyphPositioningData> glyphs = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphPositioningCollection"/> class.
    /// </summary>
    /// <param name="textOptions">The text options.</param>
    public GlyphPositioningCollection(TextOptions textOptions) => this.TextOptions = textOptions;

    /// <inheritdoc />
    public int Count => this.glyphs.Count;

    /// <inheritdoc />
    public TextOptions TextOptions { get; }

    /// <inheritdoc />
    public GlyphShapingData this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.glyphs[index].Data;
    }

    /// <summary>
    /// Gets the metrics of the glyph at the index, in the font that draws it.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The metrics, which belong to this layout alone.</returns>
    public GlyphMetrics GetMetrics(int index) => this.glyphs[index].Metrics[0];

    /// <summary>
    /// Gets the size of a unit of the font that draws the glyph at the index, in points. Glyphs of two fonts in one
    /// cluster have units of different sizes.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>The size along and across the line.</returns>
    public Vector2 GetUnitSize(int index) => new Vector2(this.glyphs[index].PointSize) / this.GetMetrics(index).ScaleFactor;

    /// <inheritdoc />
    public void AddShapingFeature(int index, TagEntry feature)
        => this.glyphs[index].Data.Features.Add(feature);

    /// <inheritdoc />
    public void EnableShapingFeature(int index, Tag feature)
    {
        List<TagEntry> features = this.glyphs[index].Data.Features;
        for (int i = 0; i < features.Count; i++)
        {
            TagEntry tagEntry = features[i];
            if (tagEntry.Tag == feature)
            {
                tagEntry.Enabled = true;
                features[i] = tagEntry;
                break;
            }
        }
    }

    /// <inheritdoc />
    public void DisableShapingFeature(int index, Tag feature)
    {
        List<TagEntry> features = this.glyphs[index].Data.Features;
        for (int i = 0; i < features.Count; i++)
        {
            TagEntry tagEntry = features[i];
            if (tagEntry.Tag == feature)
            {
                tagEntry.Enabled = false;
                features[i] = tagEntry;
                break;
            }
        }
    }

    /// <summary>
    /// Gets the glyph metrics at the given codepoint offset.
    /// </summary>
    /// <param name="offset">The zero-based index within the input codepoint collection.</param>
    /// <param name="pointSize">The font size in PT units of the font containing this glyph.</param>
    /// <param name="isDecomposed">Whether the glyph is the result of a decomposition substitution.</param>
    /// <param name="hasVerticalForm">Whether the glyph was resolved to a vertical form.</param>
    /// <param name="metrics">
    /// When this method returns, contains the glyph metrics associated with the specified offset,
    /// if the value is found; otherwise, the default value for the type of the metrics parameter.
    /// This parameter is passed uninitialized.
    /// </param>
    /// <returns>The metrics.</returns>
    public bool TryGetGlyphMetricsAtOffset(int offset, out float pointSize, out bool isDecomposed, out bool hasVerticalForm, [NotNullWhen(true)] out IReadOnlyList<GlyphMetrics>? metrics)
    {
        List<GlyphMetrics> match = [];
        pointSize = 0;
        isDecomposed = false;
        hasVerticalForm = false;
        for (int i = 0; i < this.glyphs.Count; i++)
        {
            if (this.glyphs[i].Offset == offset)
            {
                GlyphPositioningData glyph = this.glyphs[i];

                // A reordering can move a glyph that was not decomposed into the place of one that was, such as a
                // sign before an inserted dotted circle. The glyphs at the offset still each take an advance.
                isDecomposed |= glyph.Data.IsDecomposed;
                hasVerticalForm |= glyph.Data.HasVerticalForm;
                pointSize = glyph.PointSize;
                match.AddRange(glyph.Metrics);
            }
            else if (match.Count > 0)
            {
                // Offsets, though non-sequential, are sorted, so we can stop searching.
                break;
            }
        }

        metrics = match;
        return match.Count > 0;
    }

    /// <summary>
    /// Gets the <see cref="GlyphShapingData.SourceOffset"/> of each character that no font so far has a glyph for.
    /// </summary>
    /// <returns>The offsets within the input codepoint collection.</returns>
    public HashSet<int> GetUnresolvedOffsets()
    {
        HashSet<int> offsets = [];
        foreach (GlyphPositioningData glyph in this.glyphs)
        {
            if (glyph.Metrics[0].GlyphType == GlyphType.Fallback && !glyph.Data.IsInserted)
            {
                offsets.Add(glyph.Data.SourceOffset);
            }
        }

        return offsets;
    }

    /// <summary>
    /// Replaces the glyphs that no font so far has with the glyphs that a fallback font gives the same character. The font
    /// of the text decided which glyphs the text has and in what order, so the new glyphs take the place of the glyphs
    /// they replace, wherever the shaper of that font put them.
    /// </summary>
    /// <param name="font">The fallback font.</param>
    /// <param name="collection">The glyphs of the whole text, shaped with the fallback font.</param>
    /// <returns><see langword="true"/> if the metrics collection does not contain any fallbacks; otherwise <see langword="false"/>.</returns>
    public bool TryUpdate(Font font, GlyphSubstitutionCollection collection)
    {
        Dictionary<int, Character> characters = [];
        for (int i = 0; i < this.glyphs.Count; i++)
        {
            if (IsMissing(this.glyphs[i]) && !characters.ContainsKey(this.glyphs[i].Data.SourceOffset))
            {
                characters.Add(this.glyphs[i].Data.SourceOffset, new Character());
            }
        }

        for (int i = 0; i < this.glyphs.Count; i++)
        {
            if (characters.TryGetValue(this.glyphs[i].Data.SourceOffset, out Character? character))
            {
                character.Indices.Add(i);
            }
        }

        for (int i = 0; i < collection.Count; i++)
        {
            if (characters.TryGetValue(collection[i].SourceOffset, out Character? character))
            {
                character.Fallback.Add(collection[i]);
            }
        }

        bool hasFallBacks = false;
        Dictionary<int, GlyphPositioningData[]> replacements = [];
        foreach (Character character in characters.Values)
        {
            hasFallBacks |= !this.TryReplaceCharacter(font.FontMetrics, character, replacements);
        }

        this.TakeLigatureProperties(replacements);

        List<GlyphPositioningData> updated = new(this.glyphs.Count);
        for (int i = 0; i < this.glyphs.Count; i++)
        {
            if (replacements.TryGetValue(i, out GlyphPositioningData[]? replacement))
            {
                updated.AddRange(replacement);
            }
            else
            {
                updated.Add(this.glyphs[i]);
            }
        }

        this.glyphs.Clear();
        this.glyphs.AddRange(updated);
        return !hasFallBacks;
    }

    private static bool IsMissing(GlyphPositioningData glyph) => glyph.Metrics[0].GlyphType == GlyphType.Fallback;

    /// <summary>
    /// Gives the glyphs of a fallback font ligature ids that no glyph here has, as each font numbers its own ligatures. A
    /// glyph that no ligature of the fallback font skipped takes the ligature id and the component of the glyph whose
    /// place it takes, if a ligature of the font skipped that glyph: a mark then goes on the component of that ligature.
    /// </summary>
    /// <param name="replacements">The glyphs that replace each glyph, by index.</param>
    private void TakeLigatureProperties(Dictionary<int, GlyphPositioningData[]> replacements)
    {
        int lastLigatureId = 0;
        foreach (GlyphPositioningData glyph in this.glyphs)
        {
            lastLigatureId = Math.Max(lastLigatureId, glyph.Data.LigatureId);
        }

        foreach (KeyValuePair<int, GlyphPositioningData[]> replacement in replacements)
        {
            GlyphShapingData place = this.glyphs[replacement.Key].Data;
            foreach (GlyphPositioningData glyph in replacement.Value)
            {
                GlyphShapingData data = glyph.Data;
                if (data.LigatureId > 0)
                {
                    data.LigatureId += lastLigatureId;
                }
                else if (data.LigatureComponent == -1 && place.LigatureId > 0 && place.LigatureComponent > 0)
                {
                    data.LigatureId = place.LigatureId;
                    data.LigatureComponent = place.LigatureComponent;
                }
            }
        }
    }

    // Two glyphs of a character draw the same part when a shaper inserted both or neither, as it inserts a dotted circle,
    // split both or neither from a vowel sign, and they have the same code point, as each part of a split sign has its own.
    private static bool IsSamePart(GlyphShapingData glyph, GlyphShapingData other)
        => glyph.IsInserted == other.IsInserted
            && glyph.IsVowelSignPart == other.IsVowelSignPart
            && glyph.CodePoint == other.CodePoint;

    /// <summary>
    /// Plans the glyphs that replace the glyphs of one character. Each missing glyph takes the glyphs of the fallback font
    /// that draw the same part. The fallback font draws the whole character instead when it does not draw the missing
    /// parts alone, as when it keeps whole a vowel sign that the font split, or makes one glyph of a sign and of the dotted
    /// circle that the font drew. So it does when the font drew none of the character and the fallback font has glyphs of
    /// it left, as when it splits a sign that the font kept whole. Its glyphs then take the place of the last missing
    /// glyph, where a shaper puts a sign that it keeps whole, and the other glyphs of the character go. A dotted circle
    /// that the font drew stays, unless the fallback font draws its own, alone or in a ligature.
    /// </summary>
    /// <param name="fontMetrics">The fallback font.</param>
    /// <param name="character">The glyphs of the character here and in the fallback font.</param>
    /// <param name="replacements">The glyphs that replace each glyph, by index. An empty array removes the glyph.</param>
    /// <returns><see langword="false"/> if the fallback font lacks a glyph too, which is left for the next fallback font.</returns>
    private bool TryReplaceCharacter(FontMetrics fontMetrics, Character character, Dictionary<int, GlyphPositioningData[]> replacements)
    {
        // The font of the text decided whether the character has an inserted glyph, such as a dotted circle.
        List<int> indices = character.Indices;
        bool hasInserted = indices.Exists(i => this.glyphs[i].Data.IsInserted);
        bool hasOwn = indices.Exists(i => !this.glyphs[i].Data.IsInserted);
        List<GlyphShapingData> fallback = character.Fallback.FindAll(g => g.IsInserted ? hasInserted : hasOwn);
        List<GlyphShapingData> untaken = [.. fallback];
        Dictionary<int, List<GlyphShapingData>> parts = [];
        bool isWhole = false;
        foreach (int i in indices)
        {
            if (!IsMissing(this.glyphs[i]))
            {
                continue;
            }

            GlyphShapingData missing = this.glyphs[i].Data;

            // When the font drew another glyph of the same code point, as of a sign that a substitution split, the code
            // point does not tell which part is missing.
            List<GlyphShapingData> part = indices.Exists(j => !IsMissing(this.glyphs[j]) && IsSamePart(this.glyphs[j].Data, missing))
                ? []
                : untaken.FindAll(g => IsSamePart(g, missing));

            if (part.Count == 0)
            {
                isWhole = true;
                break;
            }

            untaken.RemoveAll(part.Contains);
            parts.Add(i, part);
        }

        isWhole |= untaken.Count > 0 && indices.TrueForAll(i => IsMissing(this.glyphs[i]));
        if (isWhole)
        {
            int last = indices.FindLast(i => IsMissing(this.glyphs[i]));
            if (!this.TryCreateGlyphs(fontMetrics, fallback, this.glyphs[last], out GlyphPositioningData[] whole))
            {
                return false;
            }

            bool hasFallbackInserted = fallback.Exists(g => g.HoldsInsertedGlyph);
            foreach (int i in indices)
            {
                if (i == last)
                {
                    replacements.Add(i, whole);
                }
                else if (hasFallbackInserted || !this.glyphs[i].Data.IsInserted || IsMissing(this.glyphs[i]))
                {
                    replacements.Add(i, []);
                }
            }

            return true;
        }

        bool isComplete = true;
        foreach (KeyValuePair<int, List<GlyphShapingData>> part in parts)
        {
            if (this.TryCreateGlyphs(fontMetrics, part.Value, this.glyphs[part.Key], out GlyphPositioningData[] glyphs))
            {
                replacements.Add(part.Key, glyphs);
            }
            else
            {
                isComplete = false;
            }
        }

        return isComplete;
    }

    /// <summary>
    /// Makes the glyphs of a fallback font that take the place of a glyph.
    /// </summary>
    /// <param name="fontMetrics">The fallback font.</param>
    /// <param name="shapes">The glyphs of the fallback font, in its order.</param>
    /// <param name="place">The glyph whose place they take.</param>
    /// <param name="glyphs">The new glyphs.</param>
    /// <returns><see langword="false"/> if the fallback font lacks one of the glyphs too.</returns>
    private bool TryCreateGlyphs(FontMetrics fontMetrics, List<GlyphShapingData> shapes, GlyphPositioningData place, out GlyphPositioningData[] glyphs)
    {
        LayoutMode layoutMode = this.TextOptions.LayoutMode;
        ColorFontSupport colorFontSupport = this.TextOptions.ColorFontSupport;
        glyphs = new GlyphPositioningData[shapes.Count];
        for (int i = 0; i < shapes.Count; i++)
        {
            GlyphShapingData shape = shapes[i];
            ushort id = shape.GlyphId;
            CodePoint codePoint = shape.CodePoint;

            // Perform a semi-deep clone (FontMetrics is not cloned) so we can continue to
            // cache the original in the font metrics and only update our collection.
            List<GlyphMetrics> metrics = [];
            TextAttributes textAttributes = shape.TextRun.TextAttributes;
            TextDecorations textDecorations = shape.TextRun.TextDecorations;
            foreach (GlyphMetrics gm in fontMetrics.GetGlyphMetrics(codePoint, id, textAttributes, textDecorations, layoutMode, colorFontSupport, shape.HasVerticalForm))
            {
                if (gm.GlyphType == GlyphType.Fallback && !CodePoint.IsControl(codePoint))
                {
                    return false;
                }

                metrics.Add(gm.CloneForRendering(shape.TextRun, codePoint));
            }

            SpaceFallback.SetAdvance(metrics, shape, fontMetrics, this.TextOptions, AdvancedTypographicUtils.IsVerticalGlyph(codePoint, layoutMode, shape.HasVerticalForm));
            int maxAdvancedWidth = 0;
            int maxAdvancedHeight = 0;
            for (int k = 0; k < metrics.Count; k++)
            {
                maxAdvancedWidth = Math.Max(maxAdvancedWidth, metrics[k].AdvanceWidth);
                maxAdvancedHeight = Math.Max(maxAdvancedHeight, metrics[k].AdvanceHeight);
            }

            // The glyphs at an offset each take an advance only when one of them is decomposed. So a new glyph keeps that
            // of the glyph whose place it takes, which a shaper may have put at the offset of another glyph.
            GlyphShapingData data = new(shape, true)
            {
                Bounds = new(0, 0, maxAdvancedWidth, maxAdvancedHeight),
                IsDecomposed = shape.IsDecomposed || place.Data.IsDecomposed,
            };

            glyphs[i] = new(place.Offset, data, place.PointSize, metrics.ToArray());
        }

        return true;
    }

    /// <summary>
    /// Adds the collection of glyph ids to the metrics collection.
    /// identified as fallbacks.
    /// </summary>
    /// <param name="font">The font face with metrics.</param>
    /// <param name="collection">The glyph substitution collection.</param>
    /// <returns><see langword="true"/> if the metrics collection does not contain any fallbacks; otherwise <see langword="false"/>.</returns>
    public bool TryAdd(Font font, GlyphSubstitutionCollection collection)
    {
        bool hasFallBacks = false;
        FontMetrics fontMetrics = font.FontMetrics;
        LayoutMode layoutMode = this.TextOptions.LayoutMode;
        ColorFontSupport colorFontSupport = this.TextOptions.ColorFontSupport;

        for (int i = 0; i < collection.Count; i++)
        {
            GlyphShapingData data = collection.GetGlyphShapingData(i, out int offset);
            CodePoint codePoint = data.CodePoint;
            ushort id = data.GlyphId;
            List<GlyphMetrics> metrics = [];

            // Perform a semi-deep clone (FontMetrics is not cloned) so we can continue to
            // cache the original in the font metrics and only update our collection.
            TextAttributes textAttributes = data.TextRun.TextAttributes;
            TextDecorations textDecorations = data.TextRun.TextDecorations;
            bool isVerticalLayout = AdvancedTypographicUtils.IsVerticalGlyph(codePoint, layoutMode, data.HasVerticalForm);

            foreach (GlyphMetrics gm in fontMetrics.GetGlyphMetrics(codePoint, id, textAttributes, textDecorations, layoutMode, colorFontSupport, data.HasVerticalForm))
            {
                if (gm.GlyphType == GlyphType.Fallback && !CodePoint.IsControl(codePoint))
                {
                    hasFallBacks = true;
                }

                metrics.Add(gm.CloneForRendering(data.TextRun, codePoint));
            }

            if (metrics.Count > 0)
            {
                GlyphMetrics[] gm = metrics.ToArray();
                SpaceFallback.SetAdvance(gm, data, fontMetrics, this.TextOptions, isVerticalLayout);
                if (isVerticalLayout)
                {
                    this.glyphs.Add(new(offset, new(data, true) { Bounds = new(0, 0, 0, gm[0].AdvanceHeight) }, font.Size, gm));
                }
                else
                {
                    this.glyphs.Add(new(offset, new(data, true) { Bounds = new(0, 0, gm[0].AdvanceWidth, 0) }, font.Size, gm));
                }
            }
        }

        return !hasFallBacks;
    }

    /// <summary>
    /// Updates the position of the glyph at the specified index.
    /// </summary>
    /// <param name="fontMetrics">The font metrics.</param>
    /// <param name="index">The zero-based index of the element.</param>
    public void UpdatePosition(FontMetrics fontMetrics, int index)
    {
        GlyphShapingData data = this[index];
        bool isDirtyXY = data.Bounds.IsDirtyXY;
        bool isDirtyWH = data.Bounds.IsDirtyWH;
        if (!isDirtyXY && !isDirtyWH)
        {
            return;
        }

        ushort glyphId = data.GlyphId;
        LayoutMode layoutMode = this.TextOptions.LayoutMode;
        foreach (GlyphMetrics m in this.glyphs[index].Metrics)
        {
            if (m.GlyphId == glyphId && fontMetrics == m.FontMetrics)
            {
                if (isDirtyXY)
                {
                    m.ApplyOffset(data.Bounds.X, data.Bounds.Y);
                }

                // As in HarfBuzz, positioning changes the advance along the line only. Across the line the glyph keeps
                // the advance of the font, which the bounds do not hold.
                if (isDirtyWH)
                {
                    if (AdvancedTypographicUtils.IsVerticalGlyph(m.CodePoint, layoutMode, data.HasVerticalForm))
                    {
                        m.SetAdvanceHeight(data.Bounds.Height);
                    }
                    else
                    {
                        m.SetAdvanceWidth(data.Bounds.Width);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adds dx to the advance width of the glyphs at the given index and id, which are glyphs of horizontal text.
    /// </summary>
    /// <param name="fontMetrics">The font face with metrics.</param>
    /// <param name="index">The zero-based index of the element.</param>
    /// <param name="glyphId">The id of the glyph to offset.</param>
    /// <param name="dx">The delta x-advance.</param>
    public void Advance(FontMetrics fontMetrics, int index, ushort glyphId, short dx)
    {
        foreach (GlyphMetrics m in this.glyphs[index].Metrics)
        {
            if (m.GlyphId == glyphId && fontMetrics == m.FontMetrics)
            {
                m.ApplyAdvance(dx);
            }
        }
    }

    /// <summary>
    /// Moves the glyphs at the given index and id by dx and dy. Their advance stays the same.
    /// </summary>
    /// <param name="fontMetrics">The font face with metrics.</param>
    /// <param name="index">The zero-based index of the element.</param>
    /// <param name="glyphId">The id of the glyph to move.</param>
    /// <param name="dx">The delta x-offset.</param>
    /// <param name="dy">The delta y-offset.</param>
    public void Offset(FontMetrics fontMetrics, int index, ushort glyphId, short dx, short dy)
    {
        foreach (GlyphMetrics m in this.glyphs[index].Metrics)
        {
            if (m.GlyphId == glyphId && fontMetrics == m.FontMetrics)
            {
                m.ApplyOffset(dx, dy);
            }
        }
    }

    /// <summary>
    /// Hides the default-ignorable characters that the font drew and GSUB did not substitute, as HarfBuzz does once
    /// they have done their work in shaping. Each takes the glyph of U+0020 with no advance and no offset, or is
    /// left out when the font has no U+0020. The character stays, so line breaking does not change.
    /// </summary>
    /// <param name="fontMetrics">The font whose glyphs to hide.</param>
    public void HideDefaultIgnorables(FontMetrics fontMetrics)
    {
        bool hasSpace = fontMetrics.TryGetGlyphId(new CodePoint(SpaceFallback.Space), out ushort space);
        LayoutMode layoutMode = this.TextOptions.LayoutMode;
        for (int i = this.glyphs.Count - 1; i >= 0; i--)
        {
            GlyphPositioningData glyph = this.glyphs[i];
            GlyphShapingData data = glyph.Data;
            if (!this.ShouldProcess(fontMetrics, i) || !data.IsDefaultIgnorable)
            {
                continue;
            }

            if (!hasSpace)
            {
                this.glyphs.RemoveAt(i);
                continue;
            }

            GlyphMetrics metrics = fontMetrics
                .GetGlyphMetrics(data.CodePoint, space, data.TextRun.TextAttributes, data.TextRun.TextDecorations, layoutMode, ColorFontSupport.None, data.HasVerticalForm)[0]
                .CloneForRendering(data.TextRun, data.CodePoint);
            metrics.SetAdvanceWidth(0);
            metrics.SetAdvanceHeight(0);
            data.GlyphId = space;
            data.Bounds = new(0, 0, 0, 0);
            glyph.Metrics = [metrics];
        }
    }

    /// <summary>
    /// Returns a value indicating whether the element at the given index should be processed.
    /// </summary>
    /// <param name="fontMetrics">The font face with metrics.</param>
    /// <param name="index">The zero-based index of the elements to position.</param>
    /// <returns><see langword="true"/> if the element should be processed; otherwise, <see langword="false"/>.</returns>
    public bool ShouldProcess(FontMetrics fontMetrics, int index)
        => this.glyphs[index].Metrics[0].FontMetrics == fontMetrics;

    /// <summary>
    /// The glyphs of a character that a glyph is missing for: the indices of its glyphs in this collection, in order, and
    /// its glyphs in the run of a fallback font.
    /// </summary>
    private sealed class Character
    {
        public List<int> Indices { get; } = [];

        public List<GlyphShapingData> Fallback { get; } = [];
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private class GlyphPositioningData
    {
        public GlyphPositioningData(int offset, GlyphShapingData data, float pointSize, GlyphMetrics[] metrics)
        {
            this.Offset = offset;
            this.Data = data;
            this.PointSize = pointSize;
            this.Metrics = metrics;
        }

        public int Offset { get; set; }

        public GlyphShapingData Data { get; set; }

        public float PointSize { get; set; }

        public GlyphMetrics[] Metrics { get; set; }

        private string DebuggerDisplay => FormattableString.Invariant($"Offset: {this.Offset}, Data: {this.Data.ToDebuggerDisplay()}");
    }
}
