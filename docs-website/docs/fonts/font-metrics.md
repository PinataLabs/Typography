---
title: Font and glyph metrics
description: Read the design metrics of a font and of single glyphs, such as units per em, ascender, descender, underline position, advance widths and side bearings, and the PDF font descriptor values and embedding permissions of a font.
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
FontMetrics bold = family.MatchFace(FontStyle.Bold);
Console.WriteLine(bold.Description.FontNameInvariantCulture);
```

`MatchFace` returns the face, not an instance. For a [variable font](./variable-fonts.md) the face
has the metrics of its default instance. To read the metrics of the bold instance, make the font:
`family.CreateFont(12, FontStyle.Bold).FontMetrics`.

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

## Font descriptor metrics

`FontMetrics.DescriptorMetrics` gives the values that a PDF `/FontDescriptor` is written from. The values are
in font units, and each value has a `MetricSource` that names the table field, or the fallback, that gave it:

```csharp
FontDescriptorMetrics descriptor = font.FontMetrics.DescriptorMetrics;

Console.WriteLine($"Ascent:    {descriptor.Ascent} from {descriptor.AscentSource}");       // 2189 from Os2TypoAscender
Console.WriteLine($"Descent:   {descriptor.Descent} from {descriptor.DescentSource}");     // -600 from Os2TypoDescender
Console.WriteLine($"CapHeight: {descriptor.CapHeight} from {descriptor.CapHeightSource}"); // 1462 from Os2CapHeight
Console.WriteLine($"XHeight:   {descriptor.XHeight} from {descriptor.XHeightSource}");     // 1096 from Os2XHeight
Console.WriteLine($"Flags:     {(int)descriptor.Flags}");                                  // 32 (Nonsymbolic)
```

The comments show the values for Open Sans Regular.

| Value | Where it comes from, in order |
| --- | --- |
| `Ascent`, `Descent` | The same values as `HorizontalMetrics`, so a PDF line matches a laid-out line. When the font states no ascent and descent, `head` `yMax` and `yMin`. |
| `CapHeight` | `OS/2` `sCapHeight` (version 2 or later, when it is not 0), then the top of the glyph for `H`, then the ascent. |
| `XHeight` | `OS/2` `sxHeight` (version 2 or later, when it is not 0), then the top of the glyph for `x`, then 0. |
| `ItalicAngle` | `post` `italicAngle`. |
| `BoundingBox` | `head` `xMin`, `yMin`, `xMax` and `yMax`, as a `FontBoundingBox`. |

The descent is negative. For a variable font, `Ascent`, `Descent`, `CapHeight` and `XHeight` include the `MVAR`
deltas of the instance. `ItalicAngle` and `BoundingBox` are those of the default design.

`Flags` is a `FontDescriptorFlags` value, at the bit positions of ISO 32000-1, Table 123. Write it as the
`/Flags` entry of the descriptor. The library sets a flag only when a table of the font states it:

| Flag | Set when |
| --- | --- |
| `FixedPitch` | `post` `isFixedPitch` is not 0. |
| `Serif` | `OS/2` `sFamilyClass` names a serif class, or it is 0 and the PANOSE values give a serif style. |
| `Script` | `sFamilyClass` is class 10 (Scripts), or it is 0 and the PANOSE family kind is Latin Script. |
| `Italic` | `fsSelection` has the italic or oblique bit, `head` `macStyle` has the italic bit, or `post` `italicAngle` is not 0. |
| `Symbolic` | The `cmap` table has a (3, 0) Microsoft Symbol subtable. |
| `Nonsymbolic` | The `cmap` table has no (3, 0) subtable. A font has exactly one of `Symbolic` and `Nonsymbolic`. |
| `AllCap`, `SmallCap`, `ForceBold` | Never. No table of a font states them. |

## Embedding permissions

`FontMetrics.EmbeddingPermissions` gives the permissions of the `fsType` field of the `OS/2` table:

```csharp
FontEmbeddingPermissions permissions = font.FontMetrics.EmbeddingPermissions;

Console.WriteLine(permissions.Usage);               // Installable
Console.WriteLine(permissions.NoSubsetting);        // False
Console.WriteLine(permissions.BitmapEmbeddingOnly); // False
```

| Member | Description |
| --- | --- |
| `Usage` | A `FontEmbeddingUsage`: `Installable`, `Restricted`, `PreviewAndPrint` or `Editable`. When the font sets more than one of bits 1 to 3, the least restrictive one applies. |
| `NoSubsetting` | `true` when the font must not be subset before you embed it (bit 8). |
| `BitmapEmbeddingOnly` | `true` when you can embed only the bitmaps of the font (bit 9). |
| `FsType` | The `fsType` field as the font stores it. |
| `IsStated` | `false` when the font has no `OS/2` table. Then `Usage` is `Installable` and the other values are `false`. |

The library does not act on the permissions. Your application decides what to do, for example before it
[subsets a font](./subsetting.md#check-the-embedding-permissions-first).

## Fonts without an OS/2 table

Some fonts have no `OS/2` table, such as AppleGothic and AppleMyungjo on macOS. These fonts load, and the
library uses the values that HarfBuzz 15.0.0 uses in their place:

- The ascender, descender and line gap come from the `hhea` table.
- `head` `macStyle` gives the weight (400, or 700 for bold), the width (100%, or 75% for condensed and 125% for
  expanded) and the slope.
- The subscript and superscript sizes are 10/12 em, with an x offset of 0 and a y offset of 1/5 em.
- The strike-out line is 1/18 em thick, at half the ascender.

Such a font has no `fsType`, so `EmbeddingPermissions.IsStated` is `false`. It has no `sCapHeight` and no
`sxHeight`, so `DescriptorMetrics` takes the cap height and the x-height from the fallbacks in the table of
[font descriptor metrics](#font-descriptor-metrics).
