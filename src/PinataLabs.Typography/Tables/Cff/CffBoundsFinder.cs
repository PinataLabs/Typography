// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// An outline sink that finds the bounds of a glyph. A curve is sampled, not solved.
/// </summary>
internal struct CffBoundsFinder : IGlyphOutlineSink
{
    private const int Steps = 3;
    private float minX;
    private float maxX;
    private float minY;
    private float maxY;
    private Vector2 current;
    private bool hasPoint;

    public void MoveTo(Vector2 point)
    {
        this.current = point;
        this.Include(point);
    }

    public void LineTo(Vector2 point)
    {
        this.current = point;
        this.Include(point);
    }

    public void QuadraticBezierTo(Vector2 control, Vector2 point)
    {
        const float step = 1F / Steps;
        float t = step;

        for (int n = 1; n < Steps; ++n)
        {
            float c = 1F - t;
            this.Include((this.current * c * c) + (control * 2 * t * c) + (point * t * t));
            t += step;
        }

        this.current = point;
        this.Include(point);
    }

    public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
    {
        const float step = 1F / Steps;
        float t = step;

        for (int n = 1; n < Steps; ++n)
        {
            float c = 1F - t;
            this.Include((this.current * c * c * c) + (control1 * 3 * t * c * c) + (control2 * 3 * t * t * c) + (point * t * t * t));
            t += step;
        }

        this.current = point;
        this.Include(point);
    }

    public void ClosePath()
        => this.current = Vector2.Zero;

    public readonly Bounds GetBounds()
    {
        if (!this.hasPoint)
        {
            return Bounds.Empty;
        }

        return new(
            (short)Math.Floor(this.minX),
            (short)Math.Floor(this.minY),
            (short)Math.Ceiling(this.maxX),
            (short)Math.Ceiling(this.maxY));
    }

    private void Include(Vector2 point)
    {
        if (!this.hasPoint)
        {
            this.minX = this.maxX = point.X;
            this.minY = this.maxY = point.Y;
            this.hasPoint = true;
            return;
        }

        this.minX = MathF.Min(this.minX, point.X);
        this.maxX = MathF.Max(this.maxX, point.X);
        this.minY = MathF.Min(this.minY, point.Y);
        this.maxY = MathF.Max(this.maxY, point.Y);
    }
}
