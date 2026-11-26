// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Runs one input through the Item Variation Store reader and the DeltaSetIndexMap reader. The readers follow
/// offsets and counts from untrusted data, so each input is read as a store, as a CFF2 store and as a map, at
/// several offsets. Reading may return <see langword="false"/> and nothing else. A store that is read allocates
/// nothing and answers every question, with no exception: a row that it does not have has the delta 0, and its
/// scalars are in 0 to 1. A broken rule throws <see cref="FuzzFindingException"/>.
/// </summary>
/// <remarks>
/// The fuzz harness and the deterministic mutation test in the test project both compile this file,
/// so a fuzz finding and a CI failure are judged by the same rules.
/// </remarks>
internal static class ItemVariationStoreOracle
{
    /// <summary>
    /// The input is a table that holds a store or a map at offset 0, or at a few other offsets.
    /// </summary>
    public const string Target = "item-variation-store";

    // A subtable or a row count is a 16-bit number.
    private const int MaxCount = ushort.MaxValue;

    // Coordinates in fvar order, where a store has more or fewer axes than a set: none, the default, the extremes
    // of a normalised axis, and values beyond them.
    private static readonly short[][] Coordinates =
    [
        [],
        [0],
        [16384],
        [-16384],
        [8192, -8192, 4096],
        [short.MaxValue, short.MinValue, 1, -1],
    ];

    // Rows to ask each sampled subtable for, besides its last row and the one after it.
    private static readonly int[] Rows = [0, 1, -1, int.MaxValue, int.MinValue];

    /// <summary>
    /// Runs the input.
    /// </summary>
    /// <param name="data">The input.</param>
    /// <returns>What happened, for replay: a rejection or a count of what was read.</returns>
    /// <exception cref="FuzzFindingException">The input broke the contract.</exception>
    public static string Run(ReadOnlySpan<byte> data)
    {
        byte[] table = data.ToArray();
        int stores = 0;
        int maps = 0;
        try
        {
            foreach (int offset in Offsets(table.Length))
            {
                stores += CheckStore(table, offset, cff2: false) ? 1 : 0;
                stores += CheckStore(table, offset, cff2: true) ? 1 : 0;
                maps += CheckMap(table, offset) ? 1 : 0;
            }
        }
        catch (FuzzFindingException)
        {
            throw;
        }
        catch (Exception e)
        {
            throw new FuzzFindingException($"A call threw {e.GetType().Name}.", e);
        }

        return stores + maps == 0 ? "rejected" : $"loaded {stores} stores and {maps} maps";
    }

    private static int[] Offsets(int length)
        => [0, 1, 2, 3, 6, 8, length / 2, length - 1, length, length + 1, -1, int.MaxValue];

    // Gives the bytes that a delegate allocates on this thread. A first run may allocate for a static that the
    // input reaches for the first time, so a second run decides.
    private static long Allocated(Action action)
    {
        long allocated = long.MaxValue;
        for (int i = 0; i < 2 && allocated > 0; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            action();
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        return allocated;
    }

    private static bool CheckStore(byte[] table, int offset, bool cff2)
    {
        ItemVariationStore store = default;
        bool read = false;
        long allocated = Allocated(() => read = cff2
            ? ItemVariationStore.TryReadCff2(table, offset, out store)
            : ItemVariationStore.TryRead(table, offset, out store));
        if (allocated > 0)
        {
            throw new FuzzFindingException($"Reading a store at {offset} allocated {allocated} bytes.");
        }

        if (!read)
        {
            if (store.AxisCount != 0 || store.RegionCount != 0 || store.DataCount != 0)
            {
                throw new FuzzFindingException($"A store that was not read has {store.AxisCount} axes, {store.RegionCount} regions and {store.DataCount} subtables.");
            }

            return false;
        }

        CheckCounts(store, table.Length);
        List<(int Index, int Items, int Regions)> samples = SampleData(store);
        float[] destination = new float[samples.Count == 0 ? 0 : samples.Max(sample => sample.Regions)];
        foreach (short[] coordinates in Coordinates)
        {
            CheckScalars(store, coordinates, samples, destination);
        }

        return true;
    }

    private static void CheckCounts(ItemVariationStore store, int length)
    {
        if (store.AxisCount is < 0 or > MaxCount
            || store.RegionCount is < 0 or > MaxCount
            || store.DataCount is < 0 or > MaxCount
            || (long)store.AxisCount * store.RegionCount * 6 > length
            || (store.AxisCount == 0 && store.RegionCount != 0))
        {
            throw new FuzzFindingException($"A store of {length} bytes has {store.AxisCount} axes, {store.RegionCount} regions and {store.DataCount} subtables.");
        }

        foreach (int index in (ReadOnlySpan<int>)[store.DataCount, -1, int.MaxValue, int.MinValue])
        {
            if (store.TryGetDataCounts(index, out int items, out int regions) || items != 0 || regions != 0)
            {
                throw new FuzzFindingException($"A store of {store.DataCount} subtables has a subtable {index}.");
            }
        }
    }

    // The first and the last subtables: reading them all would take time that no input needs.
    private static List<(int Index, int Items, int Regions)> SampleData(ItemVariationStore store)
    {
        List<(int Index, int Items, int Regions)> samples = [];
        for (int i = 0; i < store.DataCount; i = i == 3 && store.DataCount > 8 ? store.DataCount - 4 : i + 1)
        {
            if (!store.TryGetDataCounts(i, out int items, out int regions) || items is < 0 or > MaxCount || regions is < 0 or > MaxCount)
            {
                throw new FuzzFindingException($"Subtable {i} of {store.DataCount} has {items} rows and {regions} regions.");
            }

            samples.Add((i, items, regions));
        }

        return samples;
    }

    private static void CheckScalars(ItemVariationStore store, short[] coordinates, List<(int Index, int Items, int Regions)> samples, float[] destination)
    {
        ItemVariationScalars scalars = default;
        long allocated = Allocated(() => scalars = store.CreateScalars(coordinates));
        long allowed = (4L * store.RegionCount) + 64;
        if (allocated > allowed)
        {
            throw new FuzzFindingException($"{store.RegionCount} region scalars allocated {allocated} bytes, above {allowed}.");
        }

        if (scalars.Regions.Length != store.RegionCount)
        {
            throw new FuzzFindingException($"A store of {store.RegionCount} regions gave {scalars.Regions.Length} scalars.");
        }

        foreach (float scalar in scalars.Regions)
        {
            // At the default instance a region has the scalar 1 when none of its axes counts, and 0 when one does.
            if (!(scalar >= 0 && scalar <= 1) || (coordinates.Length == 0 && scalar is not (0 or 1)))
            {
                throw new FuzzFindingException($"A region scalar is {scalar} at {string.Join(",", coordinates)}.");
            }
        }

        allocated = Allocated(() => AskForDeltas(store, scalars, samples, destination));
        if (allocated > 0)
        {
            throw new FuzzFindingException($"Asking for deltas allocated {allocated} bytes.");
        }
    }

    private static void AskForDeltas(ItemVariationStore store, ItemVariationScalars scalars, List<(int Index, int Items, int Regions)> samples, float[] destination)
    {
        foreach ((int outer, int items, int regions) in samples)
        {
            foreach (int inner in Rows)
            {
                CheckDelta(scalars, outer, inner, inRange: inner >= 0 && inner < items);
            }

            CheckDelta(scalars, outer, items - 1, inRange: items > 0);
            CheckDelta(scalars, outer, items, inRange: false);
            if (!scalars.TryGetRegionScalars(outer, destination, out int count) || count != regions)
            {
                throw new FuzzFindingException($"Subtable {outer} has {regions} regions, and {count} scalars.");
            }

            for (int i = 0; i < count; i++)
            {
                if (!(destination[i] >= 0 && destination[i] <= 1))
                {
                    throw new FuzzFindingException($"Subtable {outer} gave the scalar {destination[i]}.");
                }
            }

            if (regions > 0 && scalars.TryGetRegionScalars(outer, destination.AsSpan(0, regions - 1), out _))
            {
                throw new FuzzFindingException($"Subtable {outer} gave {regions} scalars to a destination of {regions - 1}.");
            }
        }

        foreach (int outer in (ReadOnlySpan<int>)[store.DataCount, -1, int.MaxValue, int.MinValue])
        {
            CheckDelta(scalars, outer, 0, inRange: false);
            if (scalars.TryGetRegionScalars(outer, destination, out _))
            {
                throw new FuzzFindingException($"A store of {store.DataCount} subtables gave scalars for subtable {outer}.");
            }
        }
    }

    private static void CheckDelta(ItemVariationScalars scalars, int outer, int inner, bool inRange)
    {
        float delta = scalars.GetDelta(outer, inner);
        if (!float.IsFinite(delta) || (!inRange && delta != 0))
        {
            throw new FuzzFindingException($"The delta of ({outer}, {inner}) is {delta}{(inRange ? string.Empty : ", for a row that the store lacks")}.");
        }
    }

    private static bool CheckMap(byte[] table, int offset)
    {
        DeltaSetIndexMap map = default;
        bool read = false;
        long allocated = Allocated(() => read = DeltaSetIndexMap.TryRead(table, offset, out map));
        if (allocated > 0)
        {
            throw new FuzzFindingException($"Reading a map at {offset} allocated {allocated} bytes.");
        }

        if (!read)
        {
            if (map.Count != 0)
            {
                throw new FuzzFindingException($"A map that was not read has {map.Count} entries.");
            }

            return false;
        }

        if (map.Count < 0 || map.Count > table.Length)
        {
            throw new FuzzFindingException($"A map of {table.Length} bytes has {map.Count} entries.");
        }

        allocated = Allocated(() => AskForEntries(map));
        if (allocated > 0)
        {
            throw new FuzzFindingException($"Asking a map for entries allocated {allocated} bytes.");
        }

        return true;
    }

    private static void AskForEntries(DeltaSetIndexMap map)
    {
        bool hasLast = map.TryGet(map.Count - 1, out int lastOuter, out int lastInner);
        foreach (int index in (ReadOnlySpan<int>)[0, 1, map.Count - 1, map.Count, map.Count + 1, int.MaxValue])
        {
            bool found = map.TryGet(index, out int outer, out int inner);
            bool expected = map.Count > 0 && index >= 0;
            if (found != expected || outer < 0 || inner < 0 || (index >= map.Count && found && (outer, inner) != (lastOuter, lastInner)))
            {
                throw new FuzzFindingException($"Entry {index} of {map.Count} gave {found} and ({outer}, {inner}), after the last entry gave ({lastOuter}, {lastInner}).");
            }
        }

        if (map.TryGet(-1, out _, out _) || map.TryGet(int.MinValue, out _, out _) || hasLast != (map.Count > 0))
        {
            throw new FuzzFindingException($"A map of {map.Count} entries has an entry before the first, or has none after all.");
        }
    }
}
