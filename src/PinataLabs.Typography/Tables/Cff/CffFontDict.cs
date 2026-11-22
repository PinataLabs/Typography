// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// The values of a Font DICT and its Private DICT that charstring evaluation needs.
/// </summary>
internal readonly struct CffFontDict
{
    public CffFontDict(CffIndex localSubrs, float defaultWidthX, float nominalWidthX, Matrix3x2? transform = null)
    {
        this.LocalSubrs = localSubrs;
        this.DefaultWidthX = defaultWidthX;
        this.NominalWidthX = nominalWidthX;
        this.Transform = transform;
    }

    public CffIndex LocalSubrs { get; }

    public float DefaultWidthX { get; }

    public float NominalWidthX { get; }

    /// <summary>
    /// Gets the matrix from the glyph space of this Font DICT to the glyph space of the font,
    /// or <see langword="null"/> when the two are the same.
    /// </summary>
    public Matrix3x2? Transform { get; }
}
