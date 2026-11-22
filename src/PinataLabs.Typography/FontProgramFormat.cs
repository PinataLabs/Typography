// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// The format of the outlines of a <see cref="FontProgram"/>.
/// </summary>
public enum FontProgramFormat
{
    /// <summary>
    /// TrueType outlines: quadratic curves in a 'glyf' table.
    /// </summary>
    TrueType = 0,

    /// <summary>
    /// Compact Font Format outlines: cubic curves in Type 2 charstrings.
    /// </summary>
    Cff = 1,
}
