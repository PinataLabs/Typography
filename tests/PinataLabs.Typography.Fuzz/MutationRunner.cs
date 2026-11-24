// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Fuzzes in process when libFuzzer is not available. Each thread mutates the seeds and the inputs that loaded,
/// runs the oracle, and saves every finding and every slow input. A watchdog saves an input that runs too long
/// and ends the process, and each thread writes its current input to disk first, so a stack overflow leaves it behind.
/// </summary>
internal static class MutationRunner
{
    private const int MaxLength = 256 * 1024;
    private const int MaxPool = 4096;
    private const int MaxThreads = 64;

    private const int Executions = 0;
    private const int Loaded = 1;
    private const int Slow = 2;
    private const int Longest = 3;

    /// <summary>
    /// An input that takes longer is saved as slow.
    /// </summary>
    private static readonly TimeSpan SlowInput = TimeSpan.FromSeconds(1);

    /// <summary>
    /// An input that takes longer is a hang: the watchdog saves it and ends the process.
    /// </summary>
    private static readonly TimeSpan HangInput = TimeSpan.FromSeconds(20);

    public static int Run(string target, TimeSpan duration, string corpus, string artifacts, int threads, int seed)
    {
        byte[][] seeds = [.. Directory.GetFiles(corpus).OrderBy(file => file, StringComparer.Ordinal).Select(File.ReadAllBytes)];
        if (seeds.Length == 0)
        {
            Console.Error.WriteLine($"No seeds in {corpus}.");
            return 2;
        }

        threads = Math.Clamp(threads, 1, MaxThreads);
        Directory.CreateDirectory(artifacts);
        Console.WriteLine($"target {target}, {seeds.Length} seeds, {threads} threads, seed {seed}, {duration.TotalSeconds} s");

        State state = new(seeds, artifacts);
        Stopwatch clock = Stopwatch.StartNew();
        Thread[] workers = new Thread[threads];
        for (int i = 0; i < threads; i++)
        {
            int index = i;
            workers[i] = new Thread(() => Work(target, state, new Mutator(seed + index), index, clock, duration), 16 * 1024 * 1024)
            {
                IsBackground = true,
                Name = $"fuzz {i}",
            };
            workers[i].Start();
        }

        while (workers.Any(worker => worker.IsAlive))
        {
            Thread.Sleep(1000);
            for (int i = 0; i < threads; i++)
            {
                long started = Interlocked.Read(ref state.Started[i]);
                if (started != 0 && Stopwatch.GetElapsedTime(started) > HangInput)
                {
                    string file = Path.Combine(artifacts, $"hang-{target}-{Hash(state.Current[i])}");
                    File.WriteAllBytes(file, state.Current[i]);
                    Console.WriteLine($"HANG: an input ran over {HangInput.TotalSeconds} s: {file}");
                    Report(state, clock);
                    Environment.Exit(3);
                }
            }

            if ((int)clock.Elapsed.TotalSeconds % 30 == 0)
            {
                Report(state, clock);
            }
        }

        Report(state, clock);
        foreach ((string signature, int count) in state.Findings.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"  FINDING x{count}: {signature}");
        }

        return state.Findings.IsEmpty ? 0 : 1;
    }

    private static void Work(string target, State state, Mutator mutator, int index, Stopwatch clock, TimeSpan duration)
    {
        Random random = new(index);
        string current = Path.Combine(state.Artifacts, $"current-{index}");
        while (clock.Elapsed < duration)
        {
            byte[] parent;
            lock (state.Pool)
            {
                parent = state.Pool[random.Next(state.Pool.Count)];
            }

            byte[] input = mutator.Mutate(parent, state.Seeds, MaxLength);
            state.Current[index] = input;
            File.WriteAllBytes(current, input);

            long started = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref state.Started[index], started);
            string outcome;
            try
            {
                outcome = FontProgramOracle.Run(target, input);
            }
            catch (FuzzFindingException e)
            {
                Interlocked.Exchange(ref state.Started[index], 0);
                string signature = StackSummary.Signature(e);
                int count = state.Findings.AddOrUpdate(signature, 1, (_, n) => n + 1);
                if (count == 1)
                {
                    string file = Path.Combine(state.Artifacts, $"finding-{target}-{Hash(input)}");
                    File.WriteAllBytes(file, input);
                    Console.WriteLine($"FINDING: {signature}\n  {e.Message}\n  {e.InnerException}\n  saved {file}");
                }

                Interlocked.Increment(ref state.Counters[Executions]);
                continue;
            }

            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
            Interlocked.Exchange(ref state.Started[index], 0);
            Interlocked.Increment(ref state.Counters[Executions]);
            if (elapsed > SlowInput && Interlocked.Increment(ref state.Counters[Slow]) <= 20)
            {
                string file = Path.Combine(state.Artifacts, $"slow-{target}-{Hash(input)}");
                File.WriteAllBytes(file, input);
                Console.WriteLine($"SLOW: {elapsed.TotalMilliseconds:F0} ms, {outcome}: {file}");
            }

            if (outcome.StartsWith("loaded", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref state.Counters[Loaded]);

                // Inputs that load reach the glyph code, so mutate them further.
                lock (state.Pool)
                {
                    if (state.Pool.Count < MaxPool)
                    {
                        state.Pool.Add(input);
                    }
                    else
                    {
                        state.Pool[state.Seeds.Count + random.Next(MaxPool - state.Seeds.Count)] = input;
                    }
                }
            }

            long elapsedTicks = (long)elapsed.TotalMicroseconds;
            InterlockedMax(ref state.Counters[Longest], elapsedTicks);
        }

        File.Delete(current);
    }

    private static void InterlockedMax(ref long location, long value)
    {
        long seen = Interlocked.Read(ref location);
        while (value > seen)
        {
            long previous = Interlocked.CompareExchange(ref location, value, seen);
            if (previous == seen)
            {
                return;
            }

            seen = previous;
        }
    }

    private static void Report(State state, Stopwatch clock)
    {
        long executions = Interlocked.Read(ref state.Counters[Executions]);
        Console.WriteLine(
            $"{clock.Elapsed:hh\\:mm\\:ss} execs {executions} ({executions / Math.Max(1, clock.Elapsed.TotalSeconds):F0}/s), "
            + $"loaded {Interlocked.Read(ref state.Counters[Loaded])}, slow {Interlocked.Read(ref state.Counters[Slow])}, longest {Interlocked.Read(ref state.Counters[Longest]) / 1000.0:F1} ms, "
            + $"findings {state.Findings.Values.Sum()} in {state.Findings.Count} groups");
    }

    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private sealed class State
    {
        public State(byte[][] seeds, string artifacts)
        {
            this.Seeds = seeds;
            this.Pool = [.. seeds];
            this.Artifacts = artifacts;
            this.Started = new long[MaxThreads];
            this.Current = new byte[MaxThreads][];
        }

        public IReadOnlyList<byte[]> Seeds { get; }

        public List<byte[]> Pool { get; }

        public string Artifacts { get; }

        public long[] Started { get; }

        public byte[][] Current { get; }

        public ConcurrentDictionary<string, int> Findings { get; } = new(StringComparer.Ordinal);

        // Executions, loads, slow inputs and the longest input in microseconds, updated with Interlocked.
        public long[] Counters { get; } = new long[4];
    }
}
