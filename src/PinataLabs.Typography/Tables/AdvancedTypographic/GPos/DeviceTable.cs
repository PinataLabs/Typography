// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GPos;

/// <summary>
/// Reads the device tables that value records and anchor tables point to. A Device table holds pixel deltas for a range
/// of sizes of a font that does not vary, and the library ignores it. A VariationIndex table is a Device table whose
/// delta format is 0x8000, and it addresses a delta in the Item Variation Store of the GDEF table. A device table that does
/// not lie within the GPOS table has no index, though it may lie within the font.
/// <see href="https://learn.microsoft.com/typography/opentype/spec/chapter2#device-and-variationindex-tables"/>
/// </summary>
internal static class DeviceTable
{
    private const ushort VariationIndexDeltaFormat = 0x8000;
    private const int Size = 6;

    /// <summary>
    /// Reads the device table that an offset points to, and leaves the reader where it was.
    /// </summary>
    /// <param name="reader">The big endian binary reader.</param>
    /// <param name="parentOffset">The position of the table that holds the offset, from the beginning of the GPOS table.</param>
    /// <param name="offset">The offset of the device table from <paramref name="parentOffset"/>, or 0 for none.</param>
    /// <returns>The index of a VariationIndex table, or none when the offset is 0, the table is a Device table, or the table does not fit in the GPOS table.</returns>
    public static VariationIndex LoadVariationIndex(BigEndianBinaryReader reader, long parentOffset, ushort offset)
    {
        if (offset == 0)
        {
            return default;
        }

        Stream stream = reader.BaseStream;
        long resume = stream.Position;
        reader.Seek(parentOffset + offset, SeekOrigin.Begin);
        VariationIndex index = default;
        if (reader.EndOfTable - stream.Position >= Size)
        {
            ushort outer = reader.ReadUInt16();
            ushort inner = reader.ReadUInt16();
            if (reader.ReadUInt16() == VariationIndexDeltaFormat)
            {
                index = new VariationIndex(outer, inner);
            }
        }

        stream.Position = resume;
        return index;
    }
}
