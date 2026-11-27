---
title: Line breaking
description: Find the places where a line may end with the UAX #14 line breaking algorithm, ask about the seam between two runs, and tailor the rules for Thai, Lao, Khmer, Myanmar and Korean.
---

`TextLayout` wraps text for you when you set
[`WrappingLength`](../text/wrapping-and-alignment.md#wrap-lines). If you write your own layout, for
example in a PDF writer or a text editor, the namespace `PinataLabs.Typography.Unicode` gives you the
same line breaking rules. They follow [UAX #14](https://www.unicode.org/reports/tr14/) of the Unicode
version that `UnicodeInfo.Version` gives.

## Find the break opportunities in a text

`LineBreakEnumerator` returns each place in a `ReadOnlySpan<char>` where a line may end. It does not
allocate. Each `LineBreak` has three properties:

| Property | Description |
| --- | --- |
| `PositionWrap` | The index of the code point that starts the next line. |
| `PositionMeasure` | The index to measure the line to. This is `PositionWrap` without the spaces before it, and without a mandatory break such as CR LF. |
| `Required` | `true` for a mandatory break, such as after a line feed. |

```csharp
using PinataLabs.Typography.Unicode;

foreach (LineBreak lineBreak in new LineBreakEnumerator("Hello   World".AsSpan()))
{
    Console.WriteLine($"{lineBreak.PositionMeasure} {lineBreak.PositionWrap} {lineBreak.Required}");
}
// 5 8 False
// 13 13 False
```

A line that ends at the first opportunity holds `Hello`, and the next line starts at `World`. The
three spaces hang at the end of the first line and take no width.

The last opportunity is at the end of the text (rule LB3), so a text that is not empty gives at
least one.

## Convert a position to a char index

`PositionWrap` and `PositionMeasure` count code points, not `char` values. A character outside the
Basic Multilingual Plane, such as most emoji, is one code point and two `char` values. To slice a
`string`, convert the position:

```csharp
static int ToCharIndex(ReadOnlySpan<char> text, int codePointIndex)
{
    int charIndex = 0;
    int count = 0;
    foreach (CodePoint codePoint in text.EnumerateCodePoints())
    {
        if (count++ == codePointIndex)
        {
            break;
        }

        charIndex += codePoint.Utf16SequenceLength;
    }

    return charIndex;
}
```

In `"a😀 b"`, the break after the space has `PositionWrap` 3 and `PositionMeasure` 2, which are the
`char` indexes 4 and 3.

## Wrap a paragraph to a width

This greedy wrap takes the last opportunity that fits on each line. It measures each candidate
line with [`TextMeasurer`](../text/measuring-text.md):

```csharp
using PinataLabs.Typography;
using PinataLabs.Typography.Unicode;

static List<string> Wrap(string text, TextOptions options, float width)
{
    List<string> lines = [];
    int lineStart = 0;
    int lastFitWrap = -1;
    int lastFitMeasure = -1;

    foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan()))
    {
        int measure = ToCharIndex(text, lineBreak.PositionMeasure);
        int wrap = ToCharIndex(text, lineBreak.PositionWrap);
        float advance = TextMeasurer.MeasureAdvance(text.AsSpan(lineStart, measure - lineStart), options).Width;

        if (advance > width && lastFitWrap > lineStart)
        {
            lines.Add(text[lineStart..lastFitMeasure]);
            lineStart = lastFitWrap;
        }

        lastFitWrap = wrap;
        lastFitMeasure = measure;

        if (lineBreak.Required)
        {
            lines.Add(text[lineStart..measure]);
            lineStart = wrap;
        }
    }

    if (lineStart < text.Length)
    {
        lines.Add(text[lineStart..lastFitMeasure]);
    }

    return lines;
}
```

A word that is wider than `width` stays on its own line and overflows it, as with
`WordBreaking.Standard` in `TextLayout`. The sample does not keep a grapheme together. UAX #14 allows a break between a space and a combining mark after
it, and `TextLayout` moves such a break to the end of the grapheme. Use
[`EnumerateGraphemes`](./code-points-and-graphemes.md#enumerate-graphemes-and-code-points) to do the
same.

## Ask about the seam between two runs

A layout engine that holds text in runs, such as runs of different fonts or styles, needs to know
if a line may break where one run ends and the next one starts. `LineBreakAlgorithm.IsBreakAllowedBetween`
answers that. Pass the text before the seam and the text after it:

```csharp
bool afterSpace = LineBreakAlgorithm.IsBreakAllowedBetween("Hello ".AsSpan(), "World".AsSpan()); // true
bool beforeSpace = LineBreakAlgorithm.IsBreakAllowedBetween("Hello".AsSpan(), " World".AsSpan()); // false
bool inNumber = LineBreakAlgorithm.IsBreakAllowedBetween("44".AsSpan(), "-1".AsSpan()); // false
```

The method reads the two spans as one text. A rule that reads across the seam, such as rule LB25
for the number `44-1`, still applies. The answer is the one that `LineBreakEnumerator` gives at the
same place in the joined text.

- An empty `before` never breaks (rule LB2).
- An empty `after` always breaks (rule LB3).

Pass as much text on each side as can change the answer. A window of 32 `char` values on each side
is enough, unless a row of spaces, digits, combining marks or regional indicators that is longer than
that runs across the seam.

## Tailor the rules for a language

Both `LineBreakEnumerator` and `IsBreakAllowedBetween` take an optional `LineBreakTailoring`. The
default, `None`, is the algorithm as UAX #14 publishes it. The values are flags, so you can combine
them.

### Thai, Lao, Khmer and Myanmar

These scripts do not put spaces between words. UAX #14 leaves the breaks inside them to a
dictionary, and with no tailoring a run of these letters has no break opportunity.
`SouthEastAsianAsIdeographs` lets a line break between their letters as it does between ideographs.
A syllable stays whole:

- A vowel sign that follows its consonant stays with it.
- No break comes after a Thai or Lao vowel that is written before its consonant, such as U+0E40 THAI
  CHARACTER SARA E. A line can end before such a vowel.

```csharp
string thai = "ภาษาไทยง่าย";

foreach (LineBreak lineBreak in new LineBreakEnumerator(thai.AsSpan(), LineBreakTailoring.SouthEastAsianAsIdeographs))
{
    Console.Write($"{lineBreak.PositionWrap} ");
}
// 2 4 6 7 10 11
```

Without the tailoring, the same text gives one opportunity, at 11. The tailored breaks are not word
breaks. A line can end between two syllables of one word.

### Korean

By default, a line can break between any two Hangul syllables, as between ideographs.
`HangulAsAlphabetic` makes Hangul syllables and jamo break as letters do, so Korean breaks at its
spaces. The compatibility jamo (U+3131 to U+318E) and the halfwidth jamo (U+FFA0 to U+FFDC) are
included.

```csharp
string korean = "한국어 텍스트";

// None:               1 2 4 5 6 7
// HangulAsAlphabetic: 4 7
foreach (LineBreak lineBreak in new LineBreakEnumerator(korean.AsSpan(), LineBreakTailoring.HangulAsAlphabetic))
{
    Console.Write($"{lineBreak.PositionWrap} ");
}
```

`TextLayout` uses the rules with no tailoring. For Korean in `TextLayout`, use
[`WordBreaking.KeepAll`](../text/wrapping-and-alignment.md#break-long-words).

## See also

- [Unicode types](../api/unicode.md#linebreakenumerator-linebreak-and-linebreakalgorithm) for the
  API reference.
- [Bidirectional text and scripts](./bidi-and-scripts.md) to order the runs of each line.
