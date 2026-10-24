---
title: Metrics
description: API reference for FontMetrics, HorizontalMetrics, VerticalMetrics, IMetricsHeader, Glyph, GlyphMetrics and TrueTypeGlyphMetrics.
---

Namespace: `PinataLabs.Typography`. Guide: [Font and glyph metrics](../fonts/font-metrics.md).

All values on this page are in font units unless the description says otherwise.

## FontMetrics

`public abstract class FontMetrics`

The metrics of one font face. Get it from `Font.FontMetrics` or `FontFamily.TryGetMetrics`.

| Property | Type | Description |
| --- | --- | --- |
| `Description` | `FontDescription` | The names and style of the face. |
| `UnitsPerEm` | `ushort` | The size of the design grid. |
| `ScaleFactor` | `float` | `UnitsPerEm × 72`. |
| `HorizontalMetrics` | `HorizontalMetrics` | Metrics for horizontal text. |
| `VerticalMetrics` | `VerticalMetrics` | Metrics for vertical text. Made from the horizontal metrics when the font has no `vhea` table. |
| `UnderlinePosition` | `short` | The distance from the baseline to the top of the underline. Negative is below the baseline. |
| `UnderlineThickness` | `short` | The thickness of the underline. |
| `StrikeoutPosition` | `short` | The distance from the baseline to the top of the strike-out line. |
| `StrikeoutSize` | `short` | The thickness of the strike-out line. |
| `SubscriptXSize`, `SubscriptYSize` | `short` | The size of subscript glyphs. |
| `SubscriptXOffset`, `SubscriptYOffset` | `short` | The offset of subscript glyphs. |
| `SuperscriptXSize`, `SuperscriptYSize` | `short` | The size of superscript glyphs. |
| `SuperscriptXOffset`, `SuperscriptYOffset` | `short` | The offset of superscript glyphs. |
| `ItalicAngle` | `float` | The italic angle in degrees, counter-clockwise from vertical. |

| Method | Description |
| --- | --- |
| `TryGetGlyphMetrics(CodePoint, TextAttributes, TextDecorations, LayoutMode, ColorFontSupport, out IReadOnlyList<GlyphMetrics>? metrics)` | Gets the metrics of the glyphs for a code point. |

## HorizontalMetrics and VerticalMetrics

`public class HorizontalMetrics : IMetricsHeader`
`public class VerticalMetrics : IMetricsHeader`

Both have the members of `IMetricsHeader`:

| Property | Type | Description |
| --- | --- | --- |
| `Ascender` | `short` | The distance from the baseline to the top of the line. |
| `Descender` | `short` | The distance from the baseline to the bottom of the line. Usually negative. |
| `LineGap` | `short` | The extra space between lines. |
| `LineHeight` | `short` | The distance between baselines: ascender − descender + line gap. |
| `AdvanceWidthMax` | `short` | The largest advance width of any glyph. |
| `AdvanceHeightMax` | `short` | The largest advance height of any glyph. |

## Glyph

`public readonly struct Glyph`

A glyph of a `Font`, at that font's size.

| Member | Description |
| --- | --- |
| `GlyphMetrics` | The metrics of the glyph. |
| `BoundingBox(GlyphLayoutMode mode, Vector2 location, float dpi)` | The ink box of the glyph in pixels at `dpi`, placed at `location`. |

## GlyphMetrics

`public abstract class GlyphMetrics`

The metrics of one glyph. `TrueTypeGlyphMetrics` is the subclass for TrueType outlines.

| Property | Type | Description |
| --- | --- | --- |
| `GlyphId` | `ushort` | The index of the glyph in its font. |
| `CodePoint` | `CodePoint` | The code point that the glyph is for. |
| `GlyphType` | `GlyphType` | `Standard`, `Fallback` or `ColrLayer`. |
| `GlyphColor` | `GlyphColor?` | The color of a color layer, or `null`. |
| `UnitsPerEm` | `ushort` | The units per em of the font. |
| `ScaleFactor` | `Vector2` | The scale factor of the font, for each axis. |
| `AdvanceWidth` | `ushort` | How far the pen moves after the glyph in horizontal text. |
| `AdvanceHeight` | `ushort` | How far the pen moves after the glyph in vertical text. |
| `LeftSideBearing` | `short` | The space from the pen position to the left edge of the ink. |
| `RightSideBearing` | `short` | The space from the right edge of the ink to the end of the advance. |
| `TopSideBearing` | `short` | The space above the ink in vertical text. |
| `BottomSideBearing` | `short` | The space below the ink in vertical text. |
| `Width` | `float` | The width of the ink. |
| `Height` | `float` | The height of the ink. |
| `TextAttributes` | `TextAttributes` | The attributes that the metrics were made for. |
| `TextDecorations` | `TextDecorations` | The decorations that the metrics were made for. |
