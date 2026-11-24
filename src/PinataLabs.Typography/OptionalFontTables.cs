// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography;

/// <summary>
/// The sfnt tables that a <see cref="FontProgram"/> does not need to read outlines, and that a font
/// subset, such as one embedded in a PDF file, often leaves out. It names the tables that
/// <see cref="FontProgram.MissingOptionalTables"/> and <see cref="FontProgram.MalformedOptionalTables"/> report.
/// </summary>
[Flags]
public enum OptionalFontTables
{
    /// <summary>
    /// No table.
    /// </summary>
    None = 0,

    /// <summary>
    /// The 'name' table, which holds the names of the font. A font program never reads it.
    /// </summary>
    Name = 1 << 0,

    /// <summary>
    /// The 'OS/2' table, which holds the typographic metrics. A font program never reads it.
    /// </summary>
    Os2 = 1 << 1,

    /// <summary>
    /// The 'post' table, which gives the glyph names of a TrueType font.
    /// </summary>
    Post = 1 << 2,

    /// <summary>
    /// The 'cmap' table, which gives the built-in encoding of a TrueType font.
    /// </summary>
    Cmap = 1 << 3,

    /// <summary>
    /// The 'hhea' table, which says how many advance widths the 'hmtx' table holds.
    /// </summary>
    Hhea = 1 << 4,

    /// <summary>
    /// The 'hmtx' table, which holds the advance widths.
    /// </summary>
    Hmtx = 1 << 5,
}
