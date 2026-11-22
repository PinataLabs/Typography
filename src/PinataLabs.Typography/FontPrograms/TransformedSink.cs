// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// An outline sink that transforms each point and passes it to another sink.
/// </summary>
/// <remarks>
/// netstandard2.1 has no ref fields, so this holds a copy of the inner sink.
/// Copy the inner sink in, write the outline, then copy <see cref="Inner"/> back out.
/// </remarks>
/// <typeparam name="TSink">The type of the inner sink.</typeparam>
internal struct TransformedSink<TSink> : IGlyphOutlineSink
    where TSink : IGlyphOutlineSink
{
    private readonly Matrix3x2 matrix;
    private TSink inner;

    public TransformedSink(TSink inner, Matrix3x2 matrix)
    {
        this.inner = inner;
        this.matrix = matrix;
    }

    /// <summary>
    /// Gets the inner sink in its current state.
    /// </summary>
    public readonly TSink Inner => this.inner;

    public void MoveTo(Vector2 point)
        => this.inner.MoveTo(Vector2.Transform(point, this.matrix));

    public void LineTo(Vector2 point)
        => this.inner.LineTo(Vector2.Transform(point, this.matrix));

    public void QuadraticBezierTo(Vector2 control, Vector2 point)
        => this.inner.QuadraticBezierTo(Vector2.Transform(control, this.matrix), Vector2.Transform(point, this.matrix));

    public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        => this.inner.CubicBezierTo(
            Vector2.Transform(control1, this.matrix),
            Vector2.Transform(control2, this.matrix),
            Vector2.Transform(point, this.matrix));

    public void ClosePath()
        => this.inner.ClosePath();
}
