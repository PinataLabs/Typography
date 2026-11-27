// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The joiners that the lookups of a feature do not skip when they match glyphs, as the shaper plans the feature.
/// These are the <c>F_MANUAL_ZWNJ</c> and <c>F_MANUAL_ZWJ</c> flags of HarfBuzz.
/// </summary>
[Flags]
internal enum ManualJoiners : byte
{
    /// <summary>
    /// The lookups skip ZWJ and ZWNJ wherever HarfBuzz skips them by default.
    /// </summary>
    None = 0,

    /// <summary>
    /// GSUB does not skip ZWNJ in the backtrack and the lookahead.
    /// </summary>
    Zwnj = 1,

    /// <summary>
    /// GSUB and GPOS do not skip ZWJ in the input.
    /// </summary>
    Zwj = 2,

    /// <summary>
    /// Both <see cref="Zwnj"/> and <see cref="Zwj"/>.
    /// </summary>
    Both = Zwnj | Zwj,
}
