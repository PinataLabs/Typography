// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The FeatureVariations table of GSUB or GPOS. Each record has a set of conditions on the normalised coordinates of an
/// instance, and the feature tables that stand in for those of the FeatureList when the conditions hold. The first record
/// whose conditions all hold decides. This is how a variable font swaps the lookups of <c>rvrn</c> and of other features.
/// <see href="https://learn.microsoft.com/typography/opentype/spec/chapter2#featurevariations-table"/>
/// </summary>
/// <remarks>
/// The reader checks every offset against the table, and it spends at most <see cref="MaxWork"/> reads on records,
/// conditions and lookup indices, so a table that shares or overlaps its subtables cannot make it do more. A table that
/// fails any check is malformed, and <see cref="TryRead"/> gives <see langword="null"/> for it, as if the font had none.
/// A condition of a format that the library does not know never holds, and it is no failure.
/// </remarks>
internal sealed class FeatureVariationsTable
{
    private const int MaxWork = 1 << 20;

    private readonly Record[] records;

    private FeatureVariationsTable(Record[] records) => this.records = records;

    /// <summary>
    /// Reads the FeatureVariations table that a GSUB or GPOS header points to.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <param name="tableName">The tag of the GSUB or GPOS table.</param>
    /// <param name="offset">The offset of the table from the beginning of the GSUB or GPOS table, or 0 for none.</param>
    /// <param name="featureCount">The number of features in the FeatureList of the table.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none or it is malformed.</returns>
    public static FeatureVariationsTable? Load(FontReader reader, string tableName, uint offset, int featureCount)
    {
        if (offset == 0)
        {
            return null;
        }

        byte[]? data = reader.TryReadTableBytes(tableName);
        return data is null ? null : TryRead(data, offset, featureCount);
    }

    /// <summary>
    /// Reads a FeatureVariations table.
    /// </summary>
    /// <param name="table">The bytes of the GSUB or GPOS table.</param>
    /// <param name="offset">The offset of the table from the beginning of <paramref name="table"/>.</param>
    /// <param name="featureCount">The number of features in the FeatureList of the table.</param>
    /// <returns>The table, or <see langword="null"/> when it is malformed.</returns>
    public static FeatureVariationsTable? TryRead(ReadOnlySpan<byte> table, uint offset, int featureCount)
    {
        Reader reader = new(table, featureCount);
        return reader.TryReadRecords(offset, out Record[] records) ? new FeatureVariationsTable(records) : null;
    }

    /// <summary>
    /// Finds the first record whose conditions hold for an instance and applies its substitutions.
    /// </summary>
    /// <param name="featureList">The FeatureList of the table.</param>
    /// <param name="coordinates">The normalised coordinates of the instance as F2Dot14 values, in <c>fvar</c> order.</param>
    /// <param name="scalars">The scalars of the GDEF Item Variation Store for the instance.</param>
    /// <returns>The FeatureList with the feature tables of the record in place of its own, or <see langword="null"/> when no record holds or the one that does has no substitution.</returns>
    public FeatureListTable? Select(FeatureListTable featureList, ReadOnlySpan<short> coordinates, in ItemVariationScalars scalars)
    {
        foreach (Record record in this.records)
        {
            if (record.Matches(coordinates, scalars))
            {
                return record.Substitutions.Length == 0 ? null : featureList.WithLookups(record.Substitutions);
            }
        }

        return null;
    }

    private ref struct Reader
    {
        private const int HeaderSize = 8;
        private const int RecordSize = 8;
        private const int ConditionSetHeaderSize = 2;
        private const int SubstitutionHeaderSize = 6;
        private const int SubstitutionRecordSize = 6;
        private const int FeatureHeaderSize = 4;
        private const int MaxDepth = 16;

        private readonly ReadOnlySpan<byte> data;
        private readonly int featureCount;
        private long work;

        public Reader(ReadOnlySpan<byte> data, int featureCount)
        {
            this.data = data;
            this.featureCount = featureCount;
            this.work = 0;
        }

        public bool TryReadRecords(uint offset, out Record[] records)
        {
            records = [];
            if (!this.Has(offset, HeaderSize) || this.U16(offset) != 1)
            {
                return false;
            }

            long count = this.U32(offset + 4);
            if (!this.Has(offset + HeaderSize, count * RecordSize) || !this.Spend(count))
            {
                return false;
            }

            records = new Record[count];
            for (int i = 0; i < records.Length; i++)
            {
                long position = offset + HeaderSize + ((long)i * RecordSize);
                Condition[]? conditions = this.ReadConditionSet(offset, this.U32(position));
                (ushort, ushort[])[]? substitutions = this.ReadSubstitutions(offset, this.U32(position + 4));
                if (conditions is null || substitutions is null)
                {
                    return false;
                }

                records[i] = new Record(conditions, substitutions);
            }

            return true;
        }

        // A NULL offset is a set of no conditions, which every instance meets.
        private Condition[]? ReadConditionSet(long tableOffset, uint offset)
        {
            if (offset == 0)
            {
                return [];
            }

            long position = tableOffset + offset;
            if (!this.Has(position, ConditionSetHeaderSize))
            {
                return null;
            }

            int count = this.U16(position);
            if (!this.Has(position + ConditionSetHeaderSize, 4L * count) || !this.Spend(count))
            {
                return null;
            }

            Condition[] conditions = new Condition[count];
            for (int i = 0; i < count; i++)
            {
                Condition? condition = this.ReadCondition(position + this.U32(position + ConditionSetHeaderSize + (4L * i)), 0);
                if (condition is null)
                {
                    return null;
                }

                conditions[i] = condition;
            }

            return conditions;
        }

        private Condition? ReadCondition(long position, int depth)
        {
            if (depth > MaxDepth || !this.Has(position, 2) || !this.Spend(1))
            {
                return null;
            }

            int format = this.U16(position);
            switch (format)
            {
                case 1:
                    return this.Has(position, 8)
                        ? Condition.AxisRange(this.U16(position + 2), (short)this.U16(position + 4), (short)this.U16(position + 6))
                        : null;

                case 2:
                    if (!this.Has(position, 8))
                    {
                        return null;
                    }

                    uint index = this.U32(position + 4);
                    return Condition.Value((short)this.U16(position + 2), index == uint.MaxValue ? default : new VariationIndex((ushort)(index >> 16), (ushort)index));

                case 3 or 4:
                    // The offsets of the conditions are 24-bit, from the beginning of this table.
                    if (!this.Has(position, 3))
                    {
                        return null;
                    }

                    int count = this.data[(int)position + 2];
                    if (!this.Has(position + 3, 3L * count) || !this.Spend(count))
                    {
                        return null;
                    }

                    Condition[] children = new Condition[count];
                    for (int i = 0; i < count; i++)
                    {
                        Condition? child = this.ReadCondition(position + this.U24(position + 3 + (3L * i)), depth + 1);
                        if (child is null)
                        {
                            return null;
                        }

                        children[i] = child;
                    }

                    return format == 3 ? Condition.And(children) : Condition.Or(children);

                case 5:
                    Condition? negated = this.Has(position, 5) ? this.ReadCondition(position + this.U24(position + 2), depth + 1) : null;
                    return negated is null ? null : Condition.Not(negated);

                default:
                    return Condition.Unsupported();
            }
        }

        // A NULL offset is a record that makes no substitution.
        private (ushort FeatureIndex, ushort[] LookupListIndices)[]? ReadSubstitutions(long tableOffset, uint offset)
        {
            if (offset == 0)
            {
                return [];
            }

            long position = tableOffset + offset;
            if (!this.Has(position, SubstitutionHeaderSize) || this.U16(position) != 1)
            {
                return null;
            }

            int count = this.U16(position + 4);
            if (!this.Has(position + SubstitutionHeaderSize, (long)SubstitutionRecordSize * count) || !this.Spend(count))
            {
                return null;
            }

            List<(ushort, ushort[])> substitutions = new(count);
            for (int i = 0; i < count; i++)
            {
                long recordPosition = position + SubstitutionHeaderSize + ((long)SubstitutionRecordSize * i);
                ushort featureIndex = this.U16(recordPosition);

                // The feature list has no such feature, so there is nothing to replace.
                if (featureIndex >= this.featureCount)
                {
                    continue;
                }

                long featurePosition = position + this.U32(recordPosition + 2);
                if (!this.Has(featurePosition, FeatureHeaderSize))
                {
                    return null;
                }

                int lookupCount = this.U16(featurePosition + 2);
                if (!this.Has(featurePosition + FeatureHeaderSize, 2L * lookupCount) || !this.Spend(lookupCount))
                {
                    return null;
                }

                ushort[] lookups = new ushort[lookupCount];
                for (int j = 0; j < lookups.Length; j++)
                {
                    lookups[j] = this.U16(featurePosition + FeatureHeaderSize + (2L * j));
                }

                substitutions.Add((featureIndex, lookups));
            }

            return substitutions.ToArray();
        }

        private readonly bool Has(long position, long length) => position >= 0 && length >= 0 && position + length <= this.data.Length;

        private bool Spend(long amount)
        {
            this.work += (int)amount;
            return this.work <= MaxWork;
        }

        private readonly ushort U16(long position) => BinaryPrimitives.ReadUInt16BigEndian(this.data.Slice((int)position));

        private readonly uint U32(long position) => BinaryPrimitives.ReadUInt32BigEndian(this.data.Slice((int)position));

        private readonly int U24(long position) => (this.data[(int)position] << 16) | (this.data[(int)position + 1] << 8) | this.data[(int)position + 2];
    }

    private sealed class Record
    {
        private readonly Condition[] conditions;

        public Record(Condition[] conditions, (ushort FeatureIndex, ushort[] LookupListIndices)[] substitutions)
        {
            this.conditions = conditions;
            this.Substitutions = substitutions;
        }

        public (ushort FeatureIndex, ushort[] LookupListIndices)[] Substitutions { get; }

        public bool Matches(ReadOnlySpan<short> coordinates, in ItemVariationScalars scalars)
        {
            foreach (Condition condition in this.conditions)
            {
                if (!condition.Evaluate(coordinates, scalars))
                {
                    return false;
                }
            }

            return true;
        }
    }

    // A condition is a leaf that tests an axis range or a value that the GDEF store varies, or an AND, an OR or a NOT of
    // other conditions. A condition of an unknown format has a format of 0, and it never holds.
    private sealed class Condition
    {
        private readonly int format;
        private readonly ushort axisIndex;
        private readonly short minimum;
        private readonly short maximum;
        private readonly short defaultValue;
        private readonly VariationIndex index;
        private readonly Condition[] children;

        private Condition(
            int format,
            ushort axisIndex = 0,
            short minimum = 0,
            short maximum = 0,
            short defaultValue = 0,
            VariationIndex index = default,
            Condition[]? children = null)
        {
            this.format = format;
            this.axisIndex = axisIndex;
            this.minimum = minimum;
            this.maximum = maximum;
            this.defaultValue = defaultValue;
            this.index = index;
            this.children = children ?? [];
        }

        public static Condition Unsupported() => new(0);

        public static Condition AxisRange(ushort axisIndex, short minimum, short maximum)
            => new(1, axisIndex: axisIndex, minimum: minimum, maximum: maximum);

        public static Condition Value(short defaultValue, VariationIndex index) => new(2, defaultValue: defaultValue, index: index);

        public static Condition And(Condition[] children) => new(3, children: children);

        public static Condition Or(Condition[] children) => new(4, children: children);

        public static Condition Not(Condition child) => new(5, children: [child]);

        public bool Evaluate(ReadOnlySpan<short> coordinates, in ItemVariationScalars scalars)
        {
            switch (this.format)
            {
                case 1:
                    // An axis that the instance has no coordinate for is at its default.
                    short coordinate = this.axisIndex < coordinates.Length ? coordinates[this.axisIndex] : (short)0;
                    return this.minimum <= coordinate && coordinate <= this.maximum;

                case 2:
                    // HarfBuzz adds the delta to the default value as a float and truncates the sum.
                    float delta = this.index.IsNone ? 0 : scalars.GetDelta(this.index.Outer, this.index.Inner);
                    return (int)(this.defaultValue + delta) > 0;

                case 3:
                    foreach (Condition child in this.children)
                    {
                        if (!child.Evaluate(coordinates, scalars))
                        {
                            return false;
                        }
                    }

                    return true;

                case 4:
                    foreach (Condition child in this.children)
                    {
                        if (child.Evaluate(coordinates, scalars))
                        {
                            return true;
                        }
                    }

                    return false;

                case 5:
                    return !this.children[0].Evaluate(coordinates, scalars);

                default:
                    return false;
            }
        }
    }
}
