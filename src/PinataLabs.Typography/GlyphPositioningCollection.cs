// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    /// Replaces each glyph that no font so far has with the glyphs that a fallback font gives the same character. The font
    /// of the text decided which glyphs the text has and in what order, so the new glyphs take the place of the glyph
    /// they replace, wherever the shaper of that font put it.
    /// </summary>
    /// <param name="font">The fallback font.</param>
    /// <param name="collection">The glyphs of the whole text, shaped with the fallback font.</param>
    /// <returns><see langword="true"/> if the metrics collection does not contain any fallbacks; otherwise <see langword="false"/>.</returns>
    public bool TryUpdate(Font font, GlyphSubstitutionCollection collection)
    {
        FontMetrics fontMetrics = font.FontMetrics;
        LayoutMode layoutMode = this.TextOptions.LayoutMode;
        ColorFontSupport colorFontSupport = this.TextOptions.ColorFontSupport;
        Dictionary<int, List<GlyphShapingData>> glyphsByCharacter = [];
        for (int i = 0; i < collection.Count; i++)
        {
            GlyphShapingData data = collection[i];
            if (!glyphsByCharacter.TryGetValue(data.SourceOffset, out List<GlyphShapingData>? glyphs))
            {
                glyphs = [];
                glyphsByCharacter.Add(data.SourceOffset, glyphs);
            }

            glyphs.Add(data);
        }

        bool hasFallBacks = false;
        List<GlyphPositioningData> replacements = [];
        for (int i = 0; i < this.glyphs.Count; i++)
        {
            GlyphPositioningData current = this.glyphs[i];
            if (current.Metrics[0].GlyphType != GlyphType.Fallback)
            {
                continue;
            }

            List<GlyphShapingData> shapes = TakeGlyphsOfCharacter(glyphsByCharacter, current.Data);
            if (shapes.Count == 0)
            {
                // The fallback font folded the character into the glyph of another, as a ligature does.
                this.glyphs.RemoveAt(i--);
                continue;
            }

            replacements.Clear();
            foreach (GlyphShapingData shape in shapes)
            {
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
                        // This font lacks the glyph too, so the glyph is left for the next fallback font.
                        hasFallBacks = true;
                        metrics.Clear();
                        break;
                    }

                    metrics.Add(gm.CloneForRendering(shape.TextRun, codePoint));
                }

                if (metrics.Count == 0)
                {
                    replacements.Clear();
                    break;
                }

                SpaceFallback.SetAdvance(metrics, shape, fontMetrics, this.TextOptions, AdvancedTypographicUtils.IsVerticalGlyph(codePoint, layoutMode, shape.HasVerticalForm));
                int maxAdvancedWidth = 0;
                int maxAdvancedHeight = 0;
                for (int k = 0; k < metrics.Count; k++)
                {
                    maxAdvancedWidth = Math.Max(maxAdvancedWidth, metrics[k].AdvanceWidth);
                    maxAdvancedHeight = Math.Max(maxAdvancedHeight, metrics[k].AdvanceHeight);
                }

                replacements.Add(new(current.Offset, new(shape, true) { Bounds = new(0, 0, maxAdvancedWidth, maxAdvancedHeight) }, current.PointSize, metrics.ToArray()));
            }

            if (replacements.Count > 0)
            {
                this.glyphs.RemoveAt(i);
                this.glyphs.InsertRange(i, replacements);
                i += replacements.Count - 1;
            }
        }

        return !hasFallBacks;
    }

    /// <summary>
    /// Takes the glyphs of a fallback font that stand for the same character as a glyph that an earlier font lacks: an
    /// inserted glyph for an inserted glyph, and otherwise the glyphs of the character. When the fallback font kept the
    /// code point of the glyph, only the glyphs of that code point, as the two halves of a split vowel each have their own.
    /// </summary>
    /// <param name="glyphsByCharacter">The glyphs of the fallback font that are not taken yet, by source offset.</param>
    /// <param name="glyph">The glyph that the earlier font lacks.</param>
    /// <returns>The glyphs, in the order of the fallback font.</returns>
    private static List<GlyphShapingData> TakeGlyphsOfCharacter(Dictionary<int, List<GlyphShapingData>> glyphsByCharacter, GlyphShapingData glyph)
    {
        List<GlyphShapingData> taken = [];
        if (!glyphsByCharacter.TryGetValue(glyph.SourceOffset, out List<GlyphShapingData>? glyphs))
        {
            return taken;
        }

        bool hasCodePoint = glyphs.Exists(g => g.IsInserted == glyph.IsInserted && g.CodePoint == glyph.CodePoint);
        for (int i = 0; i < glyphs.Count; i++)
        {
            GlyphShapingData candidate = glyphs[i];
            if (candidate.IsInserted == glyph.IsInserted && (!hasCodePoint || candidate.CodePoint == glyph.CodePoint))
            {
                taken.Add(candidate);
                glyphs.RemoveAt(i--);
            }
        }

        return taken;
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
