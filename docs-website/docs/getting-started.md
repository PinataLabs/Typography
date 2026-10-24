---
title: Getting started
description: Load a font, measure a string, wrap a paragraph and get the glyph outlines with PinataLabs.Typography.
---

This page takes you from an empty console application to measured, wrapped text. Each step adds a
few lines to the same program.

You need the .NET 8 SDK or later, and a font file. Any `.ttf`, `.otf`, `.woff` or `.woff2` file
works. The examples use `fonts/OpenSans-Regular.ttf`. Change the path to a font you have.

## 1. Create the project

```bash
dotnet new console -o MeasureText
cd MeasureText
```

Then add the library. [Installation](./installation.md) explains how.

## 2. Load a font

A `FontCollection` holds the fonts that you load. `Add` reads a font file and returns its
`FontFamily`. `CreateFont` makes a `Font`: the family at one size, in points.

```csharp
using PinataLabs.Typography;

FontCollection fonts = new();
FontFamily family = fonts.Add("fonts/OpenSans-Regular.ttf");
Font font = family.CreateFont(24);

Console.WriteLine($"Loaded {font.Name} at {font.Size}pt");
```

To use a font that is installed on the machine instead of a file, use `SystemFonts`:

```csharp
Font font = SystemFonts.CreateFont("Arial", 24);
```

`SystemFonts.CreateFont` throws `FontFamilyNotFoundException` if the font is not installed. Use
`SystemFonts.TryGet` when you are not sure. See [System fonts](./fonts/system-fonts.md).

## 3. Measure a string

Put the font in a `TextOptions` object and give it to `TextMeasurer`:

```csharp
TextOptions options = new(font);

FontRectangle size = TextMeasurer.MeasureSize("Hello, world!", options);
Console.WriteLine($"Width: {size.Width}, height: {size.Height}");
```

The result is in pixels. At the default of 72 DPI, one point is one pixel, so the numbers are
also points. Set `options.Dpi` to measure for a screen or a printer with a different resolution.

`TextMeasurer` has more than one way to measure. `MeasureSize` gives the space the drawn text
fills. `MeasureAdvance` gives the space the text takes in a line, including the spacing on each
side of the glyphs. `MeasureBounds` gives the exact ink box and its position. See
[Measuring text](./text/measuring-text.md).

## 4. Wrap a paragraph

Set `WrappingLength` to break the text into lines no wider than that length, in pixels:

```csharp
TextOptions paragraph = new(font)
{
    WrappingLength = 300,
    LineSpacing = 1.2F,
    TextAlignment = TextAlignment.Center,
};

const string text = "The quick brown fox jumps over the lazy dog. The five boxing wizards jump quickly.";

int lines = TextMeasurer.CountLines(text, paragraph);
FontRectangle box = TextMeasurer.MeasureSize(text, paragraph);
Console.WriteLine($"{lines} lines, {box.Width} x {box.Height}");
```

See [Wrapping and alignment](./text/wrapping-and-alignment.md) for all the layout options.

## 5. Get the glyph outlines

To draw the text, implement `IGlyphRenderer`. The library calls it once for each glyph, with the
glyph outline as lines and curves, already positioned and scaled. This renderer only counts the
glyphs:

```csharp
using System.Numerics;
using PinataLabs.Typography;

sealed class CountingRenderer : IGlyphRenderer
{
    public int Glyphs { get; private set; }

    public void BeginText(in FontRectangle bounds) { }
    public void EndText() { }

    public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
    {
        this.Glyphs++;
        return true; // Return false to skip the outline of this glyph.
    }

    public void EndGlyph() { }
    public void BeginFigure() { }
    public void MoveTo(Vector2 point) { }
    public void LineTo(Vector2 point) { }
    public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point) { }
    public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point) { }
    public void EndFigure() { }

    public TextDecorations EnabledDecorations() => TextDecorations.None;
    public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness) { }
}
```

Then render the text to it:

```csharp
CountingRenderer renderer = new();
TextRenderer.RenderTextTo(renderer, "Hello, world!", options);
Console.WriteLine($"{renderer.Glyphs} glyphs");
```

[Glyph renderers](./rendering/glyph-renderers.md) shows a renderer that writes SVG paths.

## What to read next

- [Concepts](./concepts.md): points, DPI, advance, bounds and the other words these pages use.
- [Loading fonts](./fonts/loading-fonts.md): streams, font collections and web fonts.
- [International text](./text/international-text.md): right-to-left and complex scripts.
