---
title: TextMeasurer
description: API reference for TextMeasurer and GlyphBounds.
---

Namespace: `PinataLabs.Typography`. Guide: [Measuring text](../text/measuring-text.md).

## TextMeasurer

`public static class TextMeasurer`

Lays out text and measures it. Every method has an overload that takes `string` and one that takes
`ReadOnlySpan<char>`. All results are in pixels at `options.Dpi`.

| Method | Returns |
| --- | --- |
| `MeasureAdvance(text, options)` | The logical size of the text: the advances of the glyphs and the line heights. The position is `(0, 0)`. Rounded up to whole pixels. |
| `MeasureSize(text, options)` | The size from the top-left of the layout to the far edge of the ink. The position is `(0, 0)`. Rounded up to whole pixels. |
| `MeasureBounds(text, options)` | The ink box of the text, at its real position, including `Origin` and alignment. Not rounded. |
| `CountLines(text, options)` | The number of lines after wrapping and line breaks. |
| `TryMeasureCharacterAdvances(text, options, out ReadOnlySpan<GlyphBounds> advances)` | The advance of each glyph. Positions are `(0, 0)`. |
| `TryMeasureCharacterSizes(text, options, out ReadOnlySpan<GlyphBounds> sizes)` | The size of each glyph, from its origin to the edge of its ink. Positions are `(0, 0)`. |
| `TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> bounds)` | The ink box of each glyph, at its real position. |

The `TryMeasure...` methods give one entry for each glyph after shaping, which is not always one
for each character. They return `false` and an empty span when there are no glyphs.

Empty text measures as `FontRectangle.Empty`.

## GlyphBounds

`public readonly struct GlyphBounds`

| Member | Description |
| --- | --- |
| `GlyphBounds(CodePoint codePoint, in FontRectangle bounds)` | Makes a value. |
| `Codepoint` | The code point that the glyph came from. |
| `Bounds` | The rectangle that the method measured. |
