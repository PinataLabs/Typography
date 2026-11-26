// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Fuzz;
using PinataLabs.Typography.Tables.Variations;
using static PinataLabs.Typography.Fuzz.TupleVariationBytes;
using static PinataLabs.Typography.Fuzz.VariationBytes;

namespace PinataLabs.Typography.Tests.Tables.Variations;

public class TupleVariationStoreTests
{
    private static readonly short One = F2Dot14(1);

    [Test]
    [Arguments(0.0, 0.0)]
    [Arguments(0.25, 0.25)]
    [Arguments(0.5, 0.5)]
    [Arguments(1.0, 1.0)]
    [Arguments(-0.5, 0.0)]
    public async Task ATupleScalesItsDeltasByTheScalarOfItsRegion(double coordinate, double expected)
    {
        // A positive peak covers 0 up to the peak.
        TupleVariationStore store = Read(Glyph(null, Peak([One], [10, -20, 30], [1, 2, 3])), 1);

        bool read = GetVariations(store, [F2Dot14(coordinate)], 3, true, out TupleVariation[] variations);

        await Assert.That(read).IsTrue();
        if (expected == 0)
        {
            await Assert.That(variations).IsEmpty();
            return;
        }

        await Assert.That(variations.Length).IsEqualTo(1);
        await Assert.That(variations[0].Scalar).IsEqualTo((float)expected);
        await Assert.That(variations[0].Points).IsNull();
        await Assert.That(variations[0].DeltaX).IsEquivalentTo(new int[] { 10, -20, 30 });
        await Assert.That(variations[0].DeltaY!).IsEquivalentTo(new int[] { 1, 2, 3 });
    }

    [Test]
    [Arguments(-0.5, 0.5)]
    [Arguments(-1.0, 1.0)]
    [Arguments(0.5, 0.0)]
    public async Task ANegativePeakCoversTheNegativeSide(double coordinate, double expected)
    {
        TupleVariationStore store = Read(Glyph(null, Peak([(short)-One], [4], null)), 1);

        GetVariations(store, [F2Dot14(coordinate)], 1, false, out TupleVariation[] variations);

        await Assert.That(variations.Select(variation => (double)variation.Scalar).ToArray()).IsEquivalentTo(expected == 0 ? [] : new[] { expected });
    }

    [Test]
    public async Task ASharedTupleGivesThePeak()
    {
        short[][] shared = [[(short)-One], [One]];
        TupleVariationStore store = Read(Glyph(null, new TupleData(null, null, null, null, [5], null, 1)), 1, shared);

        GetVariations(store, [F2Dot14(0.5)], 1, false, out TupleVariation[] positive);
        GetVariations(store, [F2Dot14(-0.5)], 1, false, out TupleVariation[] negative);

        await Assert.That(positive.Single().Scalar).IsEqualTo(0.5F);
        await Assert.That(negative).IsEmpty();
    }

    [Test]
    [Arguments(0.25, 0.0)]
    [Arguments(0.375, 0.5)]
    [Arguments(0.5, 1.0)]
    [Arguments(0.75, 0.5)]
    [Arguments(1.0, 0.0)]
    [Arguments(0.0, 0.0)]
    public async Task AnIntermediateRegionRisesToThePeakAndFallsToTheEnd(double coordinate, double expected)
    {
        TupleVariationStore store = Read(Glyph(null, new TupleData([F2Dot14(0.5)], [F2Dot14(0.25)], [One], null, [8], null)), 1);

        GetVariations(store, [F2Dot14(coordinate)], 1, false, out TupleVariation[] variations);

        await Assert.That(variations.Select(variation => (double)variation.Scalar).ToArray()).IsEquivalentTo(expected == 0 ? [] : new[] { expected });
    }

    [Test]
    public async Task TheScalarOfATupleIsTheProductOfItsAxes()
    {
        TupleVariationStore store = Read(Glyph(null, Peak([One, One], [8], null)), 2);

        GetVariations(store, [F2Dot14(0.5), F2Dot14(0.25)], 1, false, out TupleVariation[] variations);

        await Assert.That(variations.Single().Scalar).IsEqualTo(0.125F);
    }

    [Test]
    public async Task AnAxisWithNoPeakDoesNotTakePart()
    {
        TupleVariationStore store = Read(Glyph(null, Peak([One, 0], [8], null)), 2);

        // The second axis has no peak, so the tuple has the scalar of the first axis at any coordinate of the second.
        GetVariations(store, [One, F2Dot14(-0.75)], 1, false, out TupleVariation[] variations);

        await Assert.That(variations.Single().Scalar).IsEqualTo(1F);
    }

    [Test]
    public async Task AnAxisThatTheCoordinatesLackIsAtZero()
    {
        TupleVariationStore store = Read(Glyph(null, Peak([One, One], [8], null)), 2);

        GetVariations(store, [One], 1, false, out TupleVariation[] variations);

        await Assert.That(variations).IsEmpty();
    }

    [Test]
    public async Task TuplesWithNoPointNumbersOfTheirOwnUseTheSharedOnes()
    {
        byte[] data = Glyph(
            [2, 4, 300],
            Peak([One], [1, 2, 3], null),
            new TupleData([One], null, null, [0, 5], [7, 8], null));
        TupleVariationStore store = Read(data, 1);

        GetVariations(store, [One], 400, false, out TupleVariation[] variations);

        await Assert.That(variations.Length).IsEqualTo(2);
        await Assert.That(variations[0].Points!).IsEquivalentTo(new ushort[] { 2, 4, 300 });
        await Assert.That(variations[0].DeltaX).IsEquivalentTo(new int[] { 1, 2, 3 });
        await Assert.That(variations[1].Points!).IsEquivalentTo(new ushort[] { 0, 5 });
        await Assert.That(variations[1].DeltaX).IsEquivalentTo(new int[] { 7, 8 });
    }

    [Test]
    public async Task AListOfMoreThan127PointsHasATwoByteCount()
    {
        int[] points = [.. Enumerable.Range(0, 200).Select(i => i * 3)];
        int[] deltas = [.. Enumerable.Range(0, 200).Select(i => i - 100)];
        TupleVariationStore store = Read(Glyph(null, new TupleData([One], null, null, points, deltas, deltas)), 1);

        GetVariations(store, [One], 1000, true, out TupleVariation[] variations);

        await Assert.That(variations.Single().Points!.Select(point => (int)point).ToArray()).IsEquivalentTo(points);
        await Assert.That(variations.Single().DeltaX).IsEquivalentTo(deltas);
        await Assert.That(variations.Single().DeltaY!).IsEquivalentTo(deltas);
    }

    [Test]
    public async Task DeltasInRunsOfZerosBytesAndWordsAreDecoded()
    {
        // 70 zeros are a run of 64 and a run of 6, then bytes and words, then a run that mixes both and is written as words.
        int[] deltas = [.. new int[70], 5, -128, 127, 128, -129, 300, -300, 0, 1, 32767, -32768];
        TupleVariationStore store = Read(Glyph(null, Peak([One], deltas, null)), 1);

        GetVariations(store, [One], deltas.Length, false, out TupleVariation[] variations);

        await Assert.That(variations.Single().DeltaX).IsEquivalentTo(deltas);
    }

    [Test]
    public async Task DeltasInRunsOfLongsAreDecoded()
    {
        // A run of longs has both flag bits of the control byte, so it is not a run of zeros: OpenType 1.9.1 gives 0xC0 to 32-bit
        // deltas. The runs here are zeros, bytes, a run of longs, words, and a run of longs that has a zero and a byte in it.
        int[] deltas = [0, 0, 3, -4, 70000, -70000, int.MaxValue, int.MinValue, 100, -200, 0, 5, 40000, 1];
        TupleVariationStore store = Read(Glyph(null, Peak([One], deltas, deltas)), 1);

        bool read = GetVariations(store, [One], deltas.Length, true, out TupleVariation[] variations);

        await Assert.That(read).IsTrue();
        await Assert.That(variations.Single().DeltaX).IsEquivalentTo(deltas);
        await Assert.That(variations.Single().DeltaY!).IsEquivalentTo(deltas);
    }

    [Test]
    public async Task ARunOfLongsIsNotARunOfZeros()
    {
        // The control byte 0xC1 is two longs, 0x80 + 0x40 + 1. A reader that tests the zeros bit first skips both and reads the next byte as a control.
        byte[] deltas = [0xC1, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFE];
        TupleVariationStore store = Read(WithPrivatePoints([0, .. deltas]), 1);

        GetVariations(store, [One], 2, false, out TupleVariation[] variations);

        await Assert.That(variations.Single().DeltaX).IsEquivalentTo(new[] { 65536, -2 });
    }

    [Test]
    public async Task ATruncatedRunOfLongsIsMalformed()
    {
        byte[] deltas = [0xC1, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xFF];
        TupleVariationStore store = Read(WithPrivatePoints([0, .. deltas]), 1);

        bool read = GetVariations(store, [One], 2, false, out _);

        await Assert.That(read).IsFalse();
    }

    [Test]
    public async Task ATupleWithoutPointNumbersCoversEveryPointThatTheCallerHas()
    {
        TupleVariationStore store = Read(Glyph(null, Peak([One], [1, 2, 3, 4, 5], [5, 4, 3, 2, 1])), 1);

        GetVariations(store, [One], 5, true, out TupleVariation[] variations);

        await Assert.That(variations.Single().Points).IsNull();
        await Assert.That(variations.Single().DeltaX.Length).IsEqualTo(5);
        await Assert.That(variations.Single().DeltaY!.Length).IsEqualTo(5);
    }

    [Test]
    public async Task ATupleThatTheInstanceDoesNotUseIsNotDecoded()
    {
        // The second tuple has data that is not valid, and the instance is outside its region.
        byte[] data = Glyph(null, Peak([One], [1], null), new TupleData([(short)-One], null, null, null, [1], null));
        data[^1] = 0xFF;
        data[^2] = 0xFF;
        TupleVariationStore store = Read(data, 1);

        bool read = GetVariations(store, [One], 1, false, out TupleVariation[] variations);

        await Assert.That(read).IsTrue();
        await Assert.That(variations.Length).IsEqualTo(1);
    }

    [Test]
    public async Task ACvarTableHasItsVersionBeforeTheStore()
    {
        byte[] table = Cvar(null, Peak([One], [3, 4], null) with { Points = [1, 2] });
        TupleVariationStore.TryRead(table, 4, 1, default, out TupleVariationStore store);

        GetVariations(store, [One], 10, false, out TupleVariation[] variations);

        await Assert.That(store.TupleCount).IsEqualTo(1);
        await Assert.That(variations.Single().Points!).IsEquivalentTo(new ushort[] { 1, 2 });
        await Assert.That(variations.Single().DeltaY).IsNull();
    }

    [Test]
    public async Task AStoreWithNoTuplesHasNoVariations()
    {
        TupleVariationStore store = Read(Glyph(null), 1);

        bool read = GetVariations(store, [One], 4, true, out TupleVariation[] variations);

        await Assert.That(store.TupleCount).IsEqualTo(0);
        await Assert.That(read).IsTrue();
        await Assert.That(variations).IsEmpty();
    }

    [Test]
    public async Task ADefaultStoreHasNoTuples()
    {
        TupleVariationStore store = default;

        bool read = GetVariations(store, [One], 4, true, out TupleVariation[] variations);

        await Assert.That(read).IsTrue();
        await Assert.That(variations).IsEmpty();
    }

    [Test]
    public async Task AStoreThatEndsInsideItsHeaderIsNotRead()
    {
        byte[] data = Glyph(null, Peak([One], [1], null));

        foreach (int length in new[] { 0, 1, 3, 4, 5, 7, 9, 11 })
        {
            await Assert.That(TupleVariationStore.TryRead(data.AsMemory(0, length), 0, 1, default, out _)).IsFalse();
        }
    }

    [Test]
    public async Task AStoreWhoseDataLiesBeyondTheEndIsNotRead()
    {
        byte[] data = Glyph(null, Peak([One], [1, 2, 3, 4], null));

        await Assert.That(TupleVariationStore.TryRead(data.AsMemory(0, data.Length - 1), 0, 1, default, out _)).IsFalse();
    }

    [Test]
    public async Task AnOffsetOfTheDataInsideTheHeadersIsNotRead()
    {
        byte[] data = Glyph(null, Peak([One], [1], null));
        data[3] = 4;

        await Assert.That(TupleVariationStore.TryRead(data, 0, 1, default, out _)).IsFalse();
    }

    [Test]
    public async Task ACountOfTuplesThatTheHeadersDoNotHoldIsNotRead()
    {
        byte[] data = Glyph(null, Peak([One], [1], null));
        data[1] = 3;

        await Assert.That(TupleVariationStore.TryRead(data, 0, 1, default, out _)).IsFalse();
    }

    [Test]
    public async Task ASharedTupleThatDoesNotExistFailsTheTuplesOfTheInstance()
    {
        byte[] data = Glyph(null, new TupleData(null, null, null, null, [5], null, 3));
        TupleVariationStore store = Read(data, 1, [[One]]);

        await Assert.That(GetVariations(store, [One], 1, false, out _)).IsFalse();
    }

    [Test]
    public async Task APointRunThatPassesTheCountFails()
    {
        // The count says 2, and the run says 3.
        byte[] points = [2, 0x02, 1, 1, 1];
        byte[] data = WithPrivatePoints(points);
        TupleVariationStore store = Read(data, 1);

        await Assert.That(GetVariations(store, [One], 10, false, out _)).IsFalse();
    }

    [Test]
    public async Task ACountOfPointsThatTheDataCannotHoldFails()
    {
        byte[] points = [0x7F, 0x00, 1];
        byte[] data = WithPrivatePoints(points);
        TupleVariationStore store = Read(data, 1);

        await Assert.That(GetVariations(store, [One], 10, false, out _)).IsFalse();
    }

    [Test]
    public async Task PointNumbersThatPassTheLimitOfAWordFail()
    {
        // Two words of 40000.
        byte[] points = [2, 0x81, 0x9C, 0x40, 0x9C, 0x40];
        byte[] data = WithPrivatePoints(points);
        TupleVariationStore store = Read(data, 1);

        await Assert.That(GetVariations(store, [One], 10, false, out _)).IsFalse();
    }

    [Test]
    public async Task ADeltaRunThatPassesTheCountFails()
    {
        // One point number, and a run of 3 byte deltas.
        byte[] data = WithPrivatePoints([1, 0x00, 4, 0x02, 1, 2, 3]);
        TupleVariationStore store = Read(data, 1);

        await Assert.That(GetVariations(store, [One], 10, false, out _)).IsFalse();
    }

    [Test]
    public async Task DeltasThatEndInsideARunFail()
    {
        // Two points, and a run of 2 word deltas that has bytes for one.
        byte[] data = WithPrivatePoints([2, 0x01, 0, 1, 0x41, 0, 1]);
        TupleVariationStore store = Read(data, 1);

        await Assert.That(GetVariations(store, [One], 10, false, out _)).IsFalse();
    }

    // Reads every tuple that the instance uses. It is false when the data of one is malformed, and then the tuples are those before it.
    private static bool GetVariations(TupleVariationStore store, ReadOnlySpan<short> coordinates, int pointCount, bool hasY, out TupleVariation[] variations)
    {
        List<TupleVariation> list = [];
        TupleVariationStore.Reader reader = store.CreateReader(coordinates, pointCount, hasY);
        while (reader.TryRead(out TupleVariation variation))
        {
            list.Add(variation);
        }

        variations = [.. list];
        return !reader.Failed;
    }

    // A store of one tuple with a peak at 1 and private point numbers, whose data is the bytes that the test gives.
    private static byte[] WithPrivatePoints(byte[] tupleData)
        => [0, 1, 0, 10, (byte)(tupleData.Length >> 8), (byte)tupleData.Length, 0xA0, 0, 0x40, 0, .. tupleData];

    private static TupleData Peak(short[] peak, int[] deltaX, int[]? deltaY) => new(peak, null, null, null, deltaX, deltaY);

    private static byte[] Glyph(int[]? sharedPoints, params TupleData[] tuples) => TupleVariationBytes.Glyph(sharedPoints, tuples);

    private static TupleVariationStore Read(byte[] data, int axisCount, short[][]? shared = null)
    {
        byte[] sharedTuples = new byte[(shared?.Length ?? 0) * axisCount * 2];
        for (int i = 0; i < (shared?.Length ?? 0); i++)
        {
            for (int axis = 0; axis < axisCount; axis++)
            {
                int value = shared![i][axis];
                sharedTuples[((i * axisCount) + axis) * 2] = (byte)(value >> 8);
                sharedTuples[(((i * axisCount) + axis) * 2) + 1] = (byte)value;
            }
        }

        if (!TupleVariationStore.TryRead(data, 0, axisCount, sharedTuples, out TupleVariationStore store))
        {
            throw new InvalidOperationException("The store was not read.");
        }

        return store;
    }
}
