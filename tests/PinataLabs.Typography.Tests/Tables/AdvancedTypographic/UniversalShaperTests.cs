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

    // Each row is a text of a script whose letters the universal shaper joins, such as Mongolian and N'Ko.
    // The font has a glyph for each joining form of each character.
    [Test]
    public async Task LettersOfJoiningScriptsTakeTheFormsThatHarfBuzzGives()
    {
        Font font = new FontCollection().Add(TestFonts.GetPath(HarfBuzz.JoiningFont)).CreateFont(12);

        await Assert.That(DifferentRows(font, HarfBuzz.Joining)).IsEmpty();
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
}
