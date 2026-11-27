// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// MarksWithoutGlyphClasses.ttf has no GDEF table. As in HarfBuzz, a glyph then has the class of its character: a
/// nonspacing mark that is not default ignorable is a mark, any other character is a base, and a ligature of components
/// that are not all marks is a ligature. MarksWithoutGlyphClassesFallback.ttf has no GPOS table either, so marks are
/// placed around their base. tests/tools/generate_marks_without_glyph_classes_reference.py writes the fonts and the
/// glyphs that HarfBuzz gives, and says what each text tests.
/// </summary>
public class MarksWithoutGlyphClassesTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GetPath("MarksWithoutGlyphClasses.harfbuzz.json")),
        Options)!;

    // Each row is a font, a text and the glyph ID and place of each glyph, in font units.
    [Test]
    public async Task GlyphsAreWhereHarfBuzzPutsThem()
    {
        Dictionary<string, Font> fonts = [];
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string fontName = row[0].GetString()!;
            if (!fonts.TryGetValue(fontName, out Font? font))
            {
                FontFamily family = new FontCollection().Add(TestFonts.GetPath(fontName));

                // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
                font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
                fonts.Add(fontName, font);
            }

            string text = row[1].GetString()!;
            string expected = string.Join(' ', row[2].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));
            string actual = string.Join(' ', GposVariationReference.Shape(font, text).Select(g => $"{g.Id}@{g.X},{g.Y}"));
            if (actual != expected)
            {
                string codePoints = string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));
                different.Add($"{fontName} [{codePoints}] expected [{expected}] actual [{actual}]");
            }
        }

        await Assert.That(different).IsEmpty();
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
