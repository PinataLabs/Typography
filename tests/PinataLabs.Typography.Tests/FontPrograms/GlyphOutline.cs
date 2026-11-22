// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// An outline as a list of path commands. It records what a font writes to it, parses the outline
/// text of a fixture, and compares two outlines.
/// </summary>
internal sealed class GlyphOutline : IGlyphOutlineSink
{
    /// <summary>
    /// The largest distance, in ems, between a point and its reference point.
    /// It is 0.01 units of a 1000 unit em. Outlines are computed in single precision.
    /// </summary>
    public const double Tolerance = 1e-5;

    private readonly List<(char Command, double[] Values)> commands = [];

    public static GlyphOutline Parse(string text)
    {
        GlyphOutline outline = new();
        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < tokens.Length;)
        {
            char command = tokens[i++][0];
            int count = command switch { 'M' or 'L' => 2, 'Q' => 4, 'C' => 6, 'Z' => 0, _ => throw new FormatException(text) };
            double[] values = new double[count];
            for (int j = 0; j < count; j++)
            {
                values[j] = double.Parse(tokens[i++], CultureInfo.InvariantCulture);
            }

            outline.commands.Add((command, values));
        }

        return outline;
    }

    /// <summary>
    /// Compares two outlines after each is mapped to ems by its matrix, given as [a b c d e f].
    /// </summary>
    /// <returns>A description of the first difference, or null when the outlines match.</returns>
    public static string? Compare(GlyphOutline expected, double[] expectedMatrix, GlyphOutline actual, double[] actualMatrix)
    {
        if (expected.commands.Count != actual.commands.Count)
        {
            return $"expected '{expected}' but was '{actual}'";
        }

        for (int i = 0; i < expected.commands.Count; i++)
        {
            (char command, double[] values) = expected.commands[i];
            (char actualCommand, double[] actualValues) = actual.commands[i];
            if (command != actualCommand)
            {
                return $"command {i}: expected '{expected}' but was '{actual}'";
            }

            for (int j = 0; j < values.Length; j += 2)
            {
                (double x, double y) = Apply(expectedMatrix, values[j], values[j + 1]);
                (double actualX, double actualY) = Apply(actualMatrix, actualValues[j], actualValues[j + 1]);
                if (Math.Abs(x - actualX) > Tolerance || Math.Abs(y - actualY) > Tolerance)
                {
                    return $"command {i}: expected '{expected}' but was '{actual}'";
                }
            }
        }

        return null;
    }

    public static double[] ToArray(Matrix3x2 matrix)
        => [matrix.M11, matrix.M12, matrix.M21, matrix.M22, matrix.M31, matrix.M32];

    public void MoveTo(Vector2 point) => this.commands.Add(('M', [point.X, point.Y]));

    public void LineTo(Vector2 point) => this.commands.Add(('L', [point.X, point.Y]));

    public void QuadraticBezierTo(Vector2 control, Vector2 point)
        => this.commands.Add(('Q', [control.X, control.Y, point.X, point.Y]));

    public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        => this.commands.Add(('C', [control1.X, control1.Y, control2.X, control2.Y, point.X, point.Y]));

    public void ClosePath() => this.commands.Add(('Z', []));

    public override string ToString()
    {
        StringBuilder builder = new();
        foreach ((char command, double[] values) in this.commands)
        {
            builder.Append(builder.Length > 0 ? " " : string.Empty).Append(command);
            foreach (double value in values)
            {
                builder.Append(' ').Append(value.ToString("R", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static (double X, double Y) Apply(double[] m, double x, double y)
        => ((m[0] * x) + (m[2] * y) + m[4], (m[1] * x) + (m[3] * y) + m[5]);
}
