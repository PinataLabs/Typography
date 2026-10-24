---
title: Font and glyph metrics
description: Read the design metrics of a font and of single glyphs, such as units per em, ascender, descender, underline position, advance widths and side bearings.
---

Most applications only need [`TextMeasurer`](../text/measuring-text.md). When you write your own
layout code, or need the raw numbers from the font, read the metrics directly.

## Font metrics

`Font.FontMetrics` gives the metrics of the font's face. All values are in **font units**. To convert
a value to pixels, use this rule:

```text
pixels = value × pointSize × DPI / (72 × UnitsPerEm)
```

```csharp
using PinataLabs.Typography;

FontMetrics metrics = font.FontMetrics;
HorizontalMetrics h = metrics.HorizontalMetrics;

Console.WriteLine($"Units per em: {metrics.UnitsPerEm}"); // 2048 for Open Sans
Console.WriteLine($"Ascender:     {h.Ascender}");          // Above the baseline: positive.
Console.WriteLine($"Descender:    {h.Descender}");         // Below the baseline: negative.
Console.WriteLine($"Line gap:     {h.LineGap}");
Console.WriteLine($"Line height:  {h.LineHeight}");

float ascenderPx = h.Ascender * font.Size * 72F / (72F * metrics.UnitsPerEm); // At 72 DPI.
```

| Property | Meaning |
| --- | --- |
| `UnitsPerEm` | The size of the design grid. |
| `ScaleFactor` | `UnitsPerEm × 72`. Divide a value in font units by this, then multiply by point size × DPI, to get pixels. |
| `HorizontalMetrics` | Ascender, descender, line gap, line height and maximum advances for horizontal text. |
| `VerticalMetrics` | The same values for vertical text. |
| `UnderlinePosition`, `UnderlineThickness` | Where and how thick to draw an underline. |
| `StrikeoutPosition`, `StrikeoutSize` | Where and how thick to draw a strike-out line. |
| `SubscriptXOffset`, `SubscriptYOffset`, `SubscriptXSize`, `SubscriptYSize` | How to place and scale subscript text. |
| `SuperscriptXOffset`, `SuperscriptYOffset`, `SuperscriptXSize`, `SuperscriptYSize` | How to place and scale superscript text. |
| `ItalicAngle` | The slant of the italic style, in degrees. Zero for upright fonts. |
| `Description` | The [font description](./font-descriptions.md). |

You can also read the metrics of a style without making a `Font`:

```csharp
if (family.TryGetMetrics(FontStyle.Bold, out FontMetrics? bold))
{
    Console.WriteLine(bold.Description.FontNameInvariantCulture);
}
```

## Glyph metrics

`Font.TryGetGlyphs` gives the glyphs for one code point. Most code points have one glyph. A color
emoji can have more than one, one for each color layer.

```csharp
using PinataLabs.Typography.Unicode;

if (font.TryGetGlyphs(new CodePoint('A'), out IReadOnlyList<Glyph>? glyphs))
{
    GlyphMetrics g = glyphs[0].GlyphMetrics;
    Console.WriteLine($"Glyph id:          {g.GlyphId}");
    Console.WriteLine($"Advance width:     {g.AdvanceWidth}");   // Font units.
    Console.WriteLine($"Left side bearing: {g.LeftSideBearing}");
    Console.WriteLine($"Width x height:    {g.Width} x {g.Height}");
}
```

`TryGetGlyphs` returns `false` only when it cannot make a glyph. When the font has no glyph for the
code point, you get the font's fallback glyph (glyph 0, often an empty box) with `GlyphType.Fallback`.
Check `GlyphType` to find characters that the font cannot show.

`Glyph.BoundingBox(mode, location, dpi)` gives the ink box of the glyph in pixels, placed at
`location`.

## Kerning

`Font.TryGetKerningOffset` gives the kerning adjustment between two glyphs from the font's `kern`
table:

```csharp
font.TryGetGlyphs(new CodePoint('A'), out IReadOnlyList<Glyph>? a);
font.TryGetGlyphs(new CodePoint('V'), out IReadOnlyList<Glyph>? v);

if (font.TryGetKerningOffset(a![0], v![0], 72, out System.Numerics.Vector2 offset))
{
    Console.WriteLine($"Kerning between A and V: {offset.X}px");
}
```

You do not need this when you use `TextMeasurer` or `TextRenderer`: they apply kerning for you, from
the `kern` table and from the GPOS table. See [OpenType features](../text/opentype-features.md).
