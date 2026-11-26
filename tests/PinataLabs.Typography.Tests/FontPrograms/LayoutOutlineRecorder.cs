// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Records the calls that the layout path makes on an <see cref="IGlyphRenderer"/> for one glyph.
/// </summary>
internal sealed class LayoutOutlineRecorder : IGlyphRenderer
{
    private readonly StringBuilder calls = new();

    /// <summary>
    /// Renders one glyph by glyph ID at a scale of one, so that the recorded points are in font units.
    /// The layout path points y down, so y is the negative of the glyph space y.
    /// </summary>
    public static string Render(Font font, ushort glyphId)
    {
        TextOptions options = new(font) { Dpi = 72, HintingMode = HintingMode.None };
        GlyphMetrics cached = GetMetrics(font, glyphId);
        GlyphMetrics metrics = cached.CloneForRendering(new TextRun { Font = font }, cached.CodePoint);
        LayoutOutlineRecorder recorder = new();
        metrics.RenderTo(recorder, Vector2.Zero, Vector2.Zero, GlyphLayoutMode.Horizontal, options);
        return recorder.calls.ToString();
    }

    /// <summary>
    /// Describes every glyph of a font as one line: glyph ID, bounds, advance, and a hash of the renderer calls.
    /// </summary>
    public static string[] Describe(string path)
    {
        Font font = LoadAtUnitScale(path);
        int glyphCount = ReadGlyphCount(path);
        string[] lines = new string[glyphCount];
        for (int i = 0; i < glyphCount; i++)
        {
            GlyphMetrics metrics = GetMetrics(font, (ushort)i);
            string calls = Render(font, (ushort)i);
            lines[i] = string.Create(
                CultureInfo.InvariantCulture,
                $"{i} bounds {metrics.LeftSideBearing} {metrics.Width} {metrics.Height} advance {metrics.AdvanceWidth} calls {Hash(calls):x16}");
        }

        return lines;
    }

    /// <summary>
    /// Converts recorded calls to the outline text of the fontTools fixtures: glyph space, y up,
    /// with <c>Z</c> for the end of a contour.
    /// </summary>
    public static string ToGlyphSpaceOutline(string calls)
    {
        StringBuilder builder = new();
        string[] tokens = calls.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int coordinate = 0;
        foreach (string token in tokens)
        {
            if (token is "F")
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            if (token is "E")
            {
                builder.Append('Z');
            }
            else if (char.IsLetter(token[0]))
            {
                builder.Append(token);
                coordinate = 0;
            }
            else
            {
                float value = float.Parse(token, CultureInfo.InvariantCulture);
                builder.Append(((coordinate++ & 1) == 1 ? -value : value).ToString("R", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Loads a font at one unit to the font unit. A variable font is at its default instance, which a regular request is
    /// not when the default weight is not 400.
    /// </summary>
    public static Font LoadAtUnitScale(string path)
    {
        FontFamily family = new FontCollection().Add(path);
        FontMetrics face = family.Faces[0];
        return family.CreateFont(face.UnitsPerEm, default, [.. face.Variations]);
    }

    public static int ReadGlyphCount(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int tableCount = (data[4] << 8) | data[5];
        for (int i = 0; i < tableCount; i++)
        {
            int entry = 12 + (i * 16);
            if (Encoding.ASCII.GetString(data, entry, 4) == "maxp")
            {
                int offset = (data[entry + 8] << 24) | (data[entry + 9] << 16) | (data[entry + 10] << 8) | data[entry + 11];
                return (data[offset + 4] << 8) | data[offset + 5];
            }
        }

        throw new InvalidDataException("The font has no maxp table.");
    }

    public void BeginFigure() => this.calls.Append("F ");

    public void MoveTo(Vector2 point) => this.Append('M', point);

    public void LineTo(Vector2 point) => this.Append('L', point);

    public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
    {
        this.Append('Q', secondControlPoint);
        this.Append(point);
    }

    public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
    {
        this.Append('C', secondControlPoint);
        this.Append(thirdControlPoint);
        this.Append(point);
    }

    public void EndFigure() => this.calls.Append("E ");

    public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters) => true;

    public void EndGlyph()
    {
    }

    public void BeginText(in FontRectangle bounds)
    {
    }

    public void EndText()
    {
    }

    public TextDecorations EnabledDecorations() => TextDecorations.None;

    public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)
    {
    }

    private static GlyphMetrics GetMetrics(Font font, ushort glyphId)
        => font.FontMetrics.GetGlyphMetrics(
            new CodePoint('A'),
            glyphId,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None)[0];

    private static ulong Hash(string text)
    {
        ulong hash = 14695981039346656037;
        foreach (char c in text)
        {
            hash = (hash ^ c) * 1099511628211;
        }

        return hash;
    }

    private void Append(char command, Vector2 point)
    {
        this.calls.Append(command).Append(' ');
        this.Append(point);
    }

    private void Append(Vector2 point)
        => this.calls
            .Append(point.X.ToString("R", CultureInfo.InvariantCulture)).Append(' ')
            .Append(point.Y.ToString("R", CultureInfo.InvariantCulture)).Append(' ');
}
