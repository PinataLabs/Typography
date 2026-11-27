// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// MarksWithoutGlyphClasses.ttf has no GDEF table. As in HarfBuzz, the glyph of a nonspacing mark is then a mark, so
/// mark-to-mark attaches an accent to the accent before it, and mark-to-base attaches an accent to the letter past
/// another accent. tests/tools/generate_marks_without_glyph_classes_reference.py writes the font and the glyphs that
/// HarfBuzz gives.
/// </summary>
public class MarksWithoutGlyphClassesTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GetPath("MarksWithoutGlyphClasses.harfbuzz.json")),
        Options)!;

    [Test]
    [Arguments("á̀")]
    [Arguments("à́")]
    public async Task GlyphsAreWhereHarfBuzzPutsThem(string text)
    {
        JsonElement[] row = HarfBuzz.Rows.Single(r => r[0].GetString() == text);
        string expected = string.Join(' ', row[1].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));

        FontFamily family = new FontCollection().Add(TestFonts.GetPath("MarksWithoutGlyphClasses.ttf"));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        Font font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
        string actual = string.Join(' ', GposVariationReference.Shape(font, text).Select(g => $"{g.Id}@{g.X},{g.Y}"));

        await Assert.That(actual).IsEqualTo(expected);
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
