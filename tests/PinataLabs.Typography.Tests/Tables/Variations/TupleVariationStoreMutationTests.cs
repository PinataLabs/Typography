// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Fuzz;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Runs the oracle of the fuzz harness for the tuple variation store reader, the 'gvar' table and the 'cvar' table over the
/// committed corpus, and over a few thousand mutations of its seeds made with a fixed seed. The readers follow offsets and counts
/// from untrusted data, so they must only return <see langword="false"/> for malformed data, allocate for one tuple at a time, and
/// give finite deltas for every glyph that they answer for. Each input must also finish in time.
/// </summary>
public class TupleVariationStoreMutationTests
{
    private static readonly TimeSpan MaxInputTime = TimeSpan.FromSeconds(2);

    [Test]
    public async Task EveryCorpusInputMeetsTheContract()
    {
        List<string> failures = [];
        string[] files = CorpusFiles();
        foreach (string file in files)
        {
            Check(File.ReadAllBytes(file), Path.GetFileName(file), failures);
        }

        await Assert.That(files.Length).IsGreaterThan(5);
        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task EverySeedIsReadAsWhatItIs()
    {
        // A seed that no reader takes would fuzz nothing.
        List<string> rejected = [];
        foreach (string file in CorpusFiles())
        {
            if (TupleVariationStoreOracle.Run(File.ReadAllBytes(file)) == "rejected")
            {
                rejected.Add(Path.GetFileName(file));
            }
        }

        await Assert.That(rejected).IsEmpty();
    }

    [Test]
    [Arguments(3000, 127)]
    [Arguments(3000, 130)]
    public async Task MutatedSeedsMeetTheContract(int mutations, int seed)
    {
        byte[][] seeds = [.. CorpusFiles().Where(file => Path.GetFileName(file).StartsWith("seed-", StringComparison.Ordinal)).Select(File.ReadAllBytes)];
        Mutator mutator = new(seed);
        List<string> failures = [];
        for (int i = 0; i < mutations && failures.Count < 10; i++)
        {
            byte[] parent = seeds[i % seeds.Length];
            Check(mutator.Mutate(parent, seeds, 2 * parent.Length), $"mutation {i} of seed {i % seeds.Length}", failures);
        }

        await Assert.That(failures).IsEmpty();
    }

    private static string[] CorpusFiles()
        => [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "FuzzCorpus", TupleVariationStoreOracle.Target)).OrderBy(file => file, StringComparer.Ordinal)];

    // A failure names the input in base 64, so it can be saved into the corpus and replayed.
    private static void Check(byte[] input, string name, List<string> failures)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            TupleVariationStoreOracle.Run(input);
        }
        catch (FuzzFindingException e)
        {
            failures.Add($"{name}: {e.Message} {e.InnerException}\n{Convert.ToBase64String(input)}");
            return;
        }

        if (stopwatch.Elapsed > MaxInputTime)
        {
            failures.Add($"{name}: took {stopwatch.ElapsedMilliseconds} ms\n{Convert.ToBase64String(input)}");
        }
    }
}
