---
title: Measuring text
description: Measure the size, advance and bounds of text, count its lines, and get the position of each glyph with TextMeasurer.
---

`TextMeasurer` lays out text with the `TextOptions` that you give it and returns measurements. It
does not draw anything. All results are in pixels at `TextOptions.Dpi`, which is 72 by default. At
72 DPI one point is one pixel.

## Size, advance and bounds

There are three ways to measure a whole string. They answer different questions.

```csharp
using PinataLabs.Typography;

TextOptions options = new(font); // Open Sans, 24pt

FontRectangle size    = TextMeasurer.MeasureSize("Hello, world!", options);
FontRectangle advance = TextMeasurer.MeasureAdvance("Hello, world!", options);
FontRectangle bounds  = TextMeasurer.MeasureBounds("Hello, world!", options);

// size    = [ X=0,       Y=0,       Width=139,       Height=25 ]
// advance = [ X=0,       Y=0,       Width=141,       Height=24 ]
// bounds  = [ X=2.34375, Y=3.07617, Width=135.96094, Height=21.32813 ]
```

| Method | Question it answers | Position | Rounded |
| --- | --- | --- | --- |
| `MeasureAdvance` | How much space does the text take in a line of type? Use it to place the next piece of text, or to size a text box. | Always `(0, 0)`. | Up, to whole pixels. |
| `MeasureSize` | How big a bitmap do I need to draw the text at the origin? It goes from the origin to the far edge of the ink. | Always `(0, 0)`. | Up, to whole pixels. |
| `MeasureBounds` | Where exactly is the ink? | The top-left corner of the ink, relative to `(0, 0)`, including `Origin` and alignment. | No. |

The advance includes the space that the font puts at each side of a glyph, and the full line height.
The bounds include only the ink, so they are smaller. An empty string measures as
`FontRectangle.Empty`.

## Count lines

`CountLines` gives the number of lines after the text is wrapped and split at line breaks:

```csharp
int lines = TextMeasurer.CountLines("one\ntwo\r\nthree", options); // 3

options.WrappingLength = 200;
int wrapped = TextMeasurer.CountLines(longText, options);
```

## Measure each glyph

The `TryMeasureCharacter...` methods give one `GlyphBounds` entry for each glyph in the laid-out
text. Each entry has the `Codepoint` that it came from, and a `Bounds` rectangle.

```csharp
if (TextMeasurer.TryMeasureCharacterBounds("Hi!", options, out ReadOnlySpan<GlyphBounds> glyphs))
{
    foreach (GlyphBounds glyph in glyphs)
    {
        Console.WriteLine($"{glyph.Codepoint}: {glyph.Bounds}");
    }
}
```

| Method | Gives for each glyph | Position |
| --- | --- | --- |
| `TryMeasureCharacterAdvances` | The advance width and the line height. | `(0, 0)` |
| `TryMeasureCharacterSizes` | The size from the glyph's own origin to the edge of its ink. | `(0, 0)` |
| `TryMeasureCharacterBounds` | The ink box, at its position in the layout. | The real position. |

Use `TryMeasureCharacterBounds` for hit testing, caret placement and selection highlights.

The methods return `false`, and an empty span, when the text has no glyphs.

:::note[Entries are glyphs, not characters]

There is one entry for each glyph that shaping produces. That is often one for each character, but
not always. A ligature such as *fi* in many fonts is one glyph for two characters, so
`"fi"` can give one entry. Use the `Codepoint` of each entry to map it back to the text.

:::

## Measure with the same options that you render with

Every option that changes the layout changes the measurement: the font, DPI, wrapping length, line
spacing, alignment, layout mode, kerning, feature tags, text runs and fallback fonts. Measure with the
same `TextOptions` object, or a copy of it, that you use to draw. To change one value without
changing the original, copy it:

```csharp
TextOptions copy = new(options) { WrappingLength = 400 };
```

## Measure spans

Every method also takes a `ReadOnlySpan<char>`, so you can measure part of a larger buffer without
making a new string:

```csharp
ReadOnlySpan<char> word = buffer.AsSpan(start, length);
FontRectangle wordSize = TextMeasurer.MeasureSize(word, options);
```
