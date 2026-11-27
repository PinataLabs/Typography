---
title: TextShaper
description: API reference for TextShaper, ShapingOptions, ShapingFeature and ShapedGlyph.
---

Namespace: `PinataLabs.Typography`. Guides: [Shape text](../text/shaping-text.md) and [OpenType features](../text/opentype-features.md).

## TextShaper

`public static class TextShaper`

Shapes one run of text with the GSUB and GPOS tables of a font and returns its glyphs. It does not measure, render or
break lines, so a caller that writes its own output, such as a PDF writer, can use the glyphs and the characters that
they stand for.

| Method | Returns |
| --- | --- |
| `Shape(font, text, options = null)` | The `ShapedGlyph[]` of the run in visual order. An empty text gives an empty array. `font` cannot be `null`, and the `Direction` of `options` must be a value of `TextDirection`, or the call throws `ArgumentNullException` or `ArgumentOutOfRangeException`. |

The method takes the steps that `TextRenderer` and `TextMeasurer` take for a text run: the glyphs of the characters,
normalization, GSUB, GPOS, the `kern` table and the placement of marks. The layout and the shaper give the same glyphs for
the same text. The size of the font does not matter, because the result is in font design units. A variable font
shapes with the variations that it was created with.

```csharp
using PinataLabs.Typography;

ShapedGlyph[] glyphs = TextShaper.Shape(font, "office", new ShapingOptions { Direction = TextDirection.LeftToRight });

float em = font.FontMetrics.UnitsPerEm;
foreach (ShapedGlyph glyph in glyphs)
{
    // glyph.Cluster is 1 for the ligature of "ffi", and the next glyph has the cluster 4.
    float advance = glyph.XAdvance / em * font.Size;
}
```

## ShapedGlyph

`public readonly struct ShapedGlyph : IEquatable<ShapedGlyph>`

| Member | Description |
| --- | --- |
| `GlyphId` | The ID of the glyph in the font. |
| `Cluster` | The UTF-16 index in the text of the first character that the glyph stands for. |
| `XAdvance` | The distance from the pen to the pen of the next glyph, in font design units. It can be negative, and it is not limited to 16 bits: a font of 16384 units to the em can give a glyph an advance above 65535. A glyph that the font classes as a mark has none. |
| `XOffset` | How far to the right of the pen the glyph is drawn, in font design units. It is not limited to 16 bits. |
| `YOffset` | How far above the baseline the glyph is drawn, in font design units. |

The advance and the offsets are whole numbers, as in HarfBuzz. The place of a glyph is the sum of the advances before it
plus its `XOffset`. Divide a value by `FontMetrics.UnitsPerEm` and multiply it by the font size for a length in points.

### Clusters

The clusters are those that HarfBuzz gives at its default cluster level. A glyph stands for the characters from its cluster
up to the cluster of the next glyph in the text.

- A ligature is one glyph at the cluster of its first character. The `ffi` of "office" is one glyph at the cluster 1, and the
  glyph after it has the cluster 4. A ligature of lam and alef is one glyph that covers both characters.
- A mark, a zero width joiner, an emoji modifier, a tag character, the second regional indicator of a pair, and an emoji
  after a joiner share the cluster of the character before them.
- A decomposition keeps the cluster of its character, so the base and the mark of an e with an acute have one cluster.
- A reordering, such as a vowel sign that goes in front of its consonant, gives the glyphs that it moves over the lowest
  cluster among them.

The clusters never decrease from the first character of the text to the last. A run from right to left has its glyphs from
left to right, so the first glyph has the highest cluster.

## ShapingOptions

`public sealed class ShapingOptions`

| Property | Description |
| --- | --- |
| `Script` | The `ScriptClass` of the run, or `null`. The shaper and the lookups of the font follow it, as they follow the script of a HarfBuzz buffer. When it is `null`, each character has the script that Unicode gives it, and the characters of no script of their own join the script next to them. A lookup of the font matches only the glyphs of one script run, as in HarfBuzz, so a ligature or a `kern` pair across a change of script, such as between Han and Hiragana, does not apply. Set `Script` to shape the text as one run. `Common`, `Inherited` and `Unknown` count as `null`. |
| `Language` | The `Tag` of the OpenType language system, such as `Tag.Parse("TRK ")`, or `null`. The lookups of that language system apply, and those of the default language system when the font has none with the tag. |
| `Direction` | The `TextDirection` of every character of the run. The run is not split by the Unicode Bidirectional Algorithm, so shape the runs of a text of both directions one at a time. `Auto`, the default, takes the direction of the first character that has one, anywhere in the run: a paragraph separator, such as a line feed, does not end the search, because the run is one paragraph. A run with none, such as digits and brackets, takes the direction of `Script`, as HarfBuzz does for a buffer, and runs from left to right without a script. A value that `TextDirection` does not name makes `Shape` throw `ArgumentOutOfRangeException`. |
| `Features` | A list of `ShapingFeature`, to turn features on or off. A feature that the list does not name is as the shaper of the script plans it. It is empty by default, and setting it to `null` throws `ArgumentNullException`. |

The characters of the run take the shaper in the order of the text, in both directions. HarfBuzz shapes a run in a direction
that is not the one of its script, such as Latin from right to left, in the native direction and reverses it, so a
ligature that matches "fi" does not match there. Pass the direction that follows the script, as the bidi algorithm gives it.

## ShapingFeature

`public readonly struct ShapingFeature : IEquatable<ShapingFeature>`

| Member | Description |
| --- | --- |
| `ShapingFeature(Tag tag, bool enabled)` | Makes a value, such as `new(Tag.Parse("liga"), false)`. |
| `Tag` | The tag of the feature. |
| `Enabled` | `true` to turn the feature on, `false` to turn it off. |

A feature applies to the whole run. When a list names a feature more than once, the last entry counts. Turning off
`kern` also turns off the `kern` table of the font. A feature that is off counts as absent, as in HarfBuzz: with `mark`
off, a Hebrew letter and its point compose to the presentation form of the font, and with `isol`, `fina`, `medi` or `init`
off, an Arabic font that has no joining forms of its own keeps the letters as they are, where it would otherwise take the
presentation forms. A language whose language system has no `kern` feature in GPOS reads the `kern` table of the font.
