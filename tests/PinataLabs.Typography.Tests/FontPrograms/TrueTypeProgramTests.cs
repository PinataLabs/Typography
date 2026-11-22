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
}
