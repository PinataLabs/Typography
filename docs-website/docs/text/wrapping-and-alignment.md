---
title: Wrapping and alignment
description: Wrap text at a width, break long words, align and justify lines, place the text box around the origin, and set line spacing and tab width.
---

`TextOptions` controls how text is broken into lines and where the lines go. This page covers the
options for paragraphs of horizontal text. The same options work for
[vertical text](./vertical-text.md), with the axes swapped.

## Wrap lines

Set `WrappingLength` to the width of the text box, in pixels. The library breaks lines at the
places that [UAX #14](https://www.unicode.org/reports/tr14/) allows, for example after a space or a
hyphen. The default is `-1`, which turns wrapping off: then the text breaks only at line breaks in
the text itself (`\n`, `\r\n`, and the other Unicode line separators).

```csharp
using PinataLabs.Typography;

TextOptions options = new(font)
{
    WrappingLength = 300,
};
```

## Break long words

A word that is longer than the line does not fit. `WordBreaking` says what to do:

| Value | Behaviour |
| --- | --- |
| `Standard` (default) | Break only where the line-breaking rules allow. A long word overflows the line. |
| `BreakAll` | Break between any two characters to prevent overflow. Chinese, Japanese and Korean text uses its normal rules. |
| `KeepAll` | Do not break inside Chinese, Japanese and Korean words. Other text breaks as for `Standard`. |
| `BreakWord` | Break as for `Standard`. If a word still does not fit, break it between characters. |

```csharp
options.WordBreaking = WordBreaking.BreakWord; // Good for URLs and long identifiers.
```

These values follow the CSS `word-break` and `overflow-wrap` properties.

## Align lines in the box

`TextAlignment` places each line inside the text box. The box is `WrappingLength` wide, or as wide as
the longest line when wrapping is off.

| Value | Left-to-right text | Right-to-left text |
| --- | --- | --- |
| `Start` (default) | Left | Right |
| `End` | Right | Left |
| `Center` | Centre | Centre |

`Start` and `End` follow the direction of the text, so the same options work for English and Arabic.

## Justify lines

`TextJustification` stretches lines to fill the full width of the box. It needs a `WrappingLength`.

| Value | Behaviour |
| --- | --- |
| `None` (default) | No justification. |
| `InterWord` | Add space between words. Best for languages that put spaces between words, such as English. |
| `InterCharacter` | Add space between characters. Best for languages such as Japanese. |

```csharp
TextOptions justified = new(font)
{
    WrappingLength = 300,
    TextJustification = TextJustification.InterWord,
};
```

The last line of the text is not stretched. A line that is already as wide as the box, or wider,
is not changed.

## Place the box around the origin

`Origin` is the point where the layout is anchored. `HorizontalAlignment` and `VerticalAlignment`
say which part of the text box is at the origin:

| Option | Value | The origin is at the box's |
| --- | --- | --- |
| `HorizontalAlignment` | `Left` (default) | left edge |
| | `Center` | horizontal centre |
| | `Right` | right edge |
| `VerticalAlignment` | `Top` (default) | top edge |
| | `Center` | vertical centre |
| | `Bottom` | bottom edge |

This centres a label on a point:

```csharp
using System.Numerics;

TextOptions label = new(font)
{
    Origin = new Vector2(200, 100),
    HorizontalAlignment = HorizontalAlignment.Center,
    VerticalAlignment = VerticalAlignment.Center,
};
```

`TextAlignment` and `HorizontalAlignment` are different. `TextAlignment` places lines *inside* the
box. `HorizontalAlignment` places the *box* relative to the origin. To centre a wrapped paragraph on
a point, set both to their centre values.

## Line spacing

`LineSpacing` multiplies the line height from the font. The default is `1`. It must not be `0`.

```csharp
options.LineSpacing = 1.5F; // One and a half times the font's line height.
```

## Tabs

`TabWidth` sets the width of a tab character, as a number of spaces (U+0020) in the current font.
The default, `-1`, uses the advance of the font's tab glyph.

```csharp
options.TabWidth = 4;
```

:::caution[Most fonts have no tab glyph]

`TabWidth` takes effect only when the font maps the tab character (U+0009) to a glyph. Most fonts,
including Open Sans, DejaVu Sans and Segoe UI, do not. With those fonts a tab gets the advance of
the font's missing-glyph box, whatever `TabWidth` is. If you need exact tab stops, split the text at
the tabs and place each part yourself.

:::

## Text direction

`TextDirection` sets the base direction of the text, as the CSS `direction` property does. The
default, `Auto`, finds it from the first strong character. Mixed English and Arabic text comes out
in the right order with all three values. `TextAlignment.Start` and `TextAlignment.End` follow the
base direction. See
[International text](./international-text.md#right-to-left-and-bidirectional-text).
