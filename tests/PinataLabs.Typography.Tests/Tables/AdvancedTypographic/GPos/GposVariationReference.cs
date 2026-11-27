// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

/// <summary>
/// The HarfBuzz reference for the GPOS deltas of the two variable test fonts, and the shaping of the library in the same
/// form. tests/tools/generate_gpos_variations_reference.py writes the reference. Each glyph of a text holds its position and
/// its advance after positioning, and its advance before positioning, which HarfBuzz takes from HVAR. The comparison takes the
/// advance before positioning of the library from the instance it runs on, so it holds whether or not the library varies
/// advances yet. It compares what GPOS adds: the position of a glyph from the pen of the unpositioned advances, the y offset
/// and the change of the advance. A mark takes the pen of the glyph that it attaches to, and has no advance.
/// </summary>
internal static class GposVariationReference
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference Value = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GetPath("Variations/GposVariations.harfbuzz.json")),
        Options)!;

    /// <summary>
    /// Gets the reference of a font, with the rows of one kind.
    /// </summary>
    /// <param name="path">The path of the font under the Fonts folder.</param>
    /// <param name="kind">The kind of the rows, as the generator names it, such as <c>pair</c>, <c>mark</c> or <c>cursive</c>.</param>
    /// <returns>The weights of the locations, and the rows.</returns>
    public static (int[] Weights, List<Row> Rows) Get(string path, string kind)
    {
        ReferenceFont font = Value.Fonts.Single(f => f.Font == path);
        return (font.Weights, font.Rows.Where(r => r[0].GetString() == kind).Select(Row.Create).ToList());
    }

    /// <summary>
    /// Lays the text out in font units and gives each glyph in the form of the reference, with the advance that the
    /// instance gives the glyph before positioning.
    /// </summary>
    /// <param name="font">The font, which is at a size where a pixel is a font unit.</param>
    /// <param name="text">The text.</param>
    /// <returns>The glyphs.</returns>
    public static ShapedGlyph[] Shape(Font font, string text)
    {
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));
        return layout.Select(g =>
        {
            GlyphMetrics metrics = g.Glyph.GlyphMetrics;
            int unpositioned = font.FontMetrics.GetGlyphMetrics(
                g.CodePoint,
                metrics.GlyphId,
                TextAttributes.None,
                TextDecorations.None,
                LayoutMode.HorizontalTopBottom,
                ColorFontSupport.None)[0].AdvanceWidth;
            return new ShapedGlyph(
                metrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + metrics.Offset.X),
                (int)MathF.Round(metrics.Offset.Y),
                (int)MathF.Round(g.AdvanceX),
                unpositioned,
                font.FontMetrics.TryGetGlyphClass(metrics.GlyphId, out GlyphClassDef? glyphClass) && glyphClass == GlyphClassDef.MarkGlyph);
        }).ToArray();
    }

    /// <summary>
    /// Gives what GPOS adds to the glyphs, as tests/tools/generate_gpos_variations_reference.py computes it.
    /// </summary>
    /// <param name="glyphs">The glyphs of a text.</param>
    /// <returns>For each glyph its ID, its position from the unpositioned pen, its y offset and the change of its advance.</returns>
    public static Positioned[] GposOnly(ShapedGlyph[] glyphs)
    {
        int[] unpositioned = glyphs.Select(g => g.IsMark ? 0 : g.UnpositionedAdvance).ToArray();
        Positioned[] result = new Positioned[glyphs.Length];
        int attachedTo = 0;
        for (int i = 0; i < glyphs.Length; i++)
        {
            if (!glyphs[i].IsMark)
            {
                attachedTo = i;
            }

            result[i] = new Positioned(glyphs[i].Id, glyphs[i].X - unpositioned.Take(attachedTo).Sum(), glyphs[i].Y, glyphs[i].Advance - unpositioned[i]);
        }

        return result;
    }

    public static string CodePoints(string text) => string.Join(' ', text.EnumerateRunes().Select(r => r.Value.ToString("X4")));

    public readonly record struct ShapedGlyph(int Id, int X, int Y, int Advance, int UnpositionedAdvance, bool IsMark);

    public readonly record struct Positioned(int Id, int X, int Y, int AdvanceChange);

    // A row is a kind, a text and the glyphs of the text at each weight: [id, x, y, advance, unpositioned advance, mark].
    public sealed record Row(string Text, ShapedGlyph[][] Locations)
    {
        public static Row Create(JsonElement[] row)
            => new(
                row[1].GetString()!,
                row[2].EnumerateArray()
                    .Select(location => location.EnumerateArray()
                        .Select(g => new ShapedGlyph(g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32(), g[3].GetInt32(), g[4].GetInt32(), g[5].GetInt32() == 1))
                        .ToArray())
                    .ToArray());
    }

    private sealed class Reference
    {
        public ReferenceFont[] Fonts { get; set; } = [];
    }

    private sealed class ReferenceFont
    {
        public string Font { get; set; } = string.Empty;

        public int[] Weights { get; set; } = [];

        public JsonElement[][] Rows { get; set; } = [];
    }
}
