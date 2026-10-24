---
title: TextRenderer and glyph renderers
description: API reference for TextRenderer, IGlyphRenderer, IColorGlyphRenderer, GlyphRendererParameters and IGlyphRendererExtensions.
---

Namespace: `PinataLabs.Typography`. Guides: [Glyph renderers](../rendering/glyph-renderers.md),
[Color fonts](../rendering/color-fonts.md).

## TextRenderer

`public class TextRenderer`

Lays out text and sends the glyph outlines to an `IGlyphRenderer`.

| Member | Description |
| --- | --- |
| `TextRenderer(IGlyphRenderer renderer)` | Makes a text renderer that renders to `renderer`. |
| `RenderText(string text, TextOptions options)` | Lays out and renders `text`. |
| `RenderText(ReadOnlySpan<char> text, TextOptions options)` | Lays out and renders `text`. |
| `static RenderTextTo(IGlyphRenderer renderer, string text, TextOptions options)` | Lays out and renders `text` to `renderer`. |
| `static RenderTextTo(IGlyphRenderer renderer, ReadOnlySpan<char> text, TextOptions options)` | Lays out and renders `text` to `renderer`. |

### IGlyphRendererExtensions

| Member | Description |
| --- | --- |
| `Render(this IGlyphRenderer renderer, ReadOnlySpan<char> text, TextOptions options)` | Renders `text` to `renderer`. Returns `renderer`. |

## IGlyphRenderer

`public interface IGlyphRenderer`

A surface that receives glyph outlines. All points are in pixels at the DPI of the layout, with the
y-axis pointing down.

| Member | Description |
| --- | --- |
| `void BeginText(in FontRectangle bounds)` | Called once, before the first glyph. `bounds` is the ink box of the text. |
| `bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)` | Called for each glyph. `bounds` is the ink box of the glyph. Return `true` to receive the outline and decorations, `false` to skip them. |
| `void BeginFigure()` | Starts a closed contour. |
| `void MoveTo(Vector2 point)` | Sets the first point of the contour. |
| `void LineTo(Vector2 point)` | Adds a straight line from the current point. |
| `void QuadraticBezierTo(Vector2 secondControlPoint, Vector2 point)` | Adds a quadratic Bézier curve from the current point. |
| `void CubicBezierTo(Vector2 secondControlPoint, Vector2 thirdControlPoint, Vector2 point)` | Adds a cubic Bézier curve from the current point. |
| `void EndFigure()` | Closes the contour. |
| `TextDecorations EnabledDecorations()` | Returns the decorations that the renderer wants for the current glyph. |
| `void SetDecoration(TextDecorations textDecorations, Vector2 start, Vector2 end, float thickness)` | Gives one decoration line for the current glyph. `start` and `end` are on one edge of the line. |
| `void EndGlyph()` | Called after each glyph, also when `BeginGlyph` returned `false`. |
| `void EndText()` | Called once, after the last glyph. |

In each curve method, the first control point is the current point, so it is not passed. That is why
the parameters start at `secondControlPoint`.

## IColorGlyphRenderer

`public interface IColorGlyphRenderer : IGlyphRenderer`

A renderer that supports color glyphs.

| Member | Description |
| --- | --- |
| `void SetColor(GlyphColor color)` | Called after `BeginGlyph` for each layer of a color glyph, before its outline. |

## GlyphRendererParameters

`public readonly struct GlyphRendererParameters : IEquatable<GlyphRendererParameters>`

Identifies a glyph. Two values are equal when they describe the same outline at the same size, so
the value can be a cache key.

| Property | Type | Description |
| --- | --- | --- |
| `GlyphId` | `ushort` | The index of the glyph in its font. |
| `CodePoint` | `CodePoint` | The code point that the glyph came from. |
| `Font` | `string` | The full name of the font, in upper case. |
| `FontStyle` | `FontStyle` | The style of the font. |
| `PointSize` | `float` | The size in points. |
| `Dpi` | `float` | The DPI of the layout. |
| `GlyphType` | `GlyphType` | `Standard`, `Fallback` or `ColrLayer`. |
| `GlyphColor` | `GlyphColor` | The color of a color layer. |
| `LayoutMode` | `GlyphLayoutMode` | `Horizontal`, `Vertical` or `VerticalRotated`. |
| `TextRun` | `TextRun` | The run that the glyph is in. |

Equality compares `GlyphId`, `Font`, `FontStyle`, `PointSize`, `Dpi`, `GlyphType`, `GlyphColor`,
`LayoutMode`, and the `TextAttributes` and `TextDecorations` of the run.
