---
title: Enumerations
description: API reference for the option enumerations of PinataLabs.Typography, and for Tag and FeatureTags.
---

Namespace: `PinataLabs.Typography`, unless stated otherwise. The first value in each table is the
default of the matching `TextOptions` property, unless stated otherwise.

## Layout

### LayoutMode

| Value | Description |
| --- | --- |
| `HorizontalTopBottom` | Horizontal lines, from top to bottom. |
| `HorizontalBottomTop` | Horizontal lines, from bottom to top. |
| `VerticalLeftRight` | Vertical columns, from left to right. All glyphs upright. |
| `VerticalRightLeft` | Vertical columns, from right to left. All glyphs upright. |
| `VerticalMixedLeftRight` | Vertical columns, from left to right. Horizontal glyphs turned 90° clockwise. |
| `VerticalMixedRightLeft` | Vertical columns, from right to left. Horizontal glyphs turned 90° clockwise. |

`LayoutModeExtensions` adds `IsHorizontal()`, `IsVertical()` and `IsVerticalMixed()`.

### TextDirection

Default: `Auto`.

| Value | Description |
| --- | --- |
| `Auto` | Find the base direction from the first strong character, as HTML `dir="auto"` does. |
| `LeftToRight` | Use a left-to-right base direction, as CSS `direction: ltr` does. |
| `RightToLeft` | Use a right-to-left base direction, as CSS `direction: rtl` does. |

All three apply the Unicode Bidirectional Algorithm, so each run of text keeps its own direction.

### TextAlignment

| Value | Description |
| --- | --- |
| `Start` | At the start of the line: left for left-to-right text, right for right-to-left text. |
| `End` | At the end of the line. |
| `Center` | In the centre of the line. |

### TextJustification

| Value | Description |
| --- | --- |
| `None` | Do not justify. |
| `InterWord` | Add space between words. |
| `InterCharacter` | Add space between characters. |

### HorizontalAlignment

| Value | Description |
| --- | --- |
| `Left` | The left edge of the text box is at the origin. |
| `Right` | The right edge of the text box is at the origin. |
| `Center` | The centre of the text box is at the origin. |

### VerticalAlignment

| Value | Description |
| --- | --- |
| `Top` | The top edge of the text box is at the origin. |
| `Center` | The centre of the text box is at the origin. |
| `Bottom` | The bottom edge of the text box is at the origin. |

### WordBreaking

| Value | Description |
| --- | --- |
| `Standard` | Break only where the line-breaking rules allow. |
| `BreakAll` | Break between any two characters, except in Chinese, Japanese and Korean text. |
| `KeepAll` | Do not break inside Chinese, Japanese and Korean words. |
| `BreakWord` | Use `Standard`, then break words that still do not fit. |

## Glyphs and fonts

### KerningMode

| Value | Description |
| --- | --- |
| `Standard` | Apply kerning. |
| `None` | Do not apply kerning. |
| `Auto` | Apply kerning when the layout engine chooses. At present the same as `Standard`. |

### HintingMode

| Value | Description |
| --- | --- |
| `None` | Do not hint. |
| `Standard` | Apply TrueType hinting. |

### ColorFontSupport

Default: `MicrosoftColrFormat`.

| Value | Description |
| --- | --- |
| `None` | Do not use color glyphs. |
| `MicrosoftColrFormat` | Use COLR and CPAL color glyphs. |

### TextAttributes

Default for a `TextRun`: `None`.

| Value | Description |
| --- | --- |
| `None` | Normal text. |
| `Subscript` | Smaller, below the baseline. |
| `Superscript` | Smaller, above the baseline. |

### TextDecorations

A flags enumeration. Default for a `TextRun`: `None`.

| Value | Description |
| --- | --- |
| `None` | No lines. |
| `Underline` | A line under the text. |
| `Strikeout` | A line through the text. |
| `Overline` | A line over the text. |

### GlyphType

| Value | Description |
| --- | --- |
| `Fallback` | No font had a glyph for the code point. This is the missing-glyph box. |
| `Standard` | An ordinary glyph. |
| `ColrLayer` | One layer of a color glyph. |

### GlyphLayoutMode

| Value | Description |
| --- | --- |
| `Horizontal` | The glyph is in horizontal text. |
| `Vertical` | The glyph is upright in vertical text. |
| `VerticalRotated` | The glyph is turned 90° clockwise in vertical text. |

## OpenType tags

Namespace: `PinataLabs.Typography.Tables.AdvancedTypographic`.

### Tag

`public readonly struct Tag : IEquatable<Tag>`

A four-letter OpenType tag.

| Member | Description |
| --- | --- |
| `Tag(uint value)` | A tag from its 32-bit value. |
| `static Parse(string value)` | A tag from its four letters, for example `Tag.Parse("ss01")`. |
| `Value` | The 32-bit value. |
| `ToString()` | The four letters. |
| implicit conversion from `FeatureTags` | Use a `FeatureTags` value where a `Tag` is needed. |
| implicit conversion from `uint` | Use a 32-bit value where a `Tag` is needed. |

### FeatureTags

`public enum FeatureTags : uint`

The registered OpenType layout features, by name, for example `SmallCapitals` (`smcp`),
`OldstyleFigures` (`onum`), `TabularFigures` (`tnum`), `Fractions` (`frac`),
`DiscretionaryLigatures` (`dlig`) and `Kerning` (`kern`). Each value is the 32-bit value of its tag.
See [OpenType features](../text/opentype-features.md) for the common ones, and the
[OpenType feature registry](https://learn.microsoft.com/typography/opentype/spec/featurelist) for
what each feature does.

### GlyphClassDef

The glyph classes of the GDEF table: `BaseGlyph`, `LigatureGlyph`, `MarkGlyph` and
`ComponentGlyph`.
