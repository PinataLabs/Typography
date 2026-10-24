---
title: Rich text
description: Use TextRun to change the font, add underline, strike-out and overline, and set subscript and superscript for parts of one string.
---

A `TextRun` applies a font, decorations or attributes to part of a string. You put the runs in
`TextOptions.TextRuns`. The library lays out the whole string as one piece of text, so the styled
parts wrap, align and measure together with the rest.

## Make a run

```csharp
using PinataLabs.Typography;

const string text = "Hello bold world";

TextOptions options = new(regular)
{
    TextRuns =
    [
        new TextRun { Start = 6, End = 10, Font = bold },
    ],
};

FontRectangle size = TextMeasurer.MeasureSize(text, options);
```

| Property | Meaning |
| --- | --- |
| `Start` | The index of the first grapheme in the run. Inclusive. |
| `End` | The index after the last grapheme in the run. Exclusive. |
| `Font` | The font of the run. When it is `null`, the run uses `TextOptions.Font`. |
| `TextDecorations` | Lines to draw with the run: `Underline`, `Strikeout`, `Overline`, or a combination. |
| `TextAttributes` | `Subscript` or `Superscript`. |

Text that is not in a run uses `TextOptions.Font`. If two runs overlap, the later run wins for the
overlapping part.

:::warning[Indexes count graphemes]

`Start` and `End` count **graphemes** (what a reader sees as one character), not `char` values. For
text with only plain Latin letters the two are the same. For text with combining accents, emoji or
characters outside the Basic Multilingual Plane, they are different. Use the
[Unicode helpers](../unicode/code-points-and-graphemes.md) to find grapheme indexes.

:::

```csharp
using PinataLabs.Typography.Unicode;

string text = "Café 👍🏽 ok";
int graphemes = text.GetGraphemeCount(); // 9, but text.Length is 12.
```

`TextRun.Slice(text)` returns the part of the text that a run covers. It is useful to check your
indexes.

## Decorations

Decorations are lines that go with the text. The library computes where they go from the font's
underline and strike-out metrics, and sends them to your renderer:

```csharp
TextOptions options = new(font)
{
    TextRuns =
    [
        new TextRun { Start = 0, End = 5, TextDecorations = TextDecorations.Underline },
        new TextRun { Start = 6, End = 11, TextDecorations = TextDecorations.Strikeout | TextDecorations.Overline },
    ],
};
```

Decorations do not change the layout or the measurements. To draw them, your `IGlyphRenderer` must
ask for them in `EnabledDecorations`, and draw the lines that it gets in `SetDecoration`. See
[Glyph renderers](../rendering/glyph-renderers.md#decorations).

## Subscript and superscript

`TextAttributes.Subscript` and `TextAttributes.Superscript` make the run smaller and move it below
or above the baseline. The library uses the font's subscript and superscript metrics for the size and
the position, so the result matches the design of the font.

```csharp
const string formula = "H2O and E=mc2";

TextOptions options = new(font)
{
    TextRuns =
    [
        new TextRun { Start = 1, End = 2, TextAttributes = TextAttributes.Subscript },
        new TextRun { Start = 12, End = 13, TextAttributes = TextAttributes.Superscript },
    ],
};
```

## Mix fonts from different families

A run can use a font from any family, at any size. The line height grows to fit the largest font in
the line.

```csharp
Font heading = headingFamily.CreateFont(32);
Font body = bodyFamily.CreateFont(14);

string text = "Chapter 1 It was a dark and stormy night.";

TextOptions options = new(body)
{
    WrappingLength = 400,
    TextRuns = [new TextRun { Start = 0, End = 9, Font = heading }],
};
```

## Subclass TextRun

`TextRun` is not sealed. You can add your own properties, for example a color or a link target, and
read them back in your renderer from `GlyphRendererParameters.TextRun`:

```csharp
sealed class ColorRun : TextRun
{
    public string Color { get; init; } = "black";
}

// In your IGlyphRenderer:
public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
{
    string color = parameters.TextRun is ColorRun run ? run.Color : "black";
    // ...
    return true;
}
```
