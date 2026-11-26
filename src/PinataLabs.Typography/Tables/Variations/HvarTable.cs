// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'HVAR' table: how the advance widths and left side bearings of a variable font change along its axes.
/// https://learn.microsoft.com/typography/opentype/spec/hvar
/// </summary>
internal sealed class HvarTable : MetricsVariationTable
{
    internal const string TableName = "HVAR";

    // uint16 majorVersion, uint16 minorVersion, then Offset32 for the store and for each of the maps of the
    // advance width, left side bearing and right side bearing.
    private const int HeaderSize = 20;

    private HvarTable(in ItemVariationStore store, DeltaSetIndexMap? advanceMap, DeltaSetIndexMap? leftSideBearingMap)
        : base(store, advanceMap, leftSideBearingMap)
    {
    }

    /// <summary>
    /// Loads the table.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none or it is malformed.</returns>
    public static HvarTable? Load(FontReader reader)
    {
        byte[]? data = reader.TryReadTableBytes(TableName);
        return data is null ? null : Parse(data);
    }

    /// <summary>
    /// Reads a table.
    /// </summary>
    /// <param name="data">The bytes of the table.</param>
    /// <returns>The table, or <see langword="null"/> when it is malformed.</returns>
    public static HvarTable? Parse(ReadOnlyMemory<byte> data)
        => TryRead(data, HeaderSize, out ItemVariationStore store, out DeltaSetIndexMap? advanceMap, out DeltaSetIndexMap? leftSideBearingMap)
            ? new HvarTable(store, advanceMap, leftSideBearingMap)
            : null;
}
