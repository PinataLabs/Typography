// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace PinataLabs.Typography.AotSmoke;

internal struct CountingSink : IGlyphOutlineSink
{
    public int Commands { get; private set; }

    public void MoveTo(Vector2 point) => this.Commands++;

    public void LineTo(Vector2 point) => this.Commands++;

    public void QuadraticBezierTo(Vector2 control, Vector2 point) => this.Commands++;

    public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point) => this.Commands++;

    public void ClosePath() => this.Commands++;
}
