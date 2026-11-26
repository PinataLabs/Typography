// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Fuzz;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Tests for how the HVAR and VVAR tables read their mappings and apply their deltas, on tables that the test writes.
/// The fonts are checked against fontTools in MetricsVariationReferenceTests.
/// </summary>
public class MetricsVariationTableTests
{
    private const short Maximum = 16384;
    private const short Half = 8192;
    private const int HvarHeaderSize = 20;
    private const int VvarHeaderSize = 24;

    // One axis, and two regions: the peak at 1, and the peak at -1. The rows give the delta of each region.
    // At the coordinate 1 a row is its first delta, at -1 its second, and at 0.5 half its first.
    private static readonly short[][] Regions = [[0, Maximum, Maximum], [-Maximum, -Maximum, 0]];
    private static readonly int[][] Rows = [[10, -20], [30, 0], [-5, 5], [7, 7]];

    private static readonly byte[] Store = VariationBytes.Store(1, Regions, new VariationBytes.ItemData([0, 1], 1, Rows));

    [Test]
    public async Task WithoutAnAdvanceMapTheGlyphIsTheInnerIndexAndTheOuterIndexIs0()
    {
        MetricsVariation variation = ReadHvar(Hvar(Store)).CreateVariation([Maximum]);

        await Assert.That(variation.VaryAdvance(0, 500)).IsEqualTo((ushort)510);
        await Assert.That(variation.VaryAdvance(1, 500)).IsEqualTo((ushort)530);
        await Assert.That(variation.VaryAdvance(2, 500)).IsEqualTo((ushort)495);

        // The store has four rows, so a glyph past them has no delta.
        await Assert.That(variation.VaryAdvance(4, 500)).IsEqualTo((ushort)500);
        await Assert.That(variation.VaryAdvance(ushort.MaxValue, 500)).IsEqualTo((ushort)500);
    }

    [Test]
    public async Task AnAdvanceMapGivesEachGlyphItsRowAndAGlyphPastTheMapTakesTheLastEntry()
    {
        byte[] map = VariationBytes.Map(0, 1, 2, (0, 3), (0, 0));
        MetricsVariation variation = ReadHvar(Hvar(Store, advanceMap: map)).CreateVariation([Maximum]);

        await Assert.That(variation.VaryAdvance(0, 500)).IsEqualTo((ushort)507);
        await Assert.That(variation.VaryAdvance(1, 500)).IsEqualTo((ushort)510);
        await Assert.That(variation.VaryAdvance(2, 500)).IsEqualTo((ushort)510);
        await Assert.That(variation.VaryAdvance(1000, 500)).IsEqualTo((ushort)510);
    }

    [Test]
    public async Task AValueThatIsAHalfRoundsUp()
    {
        MetricsVariation variation = ReadHvar(Hvar(Store)).CreateVariation([Half]);

        // The deltas at 0.5 are 5, 15, -2.5 and 3.5, so the values are 505, 515, 497.5 and 503.5.
        await Assert.That(variation.VaryAdvance(0, 500)).IsEqualTo((ushort)505);
        await Assert.That(variation.VaryAdvance(2, 500)).IsEqualTo((ushort)498);
        await Assert.That(variation.VaryAdvance(3, 500)).IsEqualTo((ushort)504);
    }

    [Test]
    public async Task TheSumOfAllDeltasIsRoundedOnce()
    {
        // Two regions that both count at 0.5 give 2.5 + 2.5 = 5, where rounding each first would give 3 + 3.
        byte[] store = VariationBytes.Store(
            1,
            [[0, Maximum, Maximum], [0, Maximum, Maximum]],
            new VariationBytes.ItemData([0, 1], 2, [[5, 5]]));

        MetricsVariation variation = ReadHvar(Hvar(store)).CreateVariation([Half]);

        await Assert.That(variation.VaryAdvance(0, 500)).IsEqualTo((ushort)505);
    }

    [Test]
    public async Task AnAdvanceStaysInTheRangeOfItsType()
    {
        byte[] store = VariationBytes.Store(1, Regions, new VariationBytes.ItemData([0], 1, [[100], [-100]]));
        MetricsVariation variation = ReadHvar(Hvar(store)).CreateVariation([Maximum]);

        await Assert.That(variation.VaryAdvance(0, ushort.MaxValue)).IsEqualTo(ushort.MaxValue);
        await Assert.That(variation.VaryAdvance(1, 40)).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task ABearingChangesOnlyWhenTheTableMapsIt()
    {
        byte[] map = VariationBytes.Map(0, 1, 2, (0, 0), (0, 2));
        MetricsVariation without = ReadHvar(Hvar(Store)).CreateVariation([Half]);
        MetricsVariation with = ReadHvar(Hvar(Store, leftSideBearingMap: map)).CreateVariation([Half]);

        await Assert.That(without.HasLeadingBearings).IsFalse();
        await Assert.That(without.VaryLeadingBearing(0, 50)).IsEqualTo((short)50);
        await Assert.That(with.HasLeadingBearings).IsTrue();
        await Assert.That(with.VaryLeadingBearing(0, 50)).IsEqualTo((short)55);

        // -3 - 2.5 is -5.5, which rounds up to -5.
        await Assert.That(with.VaryLeadingBearing(1, -3)).IsEqualTo((short)-5);
    }

    [Test]
    public async Task ABearingOfAGlyphThatTheMapSendsToNoDeltaIsKept()
    {
        // The entry (0xFFFF, 0xFFFF) is NO_VARIATION_INDEX: the store has no such subtable.
        byte[] map = VariationBytes.Map(0, 4, 16, (0xFFFF, 0xFFFF), (0, 0));
        MetricsVariation variation = ReadHvar(Hvar(Store, leftSideBearingMap: map)).CreateVariation([Maximum]);

        await Assert.That(variation.VaryLeadingBearing(0, 50)).IsEqualTo((short)50);
        await Assert.That(variation.VaryLeadingBearing(1, 50)).IsEqualTo((short)60);
    }

    [Test]
    public async Task TheScalarsOfAnInstanceWithFewerAxesThanTheStoreAreThoseOfTheDefault()
    {
        MetricsVariation variation = ReadHvar(Hvar(Store)).CreateVariation([]);

        await Assert.That(variation.VaryAdvance(0, 500)).IsEqualTo((ushort)500);
    }

    [Test]
    public async Task ADefaultVariationChangesNothing()
    {
        MetricsVariation variation = default;

        await Assert.That(variation.IsEmpty).IsTrue();
        await Assert.That(variation.HasLeadingBearings).IsFalse();
        await Assert.That(variation.VaryAdvance(3, 612)).IsEqualTo((ushort)612);
        await Assert.That(variation.VaryLeadingBearing(3, -12)).IsEqualTo((short)-12);
    }

    [Test]
    public async Task TheRightSideBearingMapIsNotRead()
    {
        // A garbage offset for it does not make the table malformed, because nothing reads it.
        byte[] table = Hvar(Store);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(16), 0xFFFFFFF0);

        await Assert.That(HvarTable.Parse(table)).IsNotNull();
    }

    [Test]
    [Arguments("a header that is cut short")]
    [Arguments("the major version 2")]
    [Arguments("no store")]
    [Arguments("a store past the end")]
    [Arguments("a store that is malformed")]
    [Arguments("an advance map past the end")]
    [Arguments("an advance map that is malformed")]
    [Arguments("a left side bearing map past the end")]
    [Arguments("a left side bearing map that is malformed")]
    [Arguments("an offset above the range of an int")]
    public async Task AMalformedHvarIsAbsent(string defect)
    {
        byte[] map = VariationBytes.Map(0, 1, 2, (0, 0), (0, 1));
        byte[] table = Hvar(Store, advanceMap: map, leftSideBearingMap: map);

        await Assert.That(HvarTable.Parse(Malform(table, defect, HvarHeaderSize, map.Length))).IsNull();
    }

    [Test]
    [Arguments("a header that is cut short")]
    [Arguments("the major version 2")]
    [Arguments("no store")]
    [Arguments("a store past the end")]
    [Arguments("an advance map that is malformed")]
    [Arguments("a left side bearing map that is malformed")]
    public async Task AMalformedVvarIsAbsent(string defect)
    {
        byte[] map = VariationBytes.Map(0, 1, 2, (0, 0), (0, 1));
        byte[] table = Hvar(Store, advanceMap: map, leftSideBearingMap: map, headerSize: VvarHeaderSize);

        await Assert.That(VvarTable.Parse(Malform(table, defect, VvarHeaderSize, map.Length))).IsNull();
    }

    private static HvarTable ReadHvar(byte[] table) => HvarTable.Parse(table) ?? throw new InvalidOperationException("The table is malformed.");

    // The header, then the store, then the maps. A map that is absent has the offset 0.
    private static byte[] Hvar(byte[] store, byte[]? advanceMap = null, byte[]? leftSideBearingMap = null, int headerSize = HvarHeaderSize)
    {
        List<byte> bytes = [0, 1, 0, 0];
        int next = headerSize;
        List<byte> body = [];
        UInt32(bytes, (uint)next);
        body.AddRange(store);
        next += store.Length;
        foreach (byte[]? map in (ReadOnlySpan<byte[]?>)[advanceMap, leftSideBearingMap])
        {
            UInt32(bytes, map is null ? 0 : (uint)next);
            if (map is not null)
            {
                body.AddRange(map);
                next += map.Length;
            }
        }

        while (bytes.Count < headerSize)
        {
            bytes.Add(0);
        }

        bytes.AddRange(body);
        return [.. bytes];
    }

    // Breaks a table that Hvar wrote with a store and two maps of the same length after its header. The header has the
    // version, then the offsets of the store, the advance map and the left side bearing map at 4, 8 and 12.
    private static byte[] Malform(byte[] table, string defect, int headerSize, int mapLength)
    {
        int advanceMap = headerSize + Store.Length;
        int leftSideBearingMap = advanceMap + mapLength;
        return defect switch
        {
            "a header that is cut short" => table[..(headerSize - 1)],
            "the major version 2" => PatchUInt16(table, 0, 2),
            "no store" => PatchUInt32(table, 4, 0),
            "a store past the end" => PatchUInt32(table, 4, (uint)table.Length + 1),
            "a store that is malformed" => PatchByte(table, headerSize + 1, 2),
            "an advance map past the end" => PatchUInt32(table, 8, (uint)table.Length + 1),
            "an advance map that is malformed" => PatchByte(table, advanceMap, 7),
            "a left side bearing map past the end" => PatchUInt32(table, 12, (uint)table.Length + 1),
            "a left side bearing map that is malformed" => PatchByte(table, leftSideBearingMap, 7),
            _ => PatchUInt32(table, 8, 0x80000000),
        };
    }

    private static byte[] PatchUInt16(byte[] table, int position, int value)
    {
        byte[] copy = [.. table];
        BinaryPrimitives.WriteUInt16BigEndian(copy.AsSpan(position), (ushort)value);
        return copy;
    }

    private static byte[] PatchUInt32(byte[] table, int position, uint value)
    {
        byte[] copy = [.. table];
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(position), value);
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

    private static void UInt32(List<byte> bytes, uint value)
    {
        UInt16(bytes, (ushort)(value >> 16));
        UInt16(bytes, (ushort)value);
    }
}
