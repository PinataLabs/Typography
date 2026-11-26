// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.Tables.TrueType.Hinting;

namespace PinataLabs.Typography.Tables.TrueType.Glyphs;

/// <summary>
/// The points of one glyph in the form that the hinting interpreter takes: the points of every contour
/// in order, the number of the last point of each contour, and the instructions of the glyph.
/// The decoder reads one when a glyph is hinted. The points change in place.
/// </summary>
internal struct GlyphVector
{
    internal GlyphVector(
        ControlPoint[] controlPoints,
        ushort[] endPoints,
        Bounds bounds,
        ReadOnlyMemory<byte> instructions,
        bool isComposite)
    {
        this.ControlPoints = controlPoints;
        this.EndPoints = endPoints;
        this.Bounds = bounds;
        this.Instructions = instructions;
        this.IsComposite = isComposite;
    }

    public ControlPoint[] ControlPoints { get; }

    public ushort[] EndPoints { get; }

    public ReadOnlyMemory<byte> Instructions { get; }

    public bool IsComposite { get; }

    public Bounds Bounds { get; set; }

    /// <summary>
    /// Gets or sets the x of the first phantom point of the glyph at its variation instance, in font units. The points of a varied
    /// glyph are already moved left by it, so that the origin is at 0, and the hinting programs work on the points before that move.
    /// </summary>
    public float OriginX { get; set; }

    /// <summary>
    /// Gets or sets the x of the first phantom point that the hinting programs of the glyph see, in font units. It is <see cref="OriginX"/>
    /// unless the font has 'HVAR', and then it is the x of the phantom point at the default instance, as FreeType leaves the deltas
    /// of 'gvar' out of the phantom points that the table gives an advance for.
    /// </summary>
    public float HintedOriginX { get; set; }

    /// <summary>
    /// Gets or sets the y of the third phantom point that the hinting programs of the glyph see, in font units. It is the top of the
    /// vertical advance at the variation instance unless the font has 'VVAR' and 'vmtx', and then it is the top at the default instance.
    /// </summary>
    public float HintedTop { get; set; }

    /// <summary>
    /// Transforms a glyph vector by a specified 3x2 matrix.
    /// </summary>
    /// <param name="src">The glyph vector to transform.</param>
    /// <param name="matrix">The transformation matrix.</param>
    public static void TransformInPlace(ref GlyphVector src, Matrix3x2 matrix)
    {
        ControlPoint[] controlPoints = src.ControlPoints;
        for (int i = 0; i < controlPoints.Length; i++)
        {
            controlPoints[i].Point = Vector2.Transform(controlPoints[i].Point, matrix);
        }

        src.Bounds = Bounds.Transform(src.Bounds, matrix);
    }

    /// <summary>
    /// Applies True Type hinting to the specified glyph vector.
    /// </summary>
    /// <param name="glyph">The glyph vector to hint.</param>
    /// <param name="interpreter">The True Type interpreter.</param>
    /// <param name="pp1">The first phantom point.</param>
    /// <param name="pp2">The second phantom point.</param>
    /// <param name="pp3">The third phantom point.</param>
    /// <param name="pp4">The fourth phantom point.</param>
    /// <returns>The x of the first phantom point after hinting.</returns>
    public static float Hint(
        ref GlyphVector glyph,
        TrueTypeInterpreter interpreter,
        Vector2 pp1,
        Vector2 pp2,
        Vector2 pp3,
        Vector2 pp4)
    {
        // The interpreter works on a copy with the phantom points at the end,
        // so that a hint program that fails leaves the glyph as it was.
        int count = glyph.ControlPoints.Length;
        var controlPoints = new ControlPoint[count + 4];
        glyph.ControlPoints.CopyTo(controlPoints, 0);
        controlPoints[count].Point = pp1;
        controlPoints[count + 1].Point = pp2;
        controlPoints[count + 2].Point = pp3;
        controlPoints[count + 3].Point = pp4;

        interpreter.HintGlyph(controlPoints, glyph.EndPoints, glyph.Instructions, glyph.IsComposite);

        Array.Copy(controlPoints, glyph.ControlPoints, count);
        return controlPoints[count].Point.X;
    }
}
