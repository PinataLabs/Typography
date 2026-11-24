// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Fuzz;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Runs the oracle of the fuzz harness over the committed fuzz corpus, and over a few thousand mutations of
/// its seeds made with a fixed seed, so CI checks the contract of issue #74 on every change.
/// The contract: Load throws only <see cref="InvalidFontFileException"/>, glyph calls never throw and agree
/// with each other, and allocation stays under the documented bounds. Each input must also finish in time.
/// </summary>
public class FontProgramMutationTests
{
    private static readonly TimeSpan MaxInputTime = TimeSpan.FromSeconds(2);

    [Test]
    [Arguments(FontProgramOracle.Program)]
    [Arguments(FontProgramOracle.Type1Plain)]
    public async Task EveryCorpusInputMeetsTheContract(string target)
    {
        List<string> failures = [];
        string[] files = CorpusFiles(target);
        foreach (string file in files)
        {
            Check(target, File.ReadAllBytes(file), Path.GetFileName(file), failures);
        }

        await Assert.That(files.Length).IsGreaterThan(2);
        await Assert.That(failures).IsEmpty();
    }

    [Test]
    [Arguments(FontProgramOracle.Program, 3000, 74)]
    [Arguments(FontProgramOracle.Type1Plain, 1500, 75)]
    public async Task MutatedSeedsMeetTheContract(string target, int mutations, int seed)
    {
        byte[][] seeds = [.. CorpusFiles(target).Where(file => Path.GetFileName(file).StartsWith("seed-", StringComparison.Ordinal)).Select(File.ReadAllBytes)];
        Mutator mutator = new(seed);
        List<string> failures = [];
        for (int i = 0; i < mutations && failures.Count < 10; i++)
        {
            byte[] parent = seeds[i % seeds.Length];
            Check(target, mutator.Mutate(parent, seeds, 2 * parent.Length), $"mutation {i} of seed {i % seeds.Length}", failures);
        }

        await Assert.That(failures).IsEmpty();
    }

    private static string[] CorpusFiles(string target)
        => [.. Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "FuzzCorpus", target)).OrderBy(file => file, StringComparer.Ordinal)];

    // A failure names the input in base 64, so it can be saved into the corpus and replayed.
    private static void Check(string target, byte[] input, string name, List<string> failures)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            FontProgramOracle.Run(target, input);
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
