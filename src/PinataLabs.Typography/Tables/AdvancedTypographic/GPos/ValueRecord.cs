// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GPos;

/// <summary>
/// GPOS subtables use ValueRecords to describe all the variables and values used to adjust the position
/// of a glyph or set of glyphs. A ValueRecord may define any combination of X and Y values (in design units)
/// to add to (positive values) or subtract from (negative values) the placement and advance values provided in the font.
/// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#value-record"/>
/// </summary>
internal readonly struct ValueRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValueRecord"/> struct.
    /// </summary>
    /// <param name="reader">The big endian binary reader.</param>
    /// <param name="valueFormat">Defines the types of data in the ValueRecord.</param>
    /// <param name="parentOffset">
    /// The position from the beginning of the GPOS table of the immediate parent table, which the offsets
    /// of the device tables count from.
    /// </param>
    public ValueRecord(BigEndianBinaryReader reader, ValueFormat valueFormat, long parentOffset)
    {
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | Type     | Name             | Description                                                                          |
        // +==========+==================+======================================================================================+
        // | int16    | xPlacement       | Horizontal adjustment for placement, in                                              |
        // |          |                  | design units.                                                                        |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | int16    | yPlacement       | Vertical adjustment for placement, in design                                         |
        // |          |                  | units.                                                                               |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | int16    | xAdvance         | Horizontal adjustment for advance, in design                                         |
        // |          |                  | units — only used for horizontal layout.                                             |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | int16    | yAdvance         | Vertical adjustment for advance, in design                                           |
        // |          |                  | units — only used for vertical layout.                                               |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | Offset16 | xPlaDeviceOffset | Offset to Device table (non-variable font) /                                         |
        // |          |                  | VariationIndex table (variable font) for                                             |
        // |          |                  | horizontal placement, from beginning of the                                          |
        // |          |                  | immediate parent table (SinglePos or                                                 |
        // |          |                  | PairPosFormat2 lookup subtable, PairSet table                                        |
        // |          |                  | within a PairPosFormat1 lookup subtable) — may be NULL.                              |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | Offset16 | yPlaDeviceOffset | Offset to Device table (non-variable font) /                                         |
        // |          |                  | VariationIndex table (variable font) for vertical                                    |
        // |          |                  | placement, from beginning of the immediate parent table (SinglePos or PairPosFormat2 |
        // |          |                  | lookup subtable, PairSet table within a                                              |
        // |          |                  | PairPosFormat1 lookup subtable) — may be NULL.                                       |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | Offset16 | xAdvDeviceOffset | Offset to Device table (non-variable font) /                                         |
        // |          |                  | VariationIndex table (variable font) for                                             |
        // |          |                  | horizontal advance, from beginning of the                                            |
        // |          |                  | immediate parent table (SinglePos or                                                 |
        // |          |                  | PairPosFormat2 lookup subtable, PairSet table                                        |
        // |          |                  | within a PairPosFormat1 lookup subtable) — may be NULL.                              |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        // | Offset16 | yAdvDeviceOffset | Offset to Device table (non-variable font) /                                         |
        // |          |                  | VariationIndex table (variable font) for vertical                                    |
        // |          |                  | advance, from beginning of the immediate                                             |
        // |          |                  | parent table (SinglePos or PairPosFormat2                                            |
        // |          |                  | lookup subtable, PairSet table within a                                              |
        // |          |                  | PairPosFormat1 lookup subtable) — may be NULL.                                       |
        // +----------+------------------+--------------------------------------------------------------------------------------+
        this.XPlacement = (valueFormat & ValueFormat.XPlacement) != 0 ? reader.ReadInt16() : (short)0;
        this.YPlacement = (valueFormat & ValueFormat.YPlacement) != 0 ? reader.ReadInt16() : (short)0;
        this.XAdvance = (valueFormat & ValueFormat.XAdvance) != 0 ? reader.ReadInt16() : (short)0;
        this.YAdvance = (valueFormat & ValueFormat.YAdvance) != 0 ? reader.ReadInt16() : (short)0;
        ushort xPlacementDeviceOffset = (valueFormat & ValueFormat.XPlacementDevice) != 0 ? reader.ReadOffset16() : (ushort)0;
        ushort yPlacementDeviceOffset = (valueFormat & ValueFormat.YPlacementDevice) != 0 ? reader.ReadOffset16() : (ushort)0;
        ushort xAdvanceDeviceOffset = (valueFormat & ValueFormat.XAdvanceDevice) != 0 ? reader.ReadOffset16() : (ushort)0;
        ushort yAdvanceDeviceOffset = (valueFormat & ValueFormat.YAdvanceDevice) != 0 ? reader.ReadOffset16() : (ushort)0;

        // Only a VariationIndex table has a use. A Device table holds deltas for sizes of a font that does not vary.
        this.XPlacementVariation = DeviceTable.LoadVariationIndex(reader, parentOffset, xPlacementDeviceOffset);
        this.YPlacementVariation = DeviceTable.LoadVariationIndex(reader, parentOffset, yPlacementDeviceOffset);
        this.XAdvanceVariation = DeviceTable.LoadVariationIndex(reader, parentOffset, xAdvanceDeviceOffset);
        this.YAdvanceVariation = DeviceTable.LoadVariationIndex(reader, parentOffset, yAdvanceDeviceOffset);
    }

    public short XPlacement { get; }

    public short YPlacement { get; }

    public short XAdvance { get; }

    public short YAdvance { get; }

    /// <summary>
    /// Gets the index of the delta of the horizontal placement, in design units.
    /// </summary>
    public VariationIndex XPlacementVariation { get; }

    /// <summary>
    /// Gets the index of the delta of the vertical placement, in design units.
    /// </summary>
    public VariationIndex YPlacementVariation { get; }

    /// <summary>
    /// Gets the index of the delta of the horizontal advance, in design units.
    /// </summary>
    public VariationIndex XAdvanceVariation { get; }

    /// <summary>
    /// Gets the index of the delta of the vertical advance, in design units.
    /// </summary>
    public VariationIndex YAdvanceVariation { get; }
}
