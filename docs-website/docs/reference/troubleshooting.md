---
title: Troubleshooting
description: Fixes for common problems with PinataLabs.Typography, such as missing fonts, boxes instead of characters, wrong sizes and reversed text.
---

## `FontFamilyNotFoundException` for an installed font

- Check the name. `Get` matches the **family** name, such as `Segoe UI`, not the full name such as
  `Segoe UI Bold`, and not the file name. List the names with `SystemFonts.Families`.
- On Linux and in containers, the font may not be installed, or may be in a folder that the library
  does not search. The exception message lists the folders that it searched. See
  [System fonts](../fonts/system-fonts.md#where-the-library-looks).
- WOFF and WOFF2 files in system folders are not loaded. Add them to a `FontCollection` yourself.

## Boxes (tofu) instead of characters

The font has no glyph for those characters, so the library used its missing-glyph box. Add a font
that has them to [`FallbackFontFamilies`](../text/fallback-fonts.md). To find the characters, look
for glyphs with `GlyphType.Fallback` in your renderer.

## The text is the wrong size

- Results are in pixels at `TextOptions.Dpi`, which is 72 by default. At 72 DPI, a 12pt font is 12
  pixels high. For a 96 DPI screen, set `Dpi = 96`.
- `MeasureAdvance`, `MeasureSize` and `MeasureBounds` measure different things. See
  [Measuring text](../text/measuring-text.md#size-advance-and-bounds).
- Measure with the same `TextOptions` that you render with.

## The text is upside down

The library uses a y-axis that points down, as images and screens do. PDF and some graphics systems
use a y-axis that points up. Flip the points when you draw: `y' = pageHeight - y`.

## English words come out reversed

`TextDirection` is set to `RightToLeft`. A fixed direction applies to every character. Use
`TextDirection.Auto` for text that can mix directions. See
[International text](../text/international-text.md#right-to-left-and-bidirectional-text).

## Arabic letters do not join, or Indic text is in the wrong order

The font does not support the script, or the text came from a fallback that does not. Use a font
made for that script, such as a Noto font for it. Check that the text is in logical order, not
already reversed.

## A long word goes past the wrapping length

`WordBreaking.Standard` breaks only where the line-breaking rules allow. Set
`WordBreaking = WordBreaking.BreakWord` to break a word that does not fit.

## `TabWidth` has no effect

`TabWidth` works only with fonts that have a glyph for the tab character, and most do not. See
[Wrapping and alignment](../text/wrapping-and-alignment.md#tabs).

## Underlines do not appear

The library does not draw decorations itself. Your renderer must return the decorations from
`EnabledDecorations` and draw the lines it gets in `SetDecoration`. See
[Glyph renderers](../rendering/glyph-renderers.md#decorations).

## Emoji are black and white

- The emoji font has no COLR table. Fonts with only bitmap or SVG color glyphs are drawn from their
  plain outlines. See [Color fonts](../rendering/color-fonts.md#formats-that-are-not-supported).
- Your renderer does not implement `IColorGlyphRenderer`, or ignores `SetColor`.
- `TextOptions.ColorFontSupport` is `None`.

## The build of the repository fails with an SDK error

`global.json` pins the .NET 11 SDK, RC2 or later. Install it, or build only your own project
against the package. See [Installation](../installation.md).
