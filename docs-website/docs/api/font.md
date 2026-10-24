---
title: Font
description: API reference for Font and FontStyle.
---

Namespace: `PinataLabs.Typography`. Guide: [Loading fonts](../fonts/loading-fonts.md#create-a-font).

## Font

`public sealed class Font`

A font family at one size and one style. Make one with `FontFamily.CreateFont`,
`SystemFonts.CreateFont`, or one of the constructors.

### Constructors

| Constructor | Description |
| --- | --- |
| `Font(FontFamily family, float size)` | A regular font of `family` at `size` points. |
| `Font(FontFamily family, float size, FontStyle style)` | A font of `family` in `style` at `size` points. |
| `Font(Font prototype, float size)` | A copy of `prototype` at a new size. |
| `Font(Font prototype, FontStyle style)` | A copy of `prototype` in a new style. |
| `Font(Font prototype, float size, FontStyle style)` | A copy of `prototype` at a new size and in a new style. |

### Properties

| Property | Description |
| --- | --- |
| `Family` | The family of the font. |
| `Name` | The full name of the font face that is used, for example *Open Sans Bold*. |
| `Size` | The size in points. |
| `IsBold` | `true` if the face that is used is bold. |
| `IsItalic` | `true` if the face that is used is italic. |
| `FontMetrics` | The [metrics](./metrics.md#fontmetrics) of the face that is used. |

When the family does not have the requested style, the font uses the nearest style that it has:
for bold italic it tries italic, then bold, then regular. `Name`, `IsBold`, `IsItalic` and
`FontMetrics` describe the face that is really used.

### Methods

| Method | Description |
| --- | --- |
| `TryGetGlyphs(CodePoint codePoint, out IReadOnlyList<Glyph>? glyphs)` | Gets the glyphs for a code point. A color glyph can have more than one. A code point that the font does not have gives its missing glyph, with `GlyphType.Fallback`. |
| `TryGetGlyphs(CodePoint, ColorFontSupport, out …)` | As above, with or without color layers. |
| `TryGetGlyphs(CodePoint, TextAttributes, ColorFontSupport, out …)` | As above, for subscript or superscript. |
| `TryGetGlyphs(CodePoint, TextAttributes, LayoutMode, ColorFontSupport, out …)` | As above, for a layout mode. |
| `TryGetGlyphs(CodePoint, TextAttributes, TextDecorations, LayoutMode, ColorFontSupport, out …)` | As above, with decorations. |
| `TryGetKerningOffset(Glyph previous, Glyph current, float dpi, out Vector2 vector)` | Gets the kerning between two glyphs from the font's `kern` table, in pixels at `dpi`. |
| `TryGetPath(out string? path)` | Gets the file that the font was loaded from. Returns `false` for a font loaded from a stream. |

The overload of `TryGetGlyphs` without `ColorFontSupport` does not return color layers.

## FontStyle

`public enum FontStyle`

| Value | Number |
| --- | --- |
| `Regular` | 0 |
| `Bold` | 1 |
| `Italic` | 2 |
| `BoldItalic` | 3 (`Bold \| Italic`) |
