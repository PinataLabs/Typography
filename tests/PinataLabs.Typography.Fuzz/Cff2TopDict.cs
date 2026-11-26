// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Finds the variation store in a CFF2 table, for the seed corpus and the tests. The library reads the
/// Top DICT with its own parser, which has more to do than this needs.
/// </summary>
internal static class Cff2TopDict
{
    private const int VariationStoreOperator = 24;

    /// <summary>
    /// Gets the position of the variation store, which is the operand of the <c>vstore</c> operator of the Top DICT.
    /// </summary>
    /// <param name="table">The CFF2 table.</param>
    /// <param name="offset">The position of the length field of the variation store in <paramref name="table"/>.</param>
    /// <returns><see langword="false"/> if the table has no Top DICT with a <c>vstore</c> operator. Operators that this does not know are skipped.</returns>
    public static bool TryGetVariationStoreOffset(ReadOnlySpan<byte> table, out int offset)
    {
        offset = 0;
        if (table.Length < 5 || table[0] != 2)
        {
            return false;
        }

        // The header is the major and minor version, the size of the header and the length of the Top DICT.
        int position = table[2];
        int end = Math.Min(table.Length, position + BinaryPrimitives.ReadUInt16BigEndian(table.Slice(3)));
        int operand = 0;
        while (position < end)
        {
            int b = table[position++];
            int size;
            if (b <= VariationStoreOperator)
            {
                if (b == VariationStoreOperator)
                {
                    offset = operand;
                    return true;
                }

                // A two-byte operator starts with 12.
                position += b == 12 ? 1 : 0;
                continue;
            }

            switch (b)
            {
                case 28 when position + 2 <= end:
                    operand = BinaryPrimitives.ReadInt16BigEndian(table.Slice(position));
                    size = 2;
                    break;

                case 29 when position + 4 <= end:
                    operand = BinaryPrimitives.ReadInt32BigEndian(table.Slice(position));
                    size = 4;
                    break;

                case 30:
                    // A real number runs to the nibble 0xF.
                    size = 0;
                    while (position + size < end && (table[position + size] & 0x0F) != 0x0F && (table[position + size] >> 4) != 0x0F)
                    {
                        size++;
                    }

                    size++;
                    operand = 0;
                    break;

                case >= 32 and <= 246:
                    operand = b - 139;
                    size = 0;
                    break;

                case >= 247 and <= 254 when position < end:
                    operand = b <= 250
                        ? ((b - 247) * 256) + table[position] + 108
                        : (-(b - 251) * 256) - table[position] - 108;
                    size = 1;
                    break;

                default:
                    // A reserved byte is an unknown operator, and it takes no operand.
                    continue;
            }

            position += size;
        }

        return false;
    }
}
