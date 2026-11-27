// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography;

/// <summary>
/// The flags of a PDF font descriptor, at the bit positions of ISO 32000-1, Table 123. The value is the
/// <c>/Flags</c> entry of the descriptor.
/// </summary>
[Flags]
public enum FontDescriptorFlags
{
    /// <summary>
    /// No flag.
    /// </summary>
    None = 0,

    /// <summary>
    /// The glyphs have the same width. The <c>isFixedPitch</c> field of the <c>post</c> table is not 0.
    /// </summary>
    FixedPitch = 1 << 0,

    /// <summary>
    /// The glyphs have serifs. The <c>sFamilyClass</c> of the <c>OS/2</c> table names a serif class, or says nothing and
    /// its PANOSE classification has a serif style.
    /// </summary>
    Serif = 1 << 1,

    /// <summary>
    /// The font has glyphs outside the Standard Latin character set. The <c>cmap</c> table has a (3, 0) symbol subtable.
    /// A font has either this flag or <see cref="Nonsymbolic"/>.
    /// </summary>
    Symbolic = 1 << 2,

    /// <summary>
    /// The glyphs look like handwriting. The <c>sFamilyClass</c> of the <c>OS/2</c> table is Scripts, or says nothing and
    /// its PANOSE family kind is Latin Script.
    /// </summary>
    Script = 1 << 3,

    /// <summary>
    /// The font uses the Standard Latin character set. The <c>cmap</c> table has no (3, 0) symbol subtable.
    /// </summary>
    Nonsymbolic = 1 << 5,

    /// <summary>
    /// The glyphs lean. <c>fsSelection</c> of the <c>OS/2</c> table has the italic or oblique bit, <c>macStyle</c> of
    /// the <c>head</c> table has the italic bit, or the <c>post</c> table has an italic angle that is not 0.
    /// </summary>
    Italic = 1 << 6,

    /// <summary>
    /// The font has no lowercase letters. No table of a font states it, so the library never sets it.
    /// </summary>
    AllCap = 1 << 16,

    /// <summary>
    /// The font has small capitals as lowercase letters. No table of a font states it, so the library never sets it.
    /// </summary>
    SmallCap = 1 << 17,

    /// <summary>
    /// The rasteriser makes bold glyphs at small sizes. No table of a font states it, so the library never sets it.
    /// </summary>
    ForceBold = 1 << 18,
}
