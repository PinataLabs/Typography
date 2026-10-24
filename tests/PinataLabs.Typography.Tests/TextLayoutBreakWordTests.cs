// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class TextLayoutBreakWordTests
{
    [Test]
    [Arguments("số khung: RRKWCH1UM7XJ00693")]
    [Arguments("số khung RRKWCH1UM7XJ00693")]
    public async Task BreakWordKeepsTokenIntactWhenItFitsOnTheNextLine(string text)
    {
        FontCollection collection = new();
        Font font = collection.Add(TestFonts.OpenSansFile).CreateFont(25);
        int tokenStart = text.IndexOf("RRK", StringComparison.Ordinal);
        List<string> failures = [];

        // Token is ~265px wide, so any wrapping length above that must keep it whole.
        for (int w = 275; w <= 400; w += 5)
        {
            TextOptions options = new(font) { WrappingLength = w, WordBreaking = WordBreaking.BreakWord };
            TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> bounds);
            float[] xs = bounds.ToArray().Skip(tokenStart).Select(x => x.Bounds.X).ToArray();
            if (Enumerable.Range(1, xs.Length - 1).Any(i => xs[i] < xs[i - 1]))
            {
                failures.Add($"w={w}");
            }
        }

        await Assert.That(string.Join(", ", failures)).IsEqualTo(string.Empty);
    }
}
