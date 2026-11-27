// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The hinting programs of PhantomPoints.ttf move the outline to the phantom points. FreeType places the phantom points
/// from the advances of the 'hmtx' and 'vmtx' tables, so a glyph that GPOS kerns hints as it does on its own.
/// </summary>
public class PhantomPointKerningTests
{
    // The font is as large in points as it has units to the em, so at 72 dots per inch a pixel is a font unit.
    private static readonly Font Font = new FontCollection().Add(TestFonts.GetPath("PhantomPoints.ttf")).CreateFont(1024);

    // R goes from the left of its box, at 64, to the second phantom point, at the end of its advance width of 736.
    // kern moves the advance of the first R of R R by -100.
    [Test]
    public async Task AKernedGlyphHintsToTheEndOfTheAdvanceWidthOfTheFont()
    {
        TextOptions options = new(Font) { HintingMode = HintingMode.Standard };

        await Assert.That(string.Join(' ', GetInkSizes("RR", options).Select(s => s.X))).IsEqualTo("672 672");
    }

    // T goes from the third phantom point to the fourth, which is the advance height of 752 below it. vkrn moves the
    // advance of the first T of T T by -100.
    [Test]
    public async Task AKernedGlyphHintsToTheEndOfTheAdvanceHeightOfTheFont()
    {
        TextOptions options = new(Font)
        {
            HintingMode = HintingMode.Standard,
            LayoutMode = LayoutMode.VerticalLeftRight,
            FeatureTags = [Tag.Parse("vkrn")],
        };

        await Assert.That(string.Join(' ', GetInkSizes("TT", options).Select(s => s.Y))).IsEqualTo("752 752");
    }

    private static List<Vector2> GetInkSizes(string text, TextOptions options)
    {
        InkRecorder recorder = new();
        TextRenderer.RenderTextTo(recorder, text, options);
        return recorder.Glyphs
            .Select(points => new Vector2(points.Max(p => p.X) - points.Min(p => p.X), points.Max(p => p.Y) - points.Min(p => p.Y)))
            .Select(size => new Vector2(MathF.Round(size.X), MathF.Round(size.Y)))
            .ToList();
    }

    private sealed class InkRecorder : IGlyphRenderer
    {
        public List<List<Vector2>> Glyphs { get; } = [];

        public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
        {
            this.Glyphs.Add([]);
            return true;
        }

        public void MoveTo(Vector2 point) => this.Glyphs[^1].Add(point);

        public void LineTo(Vector2 point) => this.Glyphs[^1].Add(point);

        public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point) => this.Glyphs[^1].Add(point);

        public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point) => this.Glyphs[^1].Add(point);

        public void BeginFigure()
        {
        }

        public void EndFigure()
        {
        }

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
    }
}
