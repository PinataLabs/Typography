// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// Specifies the writing direction for text.
/// </summary>
public enum TextDirection
{
    /// <summary>
    /// Left to right.
    /// </summary>
    LeftToRight = 0,

    /// <summary>
    /// Right to left.
    /// </summary>
    RightToLeft = 1,

    /// <summary>
    /// Automatically determined. In a layout, each paragraph takes the direction of its first strong character, or runs
    /// left to right when it has none (rules P2 and P3 of the Unicode Bidirectional Algorithm), and a paragraph separator,
    /// such as a line feed, ends a paragraph. <see cref="ShapingOptions.Direction"/> says how a shaped run takes it.
    /// </summary>
    Auto = 2,
}
