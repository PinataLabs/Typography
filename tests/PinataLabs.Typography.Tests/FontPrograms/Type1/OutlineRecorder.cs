// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

/// <summary>
/// A sink that records the outline it receives.
/// </summary>
internal readonly struct OutlineRecorder : IGlyphOutlineSink
{
    public OutlineRecorder()
    {
    }

    public List<OutlineCommand> Commands { get; } = [];

    /// <summary>
    /// Parses an outline in the fixture format: "M x y", "L x y", "C x1 y1 x2 y2 x y", "Z", and "E",
    /// which fontTools writes for a contour that the charstring left open. The engine closes such a
    /// contour, so E reads as Z.
    /// </summary>
    /// <param name="outline">The outline.</param>
    /// <returns>The commands.</returns>
    public static List<OutlineCommand> Parse(string outline)
    {
        List<OutlineCommand> commands = [];
        string[] tokens = outline.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        int i = 0;
        while (i < tokens.Length)
        {
            char op = tokens[i++][0];
            int count = op switch
            {
                'M' or 'L' => 2,
                'C' => 6,
                _ => 0,
            };

            float[] coordinates = new float[count];
            for (int c = 0; c < count; c++)
            {
                coordinates[c] = (float)double.Parse(tokens[i++], CultureInfo.InvariantCulture);
            }

            commands.Add(new OutlineCommand(op == 'E' ? 'Z' : op, coordinates));
        }

        return commands;
    }

    /// <summary>
    /// Describes the first difference between two outlines, or returns <see langword="null"/> when
    /// they have the same commands and every coordinate is within <paramref name="tolerance"/>.
    /// </summary>
    /// <param name="expected">The expected commands.</param>
    /// <param name="actual">The actual commands.</param>
    /// <param name="tolerance">The largest accepted coordinate difference, in glyph units.</param>
    /// <returns>The difference, or <see langword="null"/>.</returns>
    public static string? Difference(List<OutlineCommand> expected, List<OutlineCommand> actual, float tolerance)
    {
        if (expected.Count != actual.Count)
        {
            return $"expected {expected.Count} commands, got {actual.Count}";
        }

        for (int i = 0; i < expected.Count; i++)
        {
            if (expected[i].Operator != actual[i].Operator)
            {
                return $"command {i}: expected {expected[i]}, got {actual[i]}";
            }

            for (int c = 0; c < expected[i].Coordinates.Length; c++)
            {
                if (!(System.MathF.Abs(expected[i].Coordinates[c] - actual[i].Coordinates[c]) <= tolerance))
                {
                    return $"command {i}: expected {expected[i]}, got {actual[i]}";
                }
            }
        }

        return null;
    }

    public void MoveTo(Vector2 point) => this.Commands.Add(new OutlineCommand('M', [point.X, point.Y]));

    public void LineTo(Vector2 point) => this.Commands.Add(new OutlineCommand('L', [point.X, point.Y]));

    public void QuadraticBezierTo(Vector2 control, Vector2 point)
        => this.Commands.Add(new OutlineCommand('Q', [control.X, control.Y, point.X, point.Y]));

    public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        => this.Commands.Add(new OutlineCommand('C', [control1.X, control1.Y, control2.X, control2.Y, point.X, point.Y]));

    public void ClosePath() => this.Commands.Add(new OutlineCommand('Z', []));
}
