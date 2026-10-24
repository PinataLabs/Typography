---
title: OpenType features
description: Turn on OpenType layout features such as small capitals, old-style figures, fractions and stylistic sets, and control kerning.
---

An OpenType font can have optional typographic features in its GSUB (glyph substitution) and GPOS
(glyph positioning) tables. Each feature has a four-letter tag, such as `smcp` for small capitals.

## Features that are always on

The library applies the features that correct text needs, for every run of text:

| Tag | Feature |
| --- | --- |
| `rvrn` | Required variation alternates |
| `ltra`, `ltrm`, `rtla`, `rtlm` | Left-to-right and right-to-left alternates and mirrored forms |
| `ccmp` | Glyph composition and decomposition |
| `locl` | Localized forms |
| `rlig` | Required ligatures |
| `mark`, `mkmk` | Mark positioning (accents) |
| `calt` | Contextual alternates |
| `clig` | Contextual ligatures |
| `liga` | Standard ligatures, such as *fi* and *fl* |
| `rclt` | Required contextual alternates |
| `curs` | Cursive positioning |
| `kern` | Kerning |

In vertical text, `vert` replaces the horizontal-only features `calt`, `clig`, `liga`, `rclt`,
`curs` and `kern`. The [complex-script shapers](./international-text.md#complex-scripts) add the
features their scripts need, for example `init`, `medi` and `fina` for Arabic.

A feature has an effect only if the font has it. Most fonts have only some of them.

## Turn on more features

Add tags to `TextOptions.FeatureTags`. The `FeatureTags` enumeration names the registered features,
and `Tag.Parse` makes a tag from its four letters:

```csharp
using PinataLabs.Typography;
using PinataLabs.Typography.Tables.AdvancedTypographic;

TextOptions options = new(font)
{
    FeatureTags =
    [
        FeatureTags.SmallCapitals,   // smcp
        FeatureTags.OldstyleFigures, // onum
        Tag.Parse("ss01"),           // Stylistic set 1
    ],
};
```

`FeatureTags` values convert to `Tag` without a cast. Use `Tag.Parse` for tags that the enumeration
does not have, such as the stylistic sets `ss01` to `ss20` and the character variants `cv01` to
`cv99`.

The features apply to the whole text. To apply a feature to only part of the text, measure or render
that part with its own `TextOptions`.

Some features that are often useful:

| `FeatureTags` value | Tag | Effect |
| --- | --- | --- |
| `SmallCapitals` | `smcp` | Lower-case letters as small capitals. |
| `SmallCapitalsFromCapitals` | `c2sc` | Capital letters as small capitals. |
| `OldstyleFigures` | `onum` | Figures that rise and fall like lower-case letters. |
| `LiningFigures` | `lnum` | Figures that are all the height of capitals. |
| `TabularFigures` | `tnum` | Figures that are all the same width, for tables of numbers. |
| `ProportionalFigures` | `pnum` | Figures with their natural widths. |
| `SlashedZero` | `zero` | A zero with a slash, to tell it from the letter O. |
| `Fractions` | `frac` | Fractions such as 1/2 set as one fraction glyph or a built-up fraction. |
| `Ordinals` | `ordn` | Ordinal forms, such as the *o* in 1º. |
| `DiscretionaryLigatures` | `dlig` | Decorative ligatures, such as *ct* and *st*. |
| `HistoricalLigatures` | `hlig` | Historical ligatures. |
| `Swash` | `swsh` | Swash forms. |
| `CaseSensitiveForms` | `case` | Punctuation moved up to fit capital letters. |

The full list of registered tags is in the
[OpenType feature registry](https://learn.microsoft.com/typography/opentype/spec/featurelist).

### Fractions

When you turn on `frac`, the library finds the digits on each side of a slash (`/`) or a fraction
slash (U+2044). It applies the numerator (`numr`) feature to the digits before the slash and the
denominator (`dnom`) feature to the digits after it. Other digits are not changed.

```csharp
options.FeatureTags = [FeatureTags.Fractions];
TextMeasurer.MeasureSize("Add 3/4 cup of flour", options);
```

### You cannot turn off a feature that is always on

`FeatureTags` only adds features. There is no way to turn off a feature from the table at the top of
this page, such as `liga`. Kerning is the exception: use `KerningMode`.

## Kerning

Kerning adjusts the space between pairs of glyphs, for example to move `A` and `V` closer together.
The library uses the GPOS `kern` feature. If the font has no GPOS kerning, the library uses the
older `kern` table.

| `KerningMode` | Effect |
| --- | --- |
| `Standard` (default) | Apply kerning from the font. |
| `Auto` | Apply kerning when the layout engine chooses. At present this has the same effect as `Standard`. |
| `None` | Do not apply kerning. |

```csharp
options.KerningMode = KerningMode.None;
```
