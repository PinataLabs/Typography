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
    /// GPOS table has no <c>kern</c> feature for. A pair is two glyphs with nothing but marks between them.
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

        int start = 0;
        while (start < collection.Count)
        {
            int end = FallbackMarkPositioner.GetScriptRunEnd(collection, start, out ScriptClass script);
            if (ShaperFactory.HasFallbackPositioning(script) && gpos?.HasFeature(script, GPosTable.KernTag) != true)
            {
                int left = start;
                for (int right = start + 1; right < end; right++)
                {
                    if (collection.ShouldProcess(fontMetrics, right) && AdvancedTypographicUtils.IsZeroWidthMark(fontMetrics, collection[right]))
                    {
                        continue;
                    }

                    this.KernPair(fontMetrics, collection, left, right);
                    left = right;
                }
            }

            start = end;
        }
    }

    private void KernPair(FontMetrics fontMetrics, GlyphPositioningCollection collection, int left, int right)
    {
        GlyphShapingData leftData = collection[left];
        GlyphShapingData rightData = collection[right];
        if (!collection.ShouldProcess(fontMetrics, left) || !collection.ShouldProcess(fontMetrics, right))
        {
            return;
        }

        LayoutMode layoutMode = collection.TextOptions.LayoutMode;
        if (AdvancedTypographicUtils.IsVerticalGlyph(leftData.CodePoint, layoutMode, leftData.HasVerticalForm)
            || AdvancedTypographicUtils.IsVerticalGlyph(rightData.CodePoint, layoutMode, rightData.HasVerticalForm))
        {
            // A glyph that advances down the line takes the whole kerning, which HarfBuzz does not do.
            if (this.TryGetKerningOffset(leftData.GlyphId, rightData.GlyphId, out Vector2 vertical))
            {
                collection.Advance(fontMetrics, right, rightData.GlyphId, (short)vertical.X, (short)vertical.Y);
            }

            return;
        }

        if (leftData.Direction != rightData.Direction)
        {
            return;
        }

        // The table has the pairs in the order of the line, so the glyph that comes later in backward text is first.
        (int first, int second) = leftData.Direction == TextDirection.RightToLeft ? (right, left) : (left, right);
        if (!this.TryGetKerningOffset(collection[first].GlyphId, collection[second].GlyphId, out Vector2 kerning))
        {
            return;
        }

        // Each glyph takes half of the kerning. The second glyph also moves by its half, so that it stays in
        // place within its advance.
        int firstHalf = (int)kerning.X >> 1;
        short secondHalf = (short)((int)kerning.X - firstHalf);
        collection.Advance(fontMetrics, first, collection[first].GlyphId, (short)firstHalf, 0);
        collection.Advance(fontMetrics, second, collection[second].GlyphId, secondHalf, 0);
        collection.Offset(fontMetrics, second, collection[second].GlyphId, secondHalf, 0);
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
