// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The default-ignorable glyphs that a lookup may skip between the glyphs it matches. A glyph that may be skipped is
/// still matched when the lookup asks for it there. A glyph that GSUB substituted is never skipped this way.
/// </summary>
[Flags]
internal enum SkippableIgnorables : byte
{
    /// <summary>
    /// No default-ignorable glyph is skipped.
    /// </summary>
    None = 0,

    /// <summary>
    /// The default-ignorable glyphs that are not ZWNJ, ZWJ or hidden.
    /// </summary>
    Other = 1,

    /// <summary>
    /// U+200C ZERO WIDTH NON-JOINER.
    /// </summary>
    Zwnj = 2,

    /// <summary>
    /// U+200D ZERO WIDTH JOINER.
    /// </summary>
    Zwj = 4,

    /// <summary>
    /// COMBINING GRAPHEME JOINER, the Mongolian free variation selectors and the tag characters, which HarfBuzz skips
    /// in GPOS only.
    /// </summary>
    Hidden = 8,
}
