// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Runs one input through the HVAR, VVAR and MVAR readers, which follow offsets and counts from untrusted data. Each input
/// is read as all three tables. Reading may return <see langword="null"/> and nothing else, and allocates a table and
/// nothing that grows faster than the input. A table that was read answers every question for any coordinates with no
/// exception and no allocation, and makes the same value each time. A table that has no left side bearing mapping leaves a
/// bearing as it was. A broken rule throws <see cref="FuzzFindingException"/>.
/// </summary>
/// <remarks>
/// The fuzz harness and the deterministic mutation test in the test project both compile this file,
/// so a fuzz finding and a CI failure are judged by the same rules.
/// </remarks>
internal static class MetricsVariationOracle
{
    /// <summary>
    /// The input is the bytes of an HVAR, VVAR or MVAR table.
    /// </summary>
    public const string Target = "metrics-variation";

    // Coordinates in fvar order, where a table has more or fewer axes than a set: none, the default, the extremes
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

    private static readonly int[] Glyphs = [0, 1, 2, 3, 255, 256, 1000, ushort.MaxValue, ushort.MaxValue + 1, -1, int.MaxValue, int.MinValue];

    private static readonly ushort[] Advances = [0, 1, 600, ushort.MaxValue];

    private static readonly short[] Bearings = [short.MinValue, -1, 0, 50, short.MaxValue];

    private static readonly Tag[] Tags = [.. new[] { "hasc", "hdsc", "hlgp", "hcla", "hcld", "vasc", "vdsc", "vlgp", "sbxs", "stro", "unds", "undo", "xhgt" }.Select(Tag.Parse), default];

    /// <summary>
    /// Runs the input.
    /// </summary>
    /// <param name="data">The input.</param>
    /// <returns>What happened, for replay: a rejection or the tables that were read.</returns>
    /// <exception cref="FuzzFindingException">The input broke the contract.</exception>
    public static string Run(ReadOnlySpan<byte> data)
    {
        byte[] table = data.ToArray();
        List<string> read = [];
        try
        {
            if (CheckMetrics(table, hvar: true))
            {
                read.Add("HVAR");
            }

            if (CheckMetrics(table, hvar: false))
            {
                read.Add("VVAR");
            }

            if (CheckMvar(table))
            {
                read.Add("MVAR");
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

        return read.Count == 0 ? "rejected" : $"loaded {string.Join(", ", read)}";
    }

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

    // A table object, and for MVAR an array of its value records, which an input of n bytes holds at most n / 8 of.
    private static long AllowedForReading(int length) => (2L * length) + 512;

    // A scalar for each region, and an input of n bytes has at most n / 6 regions.
    private static long AllowedForScalars(int length) => (4L * (length / 6)) + 128;

    private static bool CheckMetrics(byte[] table, bool hvar)
    {
        MetricsVariationTable? read = null;
        long allocated = Allocated(() => read = hvar ? HvarTable.Parse(table) : VvarTable.Parse(table));
        if (allocated > AllowedForReading(table.Length))
        {
            throw new FuzzFindingException($"Reading a {(hvar ? "HVAR" : "VVAR")} table of {table.Length} bytes allocated {allocated} bytes.");
        }

        if (read is null)
        {
            return false;
        }

        foreach (short[] coordinates in Coordinates)
        {
            MetricsVariation variation = default;
            allocated = Allocated(() => variation = read.CreateVariation(coordinates));
            if (allocated > AllowedForScalars(table.Length))
            {
                throw new FuzzFindingException($"The scalars of a table of {table.Length} bytes allocated {allocated} bytes.");
            }

            allocated = Allocated(() => AskForMetrics(variation));
            if (allocated > 0)
            {
                throw new FuzzFindingException($"Asking for advances and bearings allocated {allocated} bytes.");
            }
        }

        return true;
    }

    private static void AskForMetrics(MetricsVariation variation)
    {
        if (variation.IsEmpty)
        {
            throw new FuzzFindingException("The scalars of a table that was read are empty.");
        }

        foreach (int glyph in Glyphs)
        {
            foreach (ushort advance in Advances)
            {
                int first = variation.VaryAdvance(glyph, advance);
                int second = variation.VaryAdvance(glyph, advance);
                if (first != second)
                {
                    throw new FuzzFindingException($"The advance {advance} of glyph {glyph} varies to two values.");
                }
            }

            foreach (short bearing in Bearings)
            {
                short varied = variation.VaryLeadingBearing(glyph, bearing);
                if (varied != variation.VaryLeadingBearing(glyph, bearing) || (!variation.HasLeadingBearings && varied != bearing))
                {
                    throw new FuzzFindingException($"The bearing {bearing} of glyph {glyph} varies to {varied}{(variation.HasLeadingBearings ? string.Empty : ", but the table maps no bearing")}.");
                }
            }
        }
    }

    private static bool CheckMvar(byte[] table)
    {
        MvarTable? read = null;
        long allocated = Allocated(() => read = MvarTable.Parse(table));
        if (allocated > AllowedForReading(table.Length))
        {
            throw new FuzzFindingException($"Reading an MVAR table of {table.Length} bytes allocated {allocated} bytes.");
        }

        if (read is null)
        {
            return false;
        }

        foreach (short[] coordinates in Coordinates)
        {
            MvarVariation variation = default;
            allocated = Allocated(() => variation = read.CreateVariation(coordinates));
            if (allocated > AllowedForScalars(table.Length))
            {
                throw new FuzzFindingException($"The scalars of a table of {table.Length} bytes allocated {allocated} bytes.");
            }

            allocated = Allocated(() => AskForValues(variation));
            if (allocated > 0)
            {
                throw new FuzzFindingException($"Asking for metrics allocated {allocated} bytes.");
            }
        }

        return true;
    }

    private static void AskForValues(MvarVariation variation)
    {
        foreach (Tag tag in Tags)
        {
            foreach (short value in Bearings)
            {
                int first = variation.Apply(tag, value);
                int second = variation.Apply(tag, value);
                if (first != second)
                {
                    throw new FuzzFindingException($"The value {value} of tag {tag.Value:X8} varies to two values.");
                }
            }
        }
    }
}
