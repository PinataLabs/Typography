// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// ScriptRunContext.ttf has GSUB and GPOS lookups that apply only when a glyph of another script is next to the glyph
/// they change: a context, a ligature, a pair, a mark and its base, and a cursive join. A caller of HarfBuzz shapes each
/// script run in a buffer of its own, so none of them applies across the boundary of two runs (issue #194). In one
/// buffer of one script, each of them applies.
/// </summary>
public class ScriptRunContextTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, ScriptClass> Scripts = new()
    {
        ["Latn"] = ScriptClass.Latin,
        ["Hani"] = ScriptClass.Han,
    };

    private static readonly Font Font = new FontCollection().Add(TestFonts.ScriptRunContextFile).CreateFont(12);

    private static readonly JsonElement[][] Rows = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ScriptRunContextHarfBuzzReference),
        Options)!.Rows;

    // Each row is a case, a text, the script of one buffer or "" for the script runs, and the glyph ID, x and y of each
    // glyph. A text without a script is split into script runs, and a text with one is a single run.
    [Test]
    public async Task GlyphsHaveTheIdsAndPlacesThatHarfBuzzGives()
    {
        List<string> different = [];
        foreach (JsonElement[] row in Rows)
        {
            string text = row[1].GetString()!;
            string script = row[2].GetString()!;
            ShapingOptions options = new() { Script = script.Length == 0 ? null : Scripts[script] };
            string expected = string.Join(' ', row[3].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));
            string actual = string.Join(' ', Places(TextShaper.Shape(Font, text, options)).Select(g => $"{g.Id}@{g.X},{g.Y}"));
            if (expected != actual)
            {
                different.Add($"{row[0].GetString()} {(script.Length == 0 ? "runs" : script)} expected [{expected}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The layout splits the text into script runs as well.
    [Test]
    public async Task LayoutHasTheGlyphsThatHarfBuzzGivesTheScriptRuns()
    {
        List<string> different = [];
        foreach (JsonElement[] row in Rows.Where(row => row[2].GetString()!.Length == 0))
        {
            string text = row[1].GetString()!;
            int[] expected = row[3].EnumerateArray().Select(g => g[0].GetInt32()).ToArray();
            int[] actual = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(Font))
                .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                different.Add($"{row[0].GetString()} expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The place of each glyph: the sum of the advances before it, and its offset.
    private static IEnumerable<(int Id, int X, int Y)> Places(ShapedGlyph[] glyphs)
    {
        int pen = 0;
        foreach (ShapedGlyph glyph in glyphs)
        {
            yield return (glyph.GlyphId, pen + glyph.XOffset, glyph.YOffset);
            pen += glyph.XAdvance;
        }
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
