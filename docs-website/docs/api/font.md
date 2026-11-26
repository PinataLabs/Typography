---
title: Font
description: API reference for Font, FontStyle and FontStyleRange.
---

Namespace: `PinataLabs.Typography`. Guide: [Loading fonts](../fonts/loading-fonts.md#create-a-font).

## Font

`public sealed class Font`

A font family at one size and one style. Make one with `FontFamily.CreateFont`,
`SystemFonts.CreateFont`, or one of the constructors.

### Constructors

| Constructor | Description |
| --- | --- |
| `Font(FontFamily family, float size, FontStyle style = default)` | A font of `family` in `style` at `size` points. The default style is regular. |

### Properties

| Property | Description |
| --- | --- |
| `Family` | The family of the font. |
| `Name` | The full name of the font face that is used, for example *Open Sans Bold*. |
| `Size` | The size in points. |
| `Style` | The style that was asked for. |
| `FontMetrics` | The [metrics](./metrics.md#fontmetrics) of the face that is used. |

The font uses the face of the family that best matches `Style`, as `FontFamily.MatchFace` picks it.
When the family does not have the requested style, the face is the nearest one in the order that CSS
Fonts Level 4 gives. `Name` and `FontMetrics` describe the face that is really used. Compare `Style`
with `FontMetrics.Description.StyleRange` to see whether the face has the style.

### Methods

| Method | Description |
| --- | --- |
| `WithSize(float size)` | A font of the same family and style at `size` points. |
| `WithStyle(FontStyle style)` | A font of the same family and size in `style`. |
| `TryGetGlyphs(CodePoint codePoint, out IReadOnlyList<Glyph>? glyphs)` | Gets the glyphs for a code point. A color glyph can have more than one. A code point that the font does not have gives its missing glyph, with `GlyphType.Fallback`. |
| `TryGetGlyphs(CodePoint, ColorFontSupport, out …)` | As above, with or without color layers. |
| `TryGetGlyphs(CodePoint, TextAttributes, ColorFontSupport, out …)` | As above, for subscript or superscript. |
| `TryGetGlyphs(CodePoint, TextAttributes, LayoutMode, ColorFontSupport, out …)` | As above, for a layout mode. |
| `TryGetGlyphs(CodePoint, TextAttributes, TextDecorations, LayoutMode, ColorFontSupport, out …)` | As above, with decorations. |
| `TryGetKerningOffset(Glyph previous, Glyph current, float dpi, out Vector2 vector)` | Gets the kerning between two glyphs from the font's `kern` table, in pixels at `dpi`. |
| `TryGetPath(out string? path)` | Gets the file that the font was loaded from. Returns `false` for a font loaded from a stream. |

The overload of `TryGetGlyphs` without `ColorFontSupport` does not return color layers.

## FontStyle

`public readonly struct FontStyle : IEquatable<FontStyle>`

The style that a caller asks for: a weight, a width and a slope, as CSS gives them. The default value is
`FontStyle.Regular`.

| Member | Description |
| --- | --- |
| `FontStyle(FontWeight weight, FontWidth width, FontSlope slope)` | A style from its three parts. |
| `Weight`, `Width`, `Slope` | The parts. |
| `WithWeight(FontWeight)`, `WithWidth(FontWidth)`, `WithSlope(FontSlope)` | A style with one part changed. |
| `static Regular` | Weight 400, width 100%, normal slope. |
| `static Bold` | Weight 700, width 100%, normal slope. |
| `static Italic` | Weight 400, width 100%, italic slope. |
| `static BoldItalic` | Weight 700, width 100%, italic slope. |

## FontWeight, FontWidth and FontSlope

`FontWeight` is a number from 1 to 1000, as CSS `font-weight`. It has the keywords `Thin` (100), `ExtraLight`,
`Light`, `Normal` (400), `Medium`, `SemiBold`, `Bold` (700), `ExtraBold` and `Black` (900).

`FontWidth` is a percentage from 50 to 200, as CSS `font-stretch`. It has the keywords `UltraCondensed` (50%),
`ExtraCondensed`, `Condensed` (75%), `SemiCondensed`, `Normal` (100%), `SemiExpanded`, `Expanded` (125%),
`ExtraExpanded` and `UltraExpanded` (200%).

`FontSlope` is `Normal`, `Italic` or `Oblique(float degrees = 14)`, as CSS `font-style`. `Kind` is the
`FontSlopeKind` and `ObliqueAngle` is the angle in degrees.

A constructor clamps a value to its range. The default value of each type is its normal value.

## FontStyleRange

`public sealed class FontStyleRange`

The styles that a face covers, from `FontDescription.StyleRange`. `MinWeight`, `MaxWeight`, `MinWidth` and
`MaxWidth` give the ranges, `HasNormal`, `HasItalic` and `HasOblique` say which slopes the face has,
`MinObliqueAngle` and `MaxObliqueAngle` give the oblique angles, and `Contains(FontStyle style)` says whether the
face covers a style. A static face covers one point. The weight comes from `OS/2.usWeightClass`, the width from
`usWidthClass`, the slope from `fsSelection` and `post.italicAngle`.
