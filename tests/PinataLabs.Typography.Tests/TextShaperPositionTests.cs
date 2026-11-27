// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The references of the other shaping tests hold the glyph IDs and the places that HarfBuzz gives. The place of a glyph
/// is the sum of the advances before it and its x offset, so <see cref="TextShaper.Shape"/> gives the same places in
/// font units as the layout does, and it gives the same advance for the run.
/// </summary>
public class TextShaperPositionTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, ScriptClass> Scripts = new()
    {
        ["Bali"] = ScriptClass.Balinese,
        ["Deva"] = ScriptClass.Devanagari,
        ["Gonm"] = ScriptClass.MasaramGondi,
        ["Mlym"] = ScriptClass.Malayalam,
    };

    private static readonly Dictionary<string, Font> Fonts = [];

    // Each row of Clusters.harfbuzz.json is a font, an ISO 15924 code, a text, and the glyph ID and x of each glyph.
    [Test]
    public async Task SignsWithoutALetterBeforeThemHaveTheGlyphsAndPlacesThatHarfBuzzGives()
    {
        JsonElement[][] rows = Read(TestFonts.ClustersHarfBuzzReference);
        List<string> different = [];
        foreach (JsonElement[] row in rows)
        {
            string text = row[2].GetString()!;
            ShapedGlyph[] glyphs = TextShaper.Shape(GetFont(row[0].GetString()!), text, new() { Script = Scripts[row[1].GetString()!] });
            string expected = string.Join(' ', row[3].EnumerateArray().Select(g => $"{g[0]}@{g[1]}"));
            string actual = string.Join(' ', Places(glyphs).Select(g => $"{g.Id}@{g.X}"));
            if (expected != actual)
            {
                different.Add($"[{Describe(text)}] expected [{expected}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // Each row of LookupFlags.harfbuzz.json is a case, a text, and the glyph ID, x and y of each glyph.
    [Test]
    public async Task CursiveAndMarkAttachmentsHaveThePlacesThatHarfBuzzGives()
    {
        Font font = GetFont("LookupFlags.ttf");
        List<string> different = [];
        foreach (JsonElement[] row in Read(TestFonts.LookupFlagsHarfBuzzReference))
        {
            string text = row[1].GetString()!;
            string expected = string.Join(' ', row[2].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));
            string actual = string.Join(' ', Places(TextShaper.Shape(font, text)).Select(g => $"{g.Id}@{g.X},{g.Y}"));
            if (expected != actual)
            {
                different.Add($"{row[0].GetString()} expected [{expected}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // Each row of DefaultIgnorables.harfbuzz.json is a font, a text, the glyph ID, x and y of each glyph, and the advance of
    // the run.
    [Test]
    public async Task DefaultIgnorablesHaveThePlacesAndTheAdvanceThatHarfBuzzGives()
    {
        List<string> different = [];
        foreach (JsonElement[] row in Read(TestFonts.DefaultIgnorablesHarfBuzzReference))
        {
            string text = row[1].GetString()!;
            ShapedGlyph[] glyphs = TextShaper.Shape(GetFont(row[0].GetString()!), text);
            string expected = string.Join(' ', row[2].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}")) + $" advance {row[3].GetInt32()}";
            string actual = string.Join(' ', Places(glyphs).Select(g => $"{g.Id}@{g.X},{g.Y}")) + $" advance {glyphs.Sum(g => g.XAdvance)}";
            if (expected != actual)
            {
                different.Add($"[{Describe(text)}] expected [{expected}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    // Khmer.harfbuzz.json has a font, and a text and the glyph ID, x and y of each glyph in each row.
    [Test]
    public async Task KhmerHasThePlacesThatHarfBuzzGives()
    {
        Font font = GetFont("NotoSansKhmer-Regular.ttf");
        List<string> different = [];
        foreach (JsonElement[] row in Read(TestFonts.KhmerHarfBuzzReference))
        {
            string text = row[0].GetString()!;
            string expected = string.Join(' ', row[1].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));
            string actual = string.Join(' ', Places(TextShaper.Shape(font, text)).Select(g => $"{g.Id}@{g.X},{g.Y}"));
            if (expected != actual)
            {
                different.Add($"[{Describe(text)}] expected [{expected}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private static JsonElement[][] Read(string path)
        => JsonSerializer.Deserialize<Reference>(File.ReadAllText(path), Options)!.Rows;

    private static Font GetFont(string name)
    {
        lock (Fonts)
        {
            if (!Fonts.TryGetValue(name, out Font? font))
            {
                font = new FontCollection().Add(TestFonts.GetPath(name)).CreateFont(12);
                Fonts.Add(name, font);
            }

            return font;
        }
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

    private static string Describe(string text) => string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
