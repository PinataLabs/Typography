// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Variations;
using static PinataLabs.Typography.Fuzz.VariationBytes;

namespace PinataLabs.Typography.Tests.Tables.Variations;

public class ItemVariationStoreTests
{
    // One axis. Region 0 peaks at 1, region 1 peaks at 0.5 and region 2 peaks at -0.5.
    private static readonly short[][] Regions =
    [
        [0, F2Dot14(1), F2Dot14(1)],
        [0, F2Dot14(0.5), F2Dot14(1)],
        [F2Dot14(-1), F2Dot14(-0.5), 0],
    ];

    [Test]
    [Arguments(0.0, 0.0)]
    [Arguments(0.25, 0.5)]
    [Arguments(0.5, 1.0)]
    [Arguments(0.75, 0.5)]
    [Arguments(1.0, 0.0)]
    [Arguments(1.5, 0.0)]
    [Arguments(-0.25, 0.0)]
    public async Task ScalarFallsFromThePeakToTheStartAndTheEnd(double coordinate, double expected)
    {
        ItemVariationStore store = Read(Store(1, [Regions[1]]));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(coordinate)]);

        await Assert.That(scalars.Regions.ToArray()).IsEquivalentTo([(float)expected]);
    }

    [Test]
    [Arguments(-0.25, 0.5)]
    [Arguments(-0.5, 1.0)]
    [Arguments(-0.75, 0.5)]
    [Arguments(-1.0, 0.0)]
    [Arguments(0.0, 0.0)]
    [Arguments(0.25, 0.0)]
    public async Task ScalarOfANegativeRegionMirrorsAPositiveOne(double coordinate, double expected)
    {
        ItemVariationStore store = Read(Store(1, [Regions[2]]));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(coordinate)]);

        await Assert.That(scalars.Regions.ToArray()).IsEquivalentTo([(float)expected]);
    }

    [Test]
    [Arguments(0.5, 0.25, 1.0)]
    [Arguments(0.0, 1.0, 0.5)]
    [Arguments(-0.5, 0.5, 0.5)]
    [Arguments(-1.0, 0.0, 1.0)]
    [Arguments(0.0, 0.0, 0.0)]
    public async Task AnAxisWithNoUsableRangeDoesNotTakePart(double start, double peak, double end)
    {
        // The start is after the peak, the peak is after the end, the range spans 0, and the peak is 0.
        ItemVariationStore store = Read(Store(1, [[F2Dot14(start), F2Dot14(peak), F2Dot14(end)]]));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(0.75)]);

        await Assert.That(scalars.Regions.ToArray()).IsEquivalentTo([1F]);
    }

    [Test]
    public async Task ScalarOfARegionIsTheProductOfItsAxes()
    {
        short[] both = [0, F2Dot14(1), F2Dot14(1), 0, F2Dot14(1), F2Dot14(1)];
        ItemVariationStore store = Read(Store(2, [both]));

        float half = store.CreateScalars([F2Dot14(0.5), F2Dot14(1)]).Regions[0];
        float quarter = store.CreateScalars([F2Dot14(0.5), F2Dot14(0.5)]).Regions[0];
        float outside = store.CreateScalars([F2Dot14(0.5), 0]).Regions[0];

        await Assert.That(half).IsEqualTo(0.5F);
        await Assert.That(quarter).IsEqualTo(0.25F);
        await Assert.That(outside).IsEqualTo(0F);
    }

    [Test]
    public async Task AnAxisWithNoCoordinateIsAtZeroAndExtraCoordinatesAreUnused()
    {
        short[] both = [0, F2Dot14(1), F2Dot14(1), 0, F2Dot14(1), F2Dot14(1)];
        ItemVariationStore store = Read(Store(2, [both]));

        float missing = store.CreateScalars([F2Dot14(1)]).Regions[0];
        float none = store.CreateScalars([]).Regions[0];
        float extra = store.CreateScalars([F2Dot14(1), F2Dot14(1), F2Dot14(-1)]).Regions[0];

        await Assert.That(missing).IsEqualTo(0F);
        await Assert.That(none).IsEqualTo(0F);
        await Assert.That(extra).IsEqualTo(1F);
    }

    [Test]
    public async Task DeltaSumsTheRowScaledByTheRegionsItNames()
    {
        // Rows of the first subtable name regions 1 and 0, and the first of the two deltas is a word.
        // The rows of the second name regions 2, 0 and 1, and its first two deltas are words.
        ItemVariationStore store = Read(Store(
            1,
            Regions,
            new ItemData([1, 0], 1, [[100, 8], [-100, -8]]),
            new ItemData([2, 0, 1], 2, [[1000, -1000, -3]])));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(0.5)]);

        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(104F);
        await Assert.That(scalars.GetDelta(0, 1)).IsEqualTo(-104F);
        await Assert.That(scalars.GetDelta(1, 0)).IsEqualTo(-503F);
    }

    [Test]
    public async Task LongWordsWidenTheWordsToThirtyTwoBitsAndTheBytesToSixteen()
    {
        ItemVariationStore store = Read(Store(
            1,
            Regions,
            new ItemData([0, 1], 1, [[100_000, -300]], LongWords: true),
            new ItemData([0, 1], 2, [[-70_000, 70_000]], LongWords: true),
            new ItemData([0, 1], 0, [[-300, 200]], LongWords: true)));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(0.5)]);

        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(49_700F);
        await Assert.That(scalars.GetDelta(1, 0)).IsEqualTo(-35_000F + 70_000F);
        await Assert.That(scalars.GetDelta(2, 0)).IsEqualTo(-150F + 200F);
    }

    [Test]
    public async Task ABytePartOfARowIsSigned()
    {
        ItemVariationStore store = Read(Store(1, Regions, new ItemData([0, 1], 0, [[-128, 127]])));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(1)]);

        // At 1 the first region has scalar 1 and the second has 0.
        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(-128F);
    }

    [Test]
    [Arguments(5, 0)]
    [Arguments(-1, 0)]
    [Arguments(0, 1)]
    [Arguments(0, -1)]
    [Arguments(int.MaxValue, int.MaxValue)]
    [Arguments(int.MinValue, int.MinValue)]
    public async Task ARowThatTheStoreLacksHasDeltaZero(int outer, int inner)
    {
        ItemVariationStore store = Read(Store(1, Regions, new ItemData([0], 1, [[40]])));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(1)]);

        await Assert.That(scalars.GetDelta(outer, inner)).IsEqualTo(0F);
        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(40F);
    }

    [Test]
    public async Task ARegionIndexPastTheRegionListContributesNothing()
    {
        ItemVariationStore store = Read(Store(1, Regions, new ItemData([9, 0], 1, [[100, 7]])));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(0.5)]);
        float[] destination = new float[2];
        bool found = scalars.TryGetRegionScalars(0, destination, out int count);

        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(3.5F);
        await Assert.That(found).IsTrue();
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(destination).IsEquivalentTo([0F, 0.5F]);
    }

    [Test]
    public async Task ASubtableWithANullOffsetHasNoRowsAndNoRegions()
    {
        ItemVariationStore store = Read(Store(1, Regions, null, new ItemData([0], 1, [[40]])));

        bool first = store.TryGetDataCounts(0, out int firstItems, out int firstRegions);
        bool second = store.TryGetDataCounts(1, out int secondItems, out int secondRegions);
        bool third = store.TryGetDataCounts(2, out _, out _);
        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(1)]);

        await Assert.That(store.DataCount).IsEqualTo(2);
        await Assert.That(first).IsTrue();
        await Assert.That((firstItems, firstRegions)).IsEqualTo((0, 0));
        await Assert.That(second).IsTrue();
        await Assert.That((secondItems, secondRegions)).IsEqualTo((1, 1));
        await Assert.That(third).IsFalse();
        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(0F);
        await Assert.That(scalars.GetDelta(1, 0)).IsEqualTo(40F);
    }

    [Test]
    public async Task RegionScalarsOfASubtableFollowTheOrderItNamesThem()
    {
        ItemVariationStore store = Read(Store(1, Regions, new ItemData([1, 0, 2], 0, [[1, 2, 3]])));
        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(0.5)]);
        float[] named = new float[3];
        float[] longer = new float[5];
        float[] shorter = new float[2];

        bool found = scalars.TryGetRegionScalars(0, named, out int namedCount);
        bool foundLonger = scalars.TryGetRegionScalars(0, longer, out int longerCount);
        bool foundShorter = scalars.TryGetRegionScalars(0, shorter, out _);
        bool foundMissing = scalars.TryGetRegionScalars(1, named, out _);

        await Assert.That(found).IsTrue();
        await Assert.That(namedCount).IsEqualTo(3);
        await Assert.That(named).IsEquivalentTo([1F, 0.5F, 0F]);
        await Assert.That(foundLonger).IsTrue();
        await Assert.That(longerCount).IsEqualTo(3);
        await Assert.That(foundShorter).IsFalse();
        await Assert.That(foundMissing).IsFalse();
    }

    [Test]
    public async Task AStoreCountsItsAxesRegionsAndSubtables()
    {
        short[] both = [0, F2Dot14(1), F2Dot14(1), 0, F2Dot14(1), F2Dot14(1)];
        ItemVariationStore store = Read(Store(2, [both, both, both], new ItemData([0, 1], 1, [[1, 2], [3, 4], [5, 6]]), new ItemData([2], 0, [])));

        bool first = store.TryGetDataCounts(0, out int firstItems, out int firstRegions);
        bool second = store.TryGetDataCounts(1, out int secondItems, out int secondRegions);

        await Assert.That((store.AxisCount, store.RegionCount, store.DataCount)).IsEqualTo((2, 3, 2));
        await Assert.That(first && second).IsTrue();
        await Assert.That((firstItems, firstRegions)).IsEqualTo((3, 2));
        await Assert.That((secondItems, secondRegions)).IsEqualTo((0, 1));
    }

    [Test]
    public async Task AStoreReadsAtAnOffsetOfATable()
    {
        byte[] store = Store(1, Regions, new ItemData([0], 1, [[40]]));
        byte[] table = [.. new byte[7], .. store, 0xFF, 0xFF];

        bool found = ItemVariationStore.TryRead(table, 7, out ItemVariationStore read);
        bool past = ItemVariationStore.TryRead(table, table.Length + 1, out _);
        bool negative = ItemVariationStore.TryRead(table, -1, out _);

        await Assert.That(found).IsTrue();
        await Assert.That(read.CreateScalars([F2Dot14(1)]).GetDelta(0, 0)).IsEqualTo(40F);
        await Assert.That(past).IsFalse();
        await Assert.That(negative).IsFalse();
    }

    [Test]
    public async Task ADefaultStoreHasNoRegionsAndNoData()
    {
        ItemVariationStore store = default;

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(1)]);

        await Assert.That(store.RegionCount).IsEqualTo(0);
        await Assert.That(store.TryGetDataCounts(0, out _, out _)).IsFalse();
        await Assert.That(scalars.Regions.Length).IsEqualTo(0);
        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(0F);
        await Assert.That(default(ItemVariationScalars).GetDelta(0, 0)).IsEqualTo(0F);
    }

    [Test]
    public async Task ACff2StoreHasALengthAndSubtablesWithNoRows()
    {
        byte[] store = Cff2Store(1, Regions, [0, 1], [2]);
        byte[] table = [.. new byte[5], .. store, 0xAA];

        bool found = ItemVariationStore.TryReadCff2(table, 5, out ItemVariationStore read);
        bool first = read.TryGetDataCounts(0, out int firstItems, out int firstRegions);
        bool second = read.TryGetDataCounts(1, out int secondItems, out int secondRegions);
        ItemVariationScalars scalars = read.CreateScalars([F2Dot14(0.5)]);
        float[] destination = new float[2];
        bool scalarsFound = scalars.TryGetRegionScalars(0, destination, out int count);

        await Assert.That(found).IsTrue();
        await Assert.That((read.RegionCount, read.DataCount)).IsEqualTo((3, 2));
        await Assert.That(first && second).IsTrue();
        await Assert.That((firstItems, firstRegions, secondItems, secondRegions)).IsEqualTo((0, 2, 0, 1));
        await Assert.That(scalarsFound).IsTrue();
        await Assert.That(count).IsEqualTo(2);
        await Assert.That(destination).IsEquivalentTo([0.5F, 1F]);
    }

    [Test]
    public async Task ACff2StoreRunsToTheEndOfTheTableWhateverItsLengthSays()
    {
        byte[] store = Cff2Store(1, Regions, [0, 1], [2]);
        List<string> failures = [];

        // A length that runs past the table, a length that cuts off the last subtable, and a length of 0.
        byte[] longer = [.. store];
        longer[1]++;
        byte[] shorter = [.. store];
        shorter[1]--;
        byte[] none = [.. store];
        none[0] = 0;
        none[1] = 0;
        Expect(ReadsCff2WithTwoSubtables(longer), true, "length past the table", failures);
        Expect(ReadsCff2WithTwoSubtables(shorter), true, "length that cuts the store", failures);
        Expect(ReadsCff2WithTwoSubtables(none), true, "length of 0", failures);
        Expect(ReadsCff2WithTwoSubtables(store), true, "intact", failures);

        // There is no room for a length, or the offset is not in the table.
        Expect(ItemVariationStore.TryReadCff2(store, store.Length - 1, out _), false, "offset on the last byte", failures);
        Expect(ItemVariationStore.TryReadCff2(store, store.Length + 1, out _), false, "offset past the table", failures);
        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task ACff2StoreWithoutARegionListKeepsTheRegionCountsOfItsSubtables()
    {
        byte[] store = Cff2Store(1, Regions, [0, 1], [2]);
        List<string> failures = [];

        // The store starts after the length, so its region list offset is at 4 and its region list is at 18.
        byte[][] broken =
        [
            Patch(store, 4, 0, 0, 0, 0),
            Patch(store, 4, 0xFF, 0xFF, 0xFF, 0xFF),
            Patch(store, 18, 0xFF, 0xFF),
        ];
        foreach (byte[] table in broken)
        {
            bool found = ItemVariationStore.TryReadCff2(table, 0, out ItemVariationStore read);
            bool first = read.TryGetDataCounts(0, out _, out int firstRegions);
            bool second = read.TryGetDataCounts(1, out _, out int secondRegions);
            Expect(found && first && second && read.RegionCount == 0 && read.AxisCount == 0 && firstRegions == 2 && secondRegions == 1, true, "region list", failures);
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task ACutThroughTheHeaderOrTheSubtableOffsetsIsMalformed()
    {
        byte[] store = Store(1, Regions, new ItemData([0, 1], 1, [[1, 2], [3, 4]]), new ItemData([2], 0, [[5]]));
        int offsetsEnd = 8 + (4 * 2);
        List<string> failures = [];

        for (int length = 0; length < offsetsEnd; length++)
        {
            Expect(IsReadable(store.AsMemory(0, length)), false, $"cut to {length}", failures);
        }

        // Past them, what is cut off is a region list or a subtable, and only that part is lost.
        for (int length = offsetsEnd; length <= store.Length; length++)
        {
            Expect(IsReadable(store.AsMemory(0, length)), true, $"cut to {length}", failures);
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task AStoreWithABrokenHeaderIsMalformed()
    {
        byte[] store = Store(1, Regions, new ItemData([0, 1], 1, [[1, 2]]));
        List<string> failures = [];

        Expect(IsReadable(Patch(store, 0, 0, 2)), false, "format 2", failures);
        Expect(IsReadable(Patch(store, 6, 0xFF, 0xFF)), false, "more subtables than offsets", failures);
        Expect(IsReadable(store), true, "intact", failures);

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task AMalformedRegionListLeavesNoRegionsAndKeepsTheSubtables()
    {
        byte[] store = Store(1, Regions, new ItemData([0, 1], 1, [[1, 2]]));
        int regionList = 8 + 4;
        List<string> failures = [];

        byte[][] broken =
        [
            Patch(store, 2, 0, 0, 0, 0),
            Patch(store, 2, 0, 0, 0xFF, 0xFF),
            Patch(store, regionList, 0xFF, 0xFF),
            Patch(store, regionList + 2, 0xFF, 0xFF),
            Patch(store, regionList, 0, 0),
        ];
        foreach (byte[] bytes in broken)
        {
            bool found = ItemVariationStore.TryRead(bytes, 0, out ItemVariationStore read);
            bool counts = read.TryGetDataCounts(0, out int items, out int regions);
            ItemVariationScalars scalars = read.CreateScalars([F2Dot14(0.5)]);
            Expect(found && counts && (read.AxisCount, read.RegionCount, read.DataCount) == (0, 0, 1) && (items, regions) == (1, 2), true, "subtable counts", failures);
            Expect(scalars.Regions.Length == 0 && scalars.GetDelta(0, 0) == 0, true, "no delta", failures);
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task AMalformedSubtableIsAbsentAndTheOthersStillAnswer()
    {
        byte[] store = Store(1, Regions, new ItemData([0, 1], 1, [[100, 8]]), new ItemData([0], 1, [[40]]));
        int first = 8 + 8 + 4 + (Regions.Length * 6);
        List<string> failures = [];

        byte[][] broken =
        [
            Patch(store, 8, 0, 0, 0xFF, 0xFF),
            Patch(store, first, 0xFF, 0xFF),
            Patch(store, first + 2, 0, 3),
            Patch(store, first + 2, 0x80, 3),
            Patch(store, first + 4, 0xFF, 0xFF),
        ];
        foreach (byte[] bytes in broken)
        {
            bool found = ItemVariationStore.TryRead(bytes, 0, out ItemVariationStore read);
            bool lost = read.TryGetDataCounts(0, out _, out _);
            bool kept = read.TryGetDataCounts(1, out _, out int regions);
            ItemVariationScalars scalars = read.CreateScalars([F2Dot14(1)]);
            Expect(found && !lost && kept && regions == 1 && read.DataCount == 2, true, "subtables", failures);
            Expect(scalars.GetDelta(0, 0) == 0 && scalars.GetDelta(1, 0) == 40F, true, "deltas", failures);
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task LargeLongWordDeltasAreSummedWithoutLosingPrecision()
    {
        // 2^24 + 1 is not a float, so a sum in floats would make this 0.
        ItemVariationStore store = Read(Store(1, Regions, new ItemData([0, 0], 2, [[16_777_217, -16_777_216]], LongWords: true)));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(1)]);

        await Assert.That(scalars.GetDelta(0, 0)).IsEqualTo(1F);
    }

    [Test]
    public async Task AStoreWithNoAxesAndNoRegionsIsEmpty()
    {
        ItemVariationStore store = Read(Store(0, []));

        ItemVariationScalars scalars = store.CreateScalars([F2Dot14(1)]);

        await Assert.That((store.AxisCount, store.RegionCount, store.DataCount)).IsEqualTo((0, 0, 0));
        await Assert.That(scalars.Regions.Length).IsEqualTo(0);
    }

    [Test]
    public async Task ReadingAndAskingForDeltasAllocateNothing()
    {
        byte[] store = Store(1, Regions, new ItemData([0, 1], 1, [[100, 8]]));
        ItemVariationScalars scalars = Read(store).CreateScalars([F2Dot14(0.5)]);
        MeasureAllocation(store, scalars);

        long allocated = MeasureAllocation(store, scalars);

        await Assert.That(allocated).IsEqualTo(0L);
    }

    private static long MeasureAllocation(byte[] store, ItemVariationScalars scalars)
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        ItemVariationStore.TryRead(store, 0, out ItemVariationStore read);
        read.TryGetDataCounts(0, out _, out _);
        scalars.GetDelta(0, 0);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static ItemVariationStore Read(byte[] bytes)
        => ItemVariationStore.TryRead(bytes, 0, out ItemVariationStore store)
            ? store
            : throw new InvalidOperationException("The test store is malformed.");

    private static bool IsReadable(ReadOnlyMemory<byte> bytes) => ItemVariationStore.TryRead(bytes, 0, out _);

    private static bool ReadsCff2WithTwoSubtables(byte[] table)
        => ItemVariationStore.TryReadCff2(table, 0, out ItemVariationStore store)
            && store.DataCount == 2
            && store.TryGetDataCounts(0, out _, out int first)
            && store.TryGetDataCounts(1, out _, out int second)
            && (first, second) == (2, 1);

    // Replaces the bytes at a position.
    private static byte[] Patch(byte[] bytes, int position, params byte[] values)
    {
        byte[] copy = [.. bytes];
        values.CopyTo(copy, position);
        return copy;
    }

    private static void Expect(bool actual, bool expected, string name, List<string> failures)
    {
        if (actual != expected)
        {
            failures.Add($"{name}: read {actual}, expected {expected}");
        }
    }
}
