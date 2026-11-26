// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SharpFuzz;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// The fuzz harness of the font program API. Without arguments it is a libFuzzer target and fuzzes the target
/// that TYPOGRAPHY_FUZZ_TARGET names. The commands <c>replay</c>, <c>mutate</c> and <c>seeds</c> run without libFuzzer.
/// </summary>
internal static class Program
{
    private const string Usage = """
        Usage:
          (no arguments)                      libFuzzer target; TYPOGRAPHY_FUZZ_TARGET selects the target
          replay <target> <file or directory> run saved inputs through the oracle and group the outcomes
          mutate <target> <seconds> <corpus> <artifacts> [threads] [seed]
                                              in-process mutation fuzzing without libFuzzer
          seeds <test fonts directory> <corpus root>
                                              write the seed corpus from the test fonts
        Targets: program, type1-plain, item-variation-store
        """;

    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            string target = Environment.GetEnvironmentVariable("TYPOGRAPHY_FUZZ_TARGET") ?? FontProgramOracle.Program;
            if (!FontProgramOracle.IsKnown(target))
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            return Fuzz(target);
        }

        try
        {
            return args[0] switch
            {
                "replay" when args.Length == 3 && FontProgramOracle.IsKnown(args[1]) => Replay(args[1], args[2]),
                "mutate" when args.Length >= 5 && FontProgramOracle.IsKnown(args[1])
                    => MutationRunner.Run(
                        args[1],
                        TimeSpan.FromSeconds(int.Parse(args[2])),
                        args[3],
                        args[4],
                        args.Length > 5 ? int.Parse(args[5]) : Math.Max(1, Environment.ProcessorCount / 2),
                        args.Length > 6 ? int.Parse(args[6]) : Environment.TickCount),
                "seeds" when args.Length == 3 => SeedCorpus.Write(args[1], args[2]),
                _ => ShowUsage(),
            };
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException)
        {
            Console.Error.WriteLine(e.Message);
            return 2;
        }
    }

    private static int ShowUsage()
    {
        Console.Error.WriteLine(Usage);
        return 2;
    }

    // Nothing in the instrumented library may run before Fuzzer.LibFuzzer.Run, so this method is not inlined into Main.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Fuzz(string target)
    {
        Fuzzer.LibFuzzer.Run(data => FontProgramOracle.Run(target, data));
        return 0;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Replay(string target, string path)
    {
        string[] files = Directory.Exists(path)
            ? [.. Directory.GetFiles(path).OrderBy(file => file, StringComparer.Ordinal)]
            : [path];

        Dictionary<string, int> outcomes = new(StringComparer.Ordinal);
        foreach (string file in files)
        {
            byte[] data = File.ReadAllBytes(file);
            string outcome;
            try
            {
                outcome = FontProgramOracle.Run(target, data);
                Console.WriteLine($"{Path.GetFileName(file)}\t{data.Length} bytes\t{outcome}");
                outcome = outcome.StartsWith("loaded", StringComparison.Ordinal) ? "loaded" : outcome;
            }
            catch (FuzzFindingException e)
            {
                outcome = StackSummary.Signature(e);
                Console.WriteLine($"{Path.GetFileName(file)}\t{data.Length} bytes\tFINDING\t{e.Message}");
                Console.WriteLine($"    {e.InnerException?.ToString() ?? e.StackTrace}");
            }

            outcomes[outcome] = outcomes.GetValueOrDefault(outcome) + 1;
        }

        Console.WriteLine($"{files.Length} input(s), {outcomes.Count} distinct outcome(s):");
        foreach ((string outcome, int count) in outcomes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  {outcome}  x{count}");
        }

        return 0;
    }
}
