// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// The width that a space takes when the font lacks it and <see cref="GlyphNormalizer"/> gives it the glyph of U+0020.
/// The value of a member up to <see cref="EmSixteenth"/> is the number of such spaces in one em.
/// </summary>
internal enum SpaceFallbackKind : byte
{
    /// <summary>
    /// The glyph is not a fallback for a space.
    /// </summary>
    None = 0,

    Em = 1,

    EmHalf = 2,

    EmThird = 3,

    EmQuarter = 4,

    EmFifth = 5,

    EmSixth = 6,

    EmSixteenth = 16,

    /// <summary>
    /// Four eighteenths of an em.
    /// </summary>
    MediumMathematical,

    /// <summary>
    /// The advance of U+0020.
    /// </summary>
    Space,

    /// <summary>
    /// The advance of the first digit that the font has.
    /// </summary>
    Figure,

    /// <summary>
    /// The advance of the full stop, or else of the comma.
    /// </summary>
    Punctuation,

    /// <summary>
    /// Half of the advance of U+0020.
    /// </summary>
    Narrow,
}

/// <summary>
/// The spaces that HarfBuzz draws with the glyph of U+0020 when the font lacks them, and the width it gives each.
/// </summary>
internal static class SpaceFallback
{
    public const uint Space = 0x0020;

    public const uint Hyphen = 0x2010;

    public const uint NoBreakHyphen = 0x2011;

    public static SpaceFallbackKind GetKind(CodePoint codePoint)
        => codePoint.Value switch
        {
            0x00A0 => SpaceFallbackKind.Space,
            0x2000 or 0x2002 => SpaceFallbackKind.EmHalf,
            0x2001 or 0x2003 or 0x3000 => SpaceFallbackKind.Em,
            0x2004 => SpaceFallbackKind.EmThird,
            0x2005 => SpaceFallbackKind.EmQuarter,
            0x2006 => SpaceFallbackKind.EmSixth,
            0x2007 => SpaceFallbackKind.Figure,
            0x2008 => SpaceFallbackKind.Punctuation,
            0x2009 => SpaceFallbackKind.EmFifth,
            0x200A => SpaceFallbackKind.EmSixteenth,
            0x202F => SpaceFallbackKind.Narrow,
            0x205F => SpaceFallbackKind.MediumMathematical,
            _ => SpaceFallbackKind.None,
        };

    /// <summary>
    /// Gives the glyph of U+0020 the advance of the space that it stands for.
    /// </summary>
    /// <param name="metrics">The metrics of the glyph, which belong to this layout alone.</param>
    /// <param name="data">The glyph.</param>
    /// <param name="fontMetrics">The font of the glyph.</param>
    /// <param name="options">The options of the layout.</param>
    /// <param name="isVertical">Whether the glyph advances down the line.</param>
    public static void SetAdvance(IReadOnlyList<GlyphMetrics> metrics, GlyphShapingData data, FontMetrics fontMetrics, TextOptions options, bool isVertical)
    {
        // A ligature that formed from the space keeps the advance that the font gives it.
        if (data.SpaceFallback is SpaceFallbackKind.None or SpaceFallbackKind.Space || data.IsLigated)
        {
            return;
        }

        for (int i = 0; i < metrics.Count; i++)
        {
            GlyphMetrics glyph = metrics[i];
            ushort advance = GetAdvance(data, fontMetrics, options, isVertical, isVertical ? glyph.AdvanceHeight : glyph.AdvanceWidth);
            if (isVertical)
            {
                glyph.SetAdvanceHeight(advance);
            }
            else
            {
                glyph.SetAdvanceWidth(advance);
            }
        }
    }

    private static ushort GetAdvance(GlyphShapingData data, FontMetrics fontMetrics, TextOptions options, bool isVertical, ushort spaceAdvance)
    {
        int em = fontMetrics.UnitsPerEm;
        SpaceFallbackKind kind = data.SpaceFallback;
        switch (kind)
        {
            case SpaceFallbackKind.MediumMathematical:
                return (ushort)(em * 4 / 18);

            case SpaceFallbackKind.Narrow:
                return (ushort)(spaceAdvance / 2);

            case SpaceFallbackKind.Figure:
                for (uint digit = '0'; digit <= '9'; digit++)
                {
                    if (TryGetAdvance(digit, data, fontMetrics, options, isVertical, out ushort advance))
                    {
                        return advance;
                    }
                }

                return spaceAdvance;

            case SpaceFallbackKind.Punctuation:
                return TryGetAdvance('.', data, fontMetrics, options, isVertical, out ushort stop)
                    || TryGetAdvance(',', data, fontMetrics, options, isVertical, out stop)
                    ? stop
                    : spaceAdvance;

            default:
                int divisor = (int)kind;
                return (ushort)((em + (divisor / 2)) / divisor);
        }
    }

    private static bool TryGetAdvance(uint codePoint, GlyphShapingData data, FontMetrics fontMetrics, TextOptions options, bool isVertical, out ushort advance)
    {
        CodePoint character = new(codePoint);
        if (!fontMetrics.TryGetGlyphId(character, out ushort glyphId) || glyphId == 0)
        {
            advance = 0;
            return false;
        }

        GlyphMetrics glyph = fontMetrics.GetGlyphMetrics(
            character,
            glyphId,
            data.TextRun.TextAttributes,
            data.TextRun.TextDecorations,
            options.LayoutMode,
            options.ColorFontSupport)[0];

        advance = isVertical ? glyph.AdvanceHeight : glyph.AdvanceWidth;
        return true;
    }
}
