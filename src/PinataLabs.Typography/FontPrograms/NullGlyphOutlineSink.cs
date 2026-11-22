// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// A sink that discards the outline, for running a charstring only for its width.
/// </summary>
internal struct NullGlyphOutlineSink : IGlyphOutlineSink
{
    public readonly void MoveTo(Vector2 point)
    {
    }

    public readonly void LineTo(Vector2 point)
    {
    }

    public readonly void QuadraticBezierTo(Vector2 control, Vector2 point)
    {
    }

    public readonly void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
    {
    }

    public readonly void ClosePath()
    {
    }
}
