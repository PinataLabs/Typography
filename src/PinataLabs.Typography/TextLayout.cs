// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// Encapsulated logic or laying out text.
/// </summary>
internal static class TextLayout
{
    /// <summary>
    /// The fraction of an em used as the width of a space when applying <see cref="TextOptions.TabWidth"/>
    /// to a font that has no space glyph.
    /// </summary>
    private const float SpaceFallbackEmFraction = 0.25F;

    public static IReadOnlyList<GlyphLayout> GenerateLayout(ReadOnlySpan<char> text, TextOptions options)
        => GenerateLayout(text, options, options.Origin);

    public static IReadOnlyList<GlyphLayout> GenerateLayout(ReadOnlySpan<char> text, TextOptions options, Vector2 origin)
    {
        if (text.IsEmpty)
        {
            return [];
        }

        TextBox textBox = ProcessText(text, options);
        return LayoutText(textBox, options, origin);
    }

    public static IReadOnlyList<TextRun> BuildTextRuns(ReadOnlySpan<char> text, TextOptions options)
    {
        if (options.TextRuns is null || options.TextRuns.Count == 0)
        {
            return new TextRun[]
            {
                new()
                {
                    Start = 0,
                    End = text.GetGraphemeCount(),
                    Font = options.Font
                }
            };
        }

        int count = text.GetGraphemeCount();

        // Normalisation policy (deterministic, documented on TextOptions.TextRuns):
        //  1. Work on private copies so the caller's instances are never written to.
        //  2. Clamp each run to [0, count]; drop runs that are empty after clamping
        //     (zero length, inverted, or entirely past the end).
        //  3. Where runs overlap or nest, the run later in list order wins inside the
        //     overlap and the earlier run resumes after it (like nested CSS spans).
        //     Start order is irrelevant; equal starts are tie-broken by list order.
        //  4. Graphemes covered by no run get a default-font run.
        // Each grapheme is assigned to the last run covering it, then consecutive graphemes
        // with the same owner are emitted as one run. A run split around a nested run yields
        // one copy per piece, so derived types stay visible in both.
        List<TextRun> copies = CopyRuns(options.TextRuns!);
        int[] owners = new int[count];
        Array.Fill(owners, -1);
        for (int i = 0; i < copies.Count; i++)
        {
            TextRun copy = copies[i];
            int from = Math.Max(copy.Start, 0);
            int to = Math.Min(copy.End, count);
            if (to <= from)
            {
                continue;
            }

            Array.Fill(owners, i, from, to - from);
        }

        List<TextRun> textRuns = [];
        int segmentStart = 0;
        while (segmentStart < count)
        {
            int owner = owners[segmentStart];
            int segmentEnd = segmentStart + 1;
            while (segmentEnd < count && owners[segmentEnd] == owner)
            {
                segmentEnd++;
            }

            TextRun run = owner < 0 ? new TextRun() : copies[owner].Clone();
            run.Start = segmentStart;
            run.End = segmentEnd;
            run.Font ??= options.Font;
            textRuns.Add(run);
            segmentStart = segmentEnd;
        }

        return textRuns;
    }

    private static List<TextRun> CopyRuns(IReadOnlyList<TextRun> source)
    {
        List<TextRun> copies = new(source.Count);
        foreach (TextRun run in source)
        {
            copies.Add(run.Clone());
        }

        return copies;
    }

    private static TextBox ProcessText(ReadOnlySpan<char> text, TextOptions options)
    {
        // Gather the font and fallbacks.
        Font[] fallbackFonts = (options.FallbackFontFamilies?.Count > 0)
            ? options.FallbackFontFamilies.Select(options.Font.WithFamily).ToArray()
            : [];

        LayoutMode layoutMode = options.LayoutMode;
        GlyphSubstitutionCollection substitutions = new(options);
        GlyphPositioningCollection positionings = new(options);

        // Analyse the text for bidi directional runs.
        // A fixed text direction sets the paragraph embedding level only (UAX #9 HL1),
        // as CSS direction does. Strong characters still resolve to their own direction.
        BidiAlgorithm bidi = BidiAlgorithm.Instance.Value!;
        var bidiData = new BidiData();
        bidiData.Init(text, (sbyte)options.TextDirection);

        // Each paragraph separator ends a paragraph (UAX #9 P1), and with Auto each paragraph takes its own level.
        // Rule L1 applies to each line, so BreakLines applies it once it knows the lines.
        bidi.Process(bidiData, resetWhitespace: false);
        BidiLevels bidiLevels = new(bidiData.Types, bidi.ResolvedLevels.Span.ToArray(), [.. bidi.Paragraphs]);

        // Shaping takes the directional runs of the levels with rule L1 applied to each paragraph as one line.
        // GSUB splits a font run by script, not by direction, so where a line ends does not change the shaping.
        foreach (BidiAlgorithm.Paragraph paragraph in bidiLevels.Paragraphs)
        {
            BidiAlgorithm.ResetWhitespaceLevels(
                bidiData.Types.Slice(paragraph.Start, paragraph.Length),
                bidi.ResolvedLevels.Slice(paragraph.Start, paragraph.Length),
                paragraph.Level);
        }

        BidiRun[] bidiRuns = BidiRun.CoalesceLevels(bidi.ResolvedLevels).ToArray();

        // Incrementally build out collection of glyphs.
        IReadOnlyList<TextRun> textRuns = BuildTextRuns(text, options);

        // First do multiple font runs using the individual text runs.
        bool complete = true;
        int textRunIndex = 0;
        int codePointIndex = 0;
        int bidiRunIndex = 0;
        int runCharStart = 0;
        foreach (TextRun textRun in textRuns)
        {
            // The runs follow each other, so each starts where the one before it ends.
            ReadOnlySpan<char> runText = textRun.Slice(text);
            if (!FontRunShaper.Shape(
                runText,
                text[..runCharStart],
                text[(runCharStart + runText.Length)..],
                textRun.Start,
                textRuns,
                ref textRunIndex,
                ref codePointIndex,
                ref bidiRunIndex,
                false,
                textRun.Font!,
                bidiRuns,
                substitutions,
                positionings))
            {
                complete = false;
            }

            runCharStart += runText.Length;
        }

        if (!complete)
        {
            // Finally try our fallback fonts.
            // We do a complete run here across the whole collection.
            foreach (Font font in fallbackFonts)
            {
                textRunIndex = 0;
                codePointIndex = 0;
                bidiRunIndex = 0;
                if (FontRunShaper.Shape(
                    text,
                    [],
                    [],
                    0,
                    textRuns,
                    ref textRunIndex,
                    ref codePointIndex,
                    ref bidiRunIndex,
                    true,
                    font,
                    bidiRuns,
                    substitutions,
                    positionings))
                {
                    break;
                }
            }
        }

        // Update the positions of the glyphs in the completed collection.
        // Each set of metrics is associated with single font and will only be updated
        // by that font so it's safe to use a single collection.
        // A font positions all of its glyphs in one pass, so the text runs and fallbacks that share it take one turn.
        HashSet<FontMetrics> positioned = [];
        foreach (TextRun textRun in textRuns)
        {
            if (positioned.Add(textRun.Font!.FontMetrics))
            {
                textRun.Font.FontMetrics.UpdatePositions(positionings);
            }
        }

        foreach (Font font in fallbackFonts)
        {
            if (positioned.Add(font.FontMetrics))
            {
                font.FontMetrics.UpdatePositions(positionings);
            }
        }

        // A cluster whose base and marks come from two fonts takes the places of its marks once both fonts have
        // positioned their own glyphs.
        if (positioned.Count > 1)
        {
            FallbackMarkPositioner.PlaceMarksAcrossFonts(positionings);
        }

        return BreakLines(text, options, bidiLevels, positionings, layoutMode);
    }

    private static IReadOnlyList<GlyphLayout> LayoutText(TextBox textBox, TextOptions options, Vector2 origin)
    {
        LayoutMode layoutMode = options.LayoutMode;
        List<GlyphLayout> glyphs = [];

        Vector2 boxLocation = origin / options.Dpi;
        Vector2 penLocation = boxLocation;

        // If a wrapping length is specified that should be used to determine the
        // box size to align text within. Only left-to-right text aligned to Start can skip it;
        // for a right-to-left line, Start is the far edge of the box.
        bool isLeftToRightStart = options.TextAlignment == TextAlignment.Start
            && textBox.TextLines.All(line => line.Direction == TextDirection.LeftToRight);
        float maxScaledAdvance = textBox.ScaledMaxAdvance();
        if (!isLeftToRightStart && options.WrappingLength > 0)
        {
            maxScaledAdvance = Math.Max(options.WrappingLength / options.Dpi, maxScaledAdvance);
        }

        if (layoutMode == LayoutMode.HorizontalTopBottom)
        {
            for (int i = 0; i < textBox.TextLines.Count; i++)
            {
                glyphs.AddRange(LayoutLineHorizontal(
                    textBox,
                    textBox.TextLines[i],
                    maxScaledAdvance,
                    options,
                    i,
                    ref boxLocation,
                    ref penLocation));
            }
        }
        else if (layoutMode == LayoutMode.HorizontalBottomTop)
        {
            int index = 0;
            for (int i = textBox.TextLines.Count - 1; i >= 0; i--)
            {
                glyphs.AddRange(LayoutLineHorizontal(
                    textBox,
                    textBox.TextLines[i],
                    maxScaledAdvance,
                    options,
                    index++,
                    ref boxLocation,
                    ref penLocation));
            }
        }
        else if (layoutMode is LayoutMode.VerticalLeftRight)
        {
            for (int i = 0; i < textBox.TextLines.Count; i++)
            {
                glyphs.AddRange(LayoutLineVertical(
                    textBox,
                    textBox.TextLines[i],
                    maxScaledAdvance,
                    options,
                    i,
                    ref boxLocation,
                    ref penLocation));
            }
        }
        else if (layoutMode is LayoutMode.VerticalRightLeft)
        {
            int index = 0;
            for (int i = textBox.TextLines.Count - 1; i >= 0; i--)
            {
                glyphs.AddRange(LayoutLineVertical(
                    textBox,
                    textBox.TextLines[i],
                    maxScaledAdvance,
                    options,
                    index++,
                    ref boxLocation,
                    ref penLocation));
            }
        }
        else if (layoutMode is LayoutMode.VerticalMixedLeftRight)
        {
            for (int i = 0; i < textBox.TextLines.Count; i++)
            {
                glyphs.AddRange(LayoutLineVerticalMixed(
                    textBox,
                    textBox.TextLines[i],
                    maxScaledAdvance,
                    options,
                    i,
                    ref boxLocation,
                    ref penLocation));
            }
        }
        else
        {
            int index = 0;
            for (int i = textBox.TextLines.Count - 1; i >= 0; i--)
            {
                glyphs.AddRange(LayoutLineVerticalMixed(
                    textBox,
                    textBox.TextLines[i],
                    maxScaledAdvance,
                    options,
                    index++,
                    ref boxLocation,
                    ref penLocation));
            }
        }

        return glyphs;
    }

    private static IEnumerable<GlyphLayout> LayoutLineHorizontal(
        TextBox textBox,
        TextLine textLine,
        float maxScaledAdvance,
        TextOptions options,
        int index,
        ref Vector2 boxLocation,
        ref Vector2 penLocation)
    {
        // Offset the location to center the line vertically.
        bool isFirstLine = index == 0;
        float lineHeight = textLine.ScaledMaxLineHeight;
        float advanceY = lineHeight * options.LineSpacing;
        float offsetY = (advanceY - lineHeight) * .5F;
        float yLineAdvance = advanceY - offsetY;

        float originX = penLocation.X;
        float offsetX = 0;

        // Set the Y-Origin for the line.
        if (isFirstLine)
        {
            switch (options.VerticalAlignment)
            {
                case VerticalAlignment.Center:
                    for (int i = 0; i < textBox.TextLines.Count; i++)
                    {
                        offsetY -= textBox.TextLines[i].ScaledMaxLineHeight * options.LineSpacing * .5F;
                    }

                    break;
                case VerticalAlignment.Bottom:
                    for (int i = 0; i < textBox.TextLines.Count; i++)
                    {
                        offsetY -= textBox.TextLines[i].ScaledMaxLineHeight * options.LineSpacing;
                    }

                    break;
            }
        }

        penLocation.Y += offsetY;

        // Set the X-Origin for horizontal alignment.
        switch (options.HorizontalAlignment)
        {
            case HorizontalAlignment.Right:
                offsetX = -maxScaledAdvance;
                break;
            case HorizontalAlignment.Center:
                offsetX = -(maxScaledAdvance * .5F);
                break;
        }

        // Set the alignment of lines within the text.
        if (textLine.Direction == TextDirection.LeftToRight)
        {
            switch (options.TextAlignment)
            {
                case TextAlignment.End:
                    offsetX += maxScaledAdvance - textLine.ScaledLineAdvance;
                    break;
                case TextAlignment.Center:
                    offsetX += (maxScaledAdvance * .5F) - (textLine.ScaledLineAdvance * .5F);
                    break;
            }
        }
        else
        {
            switch (options.TextAlignment)
            {
                case TextAlignment.Start:
                    offsetX += maxScaledAdvance - textLine.ScaledLineAdvance;
                    break;
                case TextAlignment.Center:
                    offsetX += (maxScaledAdvance * .5F) - (textLine.ScaledLineAdvance * .5F);
                    break;
            }
        }

        penLocation.X += offsetX;

        List<GlyphLayout> glyphs = [];
        for (int i = 0; i < textLine.Count; i++)
        {
            TextLine.GlyphLayoutData data = textLine[i];
            if (data.IsNewLine)
            {
                penLocation.X = originX;
                penLocation.Y += yLineAdvance;

                boxLocation.X = originX;
                boxLocation.Y += advanceY;
                continue;
            }

            Vector2 baselinePenLocation = penLocation + new Vector2(0, textLine.ScaledMaxAscender);
            foreach (GlyphMetrics metric in data.Metrics)
            {
                glyphs.Add(new GlyphLayout(
                    new Glyph(metric, data.PointSize),
                    boxLocation,
                    baselinePenLocation,
                    baselinePenLocation,
                    Vector2.Zero,
                    data.ScaledAdvance,
                    advanceY,
                    GlyphLayoutMode.Horizontal,
                    i == 0));
            }

            boxLocation.X += data.ScaledAdvance;
            penLocation.X += data.ScaledAdvance;
        }

        boxLocation.X = originX;
        penLocation.X = originX;
        if (glyphs.Count > 0)
        {
            penLocation.Y += yLineAdvance;
            boxLocation.Y += advanceY;
        }

        return glyphs;
    }

    private static IEnumerable<GlyphLayout> LayoutLineVertical(
        TextBox textBox,
        TextLine textLine,
        float maxScaledAdvance,
        TextOptions options,
        int index,
        ref Vector2 boxLocation,
        ref Vector2 penLocation)
    {
        float originY = penLocation.Y;
        float offsetY = 0;

        // Offset the location to center the line horizontally.
        float scaledMaxLineHeight = textLine.ScaledMaxLineHeight;
        float advanceX = scaledMaxLineHeight * options.LineSpacing;
        float offsetX = (advanceX - scaledMaxLineHeight) * .5F;
        float xLineAdvance = advanceX - offsetX;

        // Set the Y-Origin for the line.
        switch (options.VerticalAlignment)
        {
            case VerticalAlignment.Top:
                offsetY = 0;
                break;
            case VerticalAlignment.Center:
                offsetY -= maxScaledAdvance * .5F;
                break;
            case VerticalAlignment.Bottom:
                offsetY -= maxScaledAdvance;
                break;
        }

        // Set the alignment of lines within the text.
        if (textLine.Direction == TextDirection.LeftToRight)
        {
            switch (options.TextAlignment)
            {
                case TextAlignment.End:
                    offsetY += maxScaledAdvance - textLine.ScaledLineAdvance;
                    break;
                case TextAlignment.Center:
                    offsetY += (maxScaledAdvance * .5F) - (textLine.ScaledLineAdvance * .5F);
                    break;
            }
        }
        else
        {
            switch (options.TextAlignment)
            {
                case TextAlignment.Start:
                    offsetY += maxScaledAdvance - textLine.ScaledLineAdvance;
                    break;
                case TextAlignment.Center:
                    offsetY += (maxScaledAdvance * .5F) - (textLine.ScaledLineAdvance * .5F);
                    break;
            }
        }

        penLocation.Y += offsetY;

        bool isFirstLine = index == 0;
        if (isFirstLine)
        {
            // Set the X-Origin for horizontal alignment.
            switch (options.HorizontalAlignment)
            {
                case HorizontalAlignment.Right:
                    for (int i = 0; i < textBox.TextLines.Count; i++)
                    {
                        offsetX -= textBox.TextLines[i].ScaledMaxLineHeight * options.LineSpacing;
                    }

                    break;
                case HorizontalAlignment.Center:
                    for (int i = 0; i < textBox.TextLines.Count; i++)
                    {
                        offsetX -= textBox.TextLines[i].ScaledMaxLineHeight * options.LineSpacing * .5F;
                    }

                    break;
            }
        }

        penLocation.X += offsetX;

        List<GlyphLayout> glyphs = [];
        for (int i = 0; i < textLine.Count; i++)
        {
            TextLine.GlyphLayoutData data = textLine[i];
            if (data.IsNewLine)
            {
                boxLocation.X += advanceX;
                boxLocation.Y = originY;

                penLocation.X += xLineAdvance;
                penLocation.Y = originY;
                continue;
            }

            Vector2 uprightPenLocation = penLocation + new Vector2((scaledMaxLineHeight - data.ScaledLineHeight) * .5F, 0);
            foreach (GlyphMetrics metric in data.Metrics)
            {
                // Align the glyph horizontally and vertically centering horizontally around the baseline.
                Vector2 scale = new Vector2(data.PointSize) / metric.ScaleFactor;
                float oX = ((data.ScaledLineHeight - (metric.Bounds.Size().X * scale.X)) * .5F) - (metric.Bounds.Min.X * scale.X);
                Vector2 offset = new(oX, (metric.Bounds.Max.Y + metric.TopSideBearing) * scale.Y);

                glyphs.Add(new GlyphLayout(
                    new Glyph(metric, data.PointSize),
                    boxLocation,
                    uprightPenLocation,
                    uprightPenLocation,
                    offset,
                    advanceX,
                    data.ScaledAdvance,
                    GlyphLayoutMode.Vertical,
                    i == 0));
            }

            penLocation.Y += data.ScaledAdvance;
        }

        boxLocation.Y = originY;
        penLocation.Y = originY;
        if (glyphs.Count > 0)
        {
            boxLocation.X += advanceX;
            penLocation.X += xLineAdvance;
        }

        return glyphs;
    }

    private static IEnumerable<GlyphLayout> LayoutLineVerticalMixed(
        TextBox textBox,
        TextLine textLine,
        float maxScaledAdvance,
        TextOptions options,
        int index,
        ref Vector2 boxLocation,
        ref Vector2 penLocation)
    {
        float originY = penLocation.Y;
        float offsetY = 0;

        // Offset the location to center the line horizontally.
        float scaledMaxLineHeight = textLine.ScaledMaxLineHeight;
        float advanceX = scaledMaxLineHeight * options.LineSpacing;
        float offsetX = (advanceX - scaledMaxLineHeight) * .5F;
        float xLineAdvance = advanceX - offsetX;

        // Set the Y-Origin for the line.
        switch (options.VerticalAlignment)
        {
            case VerticalAlignment.Top:
                offsetY = 0;
                break;
            case VerticalAlignment.Center:
                offsetY -= maxScaledAdvance * .5F;
                break;
            case VerticalAlignment.Bottom:
                offsetY -= maxScaledAdvance;
                break;
        }

        // Set the alignment of lines within the text.
        if (textLine.Direction == TextDirection.LeftToRight)
        {
            switch (options.TextAlignment)
            {
                case TextAlignment.End:
                    offsetY += maxScaledAdvance - textLine.ScaledLineAdvance;
                    break;
                case TextAlignment.Center:
                    offsetY += (maxScaledAdvance * .5F) - (textLine.ScaledLineAdvance * .5F);
                    break;
            }
        }
        else
        {
            switch (options.TextAlignment)
            {
                case TextAlignment.Start:
                    offsetY += maxScaledAdvance - textLine.ScaledLineAdvance;
                    break;
                case TextAlignment.Center:
                    offsetY += (maxScaledAdvance * .5F) - (textLine.ScaledLineAdvance * .5F);
                    break;
            }
        }

        penLocation.Y += offsetY;

        bool isFirstLine = index == 0;
        if (isFirstLine)
        {
            // Set the X-Origin for horizontal alignment.
            switch (options.HorizontalAlignment)
            {
                case HorizontalAlignment.Right:
                    for (int i = 0; i < textBox.TextLines.Count; i++)
                    {
                        offsetX -= textBox.TextLines[i].ScaledMaxLineHeight * options.LineSpacing;
                    }

                    break;
                case HorizontalAlignment.Center:
                    for (int i = 0; i < textBox.TextLines.Count; i++)
                    {
                        offsetX -= textBox.TextLines[i].ScaledMaxLineHeight * options.LineSpacing * .5F;
                    }

                    break;
            }
        }

        penLocation.X += offsetX;

        List<GlyphLayout> glyphs = [];
        for (int i = 0; i < textLine.Count; i++)
        {
            TextLine.GlyphLayoutData data = textLine[i];
            if (data.IsNewLine)
            {
                boxLocation.X += advanceX;
                boxLocation.Y = originY;

                penLocation.X += xLineAdvance;
                penLocation.Y = originY;
                continue;
            }

            // A rotated glyph's pen moves onto its baseline, but its advance spans the same column as an upright glyph.
            Vector2 uprightPenLocation = penLocation + new Vector2((scaledMaxLineHeight - data.ScaledLineHeight) * .5F, 0);
            if (data.IsRotated)
            {
                foreach (GlyphMetrics metric in data.Metrics)
                {
                    // CSS Writing Modes 3 §4.2: rotated text sits on the central baseline, halfway between the
                    // used ascent and descent (hhea, or OS/2 typo with USE_TYPO_METRICS). Browsers centre it in
                    // the column whatever the line gap; see tests/tools/central_baseline.
                    float scaleY = data.PointSize / metric.ScaleFactor.Y;

                    // Some fonts store the descent as a positive number; the line metrics take its magnitude too.
                    HorizontalMetrics metrics = metric.FontMetrics.HorizontalMetrics;
                    float central = (metrics.Ascender - Math.Abs((int)metrics.Descender)) * .5F * scaleY;
                    glyphs.Add(new GlyphLayout(
                        new Glyph(metric, data.PointSize),
                        boxLocation,
                        penLocation + new Vector2((scaledMaxLineHeight * .5F) - central, 0),
                        uprightPenLocation,
                        Vector2.Zero,
                        advanceX,
                        data.ScaledAdvance,
                        GlyphLayoutMode.VerticalRotated,
                        i == 0));
                }
            }
            else
            {
                foreach (GlyphMetrics metric in data.Metrics)
                {
                    // Align the glyph horizontally and vertically centering horizontally around the baseline.
                    Vector2 scale = new Vector2(data.PointSize) / metric.ScaleFactor;
                    float oX = ((data.ScaledLineHeight - (metric.Bounds.Size().X * scale.X)) * .5F) - (metric.Bounds.Min.X * scale.X);
                    Vector2 offset = new(oX, (metric.Bounds.Max.Y + metric.TopSideBearing) * scale.Y);

                    glyphs.Add(new GlyphLayout(
                        new Glyph(metric, data.PointSize),
                        boxLocation,
                        uprightPenLocation,
                        uprightPenLocation,
                        offset,
                        advanceX,
                        data.ScaledAdvance,
                        GlyphLayoutMode.Vertical,
                        i == 0));
                }
            }

            penLocation.Y += data.ScaledAdvance;
        }

        boxLocation.Y = originY;
        penLocation.Y = originY;
        if (glyphs.Count > 0)
        {
            boxLocation.X += advanceX;
            penLocation.X += xLineAdvance;
        }

        return glyphs;
    }

    private static TextBox BreakLines(
        ReadOnlySpan<char> text,
        TextOptions options,
        BidiLevels bidiLevels,
        GlyphPositioningCollection positionings,
        LayoutMode layoutMode)
    {
        bool shouldWrap = options.WrappingLength > 0;
        float wrappingLength = shouldWrap ? options.WrappingLength / options.Dpi : float.MaxValue;
        bool breakAll = options.WordBreaking == WordBreaking.BreakAll;
        bool keepAll = options.WordBreaking == WordBreaking.KeepAll;
        bool breakWord = options.WordBreaking == WordBreaking.BreakWord;
        bool isHorizontalLayout = layoutMode.IsHorizontal();
        bool isVerticalMixedLayout = layoutMode.IsVerticalMixed();

        // Calculate the position of potential line breaks. A line breaks only between graphemes, so a break inside a
        // grapheme moves to the start of the next grapheme. When another break is at that start, the later one is kept.
        // A line must also break at the end of each bidi paragraph (UAX #9 P1), so that each line is in one paragraph:
        // U+001C to U+001E end a paragraph but are no mandatory break of UAX #14. A paragraph ends at a grapheme
        // boundary, and the last one ends with the text.
        var lineBreakEnumerator = new LineBreakEnumerator(text);
        var breakGraphemeEnumerator = new SpanGraphemeEnumerator(text);
        int graphemeEnd = 0;
        int paragraphIndex = 0;
        List<LineBreak> lineBreaks = [];
        while (lineBreakEnumerator.MoveNext())
        {
            LineBreak lineBreak = lineBreakEnumerator.Current;
            while (graphemeEnd < lineBreak.PositionWrap && breakGraphemeEnumerator.MoveNext())
            {
                graphemeEnd += CodePoint.GetCodePointCount(breakGraphemeEnumerator.Current);
            }

            if (graphemeEnd > lineBreak.PositionWrap)
            {
                lineBreak = new(lineBreak.PositionMeasure, graphemeEnd, lineBreak.Required);
            }

            for (; paragraphIndex < bidiLevels.Paragraphs.Length - 1 && bidiLevels.Paragraphs[paragraphIndex].End <= lineBreak.PositionWrap; paragraphIndex++)
            {
                int paragraphEnd = bidiLevels.Paragraphs[paragraphIndex].End;
                if (paragraphEnd == lineBreak.PositionWrap)
                {
                    lineBreak = new(lineBreak.PositionMeasure, paragraphEnd, true);
                }
                else
                {
                    AddLineBreak(lineBreaks, new(paragraphEnd, paragraphEnd, true));
                }
            }

            AddLineBreak(lineBreaks, lineBreak);
        }

        int lineBreakIndex = 0;
        LineBreak lastLineBreak = lineBreaks[lineBreakIndex];
        LineBreak currentLineBreak = lineBreaks[lineBreakIndex];
        int graphemeIndex;
        int codePointIndex = 0;
        float lineAdvance = 0;
        List<TextLine> textLines = [];
        TextLine textLine = new();
        int glyphCount = 0;

        // No glyph should contain more than 64 metrics.
        // We do a sanity check below just in case.
        Span<float> decomposedAdvancesBuffer = stackalloc float[64];

        // Enumerate through each grapheme in the text.
        var graphemeEnumerator = new SpanGraphemeEnumerator(text);
        for (graphemeIndex = 0; graphemeEnumerator.MoveNext(); graphemeIndex++)
        {
            // Now enumerate through each codepoint in the grapheme. A line breaks only at the first codepoint of the
            // grapheme that is laid out, and the codepoints skipped before it do not count.
            bool isGraphemeStart = true;
            var codePointEnumerator = new SpanCodePointEnumerator(graphemeEnumerator.Current);
            while (codePointEnumerator.MoveNext())
            {
                if (!positionings.TryGetGlyphMetricsAtOffset(codePointIndex, out float pointSize, out bool isDecomposed, out bool hasVerticalForm, out IReadOnlyList<GlyphMetrics>? metrics))
                {
                    // Codepoint was skipped during original enumeration.
                    codePointIndex++;
                    continue;
                }

                // Determine whether the glyph advance should be calculated using vertical or horizontal metrics
                // For vertical mixed layout we will be rotating glyphs with the vertical orientation type R or TR.
                CodePoint codePoint = codePointEnumerator.Current;
                VerticalOrientationType verticalOrientationType = CodePoint.GetVerticalOrientationType(codePoint);
                bool isRotated = isVerticalMixedLayout && verticalOrientationType is VerticalOrientationType.Rotate;
                if (isVerticalMixedLayout && verticalOrientationType is VerticalOrientationType.TransformRotate)
                {
                    // Tr: upright only when the font has a vertical form for the glyph; otherwise rotate.
                    isRotated = !hasVerticalForm;
                }

                if (CodePoint.IsVariationSelector(codePoint))
                {
                    codePointIndex++;
                    continue;
                }

                // Calculate the advance for the current codepoint.
                GlyphMetrics glyph = metrics[0];

                float glyphAdvance;

                // This should never happen, but we need to ensure that the buffer is large enough
                // if, for some crazy reason, a glyph does contain more than 64 metrics.
                Span<float> decomposedAdvances = metrics.Count > decomposedAdvancesBuffer.Length
                    ? new float[metrics.Count]
                    : decomposedAdvancesBuffer.Slice(0, isDecomposed ? metrics.Count : 1);

                if (isHorizontalLayout || isRotated)
                {
                    glyphAdvance = glyph.AdvanceWidth;
                }
                else
                {
                    glyphAdvance = glyph.AdvanceHeight;
                }

                decomposedAdvances[0] = glyphAdvance;

                if (CodePoint.IsTabulation(codePoint))
                {
                    if (options.TabWidth > -1F)
                    {
                        // Do not use the default font tab width. Instead find the advance for the space glyph
                        // and multiply that by the options value. A font without a space glyph falls back to
                        // a quarter of the em, so the option still takes effect.
                        float spaceAdvance = glyph.FontMetrics.UnitsPerEm * SpaceFallbackEmFraction;
                        CodePoint space = new(0x0020);
                        if (glyph.FontMetrics.TryGetGlyphId(space, out ushort spaceGlyphId))
                        {
                            GlyphMetrics spaceMetrics = glyph.FontMetrics.GetGlyphMetrics(
                                  space,
                                  spaceGlyphId,
                                  glyph.TextAttributes,
                                  glyph.TextDecorations,
                                  layoutMode,
                                  options.ColorFontSupport)[0];

                            spaceAdvance = isHorizontalLayout || isRotated
                                ? spaceMetrics.AdvanceWidth
                                : spaceMetrics.AdvanceHeight;
                        }

                        // The advance list is what reaches the line, so it keeps the exact (fractional) width.
                        // Glyph metrics store whole font units, so they are rounded rather than truncated.
                        glyphAdvance = spaceAdvance * options.TabWidth;
                        int storedAdvance = (int)Math.Clamp(Math.Round((double)glyphAdvance), int.MinValue, int.MaxValue);
                        if (isHorizontalLayout || isRotated)
                        {
                            glyph.SetAdvanceWidth(storedAdvance);
                        }
                        else
                        {
                            glyph.SetAdvanceHeight(storedAdvance);
                        }

                        decomposedAdvances[0] = glyphAdvance;
                    }
                }
                else if (!CodePoint.IsNewLine(codePoint))
                {
                    // Standard text.
                    // If decomposed we need to add the advance; otherwise, use the largest advance for the metrics.
                    if (isHorizontalLayout || isRotated)
                    {
                        for (int i = 1; i < metrics.Count; i++)
                        {
                            float a = metrics[i].AdvanceWidth;
                            if (isDecomposed)
                            {
                                glyphAdvance += a;
                                decomposedAdvances[i] = a;
                            }
                            else if (a > glyphAdvance)
                            {
                                glyphAdvance = a;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 1; i < metrics.Count; i++)
                        {
                            float a = metrics[i].AdvanceHeight;
                            if (isDecomposed)
                            {
                                glyphAdvance += a;
                                decomposedAdvances[i] = a;
                            }
                            else if (a > glyphAdvance)
                            {
                                glyphAdvance = a;
                            }
                        }
                    }
                }

                // Now scale the advance.
                if (isHorizontalLayout || isRotated)
                {
                    float scaleAX = pointSize / glyph.ScaleFactor.X;
                    glyphAdvance *= scaleAX;

                    for (int i = 0; i < decomposedAdvances.Length; i++)
                    {
                        decomposedAdvances[i] *= scaleAX;
                    }
                }
                else
                {
                    float scaleAY = pointSize / glyph.ScaleFactor.Y;
                    glyphAdvance *= scaleAY;
                    for (int i = 0; i < decomposedAdvances.Length; i++)
                    {
                        decomposedAdvances[i] *= scaleAY;
                    }
                }

                // Should we start a new line?
                bool requiredBreak = false;
                LineBreak? wrapAfterGlyph = null;
                if (isGraphemeStart)
                {
                    // Mandatory wrap at index. The line is empty only when the line break before the index has no glyph
                    // of its own, as when a font ligates it, and a line with no glyph has nothing to lay out. A break at
                    // a codepoint that is not laid out is taken at the first one after it that is.
                    if (currentLineBreak.PositionWrap <= codePointIndex && currentLineBreak.Required)
                    {
                        if (textLine.Count > 0)
                        {
                            textLines.Add(textLine);
                            glyphCount += textLine.Count;
                            textLine = new();
                        }

                        lineAdvance = 0;
                        requiredBreak = true;
                    }
                    else if (shouldWrap && !CodePoint.IsNewLine(codePoint) && lineAdvance + glyphAdvance >= wrappingLength)
                    {
                        // Forced wordbreak
                        if (textLine.Count == 0)
                        {
                            // Nothing to break. The glyph is wider than the available space
                            // so it must be placed on this line regardless.
                        }
                        else if (breakAll)
                        {
                            textLines.Add(textLine);
                            glyphCount += textLine.Count;
                            textLine = new();
                            lineAdvance = 0;
                        }
                        else if (currentLineBreak.PositionMeasure == codePointIndex)
                        {
                            // Exact length match. Check for CJK
                            if (keepAll)
                            {
                                TextLine split = textLine.SplitAt(lastLineBreak, keepAll);
                                if (split != textLine)
                                {
                                    textLines.Add(textLine);
                                    textLine = split;
                                    lineAdvance = split.ScaledLineAdvance;
                                }
                            }
                            else
                            {
                                textLines.Add(textLine);
                                glyphCount += textLine.Count;
                                textLine = new();
                                lineAdvance = 0;
                            }
                        }
                        else if (currentLineBreak.PositionWrap <= codePointIndex)
                        {
                            // The line wraps before its first glyph at or after the break. That glyph can be this one,
                            // so the line splits once this glyph is on it.
                            wrapAfterGlyph = currentLineBreak;
                        }
                        else if (lastLineBreak.PositionWrap < codePointIndex && !CodePoint.IsWhiteSpace(codePoint))
                        {
                            // Split the current text line into two at the last wrapping point if the current glyph
                            // does not represent whitespace. Whitespace characters will be correctly trimmed at the
                            // next iteration.
                            TextLine split = textLine.SplitAt(lastLineBreak, keepAll);
                            if (split != textLine)
                            {
                                textLines.Add(textLine);
                                textLine = split;
                                lineAdvance = split.ScaledLineAdvance;
                            }
                            else if (breakWord)
                            {
                                textLines.Add(textLine);
                                glyphCount += textLine.Count;
                                textLine = new();
                                lineAdvance = 0;
                            }
                        }
                        else if (breakWord)
                        {
                            textLines.Add(textLine);
                            glyphCount += textLine.Count;
                            textLine = new();
                            lineAdvance = 0;
                        }
                    }
                }

                isGraphemeStart = false;

                // Find the next line break.
                while (currentLineBreak.PositionWrap <= codePointIndex)
                {
                    lastLineBreak = currentLineBreak;
                    currentLineBreak = lineBreaks[++lineBreakIndex];
                }

                // Do not start a line following a break with breaking whitespace
                // unless the break was required.
                if (textLine.Count == 0
                    && textLines.Count > 0
                    && !requiredBreak
                    && CodePoint.IsWhiteSpace(codePoint)
                    && !CodePoint.IsNonBreakingSpace(codePoint)
                    && !CodePoint.IsTabulation(codePoint)
                    && !CodePoint.IsNewLine(codePoint))
                {
                    codePointIndex++;
                    continue;
                }

                if (textLine.Count > 0 && CodePoint.IsNewLine(codePoint))
                {
                    // Do not add new lines unless at position zero.
                    codePointIndex++;
                    continue;
                }

                // For non-decomposed glyphs the length is always 1.
                for (int i = 0; i < decomposedAdvances.Length; i++)
                {
                    float decomposedAdvance = decomposedAdvances[i];

                    // Work out the scaled metrics for the glyph.
                    GlyphMetrics metric = metrics[i];
                    float scaleY = pointSize / metric.ScaleFactor.Y;
                    IMetricsHeader metricsHeader = isHorizontalLayout || isRotated
                        ? metric.FontMetrics.HorizontalMetrics
                        : metric.FontMetrics.VerticalMetrics;
                    float ascender = metricsHeader.Ascender * scaleY;

                    // Adjust ascender for glyphs with a negative tsb. e.g. emoji to prevent cutoff.
                    if (!CodePoint.IsWhiteSpace(codePoint))
                    {
                        if (!isDecomposed)
                        {
                            short tsbOffset = 0;

                            // We need to check all the metrics.
                            for (int mi = 0; mi < metrics.Count; mi++)
                            {
                                tsbOffset = Math.Min(tsbOffset, metrics[mi].TopSideBearing);
                            }

                            if (tsbOffset < 0)
                            {
                                ascender -= tsbOffset * scaleY;
                            }
                        }
                        else
                        {
                            // Decomposed glyphs contain a single metric.
                            short tsbOffset = metric.TopSideBearing;
                            if (tsbOffset < 0)
                            {
                                ascender -= tsbOffset * scaleY;
                            }
                        }
                    }

                    // Match how line height is calculated for browsers.
                    // https://www.w3.org/TR/CSS2/visudet.html#propdef-line-height
                    float descender = Math.Abs(metricsHeader.Descender * scaleY);
                    float lineHeight = metric.UnitsPerEm * scaleY;
                    float delta = ((metricsHeader.LineHeight * scaleY) - lineHeight) * .5F;
                    ascender -= delta;
                    descender -= delta;

                    // Add our metrics to the line.
                    lineAdvance += decomposedAdvance;
                    textLine.Add(
                        isDecomposed ? new GlyphMetrics[] { metric } : metrics,
                        pointSize,
                        decomposedAdvance,
                        lineHeight,
                        ascender,
                        descender,
                        graphemeIndex,
                        codePointIndex,
                        isRotated,
                        isDecomposed);
                }

                if (wrapAfterGlyph is LineBreak wrap)
                {
                    TextLine split = textLine.SplitAt(wrap, keepAll);
                    if (split != textLine)
                    {
                        textLines.Add(textLine);
                        textLine = split;
                        lineAdvance = split.ScaledLineAdvance;
                    }
                }

                codePointIndex++;
            }
        }

        // Add the final line.
        if (textLine.Count > 0)
        {
            textLines.Add(textLine);
        }

        // Each line is in one paragraph, and is ordered and aligned in its direction (UAX #9 P2 and P3 under Auto). Its
        // glyphs are still in logical order, so the first holds its first codepoint.
        foreach (TextLine line in textLines)
        {
            sbyte paragraphLevel = bidiLevels.Paragraphs[BidiAlgorithm.FindParagraph(bidiLevels.Paragraphs, line[0].Offset)].Level;
            line.Direction = (paragraphLevel & 1) == 0 ? TextDirection.LeftToRight : TextDirection.RightToLeft;

            // When every level is 0, rule L1 changes none and no line has a run to reverse.
            if (!bidiLevels.IsLeftToRight)
            {
                line.OrderVisually(bidiLevels, paragraphLevel);
            }
        }

        return new TextBox(options, textLines);

        static void AddLineBreak(List<LineBreak> lineBreaks, LineBreak lineBreak)
        {
            if (lineBreaks.Count > 0 && lineBreaks[^1].PositionWrap == lineBreak.PositionWrap)
            {
                lineBreaks[^1] = lineBreak;
            }
            else
            {
                lineBreaks.Add(lineBreak);
            }
        }
    }

    /// <summary>
    /// The bidi types and resolved levels of the codepoints of the text, and its paragraphs. Rule L1 applies to each line,
    /// and <see cref="TextLine.OrderVisually"/> applies it to the levels of the line in place.
    /// </summary>
    /// <param name="Types">The bidi type of each codepoint.</param>
    /// <param name="Levels">The level of each codepoint, before rule L1 until its line is ordered.</param>
    /// <param name="Paragraphs">The paragraphs of the text, each with its embedding level (UAX #9 P1 to P3).</param>
    internal readonly record struct BidiLevels(ReadOnlyArraySlice<BidiCharacterType> Types, sbyte[] Levels, BidiAlgorithm.Paragraph[] Paragraphs)
    {
        /// <summary>
        /// Gets a value indicating whether the level of every paragraph and every codepoint is 0.
        /// </summary>
        public bool IsLeftToRight { get; } = Array.TrueForAll(Paragraphs, paragraph => paragraph.Level == 0) && Array.TrueForAll(Levels, level => level == 0);
    }

    internal sealed class TextBox
    {
        public TextBox(TextOptions options, IReadOnlyList<TextLine> textLines)
        {
            this.TextLines = textLines;
            for (int i = 0; i < this.TextLines.Count - 1; i++)
            {
                this.TextLines[i].Justify(options);
            }
        }

        public IReadOnlyList<TextLine> TextLines { get; }

        /// <summary>
        /// Gets the advance of the longest line, or 0 when no codepoint of the text has a glyph to lay out.
        /// </summary>
        /// <returns>The advance.</returns>
        public float ScaledMaxAdvance()
            => this.TextLines.Count == 0 ? 0 : this.TextLines.Max(x => x.ScaledLineAdvance);
    }

    internal sealed class TextLine
    {
        private readonly List<GlyphLayoutData> data = [];

        public int Count => this.data.Count;

        /// <summary>
        /// Gets or sets the direction of the paragraph that the line is in, which sets the edge that
        /// <see cref="TextAlignment.Start"/> aligns it to.
        /// </summary>
        public TextDirection Direction { get; set; }

        public float ScaledLineAdvance { get; private set; } = 0;

        public float ScaledMaxLineHeight { get; private set; } = -1;

        public float ScaledMaxAscender { get; private set; } = -1;

        public float ScaledMaxDescender { get; private set; } = -1;

        public GlyphLayoutData this[int index] => this.data[index];

        public void Add(
            IReadOnlyList<GlyphMetrics> metrics,
            float pointSize,
            float scaledAdvance,
            float scaledLineHeight,
            float scaledAscender,
            float scaledDescender,
            int graphemeIndex,
            int offset,
            bool isRotated,
            bool isDecomposed)
        {
            // Reset metrics.
            // We track the maximum metrics for each line to ensure glyphs can be aligned.
            this.ScaledLineAdvance += scaledAdvance;
            this.ScaledMaxLineHeight = MathF.Max(this.ScaledMaxLineHeight, scaledLineHeight);
            this.ScaledMaxAscender = MathF.Max(this.ScaledMaxAscender, scaledAscender);
            this.ScaledMaxDescender = MathF.Max(this.ScaledMaxDescender, scaledDescender);

            this.data.Add(new(
                metrics,
                pointSize,
                scaledAdvance,
                scaledLineHeight,
                scaledAscender,
                scaledDescender,
                graphemeIndex,
                offset,
                isRotated,
                isDecomposed));
        }

        public TextLine SplitAt(LineBreak lineBreak, bool keepAll)
        {
            // The line breaks before its first glyph at or after the break, as the codepoint at the break may have none.
            int index = this.data.Count;
            while (index > 0 && this.data[index - 1].Offset >= lineBreak.PositionWrap)
            {
                index--;
            }

            if (index == 0 || index == this.data.Count)
            {
                return this;
            }

            GlyphLayoutData glyphWrap = this.data[index];

            // A solidus allows a break after it but, as recommended by UAX #14, prefer an earlier
            // opportunity after whitespace if one exists on this line so words like "and/or" or
            // paths are kept together where possible.
            if (this.data[index - 1].CodePoint.Value == 0x2F)
            {
                for (int i = index - 1; i > 0; i--)
                {
                    CodePoint previous = this.data[i - 1].CodePoint;
                    if (CodePoint.IsWhiteSpace(previous)
                        && !CodePoint.IsNonBreakingSpace(previous)
                        && !CodePoint.IsWhiteSpace(this.data[i].CodePoint))
                    {
                        index = i;
                        glyphWrap = this.data[index];
                        break;
                    }
                }
            }

            // Word breaks should not be used for Chinese/Japanese/Korean (CJK) text
            // when word-breaking mode is keep-all.
            if (keepAll && UnicodeUtility.IsCJKCodePoint((uint)glyphWrap.CodePoint.Value))
            {
                // Loop through previous glyphs to see if there is
                // a non CJK codepoint we can break at.
                while (index > 0)
                {
                    glyphWrap = this.data[--index];
                    if (!UnicodeUtility.IsCJKCodePoint((uint)glyphWrap.CodePoint.Value))
                    {
                        index++;
                        break;
                    }
                }

                if (index == 0)
                {
                    return this;
                }
            }

            // If everything before the split point is whitespace there is nothing meaningful to keep on this
            // line once the whitespace is trimmed, so do not split.
            bool hasContent = false;
            for (int i = 0; i < index; i++)
            {
                if (!CodePoint.IsWhiteSpace(this.data[i].CodePoint))
                {
                    hasContent = true;
                    break;
                }
            }

            if (!hasContent)
            {
                return this;
            }

            // Create a new line ensuring we capture the initial metrics.
            TextLine result = new();
            result.data.AddRange(this.data.GetRange(index, this.data.Count - index));
            result.ScaledLineAdvance = result.data.Sum(x => x.ScaledAdvance);
            result.ScaledMaxAscender = result.data.Max(x => x.ScaledAscender);
            result.ScaledMaxDescender = result.data.Max(x => x.ScaledDescender);
            result.ScaledMaxLineHeight = result.data.Max(x => x.ScaledLineHeight);

            // Remove those items from this line.
            this.data.RemoveRange(index, this.data.Count - index);

            // Now trim trailing whitespace from this line.
            index = this.data.Count;
            while (index > 0)
            {
                if (!CodePoint.IsWhiteSpace(this.data[index - 1].CodePoint))
                {
                    break;
                }

                index--;
            }

            if (index < this.data.Count)
            {
                this.data.RemoveRange(index, this.data.Count - index);
            }

            // Lastly recalculate this line metrics.
            this.ScaledLineAdvance = this.data.Sum(x => x.ScaledAdvance);
            this.ScaledMaxAscender = this.data.Max(x => x.ScaledAscender);
            this.ScaledMaxDescender = this.data.Max(x => x.ScaledDescender);
            this.ScaledMaxLineHeight = this.data.Max(x => x.ScaledLineHeight);

            return result;
        }

        public void Justify(TextOptions options)
        {
            if (options.WrappingLength == -1F || options.TextJustification == TextJustification.None)
            {
                return;
            }

            if (this.ScaledLineAdvance == 0)
            {
                return;
            }

            float delta = (options.WrappingLength / options.Dpi) - this.ScaledLineAdvance;
            if (delta <= 0)
            {
                return;
            }

            // The whitespace at the logical end of the line takes no padding, whichever side of the line rule L1 draws
            // it on. The glyphs are in visual order, and padding goes after a glyph, so the last glyph before that
            // whitespace, or before the end of the line, takes none either.
            int contentEnd = -1;
            foreach (GlyphLayoutData glyph in this.data)
            {
                if (!CodePoint.IsWhiteSpace(glyph.CodePoint))
                {
                    contentEnd = Math.Max(contentEnd, glyph.Offset);
                }
            }

            int last = this.data.Count - 1;
            while (last >= 0 && CodePoint.IsWhiteSpace(this.data[last].CodePoint) && this.data[last].Offset > contentEnd)
            {
                last--;
            }

            bool interWord = options.TextJustification == TextJustification.InterWord;
            int count = 0;
            for (int i = 0; i < last; i++)
            {
                if (TakesPadding(this.data[i], contentEnd, interWord))
                {
                    count++;
                }
            }

            if (count == 0)
            {
                return;
            }

            float padding = delta / count;
            for (int i = 0; i < last; i++)
            {
                GlyphLayoutData glyph = this.data[i];
                if (TakesPadding(glyph, contentEnd, interWord))
                {
                    glyph.ScaledAdvance += padding;
                    this.data[i] = glyph;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether justification pads the glyph: whitespace before the end of the content of the
        /// line, or with <see cref="TextJustification.InterCharacter"/> any glyph but a ZWJ or ZWNJ.
        /// </summary>
        /// <param name="glyph">The glyph.</param>
        /// <param name="contentEnd">The offset of the last codepoint of the line that is not whitespace.</param>
        /// <param name="interWord">Whether only whitespace takes padding.</param>
        /// <returns><see langword="true"/> if the glyph takes padding.</returns>
        private static bool TakesPadding(GlyphLayoutData glyph, int contentEnd, bool interWord)
        {
            CodePoint codePoint = glyph.CodePoint;
            if (CodePoint.IsWhiteSpace(codePoint))
            {
                return glyph.Offset < contentEnd;
            }

            return !interWord && !CodePoint.IsZeroWidthJoiner(codePoint) && !CodePoint.IsZeroWidthNonJoiner(codePoint);
        }

        /// <summary>
        /// Orders the glyphs of the line visually. Rule L1 is applied in place to the levels of the codepoints of the
        /// line, then rule L2 orders the glyphs by the levels it gives. It changes only the levels of the whitespace and
        /// of the characters that rule X9 removes, and no two lines hold the same one of those.
        /// </summary>
        /// <param name="bidiLevels">The bidi types and levels of the text, before rule L1 for this line.</param>
        /// <param name="paragraphLevel">The embedding level of the paragraph that holds the line.</param>
        public void OrderVisually(BidiLevels bidiLevels, sbyte paragraphLevel)
        {
            // The glyphs are in logical order, so the line holds the codepoints from the first glyph to the last.
            int lineStart = this.data[0].Offset;
            int lineLength = this.data[^1].Offset + 1 - lineStart;
            ArraySlice<sbyte> lineLevels = new(bidiLevels.Levels, lineStart, lineLength);
            ReadOnlyArraySlice<BidiCharacterType> lineTypes = bidiLevels.Types.Slice(lineStart, lineLength);
            BidiAlgorithm.ResetWhitespaceLevels(lineTypes, lineLevels, paragraphLevel);

            // A character that rule X9 removes at the start of the line has no character before it on the line, and
            // keeps the level of the one before it in the paragraph, which ends the line before. It takes the level of
            // the character after it, so it is drawn in that run, as BidiParagraph.GetVisualRuns draws a joiner there.
            int first = 0;
            while (first < lineLength && BidiAlgorithm.IsRemovedByX9(lineTypes[first]))
            {
                first++;
            }

            if (first < lineLength)
            {
                lineLevels.Span.Slice(0, first).Fill(lineLevels[first]);
            }

            // Build up the collection of ordered runs.
            OrderedBidiRun orderedRun = new(lineLevels[0]);
            OrderedBidiRun? current = orderedRun;
            for (int i = 0; i < this.data.Count; i++)
            {
                GlyphLayoutData g = this.data[i];
                int level = lineLevels[g.Offset - lineStart];
                if (level != current.Level)
                {
                    current.Next = new(level);
                    current = current.Next;
                }

                current.Add(g);
            }

            // Reorder them into visual order.
            orderedRun = LinearReOrder(orderedRun);

            // Now perform a recursive reversal of each run.
            // From the highest level found in the text to the lowest odd level on each line, including intermediate levels
            // not actually present in the text, reverse any contiguous sequence of characters that are at that level or higher.
            // https://unicode.org/reports/tr9/#L2
            int max = 0;
            int min = int.MaxValue;
            for (int i = 0; i < this.data.Count; i++)
            {
                int level = lineLevels[this.data[i].Offset - lineStart];
                if (level > max)
                {
                    max = level;
                }

                if ((level & 1) != 0 && level < min)
                {
                    min = level;
                }
            }

            if (min > max)
            {
                min = max;
            }

            if (max == 0 || (min == max && (max & 1) == 0))
            {
                // Nothing to reverse.
                return;
            }

            // Now apply the reversal and replace the original contents.
            int minLevelToReverse = max;
            while (minLevelToReverse >= min)
            {
                current = orderedRun;
                while (current != null)
                {
                    if (current.Level >= minLevelToReverse)
                    {
                        current.Reverse();
                    }

                    current = current.Next;
                }

                minLevelToReverse--;
            }

            this.data.Clear();
            current = orderedRun;
            while (current != null)
            {
                this.data.AddRange(current.AsSlice());
                current = current.Next;
            }
        }

        /// <summary>
        /// Reorders a series of runs from logical to visual order, returning the left most run.
        /// <see href="https://github.com/fribidi/linear-reorder/blob/f2f872257d4d8b8e137fcf831f254d6d4db79d3c/linear-reorder.c"/>
        /// </summary>
        /// <param name="line">The ordered bidi run.</param>
        /// <returns>The <see cref="OrderedBidiRun"/>.</returns>
        private static OrderedBidiRun LinearReOrder(OrderedBidiRun? line)
        {
            BidiRange? range = null;
            OrderedBidiRun? run = line;

            while (run != null)
            {
                OrderedBidiRun? next = run.Next;

                while (range != null && range.Level > run.Level
                    && range.Previous != null && range.Previous.Level >= run.Level)
                {
                    range = BidiRange.MergeWithPrevious(range);
                }

                if (range != null && range.Level >= run.Level)
                {
                    // Attach run to the range.
                    if ((run.Level & 1) != 0)
                    {
                        // Odd, range goes to the right of run.
                        run.Next = range.Left;
                        range.Left = run;
                    }
                    else
                    {
                        // Even, range goes to the left of run.
                        range.Right!.Next = run;
                        range.Right = run;
                    }

                    range.Level = run.Level;
                }
                else
                {
                    BidiRange r = new();
                    r.Left = r.Right = run;
                    r.Level = run.Level;
                    r.Previous = range;
                    range = r;
                }

                run = next;
            }

            while (range?.Previous != null)
            {
                range = BidiRange.MergeWithPrevious(range);
            }

            // Terminate.
            range!.Right!.Next = null;
            return range!.Left!;
        }

        [DebuggerDisplay("{DebuggerDisplay,nq}")]
        internal struct GlyphLayoutData
        {
            public GlyphLayoutData(
                IReadOnlyList<GlyphMetrics> metrics,
                float pointSize,
                float scaledAdvance,
                float scaledLineHeight,
                float scaledAscender,
                float scaledDescender,
                int graphemeIndex,
                int offset,
                bool isRotated,
                bool isDecomposed)
            {
                this.Metrics = metrics;
                this.PointSize = pointSize;
                this.ScaledAdvance = scaledAdvance;
                this.ScaledLineHeight = scaledLineHeight;
                this.ScaledAscender = scaledAscender;
                this.ScaledDescender = scaledDescender;
                this.GraphemeIndex = graphemeIndex;
                this.Offset = offset;
                this.IsRotated = isRotated;
                this.IsDecomposed = isDecomposed;
            }

            public readonly CodePoint CodePoint => this.Metrics[0].CodePoint;

            public IReadOnlyList<GlyphMetrics> Metrics { get; }

            public float PointSize { get; }

            public float ScaledAdvance { get; set; }

            public float ScaledLineHeight { get; }

            public float ScaledAscender { get; }

            public float ScaledDescender { get; }

            public int GraphemeIndex { get; }

            public int Offset { get; }

            public bool IsRotated { get; }

            public bool IsDecomposed { get; }

            public readonly bool IsNewLine => CodePoint.IsNewLine(this.CodePoint);

            private readonly string DebuggerDisplay => FormattableString
                .Invariant($"{this.CodePoint.ToDebuggerDisplay()} : {this.Offset}");
        }

        private sealed class OrderedBidiRun
        {
            private ArrayBuilder<GlyphLayoutData> info;

            public OrderedBidiRun(int level) => this.Level = level;

            public int Level { get; }

            public OrderedBidiRun? Next { get; set; }

            public void Add(GlyphLayoutData info) => this.info.Add(info);

            public ArraySlice<GlyphLayoutData> AsSlice() => this.info.AsSlice();

            public void Reverse() => this.AsSlice().Span.Reverse();
        }

        private sealed class BidiRange
        {
            public int Level { get; set; }

            public OrderedBidiRun? Left { get; set; }

            public OrderedBidiRun? Right { get; set; }

            public BidiRange? Previous { get; set; }

            public static BidiRange MergeWithPrevious(BidiRange? range)
            {
                BidiRange previous = range!.Previous!;
                BidiRange left;
                BidiRange right;

                if ((previous.Level & 1) != 0)
                {
                    // Odd, previous goes to the right of range.
                    left = range;
                    right = previous;
                }
                else
                {
                    // Even, previous goes to the left of range.
                    left = previous;
                    right = range;
                }

                // Stitch them
                left.Right!.Next = right.Left;
                previous.Left = left.Left;
                previous.Right = right.Right;

                return previous;
            }
        }
    }
}
