// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Text;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// A CID-keyed CFF font whose Top DICT names a Private DICT that ends past the table.
/// A CID-keyed font keeps its Private DICTs in the Font DICTs of its FDArray, so a reader must ignore this one.
/// </summary>
internal static class FontWithBadTopPrivate
{
    // The FontBBox of the Top DICT, five operands and the operator.
    private static readonly byte[] FontBBox = [0xAD, 0xFB, 0x0C, 0xFA, 0x5A, 0xFA, 0x04, 0x05];

    public static byte[] Create()
    {
        byte[] data = File.ReadAllBytes(TestFonts.FDArrayTest257File);
        int tableOffset = 0;
        int tableLength = 0;
        int tableCount = (data[4] << 8) | data[5];
        for (int i = 0; i < tableCount; i++)
        {
            int entry = 12 + (i * 16);
            if (Encoding.ASCII.GetString(data, entry, 4) == "CFF ")
            {
                tableOffset = (data[entry + 8] << 24) | (data[entry + 9] << 16) | (data[entry + 10] << 8) | data[entry + 11];
                tableLength = (data[entry + 12] << 24) | (data[entry + 13] << 16) | (data[entry + 14] << 8) | data[entry + 15];
            }
        }

        int at = data.AsSpan(tableOffset, tableLength).IndexOf(FontBBox);
        if (at < 0)
        {
            throw new InvalidOperationException("The Top DICT of FDArrayTest257.otf has changed.");
        }

        // Private: size 1 at the offset that is the end of the table, then a FontBBox with two operands to fill the gap.
        byte[] privateOperator =
        [
            0x8C,
            0x1C, (byte)(tableLength >> 8), (byte)tableLength,
            0x12,
            0x8B, 0x8B, 0x05,
        ];
        privateOperator.CopyTo(data, tableOffset + at);
        return data;
    }
}
