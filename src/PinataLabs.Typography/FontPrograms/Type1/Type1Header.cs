// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// How the cleartext of a Type 1 font program defines <c>/Encoding</c>.
/// </summary>
internal enum Type1EncodingKind
{
    /// <summary>The font program defines no encoding.</summary>
    None,

    /// <summary>Adobe StandardEncoding.</summary>
    Standard,

    /// <summary>An array of glyph names that the font program builds.</summary>
    Custom,
}

/// <summary>
/// What the loader reads from the cleartext of a Type 1 font program. It lives only during the load.
/// </summary>
internal sealed class Type1Header
{
    public string FontName { get; set; } = string.Empty;

    public Matrix3x2 FontMatrix { get; set; } = Matrix3x2.CreateScale(0.001F);

    public Bounds FontBBox { get; set; }

    public int PaintType { get; set; }

    public Type1EncodingKind EncodingKind { get; set; }

    /// <summary>
    /// Gets the position in the cleartext of the glyph name for each code of a custom encoding.
    /// A code with no name has length 0.
    /// </summary>
    public int[] EncodingNameStarts { get; } = new int[256];

    public int[] EncodingNameLengths { get; } = new int[256];

    /// <summary>
    /// Gets or sets the position that follows the <c>eexec</c> operator, or -1 when the cleartext has none.
    /// </summary>
    public int EexecEnd { get; set; } = -1;
}
