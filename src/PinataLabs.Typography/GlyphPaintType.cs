// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// How the outlines of a <see cref="FontProgram"/> are meant to be painted.
/// </summary>
public enum GlyphPaintType
{
    /// <summary>
    /// The outlines are closed contours to fill. This is the PaintType 0 of Type 1 and CFF, and the only type of TrueType.
    /// </summary>
    Filled = 0,

    /// <summary>
    /// The outlines are centrelines to stroke with <see cref="FontProgram.StrokeWidth"/>, and not to fill.
    /// This is the PaintType 2 of Type 1 and CFF.
    /// </summary>
    Stroked = 2,
}
