// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class TextRunTests
{
    private const string Text = "This is a long and Honorificabilitudinitatibus califragilisticexpialidocious";

    [Test]
    [Arguments(-1, -1)]
    [Arguments(0, 0)]
    [Arguments(1, 0)]
    [Arguments(1, 1)]
    [Arguments(3, 2)]
    public async Task ThrowsZeroValue(int start, int end)
    {
        Exception? ex = await Assert.That(() => { _ = new TextRun() { Start = start, End = end }.Slice(Text.AsSpan()); }).Throws<Exception>();
        await Assert.That(ex).IsNotNull();
    }

    [Test]
    public async Task SlicesTextCorrectly()
    {
        bool equal = "Honorificabilitudinitatibus".AsSpan().SequenceEqual(new TextRun() { Start = 19, End = 46 }.Slice(Text.AsSpan()));
        await Assert.That(equal).IsTrue();
    }

    [Test]
    public async Task SharedTextOptionsWithTextRunsGiveIdenticalResultsAcrossThreads()
    {
        const int threads = 8;
        Font regular = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(20);
        Font large = new(regular, 32);
        const string text = "Hello parallel world";
        TextOptions shared = new(regular)
        {
            TextRuns =
            [
                new TextRun { Start = 0, End = 5, Font = large },
                new TextRun { Start = 3, End = 12, TextDecorations = TextDecorations.Underline },
                new TextRun { Start = 14, End = 40 },
            ],
        };

        (FontRectangle Advance, List<Vector2> Points) Measure()
        {
            GlyphRenderer renderer = new();
            TextRenderer.RenderTextTo(renderer, text, shared);
            return (TextMeasurer.MeasureAdvance(text, shared), renderer.ControlPoints);
        }

        (FontRectangle Advance, List<Vector2> Points) expected = Measure();
        (FontRectangle Advance, List<Vector2> Points)[] results = new (FontRectangle, List<Vector2>)[threads];
        using Barrier barrier = new(threads);
        Parallel.For(
            0,
            threads,
            new ParallelOptions { MaxDegreeOfParallelism = threads },
            i =>
            {
                barrier.SignalAndWait();
                results[i] = Measure();
            });

        await Assert.That(expected.Points.Count).IsGreaterThan(0);
        foreach ((FontRectangle advance, List<Vector2> points) in results)
        {
            await Assert.That(advance).IsEqualTo(expected.Advance);
            await Assert.That(points.SequenceEqual(expected.Points)).IsTrue();
        }

        // The caller's runs are untouched.
        await Assert.That(shared.TextRuns![0].Font).IsEqualTo(large);
        await Assert.That(shared.TextRuns[1].Font).IsNull();
        await Assert.That(shared.TextRuns[1].End).IsEqualTo(12);
        await Assert.That(shared.TextRuns[2].End).IsEqualTo(40);
    }

    [Test]
    public async Task RendererReceivesDerivedTextRunWithItsData()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(20);
        TextOptions options = new(font)
        {
            TextRuns =
            [
                new TaggedRun { Start = 0, End = 3, Tag = "first" },
                new TaggedRun { Start = 3, End = 6, Tag = "second" },
            ],
        };

        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "abcdef", options);

        string[] tags = renderer.GlyphKeys.Select(k => (k.TextRun as TaggedRun)?.Tag ?? "none").ToArray();
        await Assert.That(tags).IsEquivalentTo(new[] { "first", "first", "first", "second", "second", "second" });
    }

    private sealed class TaggedRun : TextRun
    {
        public string? Tag { get; set; }
    }
}
