// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GPos;

/// <summary>
/// Lookup Type 7: Contextual Positioning Subtables.
/// A Contextual Positioning subtable describes glyph positioning in context so a text-processing client can adjust the position
/// of one or more glyphs within a certain pattern of glyphs.
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-7-contextual-positioning-subtables"/>
/// </summary>
internal static class LookupType7SubTable
{
    public static LookupSubTable Load(BigEndianBinaryReader reader, long offset, LookupFlags lookupFlags)
    {
        reader.Seek(offset, SeekOrigin.Begin);
        ushort subTableFormat = reader.ReadUInt16();

        return subTableFormat switch
        {
            1 => LookupType7Format1SubTable.Load(reader, offset, lookupFlags),
            2 => LookupType7Format2SubTable.Load(reader, offset, lookupFlags),
            3 => LookupType7Format3SubTable.Load(reader, offset, lookupFlags),
            _ => new NotImplementedSubTable(),
        };
    }

    internal sealed class LookupType7Format1SubTable : LookupSubTable
    {
        private readonly CoverageTable coverageTable;
        private readonly SequenceRuleSetTable[] seqRuleSetTables;

        public LookupType7Format1SubTable(CoverageTable coverageTable, SequenceRuleSetTable[] seqRuleSetTables, LookupFlags lookupFlags)
            : base(lookupFlags)
        {
            this.seqRuleSetTables = seqRuleSetTables;
            this.coverageTable = coverageTable;
        }

        public static LookupType7Format1SubTable Load(BigEndianBinaryReader reader, long offset, LookupFlags lookupFlags)
        {
            SequenceRuleSetTable[] seqRuleSets = TableLoadingUtils.LoadSequenceContextFormat1(reader, offset, out CoverageTable coverageTable);

            return new LookupType7Format1SubTable(coverageTable, seqRuleSets, lookupFlags);
        }

        public override bool TryUpdatePosition(
            FontMetrics fontMetrics,
            GPosTable table,
            GlyphPositioningCollection collection,
            LookupFeatures features,
            int index,
            int count)
        {
            Span<int> positions = stackalloc int[AdvancedTypographicUtils.MaxContextLength];
            SequenceRuleTable? rule = AdvancedTypographicUtils.FindSequenceRule(fontMetrics, collection, this.LookupFlags, features, this.coverageTable, this.seqRuleSetTables, index, count, positions);
            if (rule is null)
            {
                return false;
            }

            // It's a match. The subtable has applied whether or not the nested lookups change anything.
            AdvancedTypographicUtils.ApplyLookupList(fontMetrics, table, features, rule.SequenceLookupRecords, collection, positions, rule.InputSequence.Length + 1);
            return true;
        }
    }

    internal sealed class LookupType7Format2SubTable : LookupSubTable
    {
        private readonly CoverageTable coverageTable;
        private readonly ClassDefinitionTable classDefinitionTable;
        private readonly ClassSequenceRuleSetTable[] sequenceRuleSetTables;

        public LookupType7Format2SubTable(
            CoverageTable coverageTable,
            ClassDefinitionTable classDefinitionTable,
            ClassSequenceRuleSetTable[] sequenceRuleSetTables,
            LookupFlags lookupFlags)
            : base(lookupFlags)
        {
            this.coverageTable = coverageTable;
            this.classDefinitionTable = classDefinitionTable;
            this.sequenceRuleSetTables = sequenceRuleSetTables;
        }

        public static LookupType7Format2SubTable Load(BigEndianBinaryReader reader, long offset, LookupFlags lookupFlags)
        {
            CoverageTable coverageTable = TableLoadingUtils.LoadSequenceContextFormat2(reader, offset, out ClassDefinitionTable classDefTable, out ClassSequenceRuleSetTable[] classSeqRuleSets);

            return new LookupType7Format2SubTable(coverageTable, classDefTable, classSeqRuleSets, lookupFlags);
        }

        public override bool TryUpdatePosition(
            FontMetrics fontMetrics,
            GPosTable table,
            GlyphPositioningCollection collection,
            LookupFeatures features,
            int index,
            int count)
        {
            Span<int> positions = stackalloc int[AdvancedTypographicUtils.MaxContextLength];
            ClassSequenceRuleTable? rule = AdvancedTypographicUtils.FindClassSequenceRule(fontMetrics, collection, this.LookupFlags, features, this.coverageTable, this.classDefinitionTable, this.sequenceRuleSetTables, index, count, positions);
            if (rule is null)
            {
                return false;
            }

            AdvancedTypographicUtils.ApplyLookupList(fontMetrics, table, features, rule.SequenceLookupRecords, collection, positions, rule.InputSequence.Length + 1);
            return true;
        }
    }

    internal sealed class LookupType7Format3SubTable : LookupSubTable
    {
        private readonly CoverageTable[] coverageTables;
        private readonly SequenceLookupRecord[] sequenceLookupRecords;

        public LookupType7Format3SubTable(CoverageTable[] coverageTables, SequenceLookupRecord[] sequenceLookupRecords, LookupFlags lookupFlags)
            : base(lookupFlags)
        {
            this.coverageTables = coverageTables;
            this.sequenceLookupRecords = sequenceLookupRecords;
        }

        public static LookupType7Format3SubTable Load(BigEndianBinaryReader reader, long offset, LookupFlags lookupFlags)
        {
            SequenceLookupRecord[] seqLookupRecords = TableLoadingUtils.LoadSequenceContextFormat3(reader, offset, out CoverageTable[] coverageTables);

            return new LookupType7Format3SubTable(coverageTables, seqLookupRecords, lookupFlags);
        }

        public override bool TryUpdatePosition(
            FontMetrics fontMetrics,
            GPosTable table,
            GlyphPositioningCollection collection,
            LookupFeatures features,
            int index,
            int count)
        {
            Span<int> positions = stackalloc int[AdvancedTypographicUtils.MaxContextLength];
            if (!AdvancedTypographicUtils.MatchCoverageContext(fontMetrics, collection, this.LookupFlags, features, this.coverageTables, index, positions))
            {
                return false;
            }

            AdvancedTypographicUtils.ApplyLookupList(fontMetrics, table, features, this.sequenceLookupRecords, collection, positions, this.coverageTables.Length);
            return true;
        }
    }
}
