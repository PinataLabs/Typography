// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using PinataLabs.Typography.Fuzz;
using PinataLabs.Typography.Tables.Variations;
using static PinataLabs.Typography.Fuzz.TupleVariationBytes;
using static PinataLabs.Typography.Fuzz.VariationBytes;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// The 'cvar' table adds its deltas to the values of the 'cvt ' table, scaled by the region of each tuple. The deltas of all the
/// tuples add up before the value rounds, as fontTools does, and the value is a 16-bit integer.
/// </summary>
public class CvarTableTests
{
    private static readonly short One = F2Dot14(1);

    private static readonly short[] Values = [100, 200, 300, 400];

    [Test]
    [Arguments(0.0, new short[] { 100, 200, 300, 400 })]
    [Arguments(0.5, new short[] { 100, 205, 300, 390 })]
    [Arguments(1.0, new short[] { 100, 210, 300, 380 })]
    [Arguments(-0.5, new short[] { 100, 200, 300, 400 })]
    public async Task TheDeltasOfATupleAreScaledByItsRegion(double coordinate, short[] expected)
    {
        CvarTable table = Parse(Cvar(null, new TupleData([One], null, null, [1, 3], [10, -20], null)));

        short[]? values = table.CreateControlValues(Values, [F2Dot14(coordinate)]);

        await Assert.That(values!).IsEquivalentTo(expected);
    }

    [Test]
    public async Task ATupleWithNoPointNumbersHasADeltaForEveryValue()
    {
        CvarTable table = Parse(Cvar(null, new TupleData([One], null, null, null, [1, 2, 3, 4], null)));

        short[]? values = table.CreateControlValues(Values, [One]);

        await Assert.That(values!).IsEquivalentTo(new short[] { 101, 202, 303, 404 });
    }

    [Test]
    public async Task TheSharedPointNumbersServeATupleWithNoneOfItsOwn()
    {
        CvarTable table = Parse(Cvar([0, 2], new TupleData([One], null, null, null, [5, 6], null)));

        short[]? values = table.CreateControlValues(Values, [One]);

        await Assert.That(values!).IsEquivalentTo(new short[] { 105, 200, 306, 400 });
    }

    [Test]
    public async Task TheDeltasOfTheTuplesAddUpBeforeTheValueRounds()
    {
        // Each tuple has the scalar 0.3 and the delta 1, which rounds to 0, and together they are 0.6, which rounds to 1.
        CvarTable table = Parse(Cvar(
            null,
            new TupleData([One], null, null, [0], [1], null),
            new TupleData([One], null, null, [0], [1], null)));

        short[]? values = table.CreateControlValues(Values, [F2Dot14(0.3)]);

        await Assert.That(values![0]).IsEqualTo((short)101);
    }

    [Test]
    [Arguments(1, 1)]
    [Arguments(-1, 0)]
    [Arguments(3, 2)]
    [Arguments(-3, -1)]
    public async Task AHalfRoundsUp(int delta, int expected)
    {
        // At half of its peak the tuple has the scalar 0.5, so an odd delta ends in a half.
        CvarTable table = Parse(Cvar(null, new TupleData([One], null, null, [0], [delta], null)));

        short[]? values = table.CreateControlValues([0], [F2Dot14(0.5)]);

        await Assert.That(values![0]).IsEqualTo((short)expected);
    }

    [Test]
    public async Task AValueStaysInTheRangeOfAnInteger()
    {
        CvarTable table = Parse(Cvar(null, new TupleData([One], null, null, [0, 1], [20000, -20000], null)));

        short[]? values = table.CreateControlValues([30000, -30000], [One]);

        await Assert.That(values!).IsEquivalentTo(new[] { short.MaxValue, short.MinValue });
    }

    [Test]
    public async Task ADeltaForAValueThatTheTableDoesNotHaveIsIgnored()
    {
        CvarTable table = Parse(Cvar(null, new TupleData([One], null, null, [1, 9, 70], [7, 8, 9], null)));

        short[]? values = table.CreateControlValues(Values, [One]);

        await Assert.That(values!).IsEquivalentTo(new short[] { 100, 207, 300, 400 });
    }

    [Test]
    public async Task ATupleThatTheInstanceIsOutsideOfDoesNotChangeTheValues()
    {
        CvarTable table = Parse(Cvar(null, new TupleData([(short)-One], null, null, null, [1, 2, 3, 4], null)));

        short[]? values = table.CreateControlValues(Values, [One]);

        await Assert.That(values!).IsEquivalentTo(Values);
    }

    [Test]
    public async Task AnInstanceWhoseTupleDataIsMalformedHasNoValues()
    {
        byte[] data = Cvar(null, new TupleData([One], null, null, null, [1, 2, 3, 4], null));

        // The run of deltas says 5 for a table of 4.
        data[^5] = 0x04;
        CvarTable table = Parse(data);

        await Assert.That(table.CreateControlValues(Values, [One])).IsNull();
        await Assert.That(table.CreateControlValues(Values, [0])).IsNotNull();
    }

    [Test]
    public async Task AHeaderThatDoesNotFitIsNotRead()
    {
        byte[] valid = Cvar(null, new TupleData([One], null, null, null, [1], null));

        await Assert.That(CvarTable.Parse(valid, 1)).IsNotNull();
        await Assert.That(CvarTable.Parse(valid, 2)).IsNull();
        await Assert.That(CvarTable.Parse(valid.AsMemory(0, 7), 1)).IsNull();
        await Assert.That(CvarTable.Parse(valid.AsMemory(0, valid.Length - 1), 1)).IsNull();

        byte[] version = [.. valid];
        version[1] = 2;
        await Assert.That(CvarTable.Parse(version, 1)).IsNull();
    }

    private static CvarTable Parse(byte[] data) => CvarTable.Parse(data, 1)!;
}
