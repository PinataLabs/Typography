// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;

/// <summary>
/// Draws the Thai marks of a font whose GSUB table has no <c>thai</c> script with the glyphs of the private use area
/// that Windows and Mac Thai fonts have, as HarfBuzz does. A tone mark goes lower or further left, a vowel below a
/// consonant with a descender goes lower, and YO YING and THO THAN lose their descender before a vowel below them.
/// </summary>
/// <remarks>
/// Two state machines follow the marks after each base, one for the marks above it and one for the marks below it.
/// See <see href="https://linux.thai.net/~thep/th-otf/shaping.html"/>.
/// </remarks>
internal static class ThaiPrivateUseForms
{
    // Above states: T0 room above for a tone mark, T1 an ascender, T2 an ascender and a mark, T3 no room.
    private static readonly byte[] AboveStartState = [0, 1, 0, 0, 3];

    private static readonly (Action Action, byte Next)[,] AboveStates =
    {
        { (Action.None, 3), (Action.None, 0), (Action.Down, 3) },
        { (Action.Left, 2), (Action.None, 1), (Action.DownLeft, 2) },
        { (Action.None, 3), (Action.None, 2), (Action.Left, 3) },
        { (Action.None, 3), (Action.None, 3), (Action.None, 3) },
    };

    // Below states: B0 no descender, B1 a descender that can go, B2 a descender that stays.
    private static readonly byte[] BelowStartState = [0, 0, 1, 2, 2];

    private static readonly (Action Action, byte Next)[,] BelowStates =
    {
        { (Action.None, 0), (Action.None, 2), (Action.None, 0) },
        { (Action.None, 1), (Action.RemoveDescender, 2), (Action.None, 1) },
        { (Action.None, 2), (Action.Down, 2), (Action.None, 2) },
    };

    // Each entry is a character, its Windows private use form and its Mac private use form.
    private static readonly (uint CodePoint, uint Windows, uint Mac)[] DownForms =
    [
        (0x0E48, 0xF70A, 0xF88B), (0x0E49, 0xF70B, 0xF88E), (0x0E4A, 0xF70C, 0xF891), (0x0E4B, 0xF70D, 0xF894),
        (0x0E4C, 0xF70E, 0xF897), (0x0E38, 0xF718, 0xF89B), (0x0E39, 0xF719, 0xF89C), (0x0E3A, 0xF71A, 0xF89D),
    ];

    private static readonly (uint CodePoint, uint Windows, uint Mac)[] DownLeftForms =
    [
        (0x0E48, 0xF705, 0xF88C), (0x0E49, 0xF706, 0xF88F), (0x0E4A, 0xF707, 0xF892), (0x0E4B, 0xF708, 0xF895),
        (0x0E4C, 0xF709, 0xF898),
    ];

    private static readonly (uint CodePoint, uint Windows, uint Mac)[] LeftForms =
    [
        (0x0E48, 0xF713, 0xF88A), (0x0E49, 0xF714, 0xF88D), (0x0E4A, 0xF715, 0xF890), (0x0E4B, 0xF716, 0xF893),
        (0x0E4C, 0xF717, 0xF896), (0x0E31, 0xF710, 0xF884), (0x0E34, 0xF701, 0xF885), (0x0E35, 0xF702, 0xF886),
        (0x0E36, 0xF703, 0xF887), (0x0E37, 0xF704, 0xF888), (0x0E47, 0xF712, 0xF889), (0x0E4D, 0xF711, 0xF899),
    ];

    private static readonly (uint CodePoint, uint Windows, uint Mac)[] NoDescenderForms =
    [
        (0x0E0D, 0xF70F, 0xF89A), (0x0E10, 0xF700, 0xF89E),
    ];

    private enum ConsonantType : byte
    {
        // No ascender and no descender.
        Normal,

        // An ascender.
        Ascender,

        // A descender that goes when a vowel is below it.
        RemovableDescender,

        // A descender that stays.
        Descender,

        NotConsonant,
    }

    private enum MarkType : byte
    {
        Above,
        Below,
        Tone,
        NotMark,
    }

    private enum Action : byte
    {
        None,

        // Shift the mark down.
        Down,

        // Shift the mark left.
        Left,

        // Shift the mark down and left.
        DownLeft,

        // Remove the descender of the base.
        RemoveDescender,
    }

    /// <summary>
    /// Gives the Thai glyphs of a run their private use forms. The glyphs keep their characters.
    /// </summary>
    /// <param name="fontMetrics">The font of the run.</param>
    /// <param name="collection">The glyphs.</param>
    /// <param name="start">The zero-based index of the first glyph of the run.</param>
    /// <param name="end">The zero-based index after the last glyph of the run.</param>
    public static void Apply(FontMetrics fontMetrics, GlyphSubstitutionCollection collection, int start, int end)
    {
        byte above = AboveStartState[(int)ConsonantType.NotConsonant];
        byte below = BelowStartState[(int)ConsonantType.NotConsonant];
        int baseIndex = start;
        for (int i = start; i < end; i++)
        {
            uint codePoint = (uint)collection[i].CodePoint.Value;
            MarkType markType = GetMarkType(codePoint);
            if (markType == MarkType.NotMark)
            {
                ConsonantType consonantType = GetConsonantType(codePoint);
                above = AboveStartState[(int)consonantType];
                below = BelowStartState[(int)consonantType];
                baseIndex = i;
                continue;
            }

            (Action aboveAction, above) = AboveStates[above, (int)markType];
            (Action belowAction, below) = BelowStates[below, (int)markType];

            // At most one of the two machines acts.
            Action action = aboveAction != Action.None ? aboveAction : belowAction;
            int index = action == Action.RemoveDescender ? baseIndex : i;
            (uint CodePoint, uint Windows, uint Mac)[]? forms = action switch
            {
                Action.Down => DownForms,
                Action.DownLeft => DownLeftForms,
                Action.Left => LeftForms,
                Action.RemoveDescender => NoDescenderForms,
                _ => null,
            };

            if (forms is not null && TryGetForm(fontMetrics, forms, (uint)collection[index].CodePoint.Value, out ushort glyphId))
            {
                collection[index].GlyphId = glyphId;
            }
        }
    }

    private static bool TryGetForm(FontMetrics fontMetrics, (uint CodePoint, uint Windows, uint Mac)[] forms, uint codePoint, out ushort glyphId)
    {
        foreach ((uint CodePoint, uint Windows, uint Mac) form in forms)
        {
            if (form.CodePoint == codePoint)
            {
                return TryGetGlyph(fontMetrics, form.Windows, out glyphId) || TryGetGlyph(fontMetrics, form.Mac, out glyphId);
            }
        }

        glyphId = 0;
        return false;
    }

    private static bool TryGetGlyph(FontMetrics fontMetrics, uint codePoint, out ushort glyphId)
        => fontMetrics.TryGetGlyphId(new CodePoint(codePoint), out glyphId) && glyphId != 0;

    private static ConsonantType GetConsonantType(uint codePoint)
        => codePoint switch
        {
            0x0E1B or 0x0E1D or 0x0E1F => ConsonantType.Ascender,
            0x0E0D or 0x0E10 => ConsonantType.RemovableDescender,
            0x0E0E or 0x0E0F => ConsonantType.Descender,
            >= 0x0E01 and <= 0x0E2E => ConsonantType.Normal,
            _ => ConsonantType.NotConsonant,
        };

    private static MarkType GetMarkType(uint codePoint)
        => codePoint switch
        {
            0x0E31 or (>= 0x0E34 and <= 0x0E37) or 0x0E47 or 0x0E4D or 0x0E4E => MarkType.Above,
            >= 0x0E38 and <= 0x0E3A => MarkType.Below,
            >= 0x0E48 and <= 0x0E4C => MarkType.Tone,
            _ => MarkType.NotMark,
        };
}
