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
/// ScriptRunContext.ttf has GSUB and GPOS lookups that apply only when a glyph of another script or of the other
/// direction is next to the glyph they change: a context, a ligature, a pair, a mark and its base, and a cursive join. A
/// caller of HarfBuzz shapes each run of one script and one direction in a buffer of its own, so none of them applies
/// across the boundary of two runs (issue #194). In one buffer of one script, each of them applies.
/// </summary>
public class ScriptRunContextTests
{
    private const string ScriptRuns = "";

    private const string BidiRuns = "bidi";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, ScriptClass> Scripts = new()
    {
        ["Latn"] = ScriptClass.Latin,
        ["Hani"] = ScriptClass.Han,
        ["Hebr"] = ScriptClass.Hebrew,
    };

    private static readonly JsonElement[][] Rows = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ScriptRunContextHarfBuzzReference),
        Options)!.Rows;

    // Each row is a case, a text, how the text is split, and the glyph ID, x and y of each glyph. TextShaper.Shape
    // splits a text without a script into script runs, and does not split a text with one. It does not split a text
    // by direction.
    [Test]
    public async Task ShaperGivesTheGlyphsAndPlacesThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.ScriptRunContextFile).CreateFont(12);
        List<string> different = [];
        foreach (JsonElement[] row in Rows.Where(row => row[2].GetString() != BidiRuns))
        {
            string text = row[1].GetString()!;
            string split = row[2].GetString()!;
            ShapingOptions options = new() { Script = split == ScriptRuns ? null : Scripts[split] };
            string actual = Describe(Places(TextShaper.Shape(font, text, options)));
            if (Describe(row[3]) != actual)
            {
                different.Add($"{row[0].GetString()} {Name(split)} expected [{Describe(row[3])}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // The layout splits the text into runs of one script and one direction.
    [Test]
    public async Task LayoutGivesTheGlyphsAndPlacesThatHarfBuzzGivesTheRuns()
    {
        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = new FontCollection().Add(TestFonts.ScriptRunContextFile).CreateFont(72 * 1000);
        List<string> different = [];
        foreach (JsonElement[] row in Rows.Where(row => row[2].GetString() is ScriptRuns or BidiRuns))
        {
            string text = row[1].GetString()!;
            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));
            string actual = Describe(layout.Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y))));
            if (Describe(row[3]) != actual)
            {
                different.Add($"{row[0].GetString()} {Name(row[2].GetString()!)} expected [{Describe(row[3])}] actual [{actual}]");
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

    private static string Describe(JsonElement glyphs)
        => Describe(glyphs.EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())));

    private static string Describe(IEnumerable<(int Id, int X, int Y)> glyphs)
        => string.Join(' ', glyphs.Select(g => $"{g.Id}@{g.X},{g.Y}"));

    private static string Name(string split) => split == ScriptRuns ? "runs" : split;

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
