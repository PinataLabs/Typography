// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General;

namespace PinataLabs.Typography.Tests.Tables.General;

public class OS2TableTests
{
    [Test]
    public async Task ShouldReturnNullWhenTableCouldNotBeFound()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteTrueTypeFileHeader();

        using (System.IO.MemoryStream stream = writer.GetStream())
        {
            await Assert.That(OS2Table.Load(new FontReader(stream))).IsNull();
        }
    }

    [Test]
    [Arguments(78, 0, 0)]
    [Arguments(86, 0, 0)]
    [Arguments(96, 480, 690)]
    public async Task FieldsThatTheTableLengthDoesNotCoverAreNotRead(int length, int expectedXHeight, int expectedCapHeight)
    {
        // The bytes after the table are those of the next table, and they are not 0.
        OS2Table table = LoadVersion4(length);

        await Assert.That((int)table.XHeight).IsEqualTo(expectedXHeight);
        await Assert.That((int)table.CapHeight).IsEqualTo(expectedCapHeight);
    }

    [Test]
    public async Task ATruncatedTableKeepsTheFieldsItCovers()
    {
        OS2Table table = LoadVersion4(86);

        await Assert.That((int)table.WinAscent).IsEqualTo(800);
        await Assert.That((int)table.TypoAscender).IsEqualTo(750);
    }

    private static OS2Table LoadVersion4(int tableLength)
    {
        byte[] bytes = new byte[96 + 32];
        bytes.AsSpan(96).Fill(0x7F);
        BinaryPrimitives.WriteUInt16BigEndian(bytes, 4); // version
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(68), 750); // sTypoAscender
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(74), 800); // usWinAscent
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(86), 480); // sxHeight
        BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(88), 690); // sCapHeight

        // A table shorter than 96 bytes is followed by the bytes of the next table.
        for (int i = tableLength; i < 96; i++)
        {
            bytes[i] = 0x7F;
        }

        using MemoryStream stream = new(bytes);
        using BigEndianBinaryReader reader = new(stream, true, tableLength);
        return OS2Table.Load(reader);
    }
}
