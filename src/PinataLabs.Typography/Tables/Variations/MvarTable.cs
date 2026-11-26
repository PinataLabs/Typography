// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'MVAR' table: how the global metrics of a variable font change along its axes, such as the ascender, the line
/// gap and the position of the underline. A value record names a metric by a tag and gives the delta of the metric in
/// an Item Variation Store.
/// https://learn.microsoft.com/typography/opentype/spec/mvar
/// </summary>
/// <remarks>
/// A table that is truncated, has another major version, has value records outside it, or has a store that is
/// malformed is absent. A table without value records is absent too, as it varies nothing.
/// </remarks>
internal sealed class MvarTable : Table
{
    internal const string TableName = "MVAR";

    // uint16 majorVersion, uint16 minorVersion, uint16 reserved, uint16 valueRecordSize, uint16 valueRecordCount,
    // Offset16 itemVariationStoreOffset.
    private const int HeaderSize = 12;
    private const int MajorVersion = 1;

    // Tag valueTag, uint16 deltaSetOuterIndex, uint16 deltaSetInnerIndex. A table can have larger records.
    private const int MinimumRecordSize = 8;

    private readonly ItemVariationStore store;
    private readonly (Tag Tag, int Outer, int Inner)[] records;

    private MvarTable(in ItemVariationStore store, (Tag Tag, int Outer, int Inner)[] records)
    {
        this.store = store;
        this.records = records;
    }

    /// <summary>
    /// Loads the table.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none or it is malformed.</returns>
    public static MvarTable? Load(FontReader reader)
    {
        byte[]? data = reader.TryReadTableBytes(TableName);
        return data is null ? null : Parse(data);
    }

    /// <summary>
    /// Reads a table.
    /// </summary>
    /// <param name="data">The bytes of the table.</param>
    /// <returns>The table, or <see langword="null"/> when it is malformed or has no value records.</returns>
    public static MvarTable? Parse(ReadOnlyMemory<byte> data)
    {
        ReadOnlySpan<byte> span = data.Span;
        if (span.Length < HeaderSize || BinaryPrimitives.ReadUInt16BigEndian(span) != MajorVersion)
        {
            return null;
        }

        int recordSize = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(6));
        int recordCount = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(8));
        int storeOffset = BinaryPrimitives.ReadUInt16BigEndian(span.Slice(10));
        if (recordCount == 0
            || recordSize < MinimumRecordSize
            || storeOffset == 0
            || HeaderSize + ((long)recordCount * recordSize) > span.Length
            || !ItemVariationStore.TryRead(data, storeOffset, out ItemVariationStore store))
        {
            return null;
        }

        (Tag Tag, int Outer, int Inner)[] records = new (Tag, int, int)[recordCount];
        for (int i = 0; i < records.Length; i++)
        {
            ReadOnlySpan<byte> record = span.Slice(HeaderSize + (i * recordSize), MinimumRecordSize);
            records[i] = (
                new Tag(BinaryPrimitives.ReadUInt32BigEndian(record)),
                BinaryPrimitives.ReadUInt16BigEndian(record.Slice(4)),
                BinaryPrimitives.ReadUInt16BigEndian(record.Slice(6)));
        }

        return new MvarTable(store, records);
    }

    /// <summary>
    /// Computes the region scalars of the table for the coordinates of an instance.
    /// </summary>
    /// <param name="coordinates">The normalised F2Dot14 coordinates of the instance, in <c>fvar</c> order.</param>
    /// <returns>What gives the deltas of the metrics at these coordinates.</returns>
    public MvarVariation CreateVariation(ReadOnlySpan<short> coordinates)
        => new(this, this.store.CreateScalars(coordinates));

    /// <summary>
    /// Gets the delta of a metric.
    /// </summary>
    /// <param name="scalars">The region scalars of the instance.</param>
    /// <param name="tag">The tag of the metric.</param>
    /// <returns>The delta, which is 0 when the table has no record for the tag.</returns>
    internal float GetDelta(in ItemVariationScalars scalars, Tag tag)
    {
        foreach ((Tag recordTag, int outer, int inner) in this.records)
        {
            if (recordTag == tag)
            {
                return scalars.GetDelta(outer, inner);
            }
        }

        return 0;
    }
}
