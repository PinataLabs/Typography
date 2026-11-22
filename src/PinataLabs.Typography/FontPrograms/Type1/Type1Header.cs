// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
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
/// What the loader reads from the cleartext of a Type 1 font program. It lives on the stack of the load.
/// </summary>
internal ref struct Type1Header
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Type1Header"/> struct.
    /// </summary>
    /// <param name="encodingNameStarts">256 elements for <see cref="EncodingNameStarts"/>, all zero.</param>
    /// <param name="encodingNameLengths">256 elements for <see cref="EncodingNameLengths"/>, all zero.</param>
    public Type1Header(Span<int> encodingNameStarts, Span<int> encodingNameLengths)
    {
        this.EncodingNameStarts = encodingNameStarts;
        this.EncodingNameLengths = encodingNameLengths;
        this.FontMatrix = Matrix3x2.CreateScale(0.001F);
        this.FontBBox = default;
        this.PaintType = 0;
        this.EncodingKind = Type1EncodingKind.None;
        this.EexecEnd = -1;
    }

    public Matrix3x2 FontMatrix { get; set; }

    public Bounds FontBBox { get; set; }

    public int PaintType { get; set; }

    public Type1EncodingKind EncodingKind { get; set; }

    /// <summary>
    /// Gets the position in the cleartext of the glyph name for each code of a custom encoding.
    /// A code with no name has length 0.
    /// </summary>
    public Span<int> EncodingNameStarts { get; }

    public Span<int> EncodingNameLengths { get; }

    /// <summary>
    /// Gets or sets the position that follows the <c>eexec</c> operator, or -1 when the cleartext has none.
    /// </summary>
    public int EexecEnd { get; set; }
}
