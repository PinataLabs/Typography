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
/// Runs the oracle of the fuzz harness for the HVAR, VVAR and MVAR readers over the committed corpus, and over a few
/// thousand mutations of its seeds made with a fixed seed. The readers follow offsets and counts from untrusted data, so
/// they must only return <see langword="null"/> for a malformed table, allocate nothing that grows faster than the table, and
/// answer every question. Each input must also finish in time.
/// </summary>
public class MetricsVariationMutationTests
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
            if (MetricsVariationOracle.Run(File.ReadAllBytes(file)) == "rejected")
            {
                rejected.Add(Path.GetFileName(file));
            }
        }

        await Assert.That(rejected).IsEmpty();
    }

    [Test]
    [Arguments("seed-teko-hvar.bin", "HVAR")]
    [Arguments("seed-sourcesans3vf-hvar.bin", "HVAR")]
    [Arguments("seed-variationmetrics-vvar.bin", "VVAR")]
    [Arguments("seed-variationmetrics-mvar.bin", "MVAR")]
    public async Task ASeedIsReadByTheReaderOfItsTable(string seed, string table)
    {
        string outcome = MetricsVariationOracle.Run(File.ReadAllBytes(Path.Combine(CorpusDirectory(), seed)));

        await Assert.That(outcome).Contains(table);
    }

    [Test]
    [Arguments(3000, 128)]
    [Arguments(3000, 129)]
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

    private static string CorpusDirectory() => Path.Combine(AppContext.BaseDirectory, "FuzzCorpus", MetricsVariationOracle.Target);

    private static string[] CorpusFiles()
        => [.. Directory.GetFiles(CorpusDirectory()).OrderBy(file => file, StringComparer.Ordinal)];

    // A failure names the input in base 64, so it can be saved into the corpus and replayed.
    private static void Check(byte[] input, string name, List<string> failures)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            MetricsVariationOracle.Run(input);
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
