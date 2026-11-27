// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// The direction of a paragraph, which is its base embedding level in the Unicode Bidirectional Algorithm.
/// <see href="https://unicode.org/reports/tr9/#The_Paragraph_Level"/>
/// </summary>
public enum BidiParagraphDirection
{
    /// <summary>
    /// The paragraph runs left to right: paragraph level 0, set by the caller (rule HL1).
    /// Strong characters in the text still resolve to their own direction.
    /// </summary>
    LeftToRight,

    /// <summary>
    /// The paragraph runs right to left: paragraph level 1, set by the caller (rule HL1).
    /// Strong characters in the text still resolve to their own direction.
    /// </summary>
    RightToLeft,

    /// <summary>
    /// The paragraph takes the direction of its first strong character, or runs left to right when it has none
    /// (rules P2 and P3).
    /// </summary>
    Auto
}
