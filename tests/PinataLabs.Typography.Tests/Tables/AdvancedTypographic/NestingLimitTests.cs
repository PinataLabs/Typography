// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// NestingLimit.ttf has GSUB and GPOS context lookups that name themselves, or each other, after a lookup that moves the
/// glyph one step. As in HarfBuzz, the nested lookups apply 64 levels deep and no deeper, so a font that recurses
/// shapes instead of overflowing the stack (issue #205). The control cases nest less deep than the limit.
/// </summary>
public class NestingLimitTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly JsonElement[][] Rows = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.NestingLimitHarfBuzzReference),
        Options)!.Rows;

    public static IEnumerable<string> Cases => Rows.Select(row => row[0].GetString()!);

    // Each row is a case, a text, whether HarfBuzz reported a failure, and the glyph ID, x and y of each glyph.
    [Test]
    [MethodDataSource(nameof(Cases))]
    public async Task ShaperGivesTheGlyphsAndPlacesThatHarfBuzzGives(string name)
    {
        JsonElement[] row = Rows.Single(row => row[0].GetString() == name);
        Font font = new FontCollection().Add(TestFonts.NestingLimitFile).CreateFont(12);

        (int Id, int X, int Y)[] expected = row[3].EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())).ToArray();
        int pen = 0;
        List<(int Id, int X, int Y)> actual = [];
        foreach (ShapedGlyph glyph in TextShaper.Shape(font, row[1].GetString()!, new ShapingOptions()))
        {
            actual.Add((glyph.GlyphId, pen + glyph.XOffset, glyph.YOffset));
            pen += glyph.XAdvance;
        }

        await Assert.That(actual).IsEquivalentTo(expected, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
