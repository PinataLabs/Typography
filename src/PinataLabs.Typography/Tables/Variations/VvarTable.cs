// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The 'VVAR' table: how the advance heights and top side bearings of a variable font change along its axes.
/// https://learn.microsoft.com/typography/opentype/spec/vvar
/// </summary>
internal sealed class VvarTable : MetricsVariationTable
{
    internal const string TableName = "VVAR";

    // uint16 majorVersion, uint16 minorVersion, then Offset32 for the store and for each of the maps of the
    // advance height, top side bearing, bottom side bearing and vertical origin.
    private const int HeaderSize = 24;

    private VvarTable(in ItemVariationStore store, DeltaSetIndexMap? advanceMap, DeltaSetIndexMap? topSideBearingMap)
        : base(store, advanceMap, topSideBearingMap)
    {
    }

    /// <summary>
    /// Loads the table.
    /// </summary>
    /// <param name="reader">The font reader.</param>
    /// <returns>The table, or <see langword="null"/> when the font has none or it is malformed.</returns>
    public static VvarTable? Load(FontReader reader)
    {
        byte[]? data = reader.TryReadTableBytes(TableName);
        return data is null ? null : Parse(data);
    }

    /// <summary>
    /// Reads a table.
    /// </summary>
    /// <param name="data">The bytes of the table.</param>
    /// <returns>The table, or <see langword="null"/> when it is malformed.</returns>
    public static VvarTable? Parse(ReadOnlyMemory<byte> data)
        => TryRead(data, HeaderSize, out ItemVariationStore store, out DeltaSetIndexMap? advanceMap, out DeltaSetIndexMap? topSideBearingMap)
            ? new VvarTable(store, advanceMap, topSideBearingMap)
            : null;
}
