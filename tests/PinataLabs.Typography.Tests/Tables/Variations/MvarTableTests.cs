// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using PinataLabs.Typography.Fuzz;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Tests for how the MVAR table reads its value records and applies their deltas, on tables that the test writes.
/// The fonts are checked against fontTools in GlobalMetricsReferenceTests.
/// </summary>
public class MvarTableTests
{
    private const short Maximum = 16384;
    private const short Half = 8192;
    private const int HeaderSize = 12;

    // One axis, and two regions: the peak at 1, and the peak at -1. The rows give the delta of each region.
    // At the coordinate 1 a row is its first delta, and at 0.5 half its first.
    private static readonly short[][] Regions = [[0, Maximum, Maximum], [-Maximum, -Maximum, 0]];
    private static readonly int[][] Rows = [[10, -20], [30, 0], [-5, 5], [7, 7]];

    private static readonly byte[] Store = VariationBytes.Store(1, Regions, new VariationBytes.ItemData([0, 1], 1, Rows));

    [Test]
    public async Task MvarAddsTheDeltaOfItsTagToAMetric()
    {
        MvarVariation variation = Read(Mvar(("hasc", 0, 0), ("hdsc", 0, 2), ("undo", 0, 3))).CreateVariation([Maximum]);

        await Assert.That(variation.Apply(MvarVariation.HorizontalAscender, 800)).IsEqualTo((short)810);
        await Assert.That(variation.Apply(MvarVariation.HorizontalDescender, -250)).IsEqualTo((short)-255);
        await Assert.That(variation.Apply(MvarVariation.UnderlinePosition, -100)).IsEqualTo((short)-93);

        // A tag that the table lacks changes nothing.
        await Assert.That(variation.Apply(MvarVariation.HorizontalLineGap, 50)).IsEqualTo((short)50);
    }

    [Test]
    public async Task MvarRoundsOnceAndStaysInTheRangeOfAShort()
    {
        MvarVariation half = Read(Mvar(("hasc", 0, 2), ("hdsc", 0, 3))).CreateVariation([Half]);
        MvarVariation maximum = Read(Mvar(("hasc", 0, 0))).CreateVariation([Maximum]);

        // -2.5 and 3.5 added to -250 and 800 give -252.5 and 803.5, which round up.
        await Assert.That(half.Apply(MvarVariation.HorizontalAscender, -250)).IsEqualTo((short)-252);
        await Assert.That(half.Apply(MvarVariation.HorizontalDescender, 800)).IsEqualTo((short)804);
        await Assert.That(maximum.Apply(MvarVariation.HorizontalAscender, short.MaxValue)).IsEqualTo(short.MaxValue);
    }

    [Test]
    public async Task MvarSkipsTheBytesOfALargerValueRecord()
    {
        byte[] table = Mvar(12, ("hasc", 0, 1), ("hdsc", 0, 0));
        MvarVariation variation = Read(table).CreateVariation([Maximum]);

        await Assert.That(variation.Apply(MvarVariation.HorizontalAscender, 800)).IsEqualTo((short)830);
        await Assert.That(variation.Apply(MvarVariation.HorizontalDescender, -250)).IsEqualTo((short)-240);
    }

    [Test]
    public async Task ADefaultMvarChangesNothing()
    {
        MvarVariation variation = default;

        await Assert.That(variation.Apply(MvarVariation.HorizontalAscender, 800)).IsEqualTo((short)800);
    }

    [Test]
    [Arguments("a header that is cut short")]
    [Arguments("the major version 2")]
    [Arguments("a value record that is too small")]
    [Arguments("more value records than the table holds")]
    [Arguments("no value records")]
    [Arguments("no store")]
    [Arguments("a store past the end")]
    [Arguments("a store that is malformed")]
    public async Task AMalformedMvarIsAbsent(string defect)
    {
        byte[] table = Mvar(("hasc", 0, 0), ("hdsc", 0, 1));
        int storeOffset = HeaderSize + 16;
        byte[] changed = defect switch
        {
            "a header that is cut short" => table[..(HeaderSize - 1)],
            "the major version 2" => PatchUInt16(table, 0, 2),
            "a value record that is too small" => PatchUInt16(table, 6, 4),
            "more value records than the table holds" => PatchUInt16(table, 8, 5000),
            "no value records" => PatchUInt16(table, 8, 0),
            "no store" => PatchUInt16(table, 10, 0),
            "a store past the end" => PatchUInt16(table, 10, table.Length + 1),
            _ => PatchByte(table, storeOffset + 1, 7),
        };

        await Assert.That(MvarTable.Parse(changed)).IsNull();
    }

    private static MvarTable Read(byte[] table) => MvarTable.Parse(table) ?? throw new InvalidOperationException("The table is malformed.");

    // The header, the value records, then the store.
    private static byte[] Mvar(params (string Tag, int Outer, int Inner)[] records)
        => Mvar(8, records);

    private static byte[] Mvar(int recordSize, params (string Tag, int Outer, int Inner)[] records)
    {
        List<byte> bytes = [0, 1, 0, 0, 0, 0];
        UInt16(bytes, (ushort)recordSize);
        UInt16(bytes, (ushort)records.Length);
        UInt16(bytes, (ushort)(HeaderSize + (recordSize * records.Length)));
        foreach ((string tag, int outer, int inner) in records)
        {
            bytes.AddRange(Encoding.ASCII.GetBytes(tag));
            UInt16(bytes, (ushort)outer);
            UInt16(bytes, (ushort)inner);
            bytes.AddRange(new byte[recordSize - 8]);
        }

        bytes.AddRange(Store);
        return [.. bytes];
    }

    private static byte[] PatchUInt16(byte[] table, int position, int value)
    {
        byte[] copy = [.. table];
        BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(position), (ushort)value);
        return copy;
    }

    private static byte[] PatchByte(byte[] table, int position, byte value)
    {
        byte[] copy = [.. table];
        copy[position] = value;
        return copy;
    }

    private static void UInt16(List<byte> bytes, ushort value)
    {
        bytes.Add((byte)(value >> 8));
        bytes.Add((byte)value);
    }
}
