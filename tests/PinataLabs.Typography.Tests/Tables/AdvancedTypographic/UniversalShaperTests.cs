// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// The universal shaper shapes text as the universal shaper of HarfBuzz does.
/// </summary>
public class UniversalShaperTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.UniversalShaperHarfBuzzReference),
        Options)!;

    private static readonly FormsReference HarfBuzzForms = JsonSerializer.Deserialize<FormsReference>(
        File.ReadAllText(TestFonts.UniversalFormsHarfBuzzReference),
        Options)!;

    // Each row is a text of a script whose letters the universal shaper joins, such as Mongolian and N'Ko.
    // The font has a glyph for each joining form of each character.
    [Test]
    public async Task LettersOfJoiningScriptsTakeTheFormsThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.GetPath(HarfBuzz.JoiningFont)).CreateFont(12);

        await Assert.That(DifferentRows(font, HarfBuzz.Joining)).IsEmpty();
    }

    // Each row is a text of Balinese, Tibetan or Javanese, whose letters do not join, and the glyph ID and the place
    // of each glyph that HarfBuzz gives. Each of isol, init, medi and fina gives a glyph its own form, by the place of
    // its syllable in a run of syllables.
    [Test]
    public async Task SyllablesTakeTheFormsThatHarfBuzzGives()
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(HarfBuzzForms.Font));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzzForms.Rows)
        {
            string text = row[0].GetString()!;

            // HarfBuzz 15.0.0 puts a ZWNJ or a ZWJ after a syllable in that syllable, so it takes the form of the
            // syllable. The syllables of the library come from an older USE syllable grammar, which #108 replaces.
            if (text.Contains('‌') || text.Contains('‍'))
            {
                continue;
            }

            (int Id, int X, int Y)[] expected = row[1].EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())).ToArray();
            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));
            (int Id, int X, int Y)[] actual = layout
                .Select(g => (
                    (int)g.Glyph.GlyphMetrics.GlyphId,
                    (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                    (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"[{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private static List<string> DifferentRows(Font font, JsonElement[][] rows)
    {
        List<string> different = [];
        foreach (JsonElement[] row in rows)
        {
            string text = row[0].GetString()!;
            int[] expected = row[1].EnumerateArray().Select(g => g.GetInt32()).ToArray();
            int[] actual = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font))
                .Select(g => (int)g.Glyph.GlyphMetrics.GlyphId)
                .ToArray();

            if (!actual.SequenceEqual(expected))
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"[{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        return different;
    }

    private sealed class Reference
    {
        public string JoiningFont { get; set; } = string.Empty;

        public JsonElement[][] Joining { get; set; } = [];
    }

    private sealed class FormsReference
    {
        public string Font { get; set; } = string.Empty;

        public JsonElement[][] Rows { get; set; } = [];
    }
}
