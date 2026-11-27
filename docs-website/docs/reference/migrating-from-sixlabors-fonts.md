---
title: Migrating from SixLabors.Fonts
description: Move code from SixLabors.Fonts to PinataLabs.Typography. Most types and members keep their names; the namespaces, targets, font styles and some metrics change.
---

PinataLabs.Typography is a fork of SixLabors.Fonts, taken at commit `32bef429`. Most type and member
names are the same, so most code needs only new `using` directives. Font styles and a few glyph
metrics changed shape. [Font styles and variable fonts](#font-styles-and-variable-fonts) and
[Changed members](#changed-members) list them.

## What changes

| | SixLabors.Fonts | PinataLabs.Typography |
| --- | --- | --- |
| Package and assembly | `SixLabors.Fonts` | `PinataLabs.Typography` |
| Root namespace | `SixLabors.Fonts` | `PinataLabs.Typography` |
| Other namespaces | `SixLabors.Fonts.X` | `PinataLabs.Typography.X` |
| Target frameworks | `netcoreapp3.1`, `netstandard2.0`, `netstandard2.1` and others | `netstandard2.1`, `net8.0`, `net10.0`, `net11.0` |
| Package dependencies | `System.Memory` and others on older targets | `PinataLabs.Typography.Unicode` at the same version, and `System.Runtime.CompilerServices.Unsafe` on `netstandard2.1` |
| Strong name | Strong-named | Not strong-named |
| AppContext switch to stop native font enumeration on macOS | `Switch.SixLabors.Fonts.DoNotUseNativeSystemFontsEnumeration` | `Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration` |

## Steps

1. Remove the `SixLabors.Fonts` package and add `PinataLabs.Typography`. See
   [Installation](../installation.md).
2. Replace the namespaces. Every namespace maps one to one:

   ```csharp
   // Before
   using SixLabors.Fonts;
   using SixLabors.Fonts.Unicode;
   using SixLabors.Fonts.Tables.AdvancedTypographic;

   // After
   using PinataLabs.Typography;
   using PinataLabs.Typography.Unicode;
   using PinataLabs.Typography.Tables.AdvancedTypographic;
   ```

3. If you set the AppContext switch for macOS font enumeration, rename it. The old name has no
   effect.
4. Make sure that your project targets .NET Standard 2.1, .NET 8 or later. `netstandard2.0` and
   .NET Framework are not supported.
5. If you referenced the assembly by its strong name, for example in a binding redirect or with
   `InternalsVisibleTo`, rebuild without it. The public key token is now null.

## Libraries that depend on SixLabors.Fonts

Libraries such as SixLabors.ImageSharp.Drawing take `SixLabors.Fonts` types in their APIs. The
types of the two packages have the same names but are different types, so you cannot pass a
`PinataLabs.Typography.Font` to them. Keep SixLabors.Fonts for code that talks to those libraries.

## Removed members

The fork removed some type forwarders that existed only for the old target frameworks
(`System.HashCode`, `System.MathF` and others). Your code still gets those types from the .NET
runtime. The [changelog](https://github.com/PinataLabs/Typography/blob/main/CHANGELOG.md) lists
them.

## Font styles and variable fonts

`FontStyle` is a struct of a `FontWeight`, a `FontWidth` and a `FontSlope`, not a flags enum.
`FontStyle.Regular`, `Bold`, `Italic` and `BoldItalic` keep their names and meanings, so
`CreateFont(12, FontStyle.Bold)` still compiles. Code that combines styles with `|` or calls
`HasFlag` stops compiling. Use `WithWeight`, `WithWidth` and `WithSlope`.

| SixLabors.Fonts | PinataLabs.Typography |
| --- | --- |
| `FontFamily.GetAvailableStyles()` | `FontFamily.Faces`, and the `Description.StyleRange` of each face |
| `FontFamily.TryGetMetrics(style, out metrics)` | `FontFamily.MatchFace(style)`, which always returns a face |
| `FontDescription.Style` | `FontDescription.StyleRange` |
| `Font.IsBold`, `Font.IsItalic` | `Font.Style` |
| `new Font(prototype, size, style)` and its overloads | `prototype.WithSize(size)`, `prototype.WithStyle(style)` |
| `GlyphRendererParameters.Font`, `GlyphRendererParameters.FontStyle` | `GlyphRendererParameters.FontMetrics` |

A family is grouped by its typographic family name, `name` ID 16, so `Teko-Variable.ttf` is the
family *Teko*, not *Teko Light*. `FontDescription.LegacyFontFamily(culture)` gives the old name. A
style that the family does not have uses the nearest face in the CSS Fonts Level 4 order. A variable
font draws the instance that the style asks for: `CreateFont(12)` on Teko draws `wght` 400, where
SixLabors.Fonts drew its default instance at 300. See [Font matching](../fonts/font-matching.md) and
[Variable fonts](../fonts/variable-fonts.md).

## Changed members

`GlyphMetrics.AdvanceWidth` and `GlyphMetrics.AdvanceHeight` return `int`, not `ushort`.
`GlyphMetrics.RightSideBearing` and `GlyphMetrics.BottomSideBearing` return `int`, not `short`.
GPOS positioning in a layout can take an advance below zero or above 65535. Code that assigns one of
these properties to a `ushort` or a `short` does not compile.

The side bearings follow the advance that positioning gives a glyph. Positioning changes only the
advance along the line, as in HarfBuzz: in horizontal text `AdvanceHeight` keeps the value of the
font, and in vertical text `AdvanceWidth` does. Vertical text does not apply the `kern` table, even
with `vkrn` turned on. A pair of rotated glyphs in vertical mixed text still kerns.

## Changed behaviour

The bidi algorithm splits text into paragraphs at each paragraph separator (UAX #9 rule P1) and
resolves each paragraph on its own. With `TextDirection.Auto`, each paragraph takes its direction
from its own first strong character, and `TextAlignment.Start` follows the direction of the
paragraph of each line. Every paragraph separator starts a new line. SixLabors.Fonts resolves the
whole text as one paragraph.

Shaping matches HarfBuzz 15. Glyph positions in Indic, Khmer, Myanmar, Hangul and Arabic text, and
in text that mixes fonts, can differ from SixLabors.Fonts. Each script run is shaped on its own, so
a `kern` or `calt` pair between two scripts, such as kanji and kana, no longer applies.
