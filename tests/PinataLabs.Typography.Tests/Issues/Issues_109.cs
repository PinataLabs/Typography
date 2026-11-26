// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues;

public class Issues_109
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.TextRunContextHarfBuzzReference),
        Options)!;

    // Each row is a word that two text runs style, split after one of its characters, and the glyph IDs that HarfBuzz
    // gives when each run is shaped with the text around it as context. The letters at the boundary join as in one
    // run.
    [Test]
    public async Task WordInTwoTextRunsJoinsAsHarfBuzzJoinsIt()
    {
        Dictionary<string, Font> fonts = [];
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string fontName = row[0].GetString()!;
            if (!fonts.TryGetValue(fontName, out Font? font))
            {
                font = new FontCollection().Add(TestFonts.GetPath(fontName)).CreateFont(12);
                fonts.Add(fontName, font);
            }

            string word = row[1].GetString()!;
            int split = row[2].GetInt32();
            int[] expected = row[3].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            TextRun[] runs = [new TextRun { Start = split, End = word.Length, TextDecorations = TextDecorations.Underline }];
            int[] actual = TextLayout.GenerateLayout(word.AsSpan(), new TextOptions(font) { TextRuns = runs })
                .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', word.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"{fontName} [{codePoints}] split at {split} expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
