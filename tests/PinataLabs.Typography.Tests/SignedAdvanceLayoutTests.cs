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
    // line by 50, which HarfBuzz leaves out too. VerticalAdvanceKern.ttf kerns A V with a horizontal kern subtable, which
    // HarfBuzz applies to horizontal text only. Along the line each glyph advances as far as HarfBuzz moves the pen, and
    // across it the glyph keeps the advance of the font.
    [Test]
    [Arguments("VerticalAdvance.ttf", "AV", "ltr")]
    [Arguments("VerticalAdvance.ttf", "一二三一", "ttb")]
    [Arguments("VerticalAdvance.ttf", "三一", "ttb")]
    [Arguments("VerticalAdvanceKern.ttf", "AV", "ltr")]
    [Arguments("VerticalAdvanceKern.ttf", "AV", "ttb")]
    public async Task GlyphsHaveTheAdvancesThatHarfBuzzGivesAlongAndAcrossTheLine(string fontName, string text, string direction)
    {
        JsonElement row = GetVerticalAdvanceRow(fontName, text, direction);
        bool vertical = direction == "ttb";
        int[][] glyphs = ToArrays(row.GetProperty("glyphs"));
        int[][] fontAdvances = ToArrays(row.GetProperty("advances"));
        List<string> expected = [];
        int pen = 0;
        int start = 0;
        int end = 0;
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

        TextOptions options = new(GetFont(fontName))
        {
            Dpi = 1,
            LayoutMode = vertical ? LayoutMode.VerticalLeftRight : LayoutMode.HorizontalTopBottom,
            FeatureTags = row.TryGetProperty("features", out JsonElement features)
                ? features.EnumerateArray().Select(f => Tag.Parse(f.GetString()!)).ToArray()
                : [],
        };
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), options);
        string[] actual = layout
            .Select(g => FormattableString.Invariant(
                $"{g.Glyph.GlyphMetrics.GlyphId} at {MathF.Round(vertical ? g.PenLocation.Y - layout[0].PenLocation.Y : g.PenLocation.X - layout[0].PenLocation.X)} advances {g.Glyph.GlyphMetrics.AdvanceWidth} {g.Glyph.GlyphMetrics.AdvanceHeight}"))
            .ToArray();

        await Assert.That(string.Join(", ", actual)).IsEqualTo(string.Join(", ", expected));
        FontRectangle measured = TextMeasurer.MeasureAdvance(text, options);
        await Assert.That(MathF.Round(vertical ? measured.Height : measured.Width)).IsEqualTo(end - start);
    }

    // The vertical kern subtable of VerticalAdvanceKern.ttf makes U+4E00 U+4E8C 200 units closer and U+4E8C U+4E09 300
    // units apart. HarfBuzz applies no kern subtable to vertical text (the reference row of these glyphs has the advances
    // of the font), so this is not checked against it. A positive value moves the glyphs apart, as the OpenType kern table
    // says, which is the way a positive GPOS y advance moves them, and the kerning changes the space between the two
    // glyphs of the pair, so the first one takes it.
    [Test]
    public async Task AVerticalKernSubtableMovesTheGlyphsApartForAPositiveValue()
    {
        TextOptions options = new(GetFont("VerticalAdvanceKern.ttf")) { Dpi = 1, LayoutMode = LayoutMode.VerticalLeftRight };
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout("一二三".AsSpan(), options);
        string[] actual = layout
            .Select(g => FormattableString.Invariant(
                $"{MathF.Round(g.PenLocation.Y - layout[0].PenLocation.Y)} advances {g.Glyph.GlyphMetrics.AdvanceWidth} {g.Glyph.GlyphMetrics.AdvanceHeight}"))
            .ToArray();

        await Assert.That(string.Join(", ", actual)).IsEqualTo("0 advances 900 800, 800 advances 900 1300, 2100 advances 900 1000");
    }

    private static JsonElement GetVerticalAdvanceRow(string fontName, string text, string direction)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(TestFonts.GetPath("VerticalAdvance.harfbuzz.json")));
        return document.RootElement.GetProperty("rows").EnumerateArray().Single(r =>
            r.GetProperty("font").GetString() == fontName
            && r.GetProperty("text").GetString() == text
            && r.GetProperty("direction").GetString() == direction).Clone();
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
