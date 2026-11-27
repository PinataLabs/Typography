// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.Unicode.Resources;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Breaks the vowel sequences of <c>IndicShapingInvalidCluster.txt</c> with a dotted circle, as
/// <c>_hb_preprocess_text_vowel_constraints</c> of HarfBuzz 15.0.0 does in its Indic and universal shapers, so that an
/// independent vowel and the sign after it do not look like another vowel.
/// </summary>
internal static class VowelConstraints
{
    private const int DottedCircle = 0x25CC;

    /// <summary>
    /// Inserts a dotted circle into each sequence of the script that the glyphs of a run hold. HarfBuzz looks for a
    /// sequence at each character, and goes on after the character that follows the circle.
    /// </summary>
    /// <param name="fontMetrics">The font of the run.</param>
    /// <param name="collection">The glyphs, which the 'cmap' table has just mapped one to one from the text.</param>
    /// <param name="start">The index of the first glyph of the run.</param>
    /// <param name="end">The index after the last glyph of the run.</param>
    /// <param name="script">The script of the run.</param>
    /// <returns>The number of dotted circles inserted.</returns>
    public static int Apply(FontMetrics fontMetrics, GlyphSubstitutionCollection collection, int start, int end, ScriptClass script)
    {
        VowelConstraint[] constraints = VowelConstraintData.GetConstraints(script);
        if (constraints.Length == 0)
        {
            return 0;
        }

        // As in HarfBuzz, a font without the dotted circle gets its .notdef glyph.
        fontMetrics.TryGetGlyphId(new CodePoint(DottedCircle), out ushort glyphId);

        int inserted = 0;
        for (int i = start; i + 1 < end;)
        {
            int circleIndex = MatchCircleIndex(collection, i, end, constraints);
            if (circleIndex < 0)
            {
                i++;
                continue;
            }

            // As in HarfBuzz, the circle copies the glyph that it goes before. It joins its cluster and keeps its
            // Unicode properties, so the circle before a nonspacing mark is a mark too.
            int circle = i + circleIndex;
            collection.InsertGlyph(circle, circle, glyphId, new CodePoint(DottedCircle));
            collection[circle].UnicodePropertiesCodePoint = collection[circle + 1].UnicodePropertiesCodePoint;
            inserted++;
            end++;
            i += circleIndex + 2;
        }

        return inserted;
    }

    private static int MatchCircleIndex(GlyphSubstitutionCollection collection, int index, int end, VowelConstraint[] constraints)
    {
        int first = collection[index].CodePoint.Value;
        foreach (VowelConstraint constraint in constraints)
        {
            int[] codePoints = constraint.CodePoints;
            if (codePoints[0] != first || index + codePoints.Length > end)
            {
                continue;
            }

            int i = 1;
            while (i < codePoints.Length && collection[index + i].CodePoint.Value == codePoints[i])
            {
                i++;
            }

            if (i == codePoints.Length)
            {
                return constraint.CircleIndex;
            }
        }

        return -1;
    }
}
