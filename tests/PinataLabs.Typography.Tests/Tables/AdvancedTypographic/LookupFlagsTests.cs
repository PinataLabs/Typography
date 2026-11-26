// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// LookupFlags.ttf has a case for each way that a lookup can apply where HarfBuzz does not, or apply elsewhere: a nested
/// lookup of a chained context positioning lookup past a skipped mark (#118), a mark-to-mark attachment past a mark
/// outside the mark filtering set (#119), a lookup at a first glyph that its flags skip (#120), a reverse chaining
/// substitution called from a contextual lookup (#121), and cursive chains with and without the RightToLeft flag (#139).
/// tests/tools/generate_lookup_flags_reference.py writes the font and the glyphs that HarfBuzz gives for each case.
/// </summary>
public class LookupFlagsTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.LookupFlagsHarfBuzzReference),
        Options)!;

    [Test]
    [Arguments("ChainFormat1")]
    [Arguments("ChainFormat2")]
    [Arguments("ChainFormat3")]
    [Arguments("MarkToMarkPastFilteredMark")]
    [Arguments("SubstitutionAfterLeadingIgnoredMark")]
    [Arguments("PositioningAfterLeadingIgnoredMark")]
    [Arguments("ReverseFromContext")]
    [Arguments("Cursive")]
    [Arguments("CursiveRightToLeftFlag")]
    [Arguments("CursiveRightToLeftText")]
    [Arguments("CursiveRightToLeftTextAndFlag")]
    public async Task GlyphsAreWhereHarfBuzzPutsThem(string name)
    {
        JsonElement[] row = HarfBuzz.Rows.Single(r => r[0].GetString() == name);
        string text = row[1].GetString()!;
        string expected = string.Join(' ', row[2].EnumerateArray().Select(g => $"{g[0]}@{g[1]},{g[2]}"));

        FontFamily family = new FontCollection().Add(TestFonts.LookupFlagsFile);

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
