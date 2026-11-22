// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace PinataLabs.Typography;

/// <summary>
/// Receives one glyph outline as path commands in glyph space, with y pointing up.
/// Each contour is a <see cref="MoveTo"/>, then segments, then <see cref="ClosePath"/>.
/// </summary>
public interface IGlyphOutlineSink
{
    /// <summary>Starts a new contour at <paramref name="point"/>.</summary>
    /// <param name="point">The start point.</param>
    void MoveTo(Vector2 point);

    /// <summary>Adds a straight segment to <paramref name="point"/>.</summary>
    /// <param name="point">The end point.</param>
    void LineTo(Vector2 point);

    /// <summary>Adds a quadratic Bézier segment.</summary>
    /// <param name="control">The control point.</param>
    /// <param name="point">The end point.</param>
    void QuadraticBezierTo(Vector2 control, Vector2 point);

    /// <summary>Adds a cubic Bézier segment.</summary>
    /// <param name="control1">The first control point.</param>
    /// <param name="control2">The second control point.</param>
    /// <param name="point">The end point.</param>
    void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point);

    /// <summary>Closes the current contour.</summary>
    void ClosePath();
}
