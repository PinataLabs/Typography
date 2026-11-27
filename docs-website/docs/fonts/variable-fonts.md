---
title: Variable fonts
description: Draw a variable font at any weight, width or other axis value, use its named instances, and see what the library varies.
---

A variable font holds a range of designs in one file. Each design axis, such as weight (`wght`) or
width (`wdth`), has a minimum, a default and a maximum value. A point on the axes is a *variation
instance*. The library draws a variable font at any instance: the outlines, the advances, the font
metrics, the hinting and the GPOS positions all follow the axis values.

The library reads TrueType variable fonts (`glyf` and `gvar`) and CFF2 variable fonts, in `.ttf`,
`.otf`, `.woff` and `.woff2` files.

## Load a variable font

Add a variable font as you add any other font. The font is one face, and that face covers the range
of its axes:

```csharp
using PinataLabs.Typography;

FontCollection fonts = new();
FontFamily teko = fonts.Add("fonts/Teko-Variable.ttf");

FontStyleRange range = teko.Faces[0].Description.StyleRange;
Console.WriteLine($"{range.MinWeight.Value} to {range.MaxWeight.Value}"); // 300 to 700
```

The family name is the typographic family name, `name` ID 16. `Teko-Variable.ttf` is the family
*Teko*, not *Teko Light*.

## Pick an instance with a style

A `FontStyle` sets the axes of a variable face. You use the same styles as for a family of static
faces:

```csharp
Font regular = teko.CreateFont(12);                  // wght 400
Font bold = teko.CreateFont(12, FontStyle.Bold);     // wght 700
Font semiBold = teko.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.SemiBold)); // wght 600
Font weight450 = teko.CreateFont(12, FontStyle.Regular.WithWeight(new FontWeight(450)));
```

The style and the size set these axes, when the face has them:

| Axis | Value |
| --- | --- |
| `wght` | The weight of the style. |
| `wdth` | The width of the style, as a percentage. |
| `ital` | 1 for an italic style, otherwise 0. |
| `slnt` | The negated angle of an oblique style. An italic style on a face with `slnt` and no `ital` axis gives -14. Otherwise 0. |
| `opsz` | The size of the font in points. |

Every other axis is at its default value. The library clamps each value to the range of its axis.

A regular style is weight 400, which is not always the default instance. The default instance of
Teko is weight 300. To draw the default instance of Teko, ask for `FontWeight.Light`.

The style also picks the face, as [Font matching](./font-matching.md) describes. A family can have
an upright variable face and an italic variable face. An italic style picks the italic face, then
sets its axes.

## Set the axes yourself

A `FontVariation` is an axis tag and a value in the units of the axis. Pass variations after the
style:

```csharp
using PinataLabs.Typography.Tables.AdvancedTypographic;

Font font = teko.CreateFont(
    12,
    FontStyle.Regular,
    new FontVariation(Tag.Parse("wght"), 650));
```

The variations win over the values that the style and the size give. The rules are those of the CSS
`font-variation-settings` property:

- The font ignores a variation for an axis that its face does not have.
- The font clamps a value to the range of its axis.
- When an axis is given more than once, the last value wins.
- A value that is not a number gives the default value of the axis.
- A static font ignores all variations.

The `Font` constructor takes the same parameters as `CreateFont`. `Font.WithVariations` makes a font
of the same family, size and style with other variations. The new variations replace the old ones:

```csharp
Font heavier = font.WithVariations(new FontVariation(Tag.Parse("wght"), 690));
```

`WithSize` and `WithStyle` keep the variations of the font. `Font.Variations` lists the variations
that you asked for. An axis that you gave more than once is in the list once, with its last value.

`SystemFonts.CreateFont` does not take variations. Get the family first, then call `CreateFont`:

```csharp
if (SystemFonts.TryGet("Bahnschrift", out FontFamily bahnschrift))
{
    Font narrow = bahnschrift.CreateFont(12, FontStyle.Regular, new FontVariation(Tag.Parse("wdth"), 80));
}
```

## Read the axes of a font

`FontMetrics.VariationAxes` lists the axes of a face, in the order of its `fvar` table:

```csharp
using System.Globalization;

foreach (FontVariationAxis axis in teko.Faces[0].VariationAxes)
{
    Console.WriteLine(
        $"{axis.Tag} {axis.Name(CultureInfo.InvariantCulture)}: " +
        $"{axis.MinValue} to {axis.MaxValue}, default {axis.DefaultValue}");
}
// wght Weight: 300 to 700, default 300
```

`IsHidden` is `true` when the font asks applications not to show the axis to users. The list is
empty for a static font.

## Use a named instance

A variable font can name some of its instances, such as *Light*, *Regular* and *SemiBold*.
`FontMetrics.NamedInstances` lists them. Each `FontNamedInstance` has a `Name(culture)` and the
`Variations` that give its value on every axis. To draw a named instance, pass its variations:

```csharp
FontNamedInstance semiBoldInstance = teko.Faces[0].NamedInstances
    .First(instance => instance.Name(CultureInfo.InvariantCulture) == "SemiBold");

Font font = teko.CreateFont(12, FontStyle.Regular, semiBoldInstance.Variations.ToArray());
Console.WriteLine(font.Name); // Teko SemiBold
```

When the font draws a named instance, `Font.Name` is the family name and the name of the instance.
For any other instance, `Font.Name` is the full name of the face.

## See which instance a font draws

`Font.FontMetrics` of a font on a variable face is the variation instance. Its `Variations` give the
value of each axis, after the library clamps them:

```csharp
Font font = teko.CreateFont(12, FontStyle.Regular, new FontVariation(Tag.Parse("wght"), 5000));
Console.WriteLine(string.Join(", ", font.FontMetrics.Variations)); // wght=700
```

For the default instance, `Font.FontMetrics` is the face itself, the same object as in
`FontFamily.Faces`.

Fonts that ask for the same instance share one `FontMetrics` and its glyph cache while any of them is
alive. Two requests are the same instance when their normalised coordinates are equal. The library
frees an instance when no font uses it. So you can make a new font for each draw without parsing
glyphs again, and you can animate an axis without a growing cache.

## What changes with the axis values

| What | Tables | Notes |
| --- | --- | --- |
| Outlines of TrueType glyphs | `gvar` | The deltas move the points of each glyph and the offsets of the components of a composite glyph. The bounds come from the varied points. |
| Outlines of CFF2 glyphs | `CFF2` | The `blend` operator adds the deltas of the instance. |
| Advances and side bearings | `HVAR`, `VVAR`, `gvar` | `HVAR` and `VVAR` give the advance when the font has them. Otherwise the `gvar` phantom points give it. |
| Font metrics | `MVAR` | The ascender, descender and line gap, the underline and strike-out lines, and the subscript and superscript sizes and offsets. |
| TrueType hinting | `cvar` | Each instance runs the hinting instructions with its own control values. A font with no `cvar` table uses the control values of the face. |
| Kerning and mark positions | GDEF, GPOS | GPOS value records and anchors add the deltas of their `VariationIndex` tables. |
| Lookups | GSUB, GPOS `FeatureVariations` | An instance uses the feature tables of the first record whose conditions hold for its coordinates. This includes `rvrn`. |

The axis values also apply to the text that the font measures, renders and shapes:

- [`TextMeasurer`](../text/measuring-text.md) and [`TextRenderer`](../rendering/glyph-renderers.md)
  use the varied advances and outlines.
- `TextShaper.Shape` returns the varied advances and offsets in font units. See
  [TextShaper](../api/text-shaper.md).
- A [fallback font](../text/fallback-fonts.md) takes the size, the style and the variations of the
  font of the text. It ignores a variation for an axis that it does not have.

## Glyph renderers

`GlyphRendererParameters.FontMetrics` is the variation instance. Two instances of one face give
different parameters. A renderer that caches outlines by `GlyphRendererParameters` keeps a separate
entry for each instance, so it does not draw a glyph of one weight in the place of another.

## Malformed variation data

The library loads a variable font with bad variation data and draws what it can:

- A font with a malformed `fvar` table loads as a static font with no axes.
- A malformed `avar`, `HVAR`, `VVAR`, `MVAR`, `gvar` or `cvar` table counts as absent.
- A glyph with malformed `gvar` data draws at the default instance.

Nothing throws when a glyph is drawn.

## What is not supported

- The library does not make bold or oblique glyphs. A style outside the range of the axes draws the
  nearest instance.
- The library reads only the version 1 segment maps of an `avar` version 2 table.
- The library does not read `STAT`. The style range of a face comes from `fvar` and `OS/2`.
- Color glyphs do not vary. The library reads the COLR layers of version 0, which have no variation data.
- The library does not hint CFF2 outlines. See [Hinting](../rendering/hinting.md).
- GPOS `Device` tables that give deltas for pixel sizes stay ignored. Only `VariationIndex` tables apply.
- `FontProgram` draws a TrueType variable font at its default instance, and does not load CFF2 fonts.
