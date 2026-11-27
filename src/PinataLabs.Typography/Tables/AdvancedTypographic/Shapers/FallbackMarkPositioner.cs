// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.Unicode.Resources;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Positions the marks of text that the GPOS table of the font does not position, as HarfBuzz does. A mark loses its advance, and the
/// shapers that <see cref="ShaperFactory.HasFallbackPositioning"/> names place it on its base from the boxes
/// of the two glyphs. <see cref="UnicodeData.GetCombiningClassData"/> gives the side of the base for each mark.
/// </summary>
internal static class FallbackMarkPositioner
{
    // HarfBuzz leaves a sixteenth of an em between a base and a mark that does not touch it.
    private const int GapDivisor = 16;

    /// <summary>
    /// Takes the advance of each mark away for the shapers that do so before they position glyphs.
    /// </summary>
    /// <param name="fontMetrics">The font.</param>
    /// <param name="collection">The glyphs of the text.</param>
    /// <param name="gpos">The GPOS table of the font, if it has one.</param>
    public static void ZeroMarksBeforeKerning(FontMetrics fontMetrics, GlyphPositioningCollection collection, GPosTable? gpos)
    {
        int start = 0;
        while (start < collection.Count)
        {
            int end = GetScriptRunEnd(collection, start, out ScriptClass script);
            if (gpos?.PositionsScript(script) != true && GetMarkZeroingMode(fontMetrics, script, collection.TextOptions) == MarkZeroingMode.PreGPos)
            {
                ZeroMarks(fontMetrics, collection, start, end);
                UpdatePositions(fontMetrics, collection, start, end);
            }

            start = end;
        }
    }

    /// <summary>
    /// Takes the advance of each mark away for the other shapers, and places each mark on its base.
    /// </summary>
    /// <param name="fontMetrics">The font.</param>
    /// <param name="collection">The glyphs of the text.</param>
    /// <param name="gpos">The GPOS table of the font, if it has one.</param>
    public static void PlaceMarksAfterKerning(FontMetrics fontMetrics, GlyphPositioningCollection collection, GPosTable? gpos)
    {
        int start = 0;
        while (start < collection.Count)
        {
            int end = GetScriptRunEnd(collection, start, out ScriptClass script);
            if (gpos?.PositionsScript(script) == true)
            {
                start = end;
                continue;
            }

            bool zeroes = GetMarkZeroingMode(fontMetrics, script, collection.TextOptions) == MarkZeroingMode.PostGpos;
            bool places = ShaperFactory.HasFallbackPositioning(script, fontMetrics.GetGSubTable().GetScriptTag(script));
            if (zeroes)
            {
                ZeroMarks(fontMetrics, collection, start, end);
            }

            if (places)
            {
                PlaceMarks(fontMetrics, collection, start, end);
            }

            if (zeroes || places)
            {
                UpdatePositions(fontMetrics, collection, start, end);
            }

            start = end;
        }
    }

    private static MarkZeroingMode GetMarkZeroingMode(FontMetrics fontMetrics, ScriptClass script, TextOptions options)
        => ShaperFactory.Create(script, fontMetrics.GetGSubTable().GetScriptTag(script), options).MarkZeroingMode;

    private static void ZeroMarks(FontMetrics fontMetrics, GlyphPositioningCollection collection, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            GlyphShapingData data = collection[i];
            if (!collection.ShouldProcess(fontMetrics, i) || !AdvancedTypographicUtils.IsMarkGlyph(fontMetrics, data))
            {
                continue;
            }

            // The mark was drawn after the glyph before it. In text that runs forward it moves back by its advance,
            // so that it hangs over that glyph. In text that runs backward it already hangs over the glyph after it.
            if (IsPlaceable(collection, data) && data.Direction == TextDirection.LeftToRight)
            {
                data.Bounds.X -= GetAdvance(collection, i);
            }

            data.Bounds.Width = 0;
            data.Bounds.Height = 0;
        }
    }

    // As in HarfBuzz, a dotted circle that a shaper inserted has the Unicode properties that it copied.
    private static bool IsUnicodeMark(GlyphShapingData data)
        => !data.IsOtherLetter && CodePoint.IsMark(data.UnicodePropertiesCodePoint);

    private static void PlaceMarks(FontMetrics fontMetrics, GlyphPositioningCollection collection, int start, int end)
    {
        int index = start;
        while (index < end)
        {
            if (IsUnicodeMark(collection[index]))
            {
                index++;
                continue;
            }

            // As in HarfBuzz, the marks of a base go on past a default-ignorable glyph. Such a glyph takes no advance and no
            // place, so it may come from another font.
            int marksEnd = index + 1;
            bool canPlace = collection.ShouldProcess(fontMetrics, index) && IsPlaceable(collection, collection[index]);
            while (marksEnd < end && (IsUnicodeMark(collection[marksEnd]) || collection[marksEnd].IsDefaultIgnorable))
            {
                canPlace &= collection[marksEnd].IsDefaultIgnorable || collection.ShouldProcess(fontMetrics, marksEnd);
                marksEnd++;
            }

            if (canPlace && marksEnd > index + 1)
            {
                PlaceAroundBase(fontMetrics, collection, index, marksEnd);
            }

            index = marksEnd;
        }
    }

    private static void PlaceAroundBase(FontMetrics fontMetrics, GlyphPositioningCollection collection, int baseIndex, int end)
    {
        GlyphShapingData baseData = collection[baseIndex];
        GlyphMetrics baseMetrics = collection.GetMetrics(baseIndex);
        bool isForward = baseData.Direction == TextDirection.LeftToRight;
        int gap = fontMetrics.UnitsPerEm / GapDivisor;

        // The advance of the base, not its box, aligns the mark across the line. So a base without ink also works.
        // It is the advance that the font gives the base. Kerning moves the base and leaves its marks in place.
        Box baseBox = Box.Of(baseMetrics);
        baseBox.Top += baseData.Bounds.Y;
        baseBox.Left = 0;
        baseBox.Width = GetFontAdvance(fontMetrics, collection, baseData);

        // The pen is after the base in text that runs forward, and still before it in text that runs backward.
        int penOffset = isForward ? -GetAdvance(collection, baseIndex) : 0;
        int components = GetComponentCount(fontMetrics, baseData);
        int lastComponent = -1;
        Box componentBox = baseBox;
        Box stack = baseBox;
        (MarkAlignment Alignment, MarkSide Side) last = default;
        for (int i = baseIndex + 1; i < end; i++)
        {
            GlyphShapingData data = collection[i];
            uint placement = UnicodeData.GetCombiningClassData((uint)data.UnicodePropertiesCodePoint.Value);
            MarkAlignment alignment = (MarkAlignment)((placement >> CanonicalMappingData.MarkAlignmentShift) & CanonicalMappingData.MarkPlacementMask);
            MarkSide side = (MarkSide)((placement >> CanonicalMappingData.MarkSideShift) & CanonicalMappingData.MarkPlacementMask);
            if (alignment == MarkAlignment.None)
            {
                // HarfBuzz has taken the advance of a default-ignorable glyph away by now, and the layout does so later.
                if (!data.IsDefaultIgnorable)
                {
                    penOffset += isForward ? -GetAdvance(collection, i) : GetAdvance(collection, i);
                }

                continue;
            }

            // A mark of a ligature goes on its own component, an equal part of the ligature.
            int component = GetComponent(baseData, data, components);
            if (component != lastComponent)
            {
                lastComponent = component;
                componentBox = baseBox;
                componentBox.Left += (isForward ? component : components - 1 - component) * baseBox.Width / components;
                componentBox.Width /= components;
                last = default;
            }

            // Marks of one placement stack away from the base. The first mark of another placement starts at the base again.
            if (last != (alignment, side))
            {
                last = (alignment, side);
                stack = componentBox;
            }

            Box mark = Box.Of(collection.GetMetrics(i));
            data.Bounds.X = penOffset + Align(stack, mark, alignment, isForward);
            data.Bounds.Y = Stack(ref stack, mark, side, gap);
            data.Bounds.Width = 0;
            data.Bounds.Height = 0;
        }
    }

    // Only a ligature of components has parts for marks. A base that GSUB composed with marks is still one base.
    private static int GetComponentCount(FontMetrics fontMetrics, GlyphShapingData baseData)
        => baseData.LigatureId > 0 && AdvancedTypographicUtils.GetGlyphShapingClass(fontMetrics, baseData.GlyphId, baseData).IsLigature
            ? Math.Max(baseData.CodePointCount, 1)
            : 1;

    // A mark that GSUB did not give a component of this ligature, such as a mark after the ligature, goes on its last component.
    private static int GetComponent(GlyphShapingData baseData, GlyphShapingData mark, int components)
        => mark.LigatureId == baseData.LigatureId && mark.LigatureComponent > 0 && mark.LigatureComponent <= components
            ? mark.LigatureComponent - 1
            : components - 1;

    private static int Align(Box stack, Box mark, MarkAlignment alignment, bool isForward)
        => alignment switch
        {
            MarkAlignment.Left => stack.Left - mark.Left,
            MarkAlignment.Right => stack.Left + stack.Width - mark.Width - mark.Left,

            // A double mark spans its base and the next one, so its middle is at the end of its base.
            MarkAlignment.Double when isForward => stack.Left + stack.Width - (mark.Width / 2) - mark.Left,
            MarkAlignment.Double => stack.Left - (mark.Width / 2) - mark.Left,
            _ => stack.Left + ((stack.Width - mark.Width) / 2) - mark.Left,
        };

    /// <summary>
    /// Gets how far the mark moves up to sit on its side of the stack, and adds the mark to the stack.
    /// </summary>
    private static int Stack(ref Box stack, Box mark, MarkSide side, int gap)
    {
        int offset;
        switch (side)
        {
            case MarkSide.Below:
            case MarkSide.AttachedBelow:
                if (side == MarkSide.Below)
                {
                    stack.Height -= gap;
                }

                offset = stack.Top + stack.Height - mark.Top;

                // A mark below the base never moves up.
                if (offset > 0)
                {
                    stack.Height -= offset;
                    offset = 0;
                }

                stack.Height += mark.Height;
                return offset;

            case MarkSide.Above:
            case MarkSide.AttachedAbove:
                if (side == MarkSide.Above)
                {
                    stack.Top += gap;
                    stack.Height -= gap;
                }

                offset = stack.Top - (mark.Top + mark.Height);

                // A mark above the base moves down by half of the distance only.
                if (offset < 0)
                {
                    int correction = -offset / 2;
                    stack.Top += correction;
                    stack.Height -= correction;
                    offset += correction;
                }

                stack.Top -= mark.Height;
                stack.Height += mark.Height;
                return offset;

            default:
                return 0;
        }
    }

    // The marks of a glyph that advances down the line only lose their advance.
    private static bool IsPlaceable(GlyphPositioningCollection collection, GlyphShapingData data)
        => !AdvancedTypographicUtils.IsVerticalGlyph(data.CodePoint, collection.TextOptions.LayoutMode, data.HasVerticalForm);

    // A mark that has lost its advance in this pass has not yet given the change to its metrics.
    private static int GetAdvance(GlyphPositioningCollection collection, int index)
    {
        GlyphShapingData data = collection[index];
        return data.Bounds.IsDirtyWH ? data.Bounds.Width : collection.GetMetrics(index).AdvanceWidth;
    }

    private static int GetFontAdvance(FontMetrics fontMetrics, GlyphPositioningCollection collection, GlyphShapingData data)
    {
        TextOptions options = collection.TextOptions;
        return fontMetrics.GetGlyphMetrics(
            data.CodePoint,
            data.GlyphId,
            data.TextRun.TextAttributes,
            data.TextRun.TextDecorations,
            options.LayoutMode,
            options.ColorFontSupport,
            data.HasVerticalForm)[0].AdvanceWidth;
    }

    private static void UpdatePositions(FontMetrics fontMetrics, GlyphPositioningCollection collection, int start, int end)
    {
        for (int i = start; i < end; i++)
        {
            if (collection.ShouldProcess(fontMetrics, i))
            {
                collection.UpdatePosition(fontMetrics, i);
            }
        }
    }

    /// <summary>
    /// Gets the end of the run of glyphs that one shaper positions. Characters of no script of their own join the
    /// script before or after them, as in <see cref="GPosTable.TryUpdatePositions"/>.
    /// </summary>
    public static int GetScriptRunEnd(GlyphPositioningCollection collection, int start, out ScriptClass script)
    {
        script = collection.TextOptions.GetScriptClass(collection[start].CodePoint);
        int end = start + 1;
        while (end < collection.Count)
        {
            ScriptClass next = collection.TextOptions.GetScriptClass(collection[end].CodePoint);
            if (next != script && HasOwnScript(script) && HasOwnScript(next))
            {
                break;
            }

            if (!HasOwnScript(script))
            {
                script = next;
            }

            end++;
        }

        return end;
    }

    private static bool HasOwnScript(ScriptClass script)
        => script is not ScriptClass.Common and not ScriptClass.Unknown and not ScriptClass.Inherited;

    /// <summary>
    /// The box of a glyph as HarfBuzz measures it: from its top left corner, with a height that goes down.
    /// </summary>
    private struct Box
    {
        public int Left;

        public int Top;

        public int Width;

        public int Height;

        public static Box Of(GlyphMetrics metrics)
        {
            Bounds bounds = metrics.Bounds;
            return new Box
            {
                Left = (int)bounds.Min.X,
                Top = (int)bounds.Max.Y,
                Width = (int)(bounds.Max.X - bounds.Min.X),
                Height = (int)(bounds.Min.Y - bounds.Max.Y),
            };
        }
    }
}
