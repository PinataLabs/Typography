// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Linq;
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
}
