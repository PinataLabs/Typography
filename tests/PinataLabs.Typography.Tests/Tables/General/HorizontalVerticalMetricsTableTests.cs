// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.General;

/// <summary>
/// ShortMetrics.ttf has two long metrics in <c>hmtx</c> and <c>vmtx</c> for five glyphs, and its
/// <c>.notdef</c> advance differs from the advance that the later glyphs share. A glyph at or past
/// <c>numberOfHMetrics</c> or <c>numOfLongVerMetrics</c> takes the last advance of the table (issue #137).
/// </summary>
public class HorizontalVerticalMetricsTableTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference FontTools = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ShortMetricsFontToolsReference),
        Options)!;

    public static IEnumerable<string> Characters() => FontTools.Glyphs.Select(g => g.Char);

    [Test]
    [MethodDataSource(nameof(Characters))]
    public async Task AdvancesMatchFontTools(string character)
    {
        ReferenceGlyph expected = FontTools.Glyphs.Single(g => g.Char == character);
        Font font = new FontCollection().Add(TestFonts.ShortMetricsFile).CreateFont(12);

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            new CodePoint(character[0]),
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics>? metrics)).IsTrue();

        GlyphMetrics glyph = metrics![0];
        await Assert.That((int)glyph.GlyphId).IsEqualTo(expected.GlyphId);
        await Assert.That((int)glyph.AdvanceWidth).IsEqualTo(expected.AdvanceWidth);
        await Assert.That((int)glyph.AdvanceHeight).IsEqualTo(expected.AdvanceHeight);
    }

    private sealed class Reference
    {
        public ReferenceGlyph[] Glyphs { get; set; } = [];
    }

    private sealed class ReferenceGlyph
    {
        public string Char { get; set; } = string.Empty;

        public int GlyphId { get; set; }

        public int AdvanceWidth { get; set; }

        public int AdvanceHeight { get; set; }
    }
}
