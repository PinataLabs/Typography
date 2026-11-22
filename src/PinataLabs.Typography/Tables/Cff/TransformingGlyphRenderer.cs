// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// Adapts an <see cref="IGlyphRenderer"/> to <see cref="IGlyphOutlineSink"/> for layout.
/// It maps each glyph space point to the render surface, and each contour to a figure.
/// </summary>
internal struct TransformingGlyphRenderer : IGlyphOutlineSink
{
    private static readonly Vector2 YInverter = new(1, -1);
    private readonly IGlyphRenderer renderer;
    private Vector2 origin;
    private Vector2 scale;
    private Vector2 offset;
    private Matrix3x2 transform;

    public TransformingGlyphRenderer(IGlyphRenderer renderer, Vector2 origin, Vector2 scale, Vector2 offset, Matrix3x2 transform)
    {
        this.renderer = renderer;
        this.origin = origin;
        this.scale = scale;
        this.offset = offset;
        this.transform = transform;
    }

    public void MoveTo(Vector2 point)
    {
        this.renderer.BeginFigure();
        this.renderer.MoveTo(this.Transform(point));
    }

    public void LineTo(Vector2 point)
        => this.renderer.LineTo(this.Transform(point));

    public void QuadraticBezierTo(Vector2 control, Vector2 point)
        => this.renderer.QuadraticBezierTo(this.Transform(control), this.Transform(point));

    public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        => this.renderer.CubicBezierTo(this.Transform(control1), this.Transform(control2), this.Transform(point));

    public void ClosePath()
        => this.renderer.EndFigure();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Vector2 Transform(Vector2 point)
        => (Vector2.Transform((point * this.scale) + this.offset, this.transform) * YInverter) + this.origin;
}
