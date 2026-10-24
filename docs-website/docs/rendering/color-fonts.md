---
title: Color fonts
description: Render color emoji and other color glyphs from COLR and CPAL tables with IColorGlyphRenderer.
---

Some fonts, mostly emoji fonts, have glyphs in color. The library supports color glyphs from the
OpenType **COLR** and **CPAL** tables, version 0. Each color glyph is a stack of ordinary outlines,
each with one solid color. The library sends each layer to your renderer as a glyph of its own.

## Turn color glyphs on or off

`TextOptions.ColorFontSupport` controls color glyphs:

| Value | Effect |
| --- | --- |
| `MicrosoftColrFormat` (default) | Use the COLR layers when the font has them. |
| `None` | Use the plain, one-color outline of each glyph. |

```csharp
TextOptions options = new(emojiFont)
{
    ColorFontSupport = ColorFontSupport.None, // One outline per emoji.
};
```

## Get the colors

Implement `IColorGlyphRenderer` instead of `IGlyphRenderer`. It adds one method, `SetColor`. For each
color layer, the library calls `BeginGlyph`, then `SetColor`, then sends the outline of the layer:

```csharp
using System.Numerics;
using PinataLabs.Typography;

public sealed class ColorRenderer : IColorGlyphRenderer
{
    private GlyphColor? color;

    public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
    {
        this.color = null; // SetColor is called next for a color layer.
        return true;
    }

    public void SetColor(GlyphColor color) => this.color = color;

    public void EndGlyph()
    {
        // Fill the outline that you collected with this.color, or with the text color when it is null.
        string fill = this.color is { } c ? $"rgba({c.Red},{c.Green},{c.Blue},{c.Alpha / 255F})" : "black";
        // ...
    }

    // The other IGlyphRenderer members are the same as for a one-color renderer.
    public void BeginText(in FontRectangle bounds) { }
    public void EndText() { }
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

The layers come in order from bottom to top. Draw them in the order you get them.

Each layer has `GlyphType.ColrLayer` and the same `CodePoint`. The color is also in
`GlyphRendererParameters.GlyphColor`, so a renderer that implements only `IGlyphRenderer` can read
it there.

## Emoji in ordinary text

Text fonts seldom have emoji. Add an emoji font as a [fallback font](../text/fallback-fonts.md) so
that emoji in the text take their glyphs from it:

```csharp
TextOptions options = new(textFont)
{
    FallbackFontFamilies = [emojiFamily],
};
```

Emoji sequences, such as a thumbs-up with a skin tone or a family made of several people joined by
zero-width joiners, are shaped by the font's ligatures into one glyph when the font supports them.

## Formats that are not supported

Glyphs in these formats are drawn from their plain outline, or not at all if the font has no plain
outline:

- COLR version 1 (gradients and transformations).
- `SVG ` tables (SVG glyphs).
- `CBDT`/`CBLC` and `sbix` tables (bitmap glyphs, as in Noto Color Emoji's bitmap build and Apple
  Color Emoji).
