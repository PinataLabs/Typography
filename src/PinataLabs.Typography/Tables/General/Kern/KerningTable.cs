// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.General.Kern;

internal sealed class KerningTable : Table
{
    internal const string TableName = "kern";
    private readonly KerningSubTable[] kerningSubTable;

    public KerningTable(KerningSubTable[] kerningSubTable)
        => this.kerningSubTable = kerningSubTable;

    public int Count => this.kerningSubTable.Length;

    public static KerningTable Load(FontReader fontReader)
    {
        if (!fontReader.TryGetReaderAtTablePosition(TableName, out BigEndianBinaryReader? binaryReader))
        {
            // this table is optional.
            return new KerningTable([]);
        }

        using (binaryReader)
        {
            // Move to start of table.
            return Load(binaryReader);
        }
    }

    public static KerningTable Load(BigEndianBinaryReader reader)
    {
        // +--------+---------+-------------------------------------------+
        // | Type   | Field   | Description                               |
        // +========+=========+===========================================+
        // | uint16 | version | Table version number(0)                   |
        // +--------+---------+-------------------------------------------+
        // | uint16 | nTables | Number of subtables in the kerning table. |
        // +--------+---------+-------------------------------------------+
        ushort version = reader.ReadUInt16();
        ushort subTableCount = reader.ReadUInt16();

        var tables = new List<KerningSubTable>(subTableCount);
        for (int i = 0; i < subTableCount; i++)
        {
            var t = KerningSubTable.Load(reader); // returns null for unknown/supported table format
            if (t != null)
            {
                tables.Add(t);
            }
        }

        return new KerningTable(tables.ToArray());
    }

    /// <summary>
    /// Kerns the glyphs of the font as HarfBuzz does with a <c>kern</c> table. Only the shapers that
    /// <see cref="ShaperFactory.HasFallbackPositioning"/> names kern with it, and only text of a script that the
    /// GPOS table does not position or has no <c>kern</c> feature for. A pair is two glyphs with nothing but marks
    /// between them.
    /// </summary>
    /// <param name="fontMetrics">The font of the table.</param>
    /// <param name="collection">The glyphs of the text.</param>
    /// <param name="gpos">The GPOS table of the font, if it has one.</param>
    public void UpdatePositions(FontMetrics fontMetrics, GlyphPositioningCollection collection, GPosTable? gpos)
    {
        if (this.Count == 0)
        {
            return;
        }

        ShapingRunEnumerator runs = ShapingRunEnumerator.ByFont(collection);
        while (runs.MoveNext())
        {
            ScriptClass script = runs.Script;
            int start = runs.Start;
            int end = runs.End;
            Tag substitutionScript = fontMetrics.GetGSubTable().GetScriptTag(script);
            if (collection.ShouldProcess(fontMetrics, start)
                && ShaperFactory.HasFallbackPositioning(script, substitutionScript)
                && !(gpos?.PositionsScript(script) == true && gpos.HasFeature(script, GPosTable.KernTag, collection.TextOptions.Language, ShaperFactory.Create(script, substitutionScript, collection.TextOptions).DisabledFeatureTags)))
            {
                // The change that the kerning makes to the advance and to the offset of each glyph of the run.
                using Buffer<int> buffer = new((end - start) * 2);
                Span<int> advances = buffer.GetSpan()[..(end - start)];
                Span<int> offsets = buffer.GetSpan()[(end - start)..];
                advances.Clear();
                offsets.Clear();

                bool kerned = false;
                int left = start;
                for (int right = start + 1; right < end; right++)
                {
                    if (collection.ShouldProcess(fontMetrics, right) && AdvancedTypographicUtils.IsMarkGlyph(fontMetrics, collection[right]))
                    {
                        continue;
                    }

                    kerned |= this.KernPair(fontMetrics, collection, left, right, advances[(left - start)..], offsets[(left - start)..]);
                    left = right;
                }

                if (kerned)
                {
                    MoveAttachedMarks(fontMetrics, collection, start, advances, offsets);
                }
            }
        }
    }

    // Adds the change to each advance and offset to the spans, which start at the left glyph. Returns whether the pair
    // was kerned along the line.
    private bool KernPair(FontMetrics fontMetrics, GlyphPositioningCollection collection, int left, int right, Span<int> advances, Span<int> offsets)
    {
        GlyphShapingData leftData = collection[left];
        GlyphShapingData rightData = collection[right];
        if (!collection.ShouldProcess(fontMetrics, left) || !collection.ShouldProcess(fontMetrics, right))
        {
            return false;
        }

        // HarfBuzz does not kern vertical text with this table. In vertical mixed text a run of rotated glyphs is
        // horizontal text, which HarfBuzz shapes apart from the upright glyphs around it, so a pair kerns only when
        // both of its glyphs are rotated.
        LayoutMode layoutMode = collection.TextOptions.LayoutMode;
        if (AdvancedTypographicUtils.IsVerticalGlyph(leftData.CodePoint, layoutMode, leftData.HasVerticalForm)
            || AdvancedTypographicUtils.IsVerticalGlyph(rightData.CodePoint, layoutMode, rightData.HasVerticalForm))
        {
            return false;
        }

        // The table has the pairs in the order of the line, so the glyph that comes later in backward text is first.
        (int first, int second) = leftData.Direction == TextDirection.RightToLeft ? (right, left) : (left, right);
        if (!this.TryGetKerningOffset(collection[first].GlyphId, collection[second].GlyphId, out Vector2 kerning))
        {
            return false;
        }

        // Each glyph takes half of the kerning. The second glyph also moves by its half, so that it stays in
        // place within its advance.
        int firstHalf = (int)kerning.X >> 1;
        short secondHalf = (short)((int)kerning.X - firstHalf);
        collection.Advance(fontMetrics, first, collection[first].GlyphId, (short)firstHalf);
        collection.Advance(fontMetrics, second, collection[second].GlyphId, secondHalf);
        collection.Offset(fontMetrics, second, collection[second].GlyphId, secondHalf, 0);
        advances[first - left] += firstHalf;
        advances[second - left] += secondHalf;
        offsets[second - left] += secondHalf;
        return true;
    }

    /// <summary>
    /// Moves each GPOS mark as far as the kerning moved its base. HarfBuzz places a mark from its base after it kerns
    /// with this table, and the GPOS table of the library placed the mark before.
    /// </summary>
    private static void MoveAttachedMarks(FontMetrics fontMetrics, GlyphPositioningCollection collection, int start, Span<int> advances, Span<int> offsets)
    {
        // A glyph moves by the change to its offset and by the change to the advances before it on the line. Text
        // that runs backward starts at the end of the run, so there the advances after the glyph in the text count.
        int total = 0;
        foreach (int advance in advances)
        {
            total += advance;
        }

        // The advances become the sum of the advances before each glyph in the text.
        int sum = 0;
        for (int i = 0; i < advances.Length; i++)
        {
            int advance = advances[i];
            advances[i] = sum;
            sum += advance;
        }

        for (int i = 0; i < advances.Length; i++)
        {
            GlyphShapingData data = collection[start + i];
            int j = data.MarkAttachment - start;
            if (j < 0 || j >= i || !collection.ShouldProcess(fontMetrics, start + i))
            {
                continue;
            }

            // The base comes first in the text, so it has already moved when it is itself a mark.
            int dx = GetMove(collection, start, advances, offsets, total, j) - GetMove(collection, start, advances, offsets, total, i);
            if (dx != 0)
            {
                offsets[i] += dx;
                collection.Offset(fontMetrics, start + i, data.GlyphId, (short)dx, 0);
            }
        }
    }

    private static int GetMove(GlyphPositioningCollection collection, int start, Span<int> advancesBefore, Span<int> offsets, int total, int index)
    {
        if (collection[start + index].Direction == TextDirection.RightToLeft)
        {
            int advancesAfter = index + 1 < advancesBefore.Length ? total - advancesBefore[index + 1] : 0;
            return advancesAfter + offsets[index];
        }

        return advancesBefore[index] + offsets[index];
    }

    public bool TryGetKerningOffset(ushort previous, ushort current, out Vector2 result)
    {
        result = Vector2.Zero;
        if (this.Count == 0 || previous == 0 || current == 0)
        {
            return false;
        }

        bool kerned = false;
        foreach (KerningSubTable sub in this.kerningSubTable)
        {
            kerned |= sub.TryApplyOffset(previous, current, ref result);
        }

        return kerned;
    }
}
