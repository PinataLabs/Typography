// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// GPOS kerning can pull the advance of a glyph below zero or push it above 65535. The layout places each glyph at
/// the sum of the advances before it, as HarfBuzz does. The measured advance spans the pen from the leftmost start of
/// a glyph to the rightmost end of one, so a glyph that moves the pen back past the start of the line widens it.
/// </summary>
public class SignedAdvanceLayoutTests
{
    private static readonly Dictionary<string, Font> Fonts = [];

    // TextShaper.ttf kerns o l by -700, so the o advances -100. TextShaperLarge.ttf kerns i o by -12000, so the i
    // advances -3808, and W W by 32000, so the first W advances 72960.
    [Test]
    [Arguments("TextShaper.ttf", "ole")]
    [Arguments("TextShaperLarge.ttf", "io")]
    [Arguments("TextShaperLarge.ttf", "Wio")]
    [Arguments("TextShaperLarge.ttf", "WW")]
    public async Task GlyphsHaveThePlacesAndTheAdvanceThatHarfBuzzGives(string fontName, string text)
    {
        int[][] reference = GetHarfBuzzGlyphs(fontName, text);
        List<(int Id, int X)> expected = [];
        int pen = 0;
        int left = 0;
        int right = 0;
        foreach (int[] glyph in reference)
        {
            expected.Add((glyph[0], pen + glyph[3]));
            left = Math.Min(left, pen);
            pen += glyph[2];
            right = Math.Max(right, pen);
        }

        // At one dot per inch, a measured pixel is a font unit.
        TextOptions options = new(GetFont(fontName)) { Dpi = 1 };
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        (int Id, int X)[] actual = layout
            .Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X)))
            .ToArray();

        await Assert.That(string.Join(' ', actual)).IsEqualTo(string.Join(' ', expected));
        await Assert.That(MathF.Round(TextMeasurer.MeasureAdvance(text, options).Width)).IsEqualTo(right - left);
    }

    // VerticalAdvance.ttf kerns with GPOS: vkrn makes the advance of U+4E00 200 shorter, that of U+4E8C 300 longer and
    // that of U+4E09 -300, and it moves U+4E09 by 77 across the line, which HarfBuzz leaves out. kern moves A across the
    // line by 50, which HarfBuzz leaves out too. VerticalAdvanceKern.ttf kerns A V and U+4E00 U+002D U+4E00 with a
    // horizontal kern subtable and three ideographs with a vertical one. HarfBuzz applies the horizontal subtable to horizontal text only, and neither
    // subtable to vertical text, even with vkrn turned on. Along the line each glyph advances as far as HarfBuzz moves the
    // pen, and across it the glyph keeps the advance of the font.
    [Test]
    [Arguments("VerticalAdvance.ttf", "AV", "ltr", "")]
    [Arguments("VerticalAdvance.ttf", "一二三一", "ttb", "vkrn")]
    [Arguments("VerticalAdvance.ttf", "三一", "ttb", "vkrn")]
    [Arguments("VerticalAdvanceKern.ttf", "AV", "ltr", "")]
    [Arguments("VerticalAdvanceKern.ttf", "AV", "ttb", "")]
    [Arguments("VerticalAdvanceKern.ttf", "一二三", "ttb", "")]
    [Arguments("VerticalAdvanceKern.ttf", "一二三", "ttb", "vkrn")]
    [Arguments("VerticalAdvanceKern.ttf", "一-一", "ltr", "")]
    public async Task GlyphsHaveTheAdvancesThatHarfBuzzGivesAlongAndAcrossTheLine(string fontName, string text, string direction, string features)
    {
        bool vertical = direction == "ttb";
        List<string> expected = [];
        int pen = 0;
        (int start, int end) = AddHarfBuzzGlyphs(expected, GetVerticalAdvanceRow(fontName, text, direction, features), vertical, ref pen);

        TextOptions options = new(GetFont(fontName))
        {
            Dpi = 1,
            LayoutMode = vertical ? LayoutMode.VerticalLeftRight : LayoutMode.HorizontalTopBottom,
            FeatureTags = features.Length > 0 ? [Tag.Parse(features)] : [],
        };

        await Assert.That(string.Join(", ", GetGlyphs(text, options, vertical))).IsEqualTo(string.Join(", ", expected));
        FontRectangle measured = TextMeasurer.MeasureAdvance(text, options);
        await Assert.That(MathF.Round(vertical ? measured.Height : measured.Width)).IsEqualTo(end - start);
    }

    // HarfBuzz has no vertical mixed text. There a run of rotated glyphs is laid out as HarfBuzz shapes it in horizontal
    // text, and a run of upright glyphs as HarfBuzz shapes it in vertical text, each run on its own. The horizontal kern
    // subtable of VerticalAdvanceKern.ttf kerns A V, which are rotated, and U+4E00 U+002D U+4E00, in which U+002D is
    // rotated and the ideographs are upright, so only A V is a pair within one run.
    [Test]
    [Arguments(LayoutMode.VerticalMixedLeftRight, "AV:ltr")]
    [Arguments(LayoutMode.VerticalMixedRightLeft, "AV:ltr")]
    [Arguments(LayoutMode.VerticalMixedLeftRight, "一:ttb -:ltr 一:ttb")]
    [Arguments(LayoutMode.VerticalMixedRightLeft, "一:ttb -:ltr 一:ttb")]
    public async Task VerticalMixedTextKernsEachRunOfOneOrientationAsHarfBuzzDoes(LayoutMode layoutMode, string runs)
    {
        List<string> expected = [];
        int pen = 0;
        string text = string.Empty;
        foreach (string[] run in runs.Split(' ').Select(r => r.Split(':')))
        {
            AddHarfBuzzGlyphs(expected, GetVerticalAdvanceRow("VerticalAdvanceKern.ttf", run[0], run[1], string.Empty), run[1] == "ttb", ref pen);
            text += run[0];
        }

        TextOptions options = new(GetFont("VerticalAdvanceKern.ttf")) { Dpi = 1, LayoutMode = layoutMode };

        await Assert.That(string.Join(", ", GetGlyphs(text, options, vertical: true))).IsEqualTo(string.Join(", ", expected));
        await Assert.That(MathF.Round(TextMeasurer.MeasureAdvance(text, options).Height)).IsEqualTo(pen);
    }

    // A side bearing is the space between the ink and the end of the advance, so it follows the advance that positioning
    // gives the glyph: the o of "ole" advances -100, the first W of "WW" 72960, a lone W 40960, and U+4E00 U+4E8C U+4E09
    // U+4E00 800, 1300, -300 and 1000 down the line (TextShaper.harfbuzz.json, VerticalAdvance.harfbuzz.json). The glyphs
    // are empty and their leading bearings in hmtx and vmtx are 0. TextShaper.ttf and TextShaperLarge.ttf have no vmtx, so
    // a glyph is as high as the ascender less the descender, 1000 and 16383, and starts at the ascender, 800 and 13107,
    // which leaves 200 and 3276 below it. The ideographs of VerticalAdvance.ttf are 900 wide.
    [Test]
    [Arguments("TextShaper.ttf", "ole", false, "-100 200, 600 200, 600 200")]
    [Arguments("TextShaperLarge.ttf", "WW", false, "72960 3276, 40960 3276")]
    [Arguments("TextShaperLarge.ttf", "W", false, "40960 3276")]
    [Arguments("VerticalAdvance.ttf", "一二三一", true, "900 800, 900 1300, 900 -300, 900 1000")]
    public async Task TheSideBearingsFollowThePositionedAdvance(string fontName, string text, bool vertical, string expected)
    {
        TextOptions options = new(GetFont(fontName))
        {
            Dpi = 1,
            LayoutMode = vertical ? LayoutMode.VerticalLeftRight : LayoutMode.HorizontalTopBottom,
            FeatureTags = vertical ? [Tag.Parse("vkrn")] : [],
        };
        string[] actual = TextLayout.GenerateLayout(text.AsSpan(), options)
            .Select(g => g.Glyph.GlyphMetrics)
            .Select(m => FormattableString.Invariant($"{m.RightSideBearing} {m.BottomSideBearing}"))
            .ToArray();

        await Assert.That(string.Join(", ", actual)).IsEqualTo(expected);
    }

    // Adds each glyph of the row, at the place of the pen along the line and with its advances across and down it, and
    // returns the start and the end of the space that the pen covers.
    private static (int Start, int End) AddHarfBuzzGlyphs(List<string> expected, JsonElement row, bool vertical, ref int pen)
    {
        int[][] glyphs = ToArrays(row.GetProperty("glyphs"));
        int[][] fontAdvances = ToArrays(row.GetProperty("advances"));
        int start = pen;
        int end = pen;
        for (int i = 0; i < glyphs.Length; i++)
        {
            // HarfBuzz gives the advance of vertical text in font space, where y goes up and the line goes down.
            int advance = vertical ? -glyphs[i][2] : glyphs[i][1];
            int advanceWidth = vertical ? fontAdvances[i][0] : advance;
            int advanceHeight = vertical ? advance : fontAdvances[i][1];
            expected.Add($"{glyphs[i][0]} at {pen} advances {advanceWidth} {advanceHeight}");
            start = Math.Min(start, pen);
            pen += advance;
            end = Math.Max(end, pen);
        }

        return (start, end);
    }

    private static string[] GetGlyphs(string text, TextOptions options, bool vertical)
    {
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        return layout
            .Select(g => FormattableString.Invariant(
                $"{g.Glyph.GlyphMetrics.GlyphId} at {MathF.Round(vertical ? g.PenLocation.Y - layout[0].PenLocation.Y : g.PenLocation.X - layout[0].PenLocation.X)} advances {g.Glyph.GlyphMetrics.AdvanceWidth} {g.Glyph.GlyphMetrics.AdvanceHeight}"))
            .ToArray();
    }

    // A row with no features has none, and the features of the others are one tag here.
    private static JsonElement GetVerticalAdvanceRow(string fontName, string text, string direction, string features)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(TestFonts.GetPath("VerticalAdvance.harfbuzz.json")));
        return document.RootElement.GetProperty("rows").EnumerateArray().Single(r =>
            r.GetProperty("font").GetString() == fontName
            && r.GetProperty("text").GetString() == text
            && r.GetProperty("direction").GetString() == direction
            && (r.TryGetProperty("features", out JsonElement f) ? string.Join(' ', f.EnumerateArray().Select(t => t.GetString())) : string.Empty) == features).Clone();
    }

    private static int[][] ToArrays(JsonElement rows)
        => rows.EnumerateArray().Select(g => g.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();

    // Each glyph of a row is its ID, cluster, x advance, x offset and y offset.
    private static int[][] GetHarfBuzzGlyphs(string fontName, string text)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(TestFonts.GetPath("TextShaper.harfbuzz.json")));
        JsonElement row = document.RootElement.GetProperty("rows").EnumerateArray().Single(r =>
            r.GetProperty("font").GetString() == fontName
            && r.GetProperty("text").GetString() == text
            && !r.TryGetProperty("features", out _)
            && !r.TryGetProperty("script", out _)
            && !r.TryGetProperty("language", out _));

        return row.GetProperty("glyphs").EnumerateArray().Select(g => g.EnumerateArray().Select(v => v.GetInt32()).ToArray()).ToArray();
    }

    private static Font GetFont(string fontName)
    {
        lock (Fonts)
        {
            if (!Fonts.TryGetValue(fontName, out Font? font))
            {
                FontFamily family = new FontCollection().Add(TestFonts.GetPath(fontName));

                // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
                font = family.CreateFont(72 * family.CreateFont(1).FontMetrics.UnitsPerEm);
                Fonts.Add(fontName, font);
            }

            return font;
        }
    }
}
