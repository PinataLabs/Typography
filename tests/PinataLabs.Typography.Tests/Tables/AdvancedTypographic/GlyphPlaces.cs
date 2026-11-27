// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// Writes glyphs as "ID@x,y", to compare them with a HarfBuzz reference that holds the glyph ID, x and y of each glyph.
/// </summary>
internal static class GlyphPlaces
{
    public static string Describe(JsonElement glyphs)
        => Describe(glyphs.EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())));

    public static string Describe(IEnumerable<(int Id, int X, int Y)> glyphs)
        => string.Join(' ', glyphs.Select(g => $"{g.Id}@{g.X},{g.Y}"));

    /// <summary>
    /// Writes the glyphs of a layout at a size that keeps font units unscaled, from the left of the line.
    /// </summary>
    /// <param name="layout">The glyphs of the layout.</param>
    /// <returns>The glyphs.</returns>
    public static string Describe(IReadOnlyList<GlyphLayout> layout)
        => Describe(layout.Select(g => (
            (int)g.Glyph.GlyphMetrics.GlyphId,
            (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
            (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y))));
}
