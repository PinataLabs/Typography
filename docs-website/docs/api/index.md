---
title: API reference
slug: /api
description: The public types of PinataLabs.Typography, grouped by what they do.
---

These pages list every public type of PinataLabs.Typography, grouped by what it does. The guides
explain how to use them together; these pages tell you what each member does.

The authoritative list of the public API is the file
[`src/PinataLabs.Typography/PublicAPI.Shipped.txt`](https://github.com/PinataLabs/Typography/blob/main/src/PinataLabs.Typography/PublicAPI.Shipped.txt).
The build fails if the public API differs from it, so it is always up to date.

## Namespaces

| Namespace | Contains |
| --- | --- |
| `PinataLabs.Typography` | Fonts, collections, layout options, measurement, rendering and metrics. Most code needs only this namespace. |
| `PinataLabs.Typography.Tables.AdvancedTypographic` | `Tag` and `FeatureTags`, to turn on [OpenType features](../text/opentype-features.md). |
| `PinataLabs.Typography.Unicode` | `CodePoint`, grapheme and code-point enumeration, and Unicode properties. |
| `PinataLabs.Typography.WellKnownIds` | `KnownNameIds`, the IDs of the strings in a font's `name` table. |

## Types by task

| Task | Types |
| --- | --- |
| Load and find fonts | [`FontCollection`, `SystemFonts`, `FontFamily`, `FontDescription`](./font-collections.md) |
| Make a font | [`Font`, `FontStyle`](./font.md) |
| Set up a layout | [`TextOptions`, `TextRun`](./text-options.md) |
| Measure text | [`TextMeasurer`, `GlyphBounds`](./text-measurer.md) |
| Get glyph outlines | [`TextRenderer`, `IGlyphRenderer`, `IColorGlyphRenderer`, `GlyphRendererParameters`](./text-renderer.md) |
| Read metrics | [`FontMetrics`, `HorizontalMetrics`, `VerticalMetrics`, `Glyph`, `GlyphMetrics`](./metrics.md) |
| Rectangles and colors | [`FontRectangle`, `GlyphColor`](./geometry.md) |
| Options | [Enumerations](./enumerations.md) |
| Unicode | [`CodePoint` and related types](./unicode.md) |
| Errors | [Exceptions](./exceptions.md) |

## Conventions

- Every method that takes text has an overload for `string` and one for `ReadOnlySpan<char>`.
- Methods named `TryGet...` return `false` instead of throwing when the item is not found.
- Lengths and positions in layout results are in pixels at `TextOptions.Dpi`. Values in
  `FontMetrics` and `GlyphMetrics` are in font units.
