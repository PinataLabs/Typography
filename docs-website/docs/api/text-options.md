---
title: TextOptions and TextRun
description: API reference for TextOptions, the layout settings for measuring and rendering, and TextRun, which styles part of the text.
---

Namespace: `PinataLabs.Typography`. Guides: [Wrapping and alignment](../text/wrapping-and-alignment.md),
[Rich text](../text/rich-text.md).

## TextOptions

`public class TextOptions`

The settings that `TextMeasurer` and `TextRenderer` use to lay out text. A `TextOptions` object is
mutable. Do not change one while another thread uses it.

### Constructors

| Constructor | Description |
| --- | --- |
| `TextOptions(Font font)` | Options with `font` and the default value of every other property. |
| `TextOptions(TextOptions options)` | A copy of `options`. The lists in it are copied too. |

### Properties

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Font` | `Font` | (required) | The font of the text. Must not be `null`. |
| `FallbackFontFamilies` | `IReadOnlyList<FontFamily>` | empty | Families to take glyphs from when the font has none. See [Fallback fonts](../text/fallback-fonts.md). |
| `Dpi` | `float` | `72` | The resolution, in dots per inch. Must not be negative. At 72, one point is one pixel. |
| `Origin` | `Vector2` | `(0, 0)` | The point that the text box is placed at. |
| `WrappingLength` | `float` | `-1` | The width of the text box in pixels (the height, for vertical text). Lines wrap at this length. `-1` turns wrapping off. |
| `WordBreaking` | `WordBreaking` | `Standard` | How to break words that do not fit. |
| `LineSpacing` | `float` | `1` | A multiple of the font's line height. Must not be `0`. |
| `TabWidth` | `float` | `-1` | The width of a tab, as a number of spaces. `-1` uses the font's tab glyph. Has an effect only when the font has a glyph for U+0009. |
| `TextDirection` | `TextDirection` | `Auto` | The base direction of the text, as CSS `direction`. `Auto` finds it from the first strong character. |
| `TextAlignment` | `TextAlignment` | `Start` | How each line is placed in the text box. |
| `TextJustification` | `TextJustification` | `None` | How lines are stretched to the width of the box. Needs `WrappingLength`. |
| `HorizontalAlignment` | `HorizontalAlignment` | `Left` | Which part of the text box is at `Origin`, horizontally. |
| `VerticalAlignment` | `VerticalAlignment` | `Top` | Which part of the text box is at `Origin`, vertically. |
| `LayoutMode` | `LayoutMode` | `HorizontalTopBottom` | Horizontal lines or vertical columns. See [Vertical text](../text/vertical-text.md). |
| `KerningMode` | `KerningMode` | `Standard` | Whether to apply kerning. |
| `HintingMode` | `HintingMode` | `None` | Whether to apply TrueType hinting. See [Hinting](../rendering/hinting.md). |
| `ColorFontSupport` | `ColorFontSupport` | `MicrosoftColrFormat` | Whether to use color glyphs. See [Color fonts](../rendering/color-fonts.md). |
| `FeatureTags` | `IReadOnlyList<Tag>` | empty | More OpenType features to apply. See [OpenType features](../text/opentype-features.md). |
| `TextRuns` | `IReadOnlyList<TextRun>` | empty | Runs that style parts of the text. |

## TextRun

`public class TextRun`

A part of the text with its own font, decorations or attributes. You can subclass it to carry your
own data to a renderer.

| Member | Description |
| --- | --- |
| `TextRun()` | Makes an empty run. |
| `Start` | The index of the first grapheme of the run. Inclusive. |
| `End` | The index after the last grapheme of the run. Exclusive. |
| `Font` | The font of the run. `null` means `TextOptions.Font`. |
| `TextAttributes` | `None`, `Subscript` or `Superscript`. |
| `TextDecorations` | `None`, or any combination of `Underline`, `Strikeout` and `Overline`. |
| `Slice(ReadOnlySpan<char> text)` | The part of `text` that the run covers. |

`Start` and `End` count graphemes, not `char` values. Text outside every run uses
`TextOptions.Font`.

The library does not change your `TextRun` objects, so you can reuse them.
It clamps `Start` and `End` to the grapheme count and drops runs that are empty after clamping.
When runs overlap or nest, the run that is later in the list wins for the overlapping part, and the
earlier run continues after it. The order of `Start` values does not matter.
