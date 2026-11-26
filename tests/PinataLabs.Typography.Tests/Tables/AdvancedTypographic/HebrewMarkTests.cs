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
/// Hebrew letters and the GPOS marks on them are where HarfBuzz draws them.
/// </summary>
public class HebrewMarkTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.HebrewMarksHarfBuzzReference),
        Options)!;

    // A GPOS kern pair skips a mark between its two letters.
    [Test]
    public Task GposKernPairSkipsTheMarkBetweenItsLetters()
        => AssertCase("GposKernAcrossMark");

    // Three GPOS marks attach to one alef, and the third mark stays on it.
    [Test]
    public Task ThirdMarkStaysOnItsBase()
        => AssertCase("ThreeMarks");

    // HarfBuzz kerns with the kern table when the GPOS table has no kern feature, and a GPOS mark moves with the
    // base that the kern table moved.
    [Test]
    public Task MarkMovesWithTheBaseThatTheKernTableMoved()
        => AssertCase("KernTableMovesBase");

    // Each row is a case, a font, a text, and the glyph ID and the place of each glyph that HarfBuzz gives.
    private static async Task AssertCase(string name)
    {
        Dictionary<string, Font> fonts = [];
        List<string> different = [];
        JsonElement[][] rows = HarfBuzz.Rows.Where(row => row[0].GetString() == name).ToArray();
        foreach (JsonElement[] row in rows)
        {
            string fontName = row[1].GetString()!;
            if (!fonts.TryGetValue(fontName, out Font? font))
            {
                FontFamily family = new FontCollection().Add(TestFonts.GetPath(fontName));

                // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
                font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
                fonts.Add(fontName, font);
            }

            string text = row[2].GetString()!;
            (int Id, int X, int Y)[] expected = row[3].EnumerateArray()
                .Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32()))
                .ToArray();
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
                different.Add($"{fontName} [{codePoints}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
            }
        }

        await Assert.That(rows).IsNotEmpty();
        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
