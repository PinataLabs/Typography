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
    /// as ideographs (<see cref="LineBreakClass.ID"/>).
    /// </summary>
    /// <remarks>
    /// Without a dictionary to find the words, LB1 makes an SA letter <see cref="LineBreakClass.AL"/>, and LB28 then
    /// joins every pair of letters of a paragraph in one of these scripts. This allows a break between letters
    /// instead, as between ideographs. An SA character that is a nonspacing or spacing mark stays a combining mark
    /// and stays with its letter, as LB1 has it.
    /// </remarks>
    SouthEastAsianAsIdeographs = 1,

    /// <summary>
    /// Treats Hangul syllables (<see cref="LineBreakClass.H2"/> and <see cref="LineBreakClass.H3"/>) and conjoining
    /// jamo (<see cref="LineBreakClass.JL"/>, <see cref="LineBreakClass.JV"/> and <see cref="LineBreakClass.JT"/>)
    /// as letters (<see cref="LineBreakClass.AL"/>).
    /// </summary>
    /// <remarks>
    /// The algorithm as published allows a break after any Hangul syllable, as after an ideograph. This breaks
    /// Korean at its spaces only, as a script of letters is broken. Ideographs, Hanja among them, are not changed.
    /// </remarks>
    HangulAsAlphabetic = 2,
}
