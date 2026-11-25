// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
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

    // The font has no GSUB table, so its Arabic takes the presentation forms. The font builds what that needs once,
    // on the first run of Arabic text, which here is on every thread at once.
    [Test]
    public async Task ParallelLayoutOfArabicWithoutJoiningFormsIsDeterministic()
    {
        const string text = "بسم الله";
        string path = TestFonts.GetPath("NormalizationArabicFallback.ttf");
        ushort[] expected = GlyphIds(new FontCollection().Add(path).CreateFont(15), text);
        await Assert.That(expected).IsNotEquivalentTo(new ushort[expected.Length]);

        for (int run = 0; run < Iterations; run++)
        {
            Font font = new FontCollection().Add(path).CreateFont(15);
            ushort[][] results = new ushort[Threads][];
            using Barrier barrier = new(Threads);
            Parallel.For(
                0,
                Threads,
                new ParallelOptions { MaxDegreeOfParallelism = Threads },
                i =>
                {
                    barrier.SignalAndWait();
                    results[i] = GlyphIds(font, text);
                });

            foreach (ushort[] result in results)
            {
                await Assert.That(result.SequenceEqual(expected)).IsTrue();
            }
        }
    }

    private static ushort[] GlyphIds(Font font, string text)
        => TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font)).Select(g => g.Glyph.GlyphMetrics.GlyphId).ToArray();
}
