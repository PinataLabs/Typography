// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// An encoding that a PDF simple font uses to name the glyph of each character code, from ISO 32000-1 Annex D.
/// </summary>
public enum SimpleFontEncoding
{
    /// <summary>The Adobe StandardEncoding.</summary>
    Standard,

    /// <summary>The WinAnsiEncoding, which is Windows code page 1252.</summary>
    WinAnsi,

    /// <summary>The MacRomanEncoding, which is the Mac OS Roman character set.</summary>
    MacRoman,

    /// <summary>The MacExpertEncoding, the small capitals, old-style figures and fractions of an expert font.</summary>
    MacExpert,

    /// <summary>The built-in encoding of the Symbol font.</summary>
    Symbol,

    /// <summary>The built-in encoding of the ZapfDingbats font.</summary>
    ZapfDingbats,
}
