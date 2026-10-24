---
title: Vertical text
description: Lay out text in vertical columns for Chinese, Japanese and Korean, with upright or rotated Latin text, using LayoutMode.
---

`TextOptions.LayoutMode` sets the direction of the lines. Horizontal text is the default. The
vertical modes lay out text from top to bottom in columns, as Chinese, Japanese and Korean text is
often set.

## Layout modes

| `LayoutMode` | Lines | Glyphs |
| --- | --- | --- |
| `HorizontalTopBottom` (default) | Horizontal lines, stacked from top to bottom. | Upright. |
| `HorizontalBottomTop` | Horizontal lines, stacked from bottom to top. | Upright. |
| `VerticalLeftRight` | Vertical columns, from left to right. | Upright. |
| `VerticalRightLeft` | Vertical columns, from right to left. | Upright. |
| `VerticalMixedLeftRight` | Vertical columns, from left to right. | East Asian glyphs upright. Latin and other horizontal glyphs turned 90° clockwise. |
| `VerticalMixedRightLeft` | Vertical columns, from right to left. | As for `VerticalMixedLeftRight`. |

Traditional Chinese and Japanese books use columns from right to left. Use
`VerticalMixedRightLeft` for that, so that Latin words and numbers in the text lie on their side as
they do in print.

```csharp
using PinataLabs.Typography;

TextOptions options = new(japaneseFont)
{
    LayoutMode = LayoutMode.VerticalMixedRightLeft,
    WrappingLength = 400, // The height of a column.
};

FontRectangle size = TextMeasurer.MeasureSize("縦書きのテキストと English の混在", options);
```

The library decides which glyphs stay upright from the Unicode vertical orientation property
([UAX #50](https://www.unicode.org/reports/tr50/)). You can read that property with
`CodePoint.GetVerticalOrientationType`.

## How the options change in vertical text

The options keep their meaning, but along the other axis:

- `WrappingLength` is the **height** of a column.
- `TextAlignment` places each column from the top (`Start`), the bottom (`End`) or the centre.
- `LineSpacing` sets the distance between columns.
- `MeasureAdvance` gives the column height in the vertical direction.

## Vertical glyph forms

In vertical text the library applies the font's `vert` feature. It replaces glyphs with their
vertical forms, for example brackets and the long vowel mark `ー` turned for vertical text.
Horizontal-only features such as ligatures and `kern` are not applied to vertical glyphs. The
font's vertical kerning feature (`vkrn`) is not applied by default either. Add it to
[`FeatureTags`](./opentype-features.md#turn-on-more-features) if you want it.

The font must have vertical metrics (`vhea` and `vmtx` tables) for good results. Fonts made for
Chinese, Japanese and Korean have them. For other fonts the library makes vertical metrics from the
horizontal ones.

## Render vertical text

The `GlyphRendererParameters.LayoutMode` of each glyph tells you how it was placed:

| `GlyphLayoutMode` | Meaning |
| --- | --- |
| `Horizontal` | The glyph is in horizontal text. |
| `Vertical` | The glyph is upright in vertical text. |
| `VerticalRotated` | The glyph is turned 90° clockwise in vertical text. |

The outline points that your renderer gets are already turned. You do not need to rotate them
yourself.
