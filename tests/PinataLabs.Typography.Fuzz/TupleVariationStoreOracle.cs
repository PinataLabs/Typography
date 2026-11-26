// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Runs one input through the tuple variation store reader, the 'gvar' table and the 'cvar' table. The readers follow offsets
/// and counts from untrusted data, so each input is read as the variation data of a glyph at several offsets, as a 'gvar'
/// table, and as a 'cvar' table, with one to three axes. Reading may return <see langword="false"/> and nothing else. A store
/// that is read allocates nothing until it is asked for its tuples, and then it allocates for one tuple at a time, no more than
/// the data and the number of points can fill. A broken rule throws <see cref="FuzzFindingException"/>.
/// </summary>
/// <remarks>
/// The fuzz harness and the deterministic mutation test in the test project both compile this file,
/// so a fuzz finding and a CI failure are judged by the same rules.
/// </remarks>
internal static class TupleVariationStoreOracle
{
    /// <summary>
    /// The input is the variation data of a glyph, a 'gvar' table, or a 'cvar' table.
    /// </summary>
    public const string Target = "tuple-variation-store";

    // A tuple count is 12 bits.
    private const int MaxTupleCount = 4095;

    // The points that a tuple with no point numbers covers: a small glyph, and one that has more points than any input holds.
    private static readonly int[] PointCounts = [8, 300];

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

    // A square of four points and its phantom points, and the glyph IDs to ask the table about.
    private static readonly Vector2[] Square = [new(0, 0), new(100, 0), new(100, 100), new(0, 100), default, default, default, default];

    private static readonly ushort[] SquareEnd = [3];

    // The control values that the 'cvar' table varies.
    private static readonly short[] ControlValues = [100, 200, 300, -400, 500, 600, 700, 800, 900, 1000, 1100, 1200];

    /// <summary>
    /// Runs the input.
    /// </summary>
    /// <param name="data">The input.</param>
    /// <returns>What happened, for replay: a rejection or a count of what was read.</returns>
    /// <exception cref="FuzzFindingException">The input broke the contract.</exception>
    public static string Run(ReadOnlySpan<byte> data)
    {
        byte[] input = data.ToArray();
        int stores = 0;
        int tables = 0;
        try
        {
            for (int axisCount = 1; axisCount <= 3; axisCount++)
            {
                foreach (int offset in Offsets(input.Length))
                {
                    stores += CheckStore(input, offset, 0, axisCount) ? 1 : 0;
                    stores += CheckStore(input, offset, 4, axisCount) ? 1 : 0;
                }

                tables += CheckGvar(input, axisCount) ? 1 : 0;
                tables += CheckCvar(input, axisCount) ? 1 : 0;
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

        return stores + tables == 0 ? "rejected" : $"loaded {stores} stores and {tables} tables";
    }

    private static int[] Offsets(int length)
        => [0, 1, 2, 4, 6, 8, length / 2, length - 1, length, length + 1, -1];

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

    // The store starts at the offset, with its count at the position, and its shared tuples are the end of the input.
    private static bool CheckStore(byte[] input, int offset, int position, int axisCount)
    {
        if (offset < 0 || offset > input.Length)
        {
            return false;
        }

        ReadOnlyMemory<byte> block = input.AsMemory(offset);
        ReadOnlyMemory<byte> shared = input.AsMemory(Math.Max(0, input.Length - (2 * axisCount * 2)));
        TupleVariationStore store = default;
        bool read = false;
        long allocated = Allocated(() => read = TupleVariationStore.TryRead(block, position, axisCount, shared, out store));
        if (allocated > 0)
        {
            throw new FuzzFindingException($"Reading a store at {offset} allocated {allocated} bytes.");
        }

        if (!read)
        {
            if (store.TupleCount != 0)
            {
                throw new FuzzFindingException($"A store that was not read has {store.TupleCount} tuples.");
            }

            return false;
        }

        if (store.TupleCount is < 0 or > MaxTupleCount)
        {
            throw new FuzzFindingException($"A store of {block.Length} bytes has {store.TupleCount} tuples.");
        }

        foreach (int pointCount in PointCounts)
        {
            foreach (short[] coordinates in Coordinates)
            {
                CheckTuples(store, block.Length, coordinates, pointCount, hasY: true);
                CheckTuples(store, block.Length, coordinates, pointCount, hasY: false);
            }
        }

        return true;
    }

    private static void CheckTuples(TupleVariationStore store, int length, short[] coordinates, int pointCount, bool hasY)
    {
        int tuples = 0;
        long allocated = Allocated(() =>
        {
            tuples = 0;
            TupleVariationStore.Reader reader = store.CreateReader(coordinates, pointCount, hasY);
            while (reader.TryRead(out TupleVariation variation))
            {
                tuples++;
                CheckTuple(variation, pointCount, hasY);
            }
        });

        // A tuple holds its point numbers, which are a word each and a byte of data at least, and its deltas, which are as many.
        long allowed = (tuples + 1) * (512L + (8L * pointCount) + (8L * length));
        if (allocated > allowed || tuples > store.TupleCount)
        {
            throw new FuzzFindingException($"Reading {tuples} of {store.TupleCount} tuples allocated {allocated} bytes, above {allowed}.");
        }
    }

    private static void CheckTuple(TupleVariation variation, int pointCount, bool hasY)
    {
        if (!(variation.Scalar > 0 && variation.Scalar <= 1))
        {
            throw new FuzzFindingException($"A tuple has the scalar {variation.Scalar}.");
        }

        int count = variation.Points?.Length ?? pointCount;
        if (variation.DeltaX.Length != count || (hasY && variation.DeltaY?.Length != count) || (!hasY && variation.DeltaY is not null))
        {
            throw new FuzzFindingException($"A tuple of {count} points has {variation.DeltaX.Length} x deltas and {variation.DeltaY?.Length} y deltas.");
        }

        if (variation.Points is not null)
        {
            for (int i = 1; i < variation.Points.Length; i++)
            {
                if (variation.Points[i] < variation.Points[i - 1])
                {
                    throw new FuzzFindingException($"The point numbers of a tuple fall from {variation.Points[i - 1]} to {variation.Points[i]}.");
                }
            }
        }
    }

    private static bool CheckGvar(byte[] input, int axisCount)
    {
        GvarTable? table = GvarTable.Parse(input, axisCount);
        if (table is null)
        {
            return false;
        }

        foreach (int glyphId in (ReadOnlySpan<int>)[0, 1, 2, 7, input.Length, -1, int.MaxValue, int.MinValue])
        {
            foreach (short[] coordinates in Coordinates)
            {
                CheckDeltas(table, glyphId, coordinates, Square, SquareEnd);
                CheckDeltas(table, glyphId, coordinates, Square, []);
            }
        }

        return true;
    }

    private static void CheckDeltas(GvarTable table, int glyphId, short[] coordinates, Vector2[] original, ushort[] endPoints)
    {
        Vector2[] deltas = new Vector2[original.Length];
        bool read = table.TryGetDeltas(glyphId, coordinates, original, endPoints, deltas);
        foreach (Vector2 delta in deltas)
        {
            if (!float.IsFinite(delta.X) || !float.IsFinite(delta.Y) || (!read && delta != default))
            {
                throw new FuzzFindingException($"Glyph {glyphId} has the delta {delta}{(read ? string.Empty : ", and the table says that its data is malformed")}.");
            }
        }
    }

    private static bool CheckCvar(byte[] input, int axisCount)
    {
        CvarTable? table = CvarTable.Parse(input, axisCount);
        if (table is null)
        {
            return false;
        }

        foreach (short[] coordinates in Coordinates)
        {
            short[]? values = table.CreateControlValues(ControlValues, coordinates);
            if (values is not null && values.Length != ControlValues.Length)
            {
                throw new FuzzFindingException($"{ControlValues.Length} control values became {values.Length}.");
            }
        }

        return true;
    }
}
