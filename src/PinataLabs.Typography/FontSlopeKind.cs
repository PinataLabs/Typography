// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// The kinds of slope that a font face has, named as the CSS <c>font-style</c> property names them.
/// </summary>
public enum FontSlopeKind
{
    /// <summary>
    /// Upright glyphs.
    /// </summary>
    Normal,

    /// <summary>
    /// Glyphs that the designer drew as italic.
    /// </summary>
    Italic,

    /// <summary>
    /// Upright glyphs that lean at an angle.
    /// </summary>
    Oblique,
}
