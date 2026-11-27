// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// Represents a collection of glyph indices that are mapped to input codepoints.
/// </summary>
internal sealed class GlyphSubstitutionCollection : IGlyphShapingCollection
{
    /// <summary>
    /// Contains a map the index of a map within the collection, non-sequential codepoint offsets, and their glyph ids.
    /// </summary>
    private readonly List<OffsetGlyphDataPair> glyphs = [];

    private const int ZeroWidthNonJoiner = 0x200C;

    private const int ZeroWidthJoiner = 0x200D;

    // The number of regional indicators in a row at the end of the characters added so far. The second of each pair joins
    // the cluster of the first.
    private int regionalIndicators;

    // Scratch space of ReplaceNormalized, kept so that normalizing a long text does not allocate for each cluster.
    private OffsetGlyphDataPair[] normalizedSources = [];

    private int[] normalizedOffsets = [];

    // The syllable numbers that the shapers of the script runs have taken.
    private int syllableNumbers;

    // The glyphs before and after the script run that the table shapes. Substitutions add and remove glyphs of the run
    // only, so the glyphs after it keep their number, and the end of the run follows the substitutions.
    private int runStart;
    private int glyphsAfterRun;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphSubstitutionCollection"/> class.
    /// </summary>
    /// <param name="textOptions">The text options.</param>
    public GlyphSubstitutionCollection(TextOptions textOptions) => this.TextOptions = textOptions;

    /// <summary>
    /// Gets the number of glyphs ids contained in the collection.
    /// This may be more or less than original input codepoint count (due to substitution process).
    /// </summary>
    public int Count => this.glyphs.Count;

    /// <inheritdoc />
    public TextOptions TextOptions { get; }

    /// <inheritdoc />
    public int RunStart => this.runStart;

    /// <inheritdoc />
    public int RunEnd => this.glyphs.Count - this.glyphsAfterRun;

    /// <summary>
    /// Gets or sets the metrics of the font currently being shaped. This can differ from the font
    /// of a glyph's text run when the glyph is being shaped by a fallback font.
    /// </summary>
    internal FontMetrics? FontMetrics { get; set; }

    /// <summary>
    /// Gets or sets, for a fallback font, the <see cref="GlyphShapingData.SourceOffset"/> of each character that no earlier
    /// font has a glyph for. <see langword="null"/> for the font of the text run.
    /// </summary>
    internal HashSet<int>? UnresolvedOffsets { get; set; }

    /// <summary>
    /// Gets the characters of the text before the glyphs, nearest first. As the context of a HarfBuzz buffer, they
    /// give the letters at the start of a text run their joining form.
    /// </summary>
    internal List<CodePoint> PreContext { get; } = [];

    /// <summary>
    /// Gets the characters of the text after the glyphs, nearest first.
    /// </summary>
    internal List<CodePoint> PostContext { get; } = [];

    /// <summary>
    /// Gets or sets the running id of any ligature glyphs contained withing this collection are a member of.
    /// </summary>
    public int LigatureId { get; set; } = 1;

    /// <inheritdoc />
    public GlyphShapingData this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this.glyphs[index].Data;
    }

    /// <summary>
    /// Takes syllable numbers for a script run, as many as the run has glyphs, which is the most syllables it can have.
    /// A per-syllable lookup matches only glyphs of the syllable of its first glyph, and its context can reach the
    /// glyphs of another run, which HarfBuzz shapes in a buffer of its own. So no two runs share a number.
    /// </summary>
    /// <param name="count">The number of glyphs of the run.</param>
    /// <returns>The number before the first one taken.</returns>
    public int TakeSyllableNumbers(int count)
    {
        int last = this.syllableNumbers;
        this.syllableNumbers += count;
        return last;
    }

    /// <summary>
    /// Sets the script run that the lookups apply to, as <see cref="RunStart"/> and <see cref="RunEnd"/> give it.
    /// </summary>
    /// <param name="start">The zero-based index of the first glyph of the run.</param>
    /// <param name="count">The number of glyphs of the run.</param>
    public void SetRun(int start, int count)
    {
        this.runStart = start;
        this.glyphsAfterRun = this.glyphs.Count - start - count;
    }

    /// <summary>
    /// Gets the metrics of the font that should be used to shape the given glyph.
    /// </summary>
    /// <param name="data">The glyph shaping data.</param>
    /// <returns>The font metrics.</returns>
    internal FontMetrics GetFontMetrics(GlyphShapingData data)
        => this.FontMetrics ?? data.TextRun.Font!.FontMetrics;

    /// <summary>
    /// Gets the shaping data at the specified position.
    /// </summary>
    /// <param name="index">The zero-based index of the elements to get.</param>
    /// <param name="offset">The zero-based index within the input codepoint collection.</param>
    /// <returns>The <see cref="GlyphShapingData"/>.</returns>
    internal GlyphShapingData GetGlyphShapingData(int index, out int offset)
    {
        OffsetGlyphDataPair pair = this.glyphs[index];
        offset = pair.Offset;
        return pair.Data;
    }

    /// <summary>
    /// Gets a value indicating whether the glyphs in the range may become other glyphs. A fallback font replaces glyphs
    /// character by character, so it may compose, ligate or decompose only characters that no earlier font has a glyph
    /// for. Otherwise a glyph of an earlier font would lose the characters that the fallback font folded into it.
    /// </summary>
    /// <param name="start">The zero-based index of the first glyph.</param>
    /// <param name="end">The zero-based index after the last glyph.</param>
    /// <returns><see langword="true"/> if every glyph in the range is unresolved.</returns>
    internal bool IsUnresolved(int start, int end)
    {
        if (this.UnresolvedOffsets is null)
        {
            return true;
        }

        for (int i = start; i < end; i++)
        {
            if (!this.UnresolvedOffsets.Contains(this.glyphs[i].Data.SourceOffset))
            {
                return false;
            }
        }

        return true;
    }

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
    /// Adds a clone of the glyph shaping data to the collection at the specified offset.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="offset">The zero-based index within the input codepoint collection.</param>
    public void AddGlyph(GlyphShapingData data, int offset)
        => this.glyphs.Add(new(offset, new(data, false)));

    /// <summary>
    /// Adds the glyph id and the codepoint it represents to the collection.
    /// </summary>
    /// <param name="glyphId">The id of the glyph to add.</param>
    /// <param name="codePoint">The codepoint the glyph represents.</param>
    /// <param name="direction">The resolved text direction for the codepoint.</param>
    /// <param name="textRun">The text run this glyph belongs to.</param>
    /// <param name="offset">The zero-based index within the input codepoint collection.</param>
    /// <param name="charIndex">The zero-based UTF-16 index of the codepoint in the text.</param>
    public void AddGlyph(ushort glyphId, CodePoint codePoint, TextDirection direction, TextRun textRun, int offset, int charIndex)
    {
        // A variation selector that the cmap table folds into its base has no glyph of its own, so a mark after it joins
        // the cluster of the base.
        int cluster = this.ContinuesCluster(codePoint) ? this.glyphs[^1].Data.Cluster : charIndex;

        this.glyphs.Add(new(offset, new(textRun)
        {
            CodePoint = codePoint,
            Direction = direction,
            GlyphId = glyphId,
            Cluster = cluster,
            SourceOffset = offset,
        }));
    }

    /// <summary>
    /// Inserts a glyph before the glyph at the specified index. The new glyph copies the shaping data
    /// of the source glyph, whose cluster it joins, and represents no codepoints of its own.
    /// </summary>
    /// <param name="index">The zero-based index to insert the glyph at. It can equal the count to append.</param>
    /// <param name="sourceIndex">The zero-based index of the glyph to copy, before the insertion.</param>
    /// <param name="glyphId">The id of the glyph to insert.</param>
    /// <param name="codePoint">The codepoint the glyph represents.</param>
    public void InsertGlyph(int index, int sourceIndex, ushort glyphId, CodePoint codePoint)
    {
        OffsetGlyphDataPair pair = this.glyphs[sourceIndex];

        // The two glyphs share an offset. The layout gives each of them an advance only when they are decomposed.
        pair.Data.IsDecomposed = true;
        GlyphShapingData data = new(pair.Data, false)
        {
            GlyphId = glyphId,
            CodePoint = codePoint,
            CodePointCount = 0,
            HoldsInsertedGlyph = true,
            IsSubstituted = true,
            IsLigated = false,
            LigatureId = 0,
            LigatureComponent = -1,
            MarkAttachment = -1,
            CursiveAttachment = 0,
        };

        this.glyphs.Insert(index, new(pair.Offset, data));
    }

    /// <summary>
    /// Inserts the dotted circle of a broken cluster before the glyph at the specified index, as
    /// <see cref="InsertGlyph"/> does. It has no class until a substitution gives it one.
    /// </summary>
    /// <param name="index">The zero-based index to insert the circle at. It can equal the count to append.</param>
    /// <param name="sourceIndex">The zero-based index of the glyph to copy, before the insertion.</param>
    /// <param name="glyphId">The id of the glyph of U+25CC.</param>
    public void InsertBrokenClusterCircle(int index, int sourceIndex, ushort glyphId)
    {
        this.InsertGlyph(index, sourceIndex, glyphId, new CodePoint(0x25CC));
        this.glyphs[index].Data.IsUnclassified = true;
    }

    /// <summary>
    /// Gives the glyphs in the range the lowest cluster among them, as HarfBuzz does when a ligature, a reordering or a
    /// composition joins glyphs. The glyphs next to the range that share the cluster of its first or last glyph join it,
    /// so that no cluster is split between two ranges.
    /// </summary>
    /// <param name="start">The zero-based index of the first glyph.</param>
    /// <param name="end">The zero-based index after the last glyph.</param>
    internal void MergeClusters(int start, int end)
    {
        if (end - start < 2)
        {
            return;
        }

        int cluster = this.glyphs[start].Data.Cluster;
        for (int i = start + 1; i < end; i++)
        {
            cluster = Math.Min(cluster, this.glyphs[i].Data.Cluster);
        }

        while (end < this.glyphs.Count && this.glyphs[end - 1].Data.Cluster == this.glyphs[end].Data.Cluster)
        {
            end++;
        }

        while (start > 0 && this.glyphs[start - 1].Data.Cluster == this.glyphs[start].Data.Cluster)
        {
            start--;
        }

        for (int i = start; i < end; i++)
        {
            this.glyphs[i].Data.Cluster = cluster;
        }
    }

    // As HarfBuzz does at its default cluster level, a character shares the cluster of the character before it when it is
    // a mark, or has the grapheme cluster class Extend: Grapheme_Extend and the emoji modifiers, which are the tag
    // characters, the halfwidth katakana sound marks and a few signs that Unicode keeps for canonical equivalence. The
    // zero width non-joiner is Extend too, but it ends a cluster. A zero width joiner, the second of a pair of regional
    // indicators, and an emoji after a joiner share it as well. The count of regional indicators in a row tells which
    // of a pair a character is, so call this once for each character that is added.
    private bool ContinuesCluster(CodePoint codePoint)
    {
        GraphemeClusterClass graphemeClass = CodePoint.GetGraphemeClusterClass(codePoint);
        bool isRegionalIndicator = graphemeClass == GraphemeClusterClass.RegionalIndicator;
        this.regionalIndicators = isRegionalIndicator ? this.regionalIndicators + 1 : 0;
        if (this.glyphs.Count == 0)
        {
            return false;
        }

        return CodePoint.IsMark(codePoint)
            || (graphemeClass == GraphemeClusterClass.Extend && codePoint.Value != ZeroWidthNonJoiner)
            || graphemeClass == GraphemeClusterClass.ZeroWidthJoiner
            || (isRegionalIndicator && (this.regionalIndicators & 1) == 0)
            || (this.glyphs[^1].Data.CodePoint.Value == ZeroWidthJoiner && graphemeClass == GraphemeClusterClass.ExtendedPictographic);
    }

    /// <summary>
    /// Moves the specified glyph to the specified position.
    /// </summary>
    /// <param name="fromIndex">The index to move from.</param>
    /// <param name="toIndex">The index to move to.</param>
    public void MoveGlyph(int fromIndex, int toIndex)
    {
        this.MergeClusters(Math.Min(fromIndex, toIndex), Math.Max(fromIndex, toIndex) + 1);
        this.MoveGlyphKeepingClusters(fromIndex, toIndex);
    }

    /// <summary>
    /// Moves the specified glyph to the specified position, and leaves the clusters of the glyphs as they are. A shaper
    /// that merges the clusters of a reordering itself, as the Indic shaper of HarfBuzz does, calls
    /// <see cref="MergeClusters"/> for the glyphs that the reordering joins.
    /// </summary>
    /// <param name="fromIndex">The index to move from.</param>
    /// <param name="toIndex">The index to move to.</param>
    internal void MoveGlyphKeepingClusters(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            return;
        }

        GlyphShapingData data = this[fromIndex];
        if (fromIndex > toIndex)
        {
            // Move item to the right
            for (int i = fromIndex; i > toIndex; i--)
            {
                this.glyphs[i].Data = this.glyphs[i - 1].Data;
            }
        }
        else
        {
            // Move item to the left
            for (int i = fromIndex; i < toIndex; i++)
            {
                this.glyphs[i].Data = this.glyphs[i + 1].Data;
            }
        }

        this.glyphs[toIndex].Data = data;
    }

    /// <summary>
    /// Performs a stable sort of the glyphs by the comparison delegate starting at the specified index.
    /// </summary>
    /// <param name="startIndex">The start index.</param>
    /// <param name="endIndex">The end index.</param>
    /// <param name="comparer">The comparison delegate.</param>
    public void Sort(int startIndex, int endIndex, Comparison<GlyphShapingData> comparer)
    {
        for (int i = startIndex + 1; i < endIndex; i++)
        {
            int j = i;
            while (j > startIndex && comparer(this[j - 1], this[i]) > 0)
            {
                j--;
            }

            if (i == j)
            {
                continue;
            }

            // Move item i to occupy place for item j, shift what's in between.
            this.MoveGlyph(i, j);
        }
    }

    /// <summary>
    /// Removes all elements from the collection.
    /// </summary>
    public void Clear()
    {
        this.glyphs.Clear();
        this.regionalIndicators = 0;
        this.LigatureId = 1;
        this.syllableNumbers = 0;
        this.SetRun(0, 0);
        this.PreContext.Clear();
        this.PostContext.Clear();
    }

    /// <summary>
    /// Sets the text around the glyphs, as HarfBuzz keeps up to five characters on each side of its buffer.
    /// </summary>
    /// <param name="before">The text before the glyphs.</param>
    /// <param name="after">The text after the glyphs.</param>
    public void SetContext(ReadOnlySpan<char> before, ReadOnlySpan<char> after)
    {
        const int contextLength = 5;
        this.PreContext.Clear();
        this.PostContext.Clear();
        int index = before.Length;
        while (index > 0 && this.PreContext.Count < contextLength)
        {
            index -= char.IsLowSurrogate(before[index - 1]) && index > 1 && char.IsHighSurrogate(before[index - 2]) ? 2 : 1;
            this.PreContext.Add(CodePoint.DecodeFromUtf16At(before, index));
        }

        SpanCodePointEnumerator codePoints = new(after);
        while (this.PostContext.Count < contextLength && codePoints.MoveNext())
        {
            this.PostContext.Add(codePoints.Current);
        }
    }

    /// <summary>
    /// Performs a 1:1 replacement of a glyph id at the given position. As in HarfBuzz, the glyph keeps its ligature id and
    /// component, so a mark that a ligature skipped still attaches to its component.
    /// </summary>
    /// <param name="index">The zero-based index of the element to replace.</param>
    /// <param name="glyphId">The replacement glyph id.</param>
    public void Replace(int index, ushort glyphId)
    {
        GlyphShapingData current = this.glyphs[index].Data;
        current.GlyphId = glyphId;
        current.MarkAttachment = -1;
        current.CursiveAttachment = 0;
        current.IsSubstituted = true;
        this.TakeGlyphClass(current);
    }

    /// <summary>
    /// Performs a 1:1 replacement of a glyph id at the given position while removing a series of glyph ids at the given positions within the sequence.
    /// </summary>
    /// <param name="index">The zero-based index of the element to replace.</param>
    /// <param name="removalIndices">The indices at which to remove elements.</param>
    /// <param name="glyphId">The replacement glyph id.</param>
    /// <param name="ligatureId">The ligature id.</param>
    public void Replace(int index, ReadOnlySpan<int> removalIndices, ushort glyphId, int ligatureId)
    {
        // The ligature takes the cluster of its components and of the glyphs between them.
        if (removalIndices.Length > 0)
        {
            this.MergeClusters(index, removalIndices[^1] + 1);
        }

        // Remove the glyphs at each index.
        int codePointCount = 0;
        bool holdsInsertedGlyph = false;
        for (int i = removalIndices.Length - 1; i >= 0; i--)
        {
            int match = removalIndices[i];
            codePointCount += this.glyphs[match].Data.CodePointCount;
            holdsInsertedGlyph |= this.glyphs[match].Data.HoldsInsertedGlyph;
            this.glyphs.RemoveAt(match);
        }

        // Assign our new id at the index.
        GlyphShapingData current = this.glyphs[index].Data;
        current.CodePointCount += codePointCount;
        current.HoldsInsertedGlyph |= holdsInsertedGlyph;
        current.GlyphId = glyphId;
        current.LigatureId = ligatureId;
        current.IsLigated = true;
        current.IsUnclassified = false;
        current.IsMultiplied = false;
        current.LigatureComponent = -1;
        current.MarkAttachment = -1;
        current.CursiveAttachment = 0;
        current.IsSubstituted = true;
    }

    /// <summary>
    /// Performs a 1:1 replacement of a glyph id at the given position while removing a series of glyph ids.
    /// </summary>
    /// <param name="index">The zero-based index of the element to replace.</param>
    /// <param name="count">The number of glyphs to remove.</param>
    /// <param name="glyphId">The replacement glyph id.</param>
    public void Replace(int index, int count, ushort glyphId)
    {
        this.MergeClusters(index, index + count + 1);

        // Remove the glyphs at each index.
        int codePointCount = 0;
        for (int i = count; i > 0; i--)
        {
            int match = index + i;
            codePointCount += this.glyphs[match].Data.CodePointCount;
            this.glyphs.RemoveAt(match);
        }

        // Assign our new id at the index.
        GlyphShapingData current = this.glyphs[index].Data;
        current.CodePointCount += codePointCount;
        current.GlyphId = glyphId;
        current.LigatureId = 0;
        current.LigatureComponent = -1;
        current.MarkAttachment = -1;
        current.CursiveAttachment = 0;
        current.IsSubstituted = true;
    }

    // As in HarfBuzz, a substitution in a font with glyph classes gives the glyph the class of its new glyph.
    private void TakeGlyphClass(GlyphShapingData data)
        => data.IsUnclassified &= !this.GetFontMetrics(data).TryGetGlyphClass(data.GlyphId, out _);

    /// <summary>
    /// Replaces a single glyph id with a collection of glyph ids. As in HarfBuzz, each glyph is a component of the glyph it
    /// replaces, unless that glyph has a ligature id, as a ligature or a mark that a ligature skipped has: then each glyph
    /// keeps the id and the component. A ligature gives glyphs of the base class. One glyph id is a single substitution.
    /// </summary>
    /// <param name="index">The zero-based index of the element to replace.</param>
    /// <param name="glyphIds">The collection of replacement glyph ids.</param>
    public void Replace(int index, ReadOnlySpan<ushort> glyphIds)
    {
        if (glyphIds.Length == 1)
        {
            this.Replace(index, glyphIds[0]);
        }
        else if (glyphIds.Length > 0)
        {
            OffsetGlyphDataPair pair = this.glyphs[index];
            GlyphShapingData current = pair.Data;
            bool isComponent = current.LigatureId == 0;
            if (current.SubstitutedGlyphClass == GlyphClassDef.LigatureGlyph)
            {
                current.SubstitutedGlyphClass = GlyphClassDef.BaseGlyph;
            }

            current.GlyphId = glyphIds[0];
            if (isComponent)
            {
                current.LigatureComponent = 0;
            }

            current.MarkAttachment = -1;
            current.CursiveAttachment = 0;
            current.IsSubstituted = true;
            current.IsDecomposed = true;
            this.TakeGlyphClass(current);

            // Add additional glyphs from the rest of the sequence.
            glyphIds = glyphIds.Slice(1);
            for (int i = 0; i < glyphIds.Length; i++)
            {
                GlyphShapingData data = new(current, false)
                {
                    GlyphId = glyphIds[i],
                    LigatureComponent = isComponent ? i + 1 : current.LigatureComponent
                };

                this.glyphs.Insert(++index, new(pair.Offset, data));
            }
        }
        else
        {
            // Spec disallows removal of glyphs in this manner but it's common enough practice to allow it.
            // https://github.com/MicrosoftDocs/typography-issues/issues/673
            this.glyphs.RemoveAt(index);
        }
    }

    /// <summary>
    /// Replaces the glyphs of one character and its marks with their normalized form. The offsets of the
    /// characters that the new glyphs come from are given out in ascending order, so a glyph that did not move
    /// keeps its offset. Glyphs that share an offset are laid out one after the other.
    /// </summary>
    /// <param name="index">The zero-based index of the first glyph to replace.</param>
    /// <param name="count">The number of glyphs to replace.</param>
    /// <param name="glyphs">The new glyphs.</param>
    public void ReplaceNormalized(int index, int count, ReadOnlySpan<GlyphNormalizer.Item> glyphs)
    {
        if (this.normalizedSources.Length < count)
        {
            this.normalizedSources = new OffsetGlyphDataPair[count];
        }

        if (this.normalizedOffsets.Length < glyphs.Length)
        {
            this.normalizedOffsets = new int[glyphs.Length];
        }

        Span<OffsetGlyphDataPair> sources = this.normalizedSources.AsSpan(0, count);
        Span<int> offsets = this.normalizedOffsets.AsSpan(0, glyphs.Length);
        for (int i = 0; i < count; i++)
        {
            sources[i] = this.glyphs[index + i];
        }

        for (int i = 0; i < glyphs.Length; i++)
        {
            int offset = sources[glyphs[i].Source].Offset;
            int j = i;
            while (j > 0 && offsets[j - 1] > offset)
            {
                offsets[j] = offsets[j - 1];
                j--;
            }

            offsets[j] = offset;
        }

        if (glyphs.Length < count)
        {
            this.glyphs.RemoveRange(index + glyphs.Length, count - glyphs.Length);
        }

        for (int i = 0; i < glyphs.Length; i++)
        {
            GlyphNormalizer.Item glyph = glyphs[i];
            bool sharesOffset = (i > 0 && offsets[i - 1] == offsets[i]) || (i + 1 < offsets.Length && offsets[i + 1] == offsets[i]);
            OffsetGlyphDataPair pair = new(offsets[i], new(sources[glyph.Source].Data, false)
            {
                CodePoint = glyph.CodePoint,
                UnicodePropertiesCodePoint = glyph.UnicodePropertiesCodePoint,
                GlyphId = glyph.GlyphId,
                IsDecomposed = sharesOffset,
            });

            if (i < count)
            {
                this.glyphs[index + i] = pair;
            }
            else
            {
                this.glyphs.Insert(index + i, pair);
            }
        }

        sources.Clear();
    }

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    private class OffsetGlyphDataPair
    {
        public OffsetGlyphDataPair(int offset, GlyphShapingData data)
        {
            this.Offset = offset;
            this.Data = data;
        }

        public int Offset { get; set; }

        public GlyphShapingData Data { get; set; }

        private string DebuggerDisplay => FormattableString.Invariant($"Offset: {this.Offset}, Data: {this.Data.ToDebuggerDisplay()}");
    }
}
