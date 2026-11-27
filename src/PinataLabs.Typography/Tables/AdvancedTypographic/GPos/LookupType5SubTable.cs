// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GPos;

/// <summary>
/// Mark-to-Ligature Attachment Positioning Subtable.
/// The MarkToLigature attachment (MarkLigPos) subtable is used to position combining mark glyphs with respect to ligature base glyphs.
/// With MarkToBase attachment, described previously, each base glyph has an attachment point defined for each class of marks.
/// MarkToLigature attachment is similar, except that each ligature glyph is defined to have multiple components (in a virtual sense — not actual glyphs),
/// and each component has a separate set of attachment points defined for the different mark classes.
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-5-mark-to-ligature-attachment-positioning-subtable"/>
/// </summary>
internal static class LookupType5SubTable
{
    public static LookupSubTable Load(BigEndianBinaryReader reader, long offset, LookupFlags lookupFlags)
    {
        reader.Seek(offset, SeekOrigin.Begin);
        ushort subTableFormat = reader.ReadUInt16();

        return subTableFormat switch
        {
            1 => LookupType5Format1SubTable.Load(reader, offset, lookupFlags),
            _ => new NotImplementedSubTable(),
        };
    }

    internal sealed class LookupType5Format1SubTable : LookupSubTable
    {
        private readonly CoverageTable markCoverage;
        private readonly CoverageTable ligatureCoverage;
        private readonly MarkArrayTable markArrayTable;
        private readonly LigatureArrayTable ligatureArrayTable;

        public LookupType5Format1SubTable(
            CoverageTable markCoverage,
            CoverageTable ligatureCoverage,
            MarkArrayTable markArrayTable,
            LigatureArrayTable ligatureArrayTable,
            LookupFlags lookupFlags)
            : base(lookupFlags)
        {
            this.markCoverage = markCoverage;
            this.ligatureCoverage = ligatureCoverage;
            this.markArrayTable = markArrayTable;
            this.ligatureArrayTable = ligatureArrayTable;
        }

        public static LookupType5Format1SubTable Load(BigEndianBinaryReader reader, long offset, LookupFlags lookupFlags)
        {
            // MarkLigPosFormat1 Subtable.
            // +--------------------+---------------------------------+------------------------------------------------------+
            // | Type               |  Name                           | Description                                          |
            // +====================+=================================+======================================================+
            // | uint16             | posFormat                       | Format identifier: format = 1                        |
            // +--------------------+---------------------------------+------------------------------------------------------+
            // | Offset16           | markCoverageOffset              | Offset to markCoverage table,                        |
            // |                    |                                 | from beginning of MarkLigPos subtable.               |
            // +--------------------+---------------------------------+------------------------------------------------------+
            // | Offset16           | ligatureCoverageOffset          | Offset to ligatureCoverage table,                    |
            // |                    |                                 | from beginning of MarkLigPos subtable.               |
            // +--------------------+---------------------------------+------------------------------------------------------+
            // | uint16             | markClassCount                  | Number of defined mark classes                       |
            // +--------------------+---------------------------------+------------------------------------------------------+
            // | Offset16           | markArrayOffset                 | Offset to MarkArray table, from beginning            |
            // |                    |                                 | of MarkLigPos subtable.                              |
            // +--------------------+---------------------------------+------------------------------------------------------+
            // | Offset16           | ligatureArrayOffset             | Offset to LigatureArray table,                       |
            // |                    |                                 | from beginning of MarkLigPos subtable.               |
            // +--------------------+---------------------------------+------------------------------------------------------+
            ushort markCoverageOffset = reader.ReadOffset16();
            ushort ligatureCoverageOffset = reader.ReadOffset16();
            ushort markClassCount = reader.ReadUInt16();
            ushort markArrayOffset = reader.ReadOffset16();
            ushort ligatureArrayOffset = reader.ReadOffset16();

            var markCoverage = CoverageTable.Load(reader, offset + markCoverageOffset);
            var ligatureCoverage = CoverageTable.Load(reader, offset + ligatureCoverageOffset);
            var markArrayTable = new MarkArrayTable(reader, offset + markArrayOffset);
            var ligatureArrayTable = new LigatureArrayTable(reader, offset + ligatureArrayOffset, markClassCount);

            return new LookupType5Format1SubTable(markCoverage, ligatureCoverage, markArrayTable, ligatureArrayTable, lookupFlags);
        }

        public override bool TryUpdatePosition(
            FontMetrics fontMetrics,
            GPosTable table,
            GlyphPositioningCollection collection,
            LookupFeatures features,
            int index,
            int count)
        {
            // Mark-to-Ligature Attachment Positioning.
            // Implements: https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-5-mark-to-ligature-attachment-positioning-subtable
            ushort glyphId = collection[index].GlyphId;
            if (glyphId == 0)
            {
                return false;
            }

            int markIndex = this.markCoverage.CoverageIndexOf(glyphId);
            if (markIndex < 0 || markIndex >= this.markArrayTable.MarkRecords.Length)
            {
                return false;
            }

            // Search backward in the run for a base glyph.
            int baseGlyphIndex = AdvancedTypographicUtils.FindMarkBaseIndex(fontMetrics, collection, features, index, null);
            if (baseGlyphIndex < 0)
            {
                return false;
            }

            ushort baseGlyphId = collection[baseGlyphIndex].GlyphId;
            int ligatureIndex = this.ligatureCoverage.CoverageIndexOf(baseGlyphId);
            if (ligatureIndex < 0 || ligatureIndex >= this.ligatureArrayTable.LigatureAttachTables.Length)
            {
                return false;
            }

            // As in HarfBuzz, a mark of the ligature goes on its own component, and any other mark on the last. The font
            // gives the number of components, whatever number of characters the ligature holds. A ligature without
            // components means the subtable does not apply.
            ComponentRecord[] components = this.ligatureArrayTable.LigatureAttachTables[ligatureIndex].ComponentRecords;
            if (components.Length == 0)
            {
                return false;
            }

            GlyphShapingData markGlyph = collection[index];
            GlyphShapingData ligGlyph = collection[baseGlyphIndex];
            int compIndex = ligGlyph.LigatureId > 0 && ligGlyph.LigatureId == markGlyph.LigatureId && markGlyph.LigatureComponent > 0
                ? Math.Min(markGlyph.LigatureComponent, components.Length) - 1
                : components.Length - 1;

            MarkRecord markRecord = this.markArrayTable.MarkRecords[markIndex];
            AnchorTable[] ligatureAnchors = components[compIndex].LigatureAnchorTables;
            if (markRecord.MarkClass >= (uint)ligatureAnchors.Length)
            {
                return false;
            }

            AnchorTable? baseAnchor = ligatureAnchors[markRecord.MarkClass];

            return AdvancedTypographicUtils.ApplyAnchor(fontMetrics, collection, index, baseAnchor, markRecord, baseGlyphIndex);
        }
    }
}
