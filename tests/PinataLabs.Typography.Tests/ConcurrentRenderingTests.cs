// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class ConcurrentRenderingTests
{
    private const int Threads = 8;
    private const int Iterations = 40;

    [Test]
    public async Task ParallelRenderingWithStandardHintingDoesNotThrowAndIsDeterministic()
    {
        for (int run = 0; run < Iterations; run++)
        {
            // A fresh collection per run so that the glyph and interpreter caches start cold.
            Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(15);
            List<Vector2>[] results = new List<Vector2>[Threads];
            using Barrier barrier = new(Threads);
            Parallel.For(
                0,
                Threads,
                new ParallelOptions { MaxDegreeOfParallelism = Threads },
                i =>
                {
                    GlyphRenderer renderer = new();
                    TextOptions options = new(font) { HintingMode = HintingMode.Standard };
                    barrier.SignalAndWait();
                    TextRenderer.RenderTextTo(renderer, "Hello World", options);
                    results[i] = renderer.ControlPoints;
                });

            await Assert.That(results[0].Count).IsGreaterThan(0);
            for (int i = 1; i < results.Length; i++)
            {
                await Assert.That(results[i].SequenceEqual(results[0])).IsTrue();
            }
        }
    }
}
