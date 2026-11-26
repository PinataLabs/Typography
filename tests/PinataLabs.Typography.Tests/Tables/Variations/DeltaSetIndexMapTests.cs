// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Variations;
using static PinataLabs.Typography.Tests.Tables.Variations.VariationBytes;

namespace PinataLabs.Typography.Tests.Tables.Variations;

public class DeltaSetIndexMapTests
{
    [Test]
    public async Task EveryEntrySizeAndInnerBitCountSplitsAnEntryIntoItsTwoIndexes()
    {
        List<string> failures = [];

        foreach (int format in new[] { 0, 1 })
        {
            for (int entrySize = 1; entrySize <= 4; entrySize++)
            {
                for (int innerBitCount = 1; innerBitCount <= 16; innerBitCount++)
                {
                    if (innerBitCount > 8 * entrySize)
                    {
                        continue;
                    }

                    // The largest inner index, then an outer index that fills the rest of the entry.
                    int innerMax = (1 << innerBitCount) - 1;
                    int outerMax = (int)Math.Min((1L << ((8 * entrySize) - innerBitCount)) - 1, 1 << 14);

                    (int Outer, int Inner)[] entries = [(0, 0), (0, innerMax), (outerMax, 0), (outerMax, innerMax), (outerMax / 2, innerMax / 3)];
                    byte[] bytes = Map(format, entrySize, innerBitCount, entries);
                    if (!DeltaSetIndexMap.TryRead(bytes, 0, out DeltaSetIndexMap map) || map.Count != entries.Length)
                    {
                        failures.Add($"format {format}, size {entrySize}, {innerBitCount} bits: not read");
                        continue;
                    }

                    for (int i = 0; i < entries.Length; i++)
                    {
                        bool found = map.TryGet(i, out int outer, out int inner);
                        if (!found || (outer, inner) != entries[i])
                        {
                            failures.Add($"format {format}, size {entrySize}, {innerBitCount} bits, entry {i}: ({outer}, {inner}), expected {entries[i]}");
                        }
                    }
                }
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task AnIndexPastTheLastEntryTakesTheLastEntry()
    {
        byte[] bytes = Map(0, 2, 4, (0, 1), (1, 2), (3, 4));
        DeltaSetIndexMap.TryRead(bytes, 0, out DeltaSetIndexMap map);

        bool last = map.TryGet(2, out int lastOuter, out int lastInner);
        bool past = map.TryGet(3, out int pastOuter, out int pastInner);
        bool far = map.TryGet(int.MaxValue, out int farOuter, out int farInner);

        await Assert.That(last && past && far).IsTrue();
        await Assert.That((lastOuter, lastInner)).IsEqualTo((3, 4));
        await Assert.That((pastOuter, pastInner)).IsEqualTo((3, 4));
        await Assert.That((farOuter, farInner)).IsEqualTo((3, 4));
    }

    [Test]
    public async Task ANegativeIndexHasNoEntry()
    {
        DeltaSetIndexMap.TryRead(Map(0, 1, 4, (1, 1)), 0, out DeltaSetIndexMap map);

        bool found = map.TryGet(-1, out int outer, out int inner);

        await Assert.That(found).IsFalse();
        await Assert.That((outer, inner)).IsEqualTo((0, 0));
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    public async Task AMapWithNoEntriesMapsNothing(int format)
    {
        bool read = DeltaSetIndexMap.TryRead(Map(format, 2, 4), 0, out DeltaSetIndexMap map);

        await Assert.That(read).IsTrue();
        await Assert.That(map.Count).IsEqualTo(0);
        await Assert.That(map.TryGet(0, out _, out _)).IsFalse();
        await Assert.That(default(DeltaSetIndexMap).TryGet(0, out _, out _)).IsFalse();
    }

    [Test]
    public async Task AMapReadsAtAnOffsetOfATable()
    {
        byte[] map = Map(1, 3, 8, (2, 3));
        byte[] table = [.. new byte[9], .. map, 0xEE];

        bool found = DeltaSetIndexMap.TryRead(table, 9, out DeltaSetIndexMap read);
        read.TryGet(0, out int outer, out int inner);

        await Assert.That(found).IsTrue();
        await Assert.That((outer, inner)).IsEqualTo((2, 3));
    }

    [Test]
    public async Task AMapWithABrokenHeaderOrTooFewEntryBytesIsMalformed()
    {
        byte[] format0 = Map(0, 2, 4, (1, 1), (2, 2));
        byte[] format1 = Map(1, 2, 4, (1, 1), (2, 2));
        List<string> failures = [];

        for (int length = 0; length < format0.Length; length++)
        {
            Expect(DeltaSetIndexMap.TryRead(format0.AsMemory(0, length), 0, out _), false, $"format 0 cut to {length}", failures);
        }

        for (int length = 0; length < format1.Length; length++)
        {
            Expect(DeltaSetIndexMap.TryRead(format1.AsMemory(0, length), 0, out _), false, $"format 1 cut to {length}", failures);
        }

        byte[] format2 = [.. format0];
        format2[0] = 2;
        byte[] hugeCount = [.. format1];
        hugeCount[2] = 0xFF;
        hugeCount[3] = 0xFF;
        hugeCount[4] = 0xFF;
        hugeCount[5] = 0xFF;
        Expect(DeltaSetIndexMap.TryRead(format2, 0, out _), false, "format 2", failures);
        Expect(DeltaSetIndexMap.TryRead(hugeCount, 0, out _), false, "four billion entries", failures);
        Expect(DeltaSetIndexMap.TryRead(format0, format0.Length + 1, out _), false, "offset past the table", failures);
        Expect(DeltaSetIndexMap.TryRead(format0, -1, out _), false, "negative offset", failures);
        Expect(DeltaSetIndexMap.TryRead(format0, 0, out _), true, "format 0 intact", failures);
        Expect(DeltaSetIndexMap.TryRead(format1, 0, out _), true, "format 1 intact", failures);

        await Assert.That(failures).IsEmpty();
    }

    private static void Expect(bool actual, bool expected, string name, List<string> failures)
    {
        if (actual != expected)
        {
            failures.Add($"{name}: read {actual}, expected {expected}");
        }
    }
}
