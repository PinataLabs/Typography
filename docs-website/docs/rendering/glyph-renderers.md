---
title: Glyph renderers
description: Implement IGlyphRenderer to receive positioned glyph outlines and text decorations, and turn them into SVG paths, PDF operators or any other drawing commands.
---

The library does not draw. `TextRenderer` lays out the text, then calls an `IGlyphRenderer` that you
write, once for each glyph. Your renderer gets each glyph outline as a sequence of lines and Bézier
curves, already scaled, positioned and, in vertical text, rotated. You turn those calls into
drawing commands for your target: an SVG path, a PDF content stream, a `System.Drawing` path, a GPU
mesh, and so on.

## Render text

```csharp
using PinataLabs.Typography;

TextOptions options = new(font) { Origin = new(10, 10) };

// Static form:
TextRenderer.RenderTextTo(renderer, "Hello, world!", options);

// Instance form, to render more than one string to the same renderer:
TextRenderer textRenderer = new(renderer);
textRenderer.RenderText("First line", options);

// Extension method on the renderer:
renderer.Render("Hello", options);
```

All coordinates are in pixels at `TextOptions.Dpi`. The y-axis points down: the top of the text has
smaller y-values than the bottom.

## The order of the calls

For one call to `RenderTextTo`, the library calls your renderer in this order:

```text
BeginText(bounds of all the text)
  for each glyph:
    BeginGlyph(bounds of the glyph, parameters)      -> return true to get the outline
      SetColor(color)                                 (IColorGlyphRenderer, color layers only)
      for each closed contour of the outline:
        BeginFigure()
        MoveTo(start point)
        LineTo / QuadraticBezierTo / CubicBezierTo   (one or more)
        EndFigure()
      EnabledDecorations()
      SetDecoration(...)                              (once for each decoration you asked for)
    EndGlyph()                                        (always, even when BeginGlyph returned false)
EndText()
```

| Method | What to do |
| --- | --- |
| `BeginText` | Prepare for a new piece of text. `bounds` is the ink box of all the glyphs. |
| `BeginGlyph` | Start a glyph. Return `false` to skip its outline and decorations, for example when the glyph is outside the visible area, or when you already have the glyph in a cache. |
| `BeginFigure` | Start a new closed contour. |
| `MoveTo` | Move to the first point of the contour. |
| `LineTo` | Draw a straight line to the point. |
| `QuadraticBezierTo` | Draw a quadratic curve. TrueType outlines use these. |
| `CubicBezierTo` | Draw a cubic curve. CFF and CFF2 outlines use these. |
| `EndFigure` | Close the contour. |
| `EnabledDecorations` | Return the decorations that you want for this glyph. |
| `SetDecoration` | Draw one decoration line. |
| `EndGlyph` | Finish the glyph. |
| `EndText` | Finish the text. |

Fill the contours with the **non-zero winding rule**. Outer contours and the holes inside them (as
in `o` and `A`) go in opposite directions.

A whitespace glyph gets `BeginGlyph` and `EndGlyph`, and its decorations, but no figures.

## Example: an SVG renderer

This renderer writes one SVG `<path>` element for each glyph, and a `<line>` for each decoration:

```csharp
using System.Globalization;
using System.Numerics;
using System.Text;
using PinataLabs.Typography;

public sealed class SvgGlyphRenderer : IGlyphRenderer
{
    private readonly StringBuilder svg = new();
    private readonly StringBuilder path = new();
    private TextDecorations decorations;

    public string ToSvg(float width, float height)
        => $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\">{this.svg}</svg>";

    public void BeginText(in FontRectangle bounds) { }

    public void EndText() { }

    public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
    {
        this.path.Clear();
        this.decorations = parameters.TextRun.TextDecorations;
        return true;
    }

    public void BeginFigure() { }

    public void MoveTo(Vector2 point) => this.path.Append($"M{F(point.X)} {F(point.Y)}");

    public void LineTo(Vector2 point) => this.path.Append($"L{F(point.X)} {F(point.Y)}");

    public void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)
        => this.path.Append($"Q{F(secondControlPoint.X)} {F(secondControlPoint.Y)} {F(point.X)} {F(point.Y)}");

    public void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)
        => this.path.Append(
            $"C{F(secondControlPoint.X)} {F(secondControlPoint.Y)} " +
            $"{F(thirdControlPoint.X)} {F(thirdControlPoint.Y)} {F(point.X)} {F(point.Y)}");

    public void EndFigure() => this.path.Append('Z');

    public void EndGlyph()
    {
        if (this.path.Length > 0)
        {
            this.svg.Append($"<path d=\"{this.path}\" fill=\"black\"/>");
        }
    }

    public TextDecorations EnabledDecorations() => this.decorations;

    public void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)
    {
        // start and end are on the top edge of the line, so move down by half the thickness.
        float y = start.Y + (thickness / 2);
        this.svg.Append(
            $"<line x1=\"{F(start.X)}\" y1=\"{F(y)}\" x2=\"{F(end.X)}\" y2=\"{F(y)}\" " +
            $"stroke=\"black\" stroke-width=\"{F(thickness)}\"/>");
    }

    private static string F(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
```

Use it like this:

```csharp
TextOptions options = new(font) { Origin = new(10, 10), WrappingLength = 380 };
const string text = "Hello from PinataLabs.Typography!";

FontRectangle size = TextMeasurer.MeasureSize(text, options);

SvgGlyphRenderer renderer = new();
TextRenderer.RenderTextTo(renderer, text, options);
File.WriteAllText("hello.svg", renderer.ToSvg(size.Width + 20, size.Height + 20));
```

## Glyph parameters

`BeginGlyph` gets a `GlyphRendererParameters` value. It identifies the glyph and its context:

| Property | Meaning |
| --- | --- |
| `GlyphId` | The glyph's index in its font. |
| `CodePoint` | The code point that the glyph came from. |
| `Font` | The full name of the font that the glyph came from, in upper case, for example `OPEN SANS REGULAR`. A glyph from a fallback font has the name of that font. |
| `FontStyle` | The style of that font. |
| `PointSize` | The font size in points. |
| `Dpi` | The DPI of the layout. |
| `GlyphType` | `Standard`, `Fallback` (no font had the character) or `ColrLayer` (one layer of a color glyph). |
| `GlyphColor` | The color of a `ColrLayer` glyph. |
| `LayoutMode` | `Horizontal`, `Vertical` or `VerticalRotated`. |
| `TextRun` | The [`TextRun`](../text/rich-text.md) that the glyph is in. |

`GlyphRendererParameters` is an equatable value. Two glyphs with equal parameters have the same
outline, apart from their position. Use it as a cache key: draw the outline once, keep it, and on
later glyphs return `false` from `BeginGlyph` and draw the cached shape at the new position. The
`bounds` passed to `BeginGlyph` give that position.

The parameters do not include `HintingMode`. If you render the same font with and without
[hinting](./hinting.md), keep a separate cache for each.

## Decorations

Underline, strike-out and overline come from the [`TextRun`](../text/rich-text.md#decorations)
that a glyph is in. The library asks your renderer which ones it wants, with `EnabledDecorations`,
for each glyph. The usual answer is the decorations of the run:

```csharp
public TextDecorations EnabledDecorations() => this.currentParameters.TextRun.TextDecorations;
```

You can return other values, for example to underline links that you marked with a
[custom `TextRun`](../text/rich-text.md#subclass-textrun).

Then `SetDecoration` is called once for each decoration, with:

- `start` and `end`: the ends of the line along one edge, in pixels. For horizontal text they are on
  the top edge of the line, and the line extends `thickness` pixels down from them.
- `thickness`: the thickness of the line, from the font's metrics.

Each glyph gets its own segment of the line, including spaces. Draw the segments next to each other
and they join into one line. You can also join segments that touch into one longer line.

## Thread safety

A `TextRenderer` and your `IGlyphRenderer` hold state, so use one of each on one thread at a time.
`Font`, `FontFamily` and a filled `FontCollection` can be shared: many threads can measure and
render with the same font at once, each with its own `TextOptions` and renderer. Add all fonts to a
collection before you share it.
