// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using PinataLabs.Typography.Tests.FontPrograms;

namespace PinataLabs.Typography.Tests.FontSubsetting;

/// <summary>
/// Compares a subset font with the font that it came from, through <see cref="FontProgram"/>: what a PDF reader sees.
/// </summary>
internal static class SubsetComparison
{
    /// <summary>
    /// Describes every way that a subset differs from what a subset must be: the same glyph count, names and metrics, each requested
    /// glyph with the outline and the advance that it had, and every other glyph either as it was (a glyph that a kept glyph
    /// needs) or empty.
    /// </summary>
    /// <param name="original">The font.</param>
    /// <param name="subset">The subset.</param>
    /// <param name="requested">The glyphs that the subset was asked to keep.</param>
    /// <param name="dropped">Receives the number of glyphs that are empty in the subset and were not in the font.</param>
    /// <returns>The differences, empty for a good subset.</returns>
    public static List<string> Compare(byte[] original, byte[] subset, IReadOnlyCollection<ushort> requested, out int dropped)
    {
        List<string> differences = [];
        FontProgram before = FontProgram.Load(original);
        FontProgram after = FontProgram.Load(subset);
        dropped = 0;

        if (after.Format != before.Format || after.IsCidKeyed != before.IsCidKeyed || after.GlyphCount != before.GlyphCount)
        {
            differences.Add($"format {after.Format}/{before.Format}, cid {after.IsCidKeyed}/{before.IsCidKeyed}, glyphs {after.GlyphCount}/{before.GlyphCount}");
            return differences;
        }

        if (after.FontMatrix != before.FontMatrix || after.PaintType != before.PaintType)
        {
            differences.Add("the font matrix or the paint type changed");
        }

        HashSet<ushort> mustKeep = [0, .. requested];
        for (ushort id = 0; id < before.GlyphCount && differences.Count < 10; id++)
        {
            GlyphOutline expected = new();
            GlyphOutline actual = new();
            bool drew = before.TryWriteOutline(id, ref expected);
            if (!after.TryWriteOutline(id, ref actual))
            {
                differences.Add($"glyph {id}: the subset cannot draw it");
                continue;
            }

            if (before.GetGlyphName(id) != after.GetGlyphName(id))
            {
                differences.Add($"glyph {id}: the name is '{after.GetGlyphName(id)}', and was '{before.GetGlyphName(id)}'");
            }

            bool same = expected.ToString() == actual.ToString();
            if (mustKeep.Contains(id))
            {
                if (!drew || !same)
                {
                    differences.Add($"kept glyph {id}: the outline is '{actual}', and was '{expected}'");
                }

                bool hadWidth = before.TryGetAdvanceWidth(id, out float width);
                if (after.TryGetAdvanceWidth(id, out float newWidth) != hadWidth || newWidth != width)
                {
                    differences.Add($"kept glyph {id}: the advance is {newWidth}, and was {width}");
                }
            }
            else if (!same && actual.ToString().Length > 0)
            {
                differences.Add($"glyph {id}: the outline is '{actual}', which is neither '{expected}' nor empty");
            }
            else if (!same)
            {
                dropped++;
            }
        }

        return differences;
    }

    /// <summary>
    /// Picks glyph IDs: the first glyph, the last, and every <paramref name="stride"/> glyphs from <paramref name="start"/>.
    /// </summary>
    public static ushort[] Pick(int glyphCount, int start, int stride)
        => [.. Enumerable.Range(0, glyphCount).Where(i => i == glyphCount - 1 || (i >= start && (i - start) % stride == 0)).Select(i => (ushort)i)];
}
