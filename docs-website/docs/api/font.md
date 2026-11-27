---
title: Font
description: API reference for Font, FontStyle, FontStyleRange, and the variation types FontVariation, FontVariationAxis and FontNamedInstance.
---

Namespace: `PinataLabs.Typography`. Guides: [Loading fonts](../fonts/loading-fonts.md#create-a-font),
[Font matching](../fonts/font-matching.md), [Variable fonts](../fonts/variable-fonts.md).

## Font

`public sealed class Font`

A font family at one size and one style, and for a variable font at one set of axis values. Make one with `FontFamily.CreateFont`,
`SystemFonts.CreateFont`, or one of the constructors.

### Constructors

| Constructor | Description |
| --- | --- |
| `Font(FontFamily family, float size, FontStyle style = default, params ReadOnlySpan<FontVariation> variations)` | A font of `family` in `style` at `size` points. The default style is regular. `variations` set the axes of a variable font, and win over the values that the style and the size give. A static font ignores them. |

### Properties

| Property | Description |
| --- | --- |
| `Family` | The family of the font. |
| `Name` | The full name of the font face that is used, for example *Open Sans Bold*. For a named instance of a variable font, the family name and the name of the instance, for example *Teko SemiBold*. |
| `Size` | The size in points. |
| `Style` | The style that was asked for. |
| `Variations` | The variations that were asked for. An axis that was given more than once is in the list once, with its last value. |
| `FontMetrics` | The [metrics](./metrics.md#fontmetrics) of the face that is used. For a variable face, the variation instance. |

The font uses the face of the family that best matches `Style`, as `FontFamily.MatchFace` picks it.
When the family does not have the requested style, the face is the nearest one in the order that CSS
Fonts Level 4 gives. `Name` and `FontMetrics` describe the face that is really used. Compare `Style`
with `FontMetrics.Description.StyleRange` to see whether the face has the style.

When the face is a variable font, the style sets the `wght`, `wdth`, `ital` and `slnt` axes, and the size
sets `opsz`. `Variations` then win, axis by axis. Fonts whose axis values give the same instance share one
`FontMetrics`. For the default instance, `FontMetrics` is the face itself. See
[Variable fonts](../fonts/variable-fonts.md#pick-an-instance-with-a-style).

### Methods

| Method | Description |
| --- | --- |
| `WithSize(float size)` | A font of the same family, style and variations at `size` points. |
| `WithStyle(FontStyle style)` | A font of the same family, size and variations in `style`. |
| `WithVariations(params ReadOnlySpan<FontVariation> variations)` | A font of the same family, size and style with `variations` in place of its variations. |
| `TryGetGlyphs(CodePoint codePoint, out IReadOnlyList<Glyph>? glyphs)` | Gets the glyphs for a code point. A color glyph can have more than one. A code point that the font does not have gives its missing glyph, with `GlyphType.Fallback`. |
| `TryGetGlyphs(CodePoint, ColorFontSupport, out …)` | As above, with or without color layers. |
| `TryGetGlyphs(CodePoint, TextAttributes, ColorFontSupport, out …)` | As above, for subscript or superscript. |
| `TryGetGlyphs(CodePoint, TextAttributes, LayoutMode, ColorFontSupport, out …)` | As above, for a layout mode. |
| `TryGetGlyphs(CodePoint, TextAttributes, TextDecorations, LayoutMode, ColorFontSupport, out …)` | As above, with decorations. |
| `TryGetKerningOffset(Glyph previous, Glyph current, float dpi, out Vector2 vector)` | Gets the kerning between two glyphs from the font's `kern` table, in pixels at `dpi`. |
| `TryGetPath(out string? path)` | Gets the file that the font was loaded from. Returns `false` for a font loaded from a stream. |

The overload of `TryGetGlyphs` without `ColorFontSupport` does not return color layers.

## FontStyle

`public readonly struct FontStyle : IEquatable<FontStyle>`

The style that a caller asks for: a weight, a width and a slope, as CSS gives them. The default value is
`FontStyle.Regular`.

| Member | Description |
| --- | --- |
| `FontStyle(FontWeight weight, FontWidth width, FontSlope slope)` | A style from its three parts. |
| `Weight`, `Width`, `Slope` | The parts. |
| `WithWeight(FontWeight)`, `WithWidth(FontWidth)`, `WithSlope(FontSlope)` | A style with one part changed. |
| `static Regular` | Weight 400, width 100%, normal slope. |
| `static Bold` | Weight 700, width 100%, normal slope. |
| `static Italic` | Weight 400, width 100%, italic slope. |
| `static BoldItalic` | Weight 700, width 100%, italic slope. |

## FontWeight, FontWidth and FontSlope

`public readonly struct FontWeight : IEquatable<FontWeight>, IComparable<FontWeight>`
`public readonly struct FontWidth : IEquatable<FontWidth>, IComparable<FontWidth>`
`public readonly struct FontSlope : IEquatable<FontSlope>`

`FontWeight` is a number from 1 to 1000, as CSS `font-weight`. `new FontWeight(float value)` makes one, and
`Value` gives the number. It has the keywords `Thin` (100), `ExtraLight`,
`Light`, `Normal` (400), `Medium`, `SemiBold`, `Bold` (700), `ExtraBold` and `Black` (900).

`FontWidth` is a percentage from 50 to 200, as CSS `font-stretch`. `new FontWidth(float percent)` makes one,
and `Percent` gives the percentage. It has the keywords `UltraCondensed` (50%),
`ExtraCondensed`, `Condensed` (75%), `SemiCondensed`, `Normal` (100%), `SemiExpanded`, `Expanded` (125%),
`ExtraExpanded` and `UltraExpanded` (200%).

`FontSlope` is `Normal`, `Italic` or `Oblique(float degrees = 14)`, as CSS `font-style`. `Kind` is the
[`FontSlopeKind`](./enumerations.md#fontslopekind) and `ObliqueAngle` is the angle in degrees that the glyphs
lean to the right, from -90 to 90. `ObliqueAngle` is 0 for a slope that is not oblique.

A constructor, and `Oblique`, clamp a value to its range, and throw `ArgumentOutOfRangeException` for a value
that is not a number. The default value of each type is its normal value. `FontWeight` and `FontWidth` compare
with `CompareTo`.

## FontStyleRange

`public sealed class FontStyleRange`

The styles that a face covers, from `FontDescription.StyleRange`. `MinWeight`, `MaxWeight`, `MinWidth` and
`MaxWidth` give the ranges, `HasNormal`, `HasItalic` and `HasOblique` say which slopes the face has,
`MinObliqueAngle` and `MaxObliqueAngle` give the oblique angles, and `Contains(FontStyle style)` says whether the
face covers a style. A static face covers one point. The weight comes from `OS/2.usWeightClass`, the width from
`usWidthClass`, the slope from `fsSelection` and `post.italicAngle`.

A variable face covers the weights of its `wght` axis and the widths of its `wdth` axis. An `ital` axis that
reaches 0 adds the upright slope, and one that reaches 1 adds italic. A `slnt` axis adds the oblique angles from
its negated maximum to its negated minimum. A part that has no axis comes from the `OS/2` table, as for a static
face.

## FontVariation

`public readonly struct FontVariation : IEquatable<FontVariation>`

A value for one axis of a variable font. Guide: [Variable fonts](../fonts/variable-fonts.md#set-the-axes-yourself).

| Member | Description |
| --- | --- |
| `FontVariation(Tag axis, float value)` | A value for the axis `axis`, such as `Tag.Parse("wght")`, in the user coordinates of the axis: 650 for `wght` is a weight of 650. A value that is not a number gives the default value of the axis. |
| `Axis` | The tag of the axis. |
| `Value` | The value in the user coordinates of the axis. |
| `ToString()` | The tag and the value, for example `wght=650`. |

`Tag` is in the namespace `PinataLabs.Typography.Tables.AdvancedTypographic`. See [Tag](./enumerations.md#tag).

A font ignores a variation for an axis that its face does not have. It clamps a value to the range of the axis.
When an axis is given more than once, the last value wins. These are the rules of the CSS
`font-variation-settings` property.

## FontVariationAxis

`public sealed class FontVariationAxis`

An axis of a variable font, as its `fvar` table lists it. Get the axes from `FontMetrics.VariationAxes`.

| Member | Description |
| --- | --- |
| `Tag` | The tag of the axis, such as `wght`. |
| `MinValue` | The smallest value of the axis, in user coordinates. |
| `DefaultValue` | The value of the axis in the default instance. |
| `MaxValue` | The largest value of the axis. |
| `IsHidden` | `true` when the font asks applications not to show the axis to users. The `fvar` flag `HIDDEN_AXIS` sets it. |
| `Name(CultureInfo culture)` | The name of the axis in `culture`, such as *Weight*. An empty string when the font has no such name. |

## FontNamedInstance

`public sealed class FontNamedInstance`

An instance that a variable font names in its `fvar` table, such as *SemiBold*. Get them from
`FontMetrics.NamedInstances`. To draw one, pass its `Variations` to `FontFamily.CreateFont` or the `Font`
constructor.

| Member | Description |
| --- | --- |
| `Name(CultureInfo culture)` | The subfamily name of the instance in `culture`, such as *SemiBold*. An empty string when the font has no such name. |
| `Variations` | The value of the instance on every axis, in the order of `FontMetrics.VariationAxes`, in user coordinates. |

## Variation members of FontMetrics

[`FontMetrics`](./metrics.md#fontmetrics) has three members for variable fonts. All three lists are empty for a
static font, and for a font whose `fvar` table is malformed, which loads as a static font.

| Property | Type | Description |
| --- | --- | --- |
| `VariationAxes` | `IReadOnlyList<FontVariationAxis>` | The axes of the font, in the order of its `fvar` table. |
| `NamedInstances` | `IReadOnlyList<FontNamedInstance>` | The instances that the font names. |
| `Variations` | `IReadOnlyList<FontVariation>` | The value of each axis in this instance, in the order of `VariationAxes`, after clamping. For a face, the default value of each axis. |
