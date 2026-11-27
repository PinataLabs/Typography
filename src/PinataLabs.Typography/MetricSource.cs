// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// Names the table field, or the fallback, that gave a value of <see cref="FontDescriptorMetrics"/>.
/// </summary>
public enum MetricSource
{
    /// <summary>
    /// The font gives no value and has no fallback for it. The value is 0.
    /// </summary>
    None = 0,

    /// <summary>
    /// The <c>sTypoAscender</c> field of the <c>OS/2</c> table.
    /// </summary>
    Os2TypoAscender,

    /// <summary>
    /// The <c>sTypoDescender</c> field of the <c>OS/2</c> table.
    /// </summary>
    Os2TypoDescender,

    /// <summary>
    /// The <c>usWinAscent</c> field of the <c>OS/2</c> table.
    /// </summary>
    Os2WinAscent,

    /// <summary>
    /// The <c>usWinDescent</c> field of the <c>OS/2</c> table, negated.
    /// </summary>
    Os2WinDescent,

    /// <summary>
    /// The <c>ascender</c> field of the <c>hhea</c> table.
    /// </summary>
    HheaAscender,

    /// <summary>
    /// The <c>descender</c> field of the <c>hhea</c> table.
    /// </summary>
    HheaDescender,

    /// <summary>
    /// The <c>yMax</c> field of the <c>head</c> table. A fallback for the ascent.
    /// </summary>
    HeadYMax,

    /// <summary>
    /// The <c>yMin</c> field of the <c>head</c> table. A fallback for the descent.
    /// </summary>
    HeadYMin,

    /// <summary>
    /// The <c>xMin</c>, <c>yMin</c>, <c>xMax</c> and <c>yMax</c> fields of the <c>head</c> table.
    /// </summary>
    HeadBounds,

    /// <summary>
    /// The <c>sCapHeight</c> field of the <c>OS/2</c> table, of version 2 or later.
    /// </summary>
    Os2CapHeight,

    /// <summary>
    /// The <c>sxHeight</c> field of the <c>OS/2</c> table, of version 2 or later.
    /// </summary>
    Os2XHeight,

    /// <summary>
    /// The top of the outline of the glyph for U+0048, LATIN CAPITAL LETTER H. A fallback for the cap height.
    /// </summary>
    CapitalHBounds,

    /// <summary>
    /// The top of the outline of the glyph for U+0078, LATIN SMALL LETTER X. A fallback for the x-height.
    /// </summary>
    SmallXBounds,

    /// <summary>
    /// The ascent. The last fallback for the cap height, when the font has no value and no glyph for U+0048.
    /// </summary>
    FallbackAscent,

    /// <summary>
    /// The <c>italicAngle</c> field of the <c>post</c> table.
    /// </summary>
    PostItalicAngle,
}
