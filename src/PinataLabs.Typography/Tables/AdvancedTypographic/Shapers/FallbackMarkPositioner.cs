// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
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
                PlaceMarks(collection, script, start, end, fontMetrics);
            }

            if (zeroes || places)
            {
                UpdatePositions(fontMetrics, collection, start, end);
            }

            start = end;
        }
    }

    /// <summary>
    /// Places the marks of each cluster whose marks come from another font than its base, once every font has positioned
    /// its own glyphs. A font positions only its own glyphs, so it cannot stack its marks on where another font put its
    /// own, and its GPOS cannot attach them to the base of another font.
    /// </summary>
    /// <param name="collection">The glyphs of the text.</param>
    public static void PlaceMarksAcrossFonts(GlyphPositioningCollection collection)
    {
        int start = 0;
        while (start < collection.Count)
        {
            int end = GetScriptRunEnd(collection, start, out ScriptClass script);
            PlaceMarks(collection, script, start, end, null);
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

    /// <summary>
    /// Places the marks of the clusters of a run that a font draws alone, or, without a font, of the clusters whose marks
    /// come from another font than the base.
    /// </summary>
    private static void PlaceMarks(GlyphPositioningCollection collection, ScriptClass script, int start, int end, FontMetrics? drawnBy)
    {
        int index = start;
        while (index < end)
        {
            if (IsUnicodeMark(collection[index]))
            {
                index++;
                continue;
            }

            // As in HarfBuzz, the marks of a base go on past a default-ignorable glyph. Such a glyph takes no place, so it
            // may come from any font.
            FontMetrics baseFont = collection.GetMetrics(index).FontMetrics;
            int marksEnd = index + 1;
            bool isMixed = false;
            while (marksEnd < end && (IsUnicodeMark(collection[marksEnd]) || collection[marksEnd].IsDefaultIgnorable))
            {
                isMixed |= !collection[marksEnd].IsDefaultIgnorable && collection.GetMetrics(marksEnd).FontMetrics != baseFont;
                marksEnd++;
            }

            bool isDrawn = drawnBy is null ? isMixed : !isMixed && baseFont == drawnBy;
            if (isDrawn && IsPlaceable(collection, collection[index]))
            {
                PlaceAroundBase(collection, script, index, marksEnd);
            }

            index = marksEnd;
        }
    }

    /// <summary>
    /// Places the marks that follow a base, as HarfBuzz places them in a font without GPOS. A mark whose font positions it
    /// with GPOS, or does not place marks, stays where it is, and the marks after it stack on it there. A mark that the GPOS
    /// of its font attached to a mark that moves goes with it. The places are in units of the font of the base until each
    /// mark takes its own.
    /// </summary>
    private static void PlaceAroundBase(GlyphPositioningCollection collection, ScriptClass script, int baseIndex, int end)
    {
        GlyphShapingData baseData = collection[baseIndex];
        GlyphMetrics baseMetrics = collection.GetMetrics(baseIndex);
        FontMetrics baseFont = baseMetrics.FontMetrics;
        Vector2 baseUnit = collection.GetUnitSize(baseIndex);
        bool isForward = baseData.Direction == TextDirection.LeftToRight;
        int gap = baseFont.UnitsPerEm / GapDivisor;

        // The advance of the base, not its box, aligns the mark across the line. So a base without ink also works.
        // It is the advance that the font gives the base. Kerning moves the base and leaves its marks in place.
        Box baseBox = Box.Of(baseMetrics);
        baseBox.Top += baseData.Bounds.Y;
        baseBox.Left = 0;
        baseBox.Width = GetFontAdvance(baseFont, collection, baseData);

        // The pen is after the base in text that runs forward, and still before it in text that runs backward.
        int penOffset = isForward ? -GetAdvance(collection, baseIndex) : 0;
        int components = AdvancedTypographicUtils.GetLigatureComponentCount(baseFont, baseData);
        int lastComponent = -1;
        Box componentBox = baseBox;
        Box stack = baseBox;
        (MarkAlignment Alignment, MarkSide Side) last = default;
        for (int i = baseIndex + 1; i < end; i++)
        {
            GlyphShapingData data = collection[i];
            Vector2 scale = collection.GetUnitSize(i) / baseUnit;
            uint placement = UnicodeData.GetCombiningClassData((uint)data.UnicodePropertiesCodePoint.Value);
            MarkAlignment alignment = (MarkAlignment)((placement >> CanonicalMappingData.MarkAlignmentShift) & CanonicalMappingData.MarkPlacementMask);
            MarkSide side = (MarkSide)((placement >> CanonicalMappingData.MarkSideShift) & CanonicalMappingData.MarkPlacementMask);
            if (alignment == MarkAlignment.None)
            {
                // HarfBuzz has taken the advance of a default-ignorable glyph away by now, and the layout does so later.
                if (!data.IsDefaultIgnorable)
                {
                    penOffset += GetPenStep(collection, i, scale, isForward);
                }

                continue;
            }

            // A mark of a ligature goes on its own component, an equal part of the ligature. As in HarfBuzz, a glyph of one
            // component or none is not split.
            int component = GetComponent(baseData, data, components);
            if (components > 1 && component != lastComponent)
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

            Box mark = Box.Of(collection.GetMetrics(i)).Scale(scale);
            FontMetrics markFont = collection.GetMetrics(i).FontMetrics;
            if (!PlacesAroundBase(collection, script, baseIndex, i))
            {
                // The mark keeps any advance that its font left it, which moves the pen as for a glyph that is not placed.
                mark.Top += Scale(data.Bounds.Y, scale.Y);
                Seat(ref stack, mark, side);
                penOffset += GetPenStep(collection, i, scale, isForward);
                continue;
            }

            int x = Scale(penOffset + Align(stack, mark, alignment, isForward), 1 / scale.X);
            int y = Scale(Stack(ref stack, mark, side, gap), 1 / scale.Y);
            int dx = x - data.Bounds.X;
            int dy = y - data.Bounds.Y;
            data.Bounds.X = x;
            data.Bounds.Y = y;
            data.Bounds.Width = 0;
            data.Bounds.Height = 0;
            collection.UpdatePosition(markFont, i);
            MoveAttachedMarks(collection, i, end, dx, dy);
        }
    }

    /// <summary>
    /// Gets a value indicating whether a mark goes around its base as in a font without GPOS. A font places its marks so,
    /// unless its shaper does not or its GPOS positions them. Its GPOS can attach a mark only to a glyph of the font, so
    /// a mark that it calls one and that has no glyph of its font before it in the cluster goes around the base too. A
    /// glyph that its GDEF calls a base keeps its advance and its place.
    /// </summary>
    private static bool PlacesAroundBase(GlyphPositioningCollection collection, ScriptClass script, int baseIndex, int index)
    {
        FontMetrics markFont = collection.GetMetrics(index).FontMetrics;
        if (!ShaperFactory.HasFallbackPositioning(script, markFont.GetGSubTable().GetScriptTag(script)))
        {
            return false;
        }

        if (markFont.GetGPosTable()?.PositionsScript(script) != true)
        {
            return true;
        }

        if (!AdvancedTypographicUtils.IsMarkGlyph(markFont, collection[index]))
        {
            return false;
        }

        for (int i = baseIndex; i < index; i++)
        {
            if (!collection[i].IsDefaultIgnorable && collection.GetMetrics(i).FontMetrics == markFont)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Moves the marks that the GPOS of their font attached to a mark that moved, directly or through another mark, with it.
    /// </summary>
    private static void MoveAttachedMarks(GlyphPositioningCollection collection, int parent, int end, int dx, int dy)
    {
        FontMetrics font = collection.GetMetrics(parent).FontMetrics;
        for (int i = parent + 1; i < end; i++)
        {
            GlyphShapingData data = collection[i];
            if (data.MarkAttachment == parent && collection.GetMetrics(i).FontMetrics == font)
            {
                data.Bounds.X += dx;
                data.Bounds.Y += dy;
                collection.UpdatePosition(font, i);
                MoveAttachedMarks(collection, i, end, dx, dy);
            }
        }
    }

    // Gets how far the pen offset of the marks after a glyph that keeps its advance moves, in units of the font of the base:
    // back in text that runs forward, as the pen moved forward past the glyph.
    private static int GetPenStep(GlyphPositioningCollection collection, int index, Vector2 scale, bool isForward)
    {
        int advance = Scale(GetAdvance(collection, index), scale.X);
        return isForward ? -advance : advance;
    }

    // Converts a length to the units of another font, given the size of a unit of its font in those units.
    private static int Scale(int value, float scale)
        => scale == 1 ? value : (int)MathF.Round(value * scale);

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

    /// <summary>
    /// Adds a mark that stays where it is to the stack, so that the marks after it on its side stack on it.
    /// </summary>
    private static void Seat(ref Box stack, Box mark, MarkSide side)
    {
        switch (side)
        {
            case MarkSide.Below:
            case MarkSide.AttachedBelow:
                stack.Height = Math.Min(stack.Height, mark.Top + mark.Height - stack.Top);
                break;

            case MarkSide.Above:
            case MarkSide.AttachedAbove:
                int top = Math.Max(stack.Top, mark.Top);
                stack.Height -= top - stack.Top;
                stack.Top = top;
                break;
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

        public readonly Box Scale(Vector2 scale)
            => scale == Vector2.One ? this : new Box
            {
                Left = FallbackMarkPositioner.Scale(this.Left, scale.X),
                Top = FallbackMarkPositioner.Scale(this.Top, scale.Y),
                Width = FallbackMarkPositioner.Scale(this.Width, scale.X),
                Height = FallbackMarkPositioner.Scale(this.Height, scale.Y),
            };
    }
}
