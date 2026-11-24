// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Hand-built TrueType fonts with hostile data that fontTools cannot write.
/// </summary>
public class TrueTypeProgramTests
{
    [Test]
    public async Task NameLookupTakesLinearTimeInThePostTable()
    {
        // Every glyph is named "zz". The 100,000 strings after it are all "a", which no glyph uses.
        const int glyphCount = 65535;
        ushort[] nameIndices = Enumerable.Repeat((ushort)258, glyphCount).ToArray();
        byte[] post = TrueTypeBuilder.Post(nameIndices, ["zz", .. Enumerable.Repeat("a", 100_000)]);
        FontProgram font = FontProgram.Load(TrueTypeBuilder.Build(Enumerable.Repeat(Array.Empty<byte>(), glyphCount).ToArray(), post));
        font.TryGetGlyphId("zz", out _);

        Stopwatch stopwatch = Stopwatch.StartNew();
        bool found = font.TryGetGlyphId("a", out _);
        stopwatch.Stop();

        await Assert.That(found).IsFalse();
        await Assert.That(font.TryGetGlyphId("zz", out ushort glyphId)).IsTrue();
        await Assert.That(glyphId).IsEqualTo((ushort)0);
        await Assert.That(font.GetGlyphName(65534)).IsEqualTo("zz");

        // A scan for each matching string took seconds. One pass over the strings and one over the glyphs takes about a millisecond.
        await Assert.That(stopwatch.ElapsedMilliseconds).IsLessThan(500);
    }

    [Test]
    public async Task FlagRepeatPastThePointCountFails()
    {
        byte[] glyph =
        [
            0, 1, 0, 0, 0, 0, 0, 10, 0, 10, // one contour and a bounding box
            0, 2, // the contour ends at point 2, so the glyph has 3 points
            0, 0, // no instructions
            0x3F, 5, // on curve, short positive x and y, repeated 5 more times: 6 points
            1, 2, 3, 4, 5, 6, // x
            1, 2, 3, 4, 5, 6, // y
            0, 0, 0, 0, // padding
        ];
        FontProgram font = FontProgram.Load(TrueTypeBuilder.Build([[], glyph]));
        GlyphOutline outline = new();

        await Assert.That(font.TryWriteOutline(1, ref outline)).IsFalse();
    }

    [Test]
    public async Task OutlineOfMoreThanAMillionSegmentsFails()
    {
        // A contour of 1000 points, in four runs of repeated flags: on curve, x and y one unit on.
        List<byte> simple = [0, 1, 0, 0, 0, 0, 3, 232, 3, 232, 3, 231, 0, 0];
        foreach (int run in (int[])[256, 256, 256, 232])
        {
            simple.AddRange([0x3F, (byte)(run - 1)]);
        }

        simple.AddRange(Enumerable.Repeat((byte)1, 2000));

        // 1100 components of that glyph: about 1.1 million sink calls.
        const int componentCount = 1100;
        List<byte> composite = [0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0];
        for (int i = 0; i < componentCount; i++)
        {
            int flags = 0x0002 | (i + 1 < componentCount ? 0x0020 : 0);
            composite.AddRange([0, (byte)flags, 0, 1, 0, 0]);
        }

        FontProgram font = FontProgram.Load(TrueTypeBuilder.Build([[], [.. simple], [.. composite]]));
        SegmentCounter counter = default;

        await Assert.That(font.TryWriteOutline(1, ref counter)).IsTrue();
        await Assert.That(counter.Calls).IsEqualTo(1001);

        counter = default;
        await Assert.That(font.TryWriteOutline(2, ref counter)).IsFalse();
        await Assert.That(counter.Calls).IsLessThanOrEqualTo(1_000_000);
    }

    [Test]
    public async Task PointLookupsOfACompositeCountAgainstTheOutlineBudget()
    {
        // One contour of 65,535 points, all at the origin: 256 runs of a repeated flag, on curve, x and y unchanged.
        List<byte> large = [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFE, 0, 0];
        for (int run = 0; run < 256; run++)
        {
            large.AddRange([0x39, (byte)(run < 255 ? 255 : 254)]);
        }

        byte[] point = [0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x31];

        // The large glyph, then 2000 copies of the one-point glyph, each placed on point 65,534 of the large one.
        // Each placement reads 65,535 points to find that point: 131 million reads for 8,000 sink calls.
        // The reads count against the budget of 1,000,000, so the glyph fails after 15 placements.
        const int componentCount = 2000;
        List<byte> composite = [0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x22, 0, 1, 0, 0];
        for (int i = 0; i < componentCount; i++)
        {
            int flags = 0x0001 | (i + 1 < componentCount ? 0x0020 : 0);
            composite.AddRange([0, (byte)flags, 0, 2, 0xFF, 0xFE, 0, 0]);
        }

        FontProgram font = FontProgram.Load(TrueTypeBuilder.Build([[], [.. large], point, [.. composite]]));
        SegmentCounter counter = default;

        await Assert.That(font.TryWriteOutline(1, ref counter)).IsTrue();
        await Assert.That(counter.Calls).IsEqualTo(65_536);

        counter = default;
        await Assert.That(font.TryWriteOutline(3, ref counter)).IsFalse();
        await Assert.That(counter.Calls).IsLessThan(65_536 + 100);
    }

    private struct SegmentCounter : IGlyphOutlineSink
    {
        public int Calls { get; private set; }

        public void MoveTo(Vector2 point) => this.Calls++;

        public void LineTo(Vector2 point) => this.Calls++;

        public void QuadraticBezierTo(Vector2 control, Vector2 point) => this.Calls++;

        public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point) => this.Calls++;

        public void ClosePath() => this.Calls++;
    }
}
