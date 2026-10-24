// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.TrueType.Glyphs;

namespace PinataLabs.Typography.Tests.Tables.General.Glyphs;

public class EmptyGlyphLoaderTests
{
    [Test]
    public async Task EmptyGlyphTakesTheBoundsOfGlyphZero()
    {
        var glyphZeroBounds = new Bounds(0, -200, 500, 700);
        var fallback = new Bounds(1, 2, 3, 4);
        EmptyGlyphLoader empty = new(fallback);
        GlyphTable table = new([new SimpleGlyphLoader(glyphZeroBounds), empty]);

        GlyphVector glyph = table.GetGlyph(1);

        await Assert.That(glyph.Bounds).IsEqualTo(glyphZeroBounds);
        await Assert.That(glyph.ControlPoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task EmptyGlyphZeroUsesTheFallbackBounds()
    {
        var fallback = new Bounds(1, 2, 3, 4);
        EmptyGlyphLoader empty = new(fallback);
        GlyphTable table = new([empty, empty]);

        GlyphVector glyph = table.GetGlyph(1);

        await Assert.That(glyph.Bounds).IsEqualTo(fallback);
        await Assert.That(glyph.ControlPoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentCallsWhileGlyphZeroLoadsGetGlyphZeroBounds()
    {
        var glyphZeroBounds = new Bounds(0, -200, 500, 700);
        var fallback = new Bounds(1, 2, 3, 4);
        EmptyGlyphLoader empty = new(fallback);
        using BlockingGlyphTable table = new(glyphZeroBounds, empty);

        // The first thread blocks inside GetGlyph(0) until the second thread has finished.
        Task<GlyphVector> first = Task.Run(() => empty.CreateGlyph(table));
        await Assert.That(table.FirstCallStarted.Wait(TimeSpan.FromSeconds(10))).IsTrue();

        GlyphVector second = await Task.Run(() => empty.CreateGlyph(table));
        table.Release.Set();
        GlyphVector firstResult = await first;

        await Assert.That(second.Bounds).IsEqualTo(glyphZeroBounds);
        await Assert.That(second.ControlPoints).IsNotNull();
        await Assert.That(firstResult.Bounds).IsEqualTo(glyphZeroBounds);
        await Assert.That(firstResult.ControlPoints).IsNotNull();
    }

    private sealed class BlockingGlyphTable : GlyphTable, IDisposable
    {
        private readonly Bounds glyphZeroBounds;
        private int calls;

        public BlockingGlyphTable(Bounds glyphZeroBounds, EmptyGlyphLoader empty)
            : base([new SimpleGlyphLoader(glyphZeroBounds), empty])
            => this.glyphZeroBounds = glyphZeroBounds;

        public ManualResetEventSlim FirstCallStarted { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        internal override GlyphVector GetGlyph(int index)
        {
            if (index == 0 && Interlocked.Increment(ref this.calls) == 1)
            {
                this.FirstCallStarted.Set();
                this.Release.Wait(TimeSpan.FromSeconds(10));
            }

            return GlyphVector.Empty(this.glyphZeroBounds);
        }

        public void Dispose()
        {
            this.FirstCallStarted.Dispose();
            this.Release.Dispose();
        }
    }
}
