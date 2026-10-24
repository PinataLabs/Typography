---
title: International text
description: Lay out right-to-left and bidirectional text, Arabic, Indic, Hangul and other complex scripts, and Chinese, Japanese and Korean line breaking.
---

The library shapes text by the rules of each script. You give it Unicode text in logical order (the
order in which it is typed). The library finds the direction, picks the right glyph forms, and
returns the glyphs in visual order.

## Right-to-left and bidirectional text

The default `TextDirection.Auto` applies the
[Unicode Bidirectional Algorithm](https://www.unicode.org/reports/tr9/) (UAX #9). It finds the base
direction from the first strong character of the text, and puts runs of left-to-right and
right-to-left text in the right visual order. Brackets and numbers are handled by the algorithm's
rules.

```csharp
using PinataLabs.Typography;

TextOptions options = new(arabicFont)
{
    WrappingLength = 300,
    TextAlignment = TextAlignment.Start, // Right edge for right-to-left text.
};

FontRectangle size = TextMeasurer.MeasureSize("مرحبا بالعالم 2026", options);
```

:::warning[A fixed direction applies to every character]

`TextDirection.LeftToRight` and `TextDirection.RightToLeft` do not set only the base direction. They
turn off the bidirectional algorithm and lay out **every** character in that direction. With
`RightToLeft`, the English word `abc` comes out as `cba`. Explicit Unicode direction controls in the
text, such as U+202E RIGHT-TO-LEFT OVERRIDE, have no effect either.

Keep the default `Auto` for any text that can mix directions, such as user input, names and
numbers. Use a fixed direction only for text that you know is in one direction.

:::

Use `TextAlignment.Start` and `TextAlignment.End` rather than left and right. They follow the
direction of the text, so one set of options works for both directions. See
[Wrapping and alignment](./wrapping-and-alignment.md).

## Complex scripts

Some scripts need more than one glyph for each character, glyphs in a different order from the
characters, or marks stacked above and below. The library has a shaper for each family of scripts:

| Shaper | Scripts |
| --- | --- |
| Arabic | Arabic, Syriac, N'Ko, Mongolian, Phags-pa, Mandaic, Manichaean and Psalter Pahlavi: the scripts that join letters. Picks the initial, medial, final and isolated form of each letter. |
| Indic | Devanagari, Bengali, Gurmukhi, Gujarati, Oriya, Tamil, Telugu, Kannada, Malayalam and Khmer. Reorders vowel signs and forms conjuncts. |
| Hangul | Korean. Composes and decomposes Hangul syllables from jamo. |
| Universal | About 40 other complex scripts, such as Balinese, Batak, Javanese, Sinhala, Sundanese, Tai Tham and Tibetan, by the model of the Universal Shaping Engine. |
| Default | Latin, Greek, Cyrillic, Hebrew, Chinese, Japanese and all other scripts. |

The library picks the shaper from the script of each run of text. You do not choose it.

Shaping uses the OpenType tables in the font. A font must support a script for the text to look
right: a Latin-only font cannot show Devanagari, even with a correct shaper. Use
[fallback fonts](./fallback-fonts.md) to cover more scripts.

## Line breaking

Lines break by the rules of [UAX #14](https://www.unicode.org/reports/tr14/), for every script. That
includes Chinese and Japanese, where a line can break between most characters, and the rules that
stop punctuation from starting a line.

`WordBreaking.KeepAll` keeps Chinese, Japanese and Korean words together, which is what Korean
usually wants. `WordBreaking.BreakAll` lets lines break inside words of other scripts. See
[Wrapping and alignment](./wrapping-and-alignment.md#break-long-words).

## Justification for East Asian text

`TextJustification.InterCharacter` adds space between characters, not between words. Use it for
Chinese and Japanese, which do not put spaces between words.

## Vertical text

Chinese, Japanese and Korean text can be set in vertical columns. See
[Vertical text](./vertical-text.md).
