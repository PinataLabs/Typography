// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Changes to the classes of the line breaking algorithm (UAX #14), applied where LB1 resolves them.
/// </summary>
/// <remarks>
/// <see cref="None"/> is the algorithm as published. Each other value changes the class of the characters it
/// names, and of no others.
/// </remarks>
[Flags]
public enum LineBreakTailoring
{
    /// <summary>
    /// No tailoring: the algorithm as UAX #14 publishes it.
    /// </summary>
    None = 0,

    /// <summary>
    /// Treats the letters of the complex-context class SA, which are those of Thai, Lao, Khmer and Myanmar,
    /// as ideographs (<see cref="LineBreakClass.ID"/>), except for the vowels of Thai and Lao.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without a dictionary to find the words, LB1 makes an SA letter <see cref="LineBreakClass.AL"/>, and LB28 then
    /// joins every pair of letters of a paragraph in one of these scripts. This allows a break between letters
    /// instead, as between ideographs. An SA character that is a nonspacing or spacing mark stays a combining mark
    /// and stays with its letter, as LB1 has it. The dependent vowels of Khmer and Myanmar are such marks.
    /// </para>
    /// <para>
    /// The vowels of Thai and Lao are letters of the class, and a syllable is not broken inside, so two kinds of
    /// them are not ideographs. A vowel written after its consonant (U+0E30, U+0E32, U+0E33 and U+0E45 of Thai, and
    /// U+0EB0, U+0EB2, U+0EB3 and U+0EBD of Lao) is a combining mark, so no break comes before it. A vowel written
    /// before its consonant and encoded before it (U+0E40 to U+0E44 of Thai and U+0EC0 to U+0EC4 of Lao) is an
    /// ideograph that no break follows, so a line may end before it and not after it.
    /// </para>
    /// </remarks>
    SouthEastAsianAsIdeographs = 1,

    /// <summary>
    /// Treats Hangul syllables (<see cref="LineBreakClass.H2"/> and <see cref="LineBreakClass.H3"/>), conjoining
    /// jamo (<see cref="LineBreakClass.JL"/>, <see cref="LineBreakClass.JV"/> and <see cref="LineBreakClass.JT"/>)
    /// and the compatibility jamo (U+3131 to U+318E, and the halfwidth jamo U+FFA0 to U+FFDC) as letters
    /// (<see cref="LineBreakClass.AL"/>).
    /// </summary>
    /// <remarks>
    /// The algorithm as published allows a break after any Hangul syllable, as after an ideograph. This breaks
    /// Korean at its spaces only, as a script of letters is broken. Ideographs, Hanja among them, are not changed,
    /// and nor are the circled and parenthesized Hangul of U+3200 to U+327E, which are symbols.
    /// </remarks>
    HangulAsAlphabetic = 2,
}
