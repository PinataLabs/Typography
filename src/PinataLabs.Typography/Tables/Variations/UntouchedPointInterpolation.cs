// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// Interpolation of untouched points (IUP). A 'gvar' tuple can give deltas for some points of a contour only. Each point
/// in between moves with the two nearest points that have a delta, on each axis separately:
/// https://learn.microsoft.com/typography/opentype/spec/gvar#inferred-deltas-for-un-referenced-point-numbers
/// </summary>
internal static class UntouchedPointInterpolation
{
    /// <summary>
    /// Gives each untouched point of every contour a delta from the touched points of its contour.
    /// A contour with one touched point moves as a whole, and a contour with none does not move.
    /// </summary>
    /// <param name="original">The points of the glyph before variation.</param>
    /// <param name="endPoints">The number of the last point of each contour, in increasing order, each below the number of points.</param>
    /// <param name="touched">Whether each point has a delta of its own.</param>
    /// <param name="deltas">The deltas. The method writes those of the untouched points that a contour holds.</param>
    public static void Apply(ReadOnlySpan<Vector2> original, ReadOnlySpan<ushort> endPoints, ReadOnlySpan<bool> touched, Span<Vector2> deltas)
    {
        int start = 0;
        foreach (ushort end in endPoints)
        {
            ApplyToContour(original, touched, deltas, start, end);
            start = end + 1;
        }
    }

    private static void ApplyToContour(ReadOnlySpan<Vector2> original, ReadOnlySpan<bool> touched, Span<Vector2> deltas, int start, int end)
    {
        int first = -1;
        int previous = -1;
        for (int i = start; i <= end; i++)
        {
            if (!touched[i])
            {
                continue;
            }

            if (first < 0)
            {
                first = i;
            }
            else
            {
                Interpolate(original, deltas, previous + 1, i, previous, i);
            }

            previous = i;
        }

        if (first < 0)
        {
            return;
        }

        // The points after the last touched point and before the first one are one run, which wraps around the contour.
        Interpolate(original, deltas, previous + 1, end + 1, previous, first);
        Interpolate(original, deltas, start, first, previous, first);
    }

    // Writes the deltas of the points from `from` up to `to` (exclusive), from the touched points `reference1` and `reference2`.
    private static void Interpolate(ReadOnlySpan<Vector2> original, Span<Vector2> deltas, int from, int to, int reference1, int reference2)
    {
        Vector2 point1 = original[reference1];
        Vector2 point2 = original[reference2];
        Vector2 delta1 = deltas[reference1];
        Vector2 delta2 = deltas[reference2];
        for (int i = from; i < to; i++)
        {
            deltas[i] = new Vector2(
                Interpolate(original[i].X, point1.X, point2.X, delta1.X, delta2.X),
                Interpolate(original[i].Y, point1.Y, point2.Y, delta1.Y, delta2.Y));
        }
    }

    // A point at or beyond a reference point takes the delta of the nearer one, a point between them takes a proportion,
    // and references on the same coordinate move a point only when they agree.
    private static float Interpolate(double x, double x1, double x2, double delta1, double delta2)
    {
#pragma warning disable S1244 // IUP treats references on the same coordinate, and equal deltas, exactly.
        if (x1 == x2)
        {
            return (float)(delta1 == delta2 ? delta1 : 0);
#pragma warning restore S1244
        }

        if (x1 > x2)
        {
            (x1, x2) = (x2, x1);
            (delta1, delta2) = (delta2, delta1);
        }

        if (x <= x1)
        {
            return (float)delta1;
        }

        if (x >= x2)
        {
            return (float)delta2;
        }

        return (float)(delta1 + ((x - x1) * ((delta2 - delta1) / (x2 - x1))));
    }
}
