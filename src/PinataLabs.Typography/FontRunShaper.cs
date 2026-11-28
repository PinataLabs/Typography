// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// Shapes the text of one font run: it maps the characters to glyphs, applies GSUB and fills the positioning collection.
/// </summary>
internal static class FontRunShaper
{
    /// <summary>
    /// Shapes a text of one font, one direction and, when the options name it, one script: the same steps as the layout
    /// takes for one text run, with GPOS and the kerning after them. The text is not split into lines or into bidi runs.
    /// </summary>
    /// <param name="font">The font of the text.</param>
    /// <param name="text">The text.</param>
    /// <param name="options">The options of the shaping.</param>
    /// <param name="direction">The direction of every character, which is not <see cref="TextDirection.Auto"/>.</param>
    /// <returns>The glyphs of the text in logical order, with their advances and offsets in font units.</returns>
    public static GlyphPositioningCollection Shape(Font font, ReadOnlySpan<char> text, TextOptions options, TextDirection direction)
    {
        GlyphSubstitutionCollection substitutions = new(options);
        GlyphPositioningCollection positionings = new(options);
        TextRun[] textRuns = [new() { Start = 0, End = text.GetGraphemeCount(), Font = font }];
        BidiRun[] bidiRuns = [new((BidiCharacterType)direction, (int)direction, 0, CodePoint.GetCodePointCount(text))];
        int textRunIndex = 0;
        int codePointIndex = 0;
        int bidiRunIndex = 0;
        Shape(text, default, default, 0, textRuns, ref textRunIndex, ref codePointIndex, ref bidiRunIndex, false, font, bidiRuns, substitutions, positionings);
        font.FontMetrics.UpdatePositions(positionings);
        positionings.HideDefaultIgnorables();
        return positionings;
    }

    /// <summary>
    /// Maps the characters of a text run to the glyphs of a font, applies GSUB to them, and adds the glyphs to the
    /// positioning collection, or updates its fallback glyphs when the run is a fallback run. GPOS comes after, with
    /// <see cref="FontMetrics.UpdatePositions"/>, once every run has its glyphs.
    /// </summary>
    /// <param name="text">The text of the run.</param>
    /// <param name="textBefore">The text before the run, which gives the letters at its start their joining form.</param>
    /// <param name="textAfter">The text after the run.</param>
    /// <param name="start">The index of the first grapheme of the run in the text.</param>
    /// <param name="textRuns">The text runs of the text.</param>
    /// <param name="textRunIndex">The index of the text run that the next grapheme belongs to.</param>
    /// <param name="codePointIndex">The index of the next codepoint in the text.</param>
    /// <param name="bidiRunIndex">The index of the bidi run that the next codepoint belongs to.</param>
    /// <param name="isFallbackRun">Whether the font only replaces the glyphs that an earlier font lacks.</param>
    /// <param name="font">The font of the run.</param>
    /// <param name="bidiRuns">The bidi runs of the text.</param>
    /// <param name="substitutions">The collection of glyphs to shape, which this method clears first.</param>
    /// <param name="positionings">The collection that receives the shaped glyphs.</param>
    /// <returns><see langword="true"/> if the font has a glyph for every character of the run.</returns>
    public static bool Shape(
        ReadOnlySpan<char> text,
        ReadOnlySpan<char> textBefore,
        ReadOnlySpan<char> textAfter,
        int start,
        IReadOnlyList<TextRun> textRuns,
        ref int textRunIndex,
        ref int codePointIndex,
        ref int bidiRunIndex,
        bool isFallbackRun,
        Font font,
        BidiRun[] bidiRuns,
        GlyphSubstitutionCollection substitutions,
        GlyphPositioningCollection positionings)
    {
        // For each run we start with a fresh substitution collection to avoid
        // overwriting the glyph ids.
        substitutions.Clear();
        substitutions.SetContext(textBefore, textAfter);
        substitutions.UnresolvedOffsets = isFallbackRun ? positionings.GetUnresolvedOffsets() : null;

        // Enumerate through each grapheme in the text.
        int graphemeIndex = start;
        int textIndex = textBefore.Length;
        var graphemeEnumerator = new SpanGraphemeEnumerator(text);
        while (graphemeEnumerator.MoveNext())
        {
            int graphemeMax = graphemeEnumerator.Current.Length - 1;
            int graphemeCodePointIndex = 0;
            int charIndex = 0;

            if (graphemeIndex == textRuns[textRunIndex].End)
            {
                textRunIndex++;
            }

            // Now enumerate through each codepoint in the grapheme.
            bool skipNextCodePoint = false;
            var codePointEnumerator = new SpanCodePointEnumerator(graphemeEnumerator.Current);
            while (codePointEnumerator.MoveNext())
            {
                int codePointTextIndex = textIndex;
                textIndex += codePointEnumerator.Current.Utf16SequenceLength;
                if (codePointIndex == bidiRuns[bidiRunIndex].End)
                {
                    bidiRunIndex++;
                }

                if (skipNextCodePoint)
                {
                    codePointIndex++;
                    graphemeCodePointIndex++;
                    continue;
                }

                int charsConsumed = 0;
                CodePoint current = codePointEnumerator.Current;
                charIndex += current.Utf16SequenceLength;
                CodePoint? next = graphemeCodePointIndex < graphemeMax
                    ? CodePoint.DecodeFromUtf16At(graphemeEnumerator.Current, charIndex, out charsConsumed)
                    : null;

                charIndex += charsConsumed;

                // Get the glyph id for the codepoint and add to the collection.
                font.FontMetrics.TryGetGlyphId(current, next, out ushort glyphId, out skipNextCodePoint);
                substitutions.AddGlyph(glyphId, current, (TextDirection)bidiRuns[bidiRunIndex].Direction, textRuns[textRunIndex], codePointIndex, codePointTextIndex);

                codePointIndex++;
                graphemeCodePointIndex++;
            }

            graphemeIndex++;
        }

        // Apply the simple and complex substitutions.
        GlyphNormalizer.Normalize(font.FontMetrics, substitutions);
        SubstituteBidiMirrors(font.FontMetrics, substitutions);
        SubstituteVerticalForms(font.FontMetrics, substitutions);
        font.FontMetrics.ApplySubstitution(substitutions);

        return !isFallbackRun
            ? positionings.TryAdd(font, substitutions)
            : positionings.TryUpdate(font, substitutions);
    }

    private static void SubstituteBidiMirrors(FontMetrics fontMetrics, GlyphSubstitutionCollection collection)
    {
        for (int i = 0; i < collection.Count; i++)
        {
            GlyphShapingData data = collection[i];

            if (data.Direction != TextDirection.RightToLeft)
            {
                continue;
            }

            if (!CodePoint.TryGetBidiMirror(data.CodePoint, out CodePoint mirror))
            {
                continue;
            }

            if (fontMetrics.TryGetGlyphId(mirror, out ushort glyphId))
            {
                collection.Replace(i, glyphId);
            }
        }
    }

    /// <summary>
    /// Substitutes cmap vertical presentation forms for fonts that do not provide a <c>vert</c> feature.
    /// The substitution only applies to vertical layouts and glyphs that are laid out upright; when the
    /// font has a <c>vert</c> feature that feature is authoritative.
    /// </summary>
    private static void SubstituteVerticalForms(FontMetrics fontMetrics, GlyphSubstitutionCollection collection)
    {
        LayoutMode layoutMode = collection.TextOptions.LayoutMode;
        if (!(layoutMode.IsVertical() || layoutMode.IsVerticalMixed()) || HasVertFeature(fontMetrics))
        {
            return;
        }

        for (int i = 0; i < collection.Count; i++)
        {
            GlyphShapingData data = collection[i];
            bool isTransformRotate = CodePoint.GetVerticalOrientationType(data.CodePoint) is VerticalOrientationType.TransformRotate;
            if (!isTransformRotate && !AdvancedTypographicUtils.IsVerticalGlyph(data.CodePoint, layoutMode, false))
            {
                continue;
            }

            if (!CodePoint.TryGetVerticalMirror(data.CodePoint, out CodePoint mirror))
            {
                continue;
            }

            if (fontMetrics.TryGetGlyphId(mirror, out ushort glyphId))
            {
                collection.Replace(i, glyphId);

                // A Tr glyph with a cmap vertical presentation form is laid out upright.
                data.HasVerticalForm |= isTransformRotate;
            }
        }
    }

    private static bool HasVertFeature(FontMetrics fontMetrics)
    {
        Tag vert = Tag.Parse("vert");
        foreach (FeatureTable feature in fontMetrics.GetGSubTable().FeatureList.FeatureTables)
        {
            if (feature.FeatureTag == vert)
            {
                return true;
            }
        }

        return false;
    }
}
