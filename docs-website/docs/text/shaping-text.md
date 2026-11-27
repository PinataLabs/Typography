---
title: Shape text
description: Shape runs of text into glyph IDs, clusters, advances and offsets with TextShaper, for output that you write yourself, such as PDF.
---

`TextRenderer` and `TextMeasurer` shape, lay out and draw text in one call. Some programs need only
the glyphs. A PDF writer, for example, writes glyph IDs to a content stream, and needs to know which
characters each glyph stands for. `TextShaper.Shape` gives you that. It takes the same shaping steps
as the layout, and it does not break lines, measure or render.

## Shape one run

A run is text of one script and one direction, in one font. Pass the font, the text and the options
of the run:

```csharp
using PinataLabs.Typography;

ShapedGlyph[] glyphs = TextShaper.Shape(font, "office", new ShapingOptions
{
    Direction = TextDirection.LeftToRight,
});
```

Each `ShapedGlyph` has a `GlyphId`, a `Cluster`, and an `XAdvance`, an `XOffset` and a `YOffset` in
font design units. The `Cluster` is the `char` index in the text of the first character that the
glyph stands for. In a font with an `ffi` ligature, the ligature is one glyph at the cluster 1, and
the glyph after it has the cluster 4. The size of the font does not change the result. See
[TextShaper](../api/text-shaper.md#shapedglyph) for each member.

## Place the glyphs

The glyphs come in visual order, from left to right, also for a right-to-left run. To convert a
value to points, divide it by `FontMetrics.UnitsPerEm` and multiply it by the font size. The pen
moves by `XAdvance` after each glyph. The glyph is drawn `XOffset` to the right of the pen and
`YOffset` above the baseline.

```csharp
float scale = font.Size / font.FontMetrics.UnitsPerEm;
float penX = 0;

foreach (ShapedGlyph glyph in glyphs)
{
    float x = penX + (glyph.XOffset * scale);
    float y = glyph.YOffset * scale; // Up from the baseline.

    // Write glyph.GlyphId at (x, y).
    penX += glyph.XAdvance * scale;
}
```

## Shape text in more than one script or direction

`TextShaper.Shape` does not run the bidi algorithm and does not split the text into runs. For text
such as `"Hello مرحبا"`, cut the text into runs with
[`TextItemizer`](../unicode/bidi-and-scripts.md#itemize-text-into-runs-for-a-shaper) and shape each
run on its own. Set `Script` and `Direction` from the run.

```csharp
using PinataLabs.Typography;
using PinataLabs.Typography.Unicode;

string text = "Hello مرحبا";
float scale = font.Size / font.FontMetrics.UnitsPerEm;
float penX = 0;

foreach (ItemizedRun run in TextItemizer.Itemize(text, BidiParagraphDirection.Auto))
{
    ShapingOptions options = new()
    {
        Script = run.Script,
        Direction = run.IsRightToLeft ? TextDirection.RightToLeft : TextDirection.LeftToRight,
    };

    foreach (ShapedGlyph glyph in TextShaper.Shape(font, text.AsSpan(run.Start, run.Length), options))
    {
        int charIndex = run.Start + glyph.Cluster; // The cluster counts from the start of the run.
        float x = penX + (glyph.XOffset * scale);
        float y = glyph.YOffset * scale;

        // Write glyph.GlyphId at (x, y).
        penX += glyph.XAdvance * scale;
    }
}
```

The itemizer gives the runs in visual order, and the shaper gives the glyphs of each run in visual
order, so one pen moves across the whole line.

For text that wraps, break each paragraph into lines first, then itemize each line. See
[Line breaking](../unicode/line-breaking.md) and
[Order the runs of a line](../unicode/bidi-and-scripts.md#order-the-runs-of-a-line).

### Set the script of a run

A lookup in the font matches only the glyphs of one script run, as in HarfBuzz. With `Script` left
`null`, the shaper finds the script of each character, and a character of no script of its own, such
as a space or a digit, joins the script next to it. A ligature or a kerning pair across a change of
script then does not apply. For example, a `kern` pair between a kanji and a kana does not apply,
because Han and Hiragana are different scripts. Set `Script` to make the whole text one run of that
script.

### Set the direction of a run

`Direction` defaults to `Auto`, which takes the direction of the first strong character anywhere in
the run. A line feed does not end the search, because the shaper treats the run as one paragraph. In
`"\nשלום"`, the Hebrew letters make the whole run right to left. A run with no strong character, such
as digits, takes the direction of `Script`, and runs from left to right without one.

The characters of a run all take one direction. Do not pass a Latin run as `RightToLeft`. HarfBuzz
reverses such a run, and a ligature such as `fi` does not match. Pass the direction that the bidi
algorithm gives the run.

## Turn features on or off

`Features` takes a list of `ShapingFeature` values. A feature that the list does not name is applied
as the shaper of the script plans it.

```csharp
using PinataLabs.Typography.Tables.AdvancedTypographic;

ShapingOptions options = new()
{
    Language = Tag.Parse("TRK "),
    Features =
    [
        new ShapingFeature(Tag.Parse("liga"), false),
        new ShapingFeature(Tag.Parse("smcp"), true),
    ],
};
```

`Language` picks the OpenType language system of the font, here Turkish. A font with no language
system for the tag uses its default language system. Turning off `kern` also turns off the `kern`
table of the font.

## Differences from the layout

- `TextShaper` uses one font. It does not use [fallback fonts](./fallback-fonts.md). A character
  that the font does not have gives glyph 0.
- It does not draw color glyphs as layers. A color glyph is one glyph.
- A variable font shapes with the variations that the `Font` was created with.
