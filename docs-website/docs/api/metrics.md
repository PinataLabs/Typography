---
title: Metrics
description: API reference for FontMetrics, HorizontalMetrics, VerticalMetrics, IMetricsHeader, Glyph, GlyphMetrics, TrueTypeGlyphMetrics, FontDescriptorMetrics and FontEmbeddingPermissions.
---

Namespace: `PinataLabs.Typography`. Guide: [Font and glyph metrics](../fonts/font-metrics.md).

All values on this page are in font units unless the description says otherwise.

## FontMetrics

`public abstract class FontMetrics`

The metrics of one font face. Get it from `Font.FontMetrics`, `FontFamily.MatchFace` or `FontFamily.Faces`.
The members for variable fonts are listed in
[Variation members of FontMetrics](./font.md#variation-members-of-fontmetrics).

| Property | Type | Description |
| --- | --- | --- |
| `Description` | `FontDescription` | The names and style of the face. |
| `UnitsPerEm` | `ushort` | The size of the design grid. |
| `ScaleFactor` | `float` | `UnitsPerEm × 72`. |
| `HorizontalMetrics` | `HorizontalMetrics` | Metrics for horizontal text. |
| `VerticalMetrics` | `VerticalMetrics` | Metrics for vertical text. Made from the horizontal metrics when the font has no `vhea` table. |
| `UnderlinePosition` | `short` | The distance from the baseline to the top of the underline. Negative is below the baseline. |
| `UnderlineThickness` | `short` | The thickness of the underline. |
| `StrikeoutPosition` | `short` | The distance from the baseline to the top of the strike-out line. |
| `StrikeoutSize` | `short` | The thickness of the strike-out line. |
| `SubscriptXSize`, `SubscriptYSize` | `short` | The size of subscript glyphs. |
| `SubscriptXOffset`, `SubscriptYOffset` | `short` | The offset of subscript glyphs. |
| `SuperscriptXSize`, `SuperscriptYSize` | `short` | The size of superscript glyphs. |
| `SuperscriptXOffset`, `SuperscriptYOffset` | `short` | The offset of superscript glyphs. |
| `ItalicAngle` | `float` | The italic angle in degrees, counter-clockwise from vertical. |
| `DescriptorMetrics` | `FontDescriptorMetrics` | The values that a PDF font descriptor is written from. See [FontDescriptorMetrics](#fontdescriptormetrics). |
| `EmbeddingPermissions` | `FontEmbeddingPermissions` | The embedding permissions of the `OS/2` `fsType` field. See [FontEmbeddingPermissions](#fontembeddingpermissions). |

| Method | Description |
| --- | --- |
| `TryGetGlyphMetrics(CodePoint, TextAttributes, TextDecorations, LayoutMode, ColorFontSupport, out IReadOnlyList<GlyphMetrics>? metrics)` | Gets the metrics of the glyphs for a code point. |

## HorizontalMetrics and VerticalMetrics

`public class HorizontalMetrics : IMetricsHeader`
`public class VerticalMetrics : IMetricsHeader`

Both have the members of `IMetricsHeader`:

| Property | Type | Description |
| --- | --- | --- |
| `Ascender` | `short` | The distance from the baseline to the top of the line. |
| `Descender` | `short` | The distance from the baseline to the bottom of the line. Usually negative. |
| `LineGap` | `short` | The extra space between lines. |
| `LineHeight` | `short` | The distance between baselines: ascender − descender + line gap. |
| `AdvanceWidthMax` | `short` | The largest advance width of any glyph. |
| `AdvanceHeightMax` | `short` | The largest advance height of any glyph. |

## Glyph

`public readonly struct Glyph`

A glyph of a `Font`, at that font's size.

| Member | Description |
| --- | --- |
| `GlyphMetrics` | The metrics of the glyph. |
| `BoundingBox(GlyphLayoutMode mode, Vector2 location, float dpi)` | The ink box of the glyph in pixels at `dpi`, placed at `location`. |

## GlyphMetrics

`public abstract class GlyphMetrics`

The metrics of one glyph. `TrueTypeGlyphMetrics` is the subclass for TrueType outlines.

| Property | Type | Description |
| --- | --- | --- |
| `GlyphId` | `ushort` | The index of the glyph in its font. |
| `CodePoint` | `CodePoint` | The code point that the glyph is for. |
| `GlyphType` | `GlyphType` | `Standard`, `Fallback` or `ColrLayer`. |
| `GlyphColor` | `GlyphColor?` | The color of a color layer, or `null`. |
| `UnitsPerEm` | `ushort` | The units per em of the font. |
| `ScaleFactor` | `Vector2` | The scale factor of the font, for each axis. |
| `AdvanceWidth` | `int` | How far the pen moves after the glyph in horizontal text. Positioning in a layout can make it negative or larger than 65535. |
| `AdvanceHeight` | `int` | How far the pen moves after the glyph in vertical text. Positioning in a layout can make it negative or larger than 65535. |
| `LeftSideBearing` | `short` | The space from the pen position to the left edge of the ink. |
| `RightSideBearing` | `int` | The space from the right edge of the ink to the end of `AdvanceWidth`. It follows the advance that positioning in a layout gives the glyph. |
| `TopSideBearing` | `short` | The space above the ink in vertical text. |
| `BottomSideBearing` | `int` | The space from the bottom edge of the ink to the end of `AdvanceHeight` in vertical text. It follows the advance that positioning in a layout gives the glyph. |
| `Width` | `float` | The width of the ink. |
| `Height` | `float` | The height of the ink. |
| `TextAttributes` | `TextAttributes` | The attributes that the metrics were made for. |
| `TextDecorations` | `TextDecorations` | The decorations that the metrics were made for. |

## FontDescriptorMetrics

`public sealed class FontDescriptorMetrics`

The values that a PDF `/FontDescriptor` is written from, in font units. Get it from
`FontMetrics.DescriptorMetrics`. Guide: [Font descriptor metrics](../fonts/font-metrics.md#font-descriptor-metrics).

| Property | Type | Description |
| --- | --- | --- |
| `Ascent` | `short` | The ascent above the baseline. It follows the rules of `HorizontalMetrics`. |
| `AscentSource` | `MetricSource` | Where `Ascent` came from. |
| `Descent` | `short` | The descent, negative below the baseline. |
| `DescentSource` | `MetricSource` | Where `Descent` came from. |
| `CapHeight` | `short` | The height of a flat capital letter above the baseline. |
| `CapHeightSource` | `MetricSource` | Where `CapHeight` came from. |
| `XHeight` | `short` | The height of a flat lowercase letter above the baseline, or 0 when the font gives none. |
| `XHeightSource` | `MetricSource` | Where `XHeight` came from. |
| `ItalicAngle` | `float` | The italic angle in degrees, counter-clockwise from vertical. Negative for text that leans to the right. |
| `ItalicAngleSource` | `MetricSource` | Always `PostItalicAngle`. |
| `BoundingBox` | `FontBoundingBox` | The box that holds every glyph of the face, from the `head` table. |
| `BoundingBoxSource` | `MetricSource` | Always `HeadBounds`. |
| `Flags` | `FontDescriptorFlags` | The flags that the tables of the font state. |

## FontBoundingBox

`public readonly struct FontBoundingBox`

A box in font units, with the y axis up.

| Member | Description |
| --- | --- |
| `FontBoundingBox(short xMin, short yMin, short xMax, short yMax)` | Makes a box. |
| `XMin`, `YMin`, `XMax`, `YMax` | The left, bottom, right and top edges, as `short`. |

## MetricSource

`public enum MetricSource`

The table field, or the fallback, that gave a value of `FontDescriptorMetrics`.

| Value | Source |
| --- | --- |
| `None` | The font gives no value and has no fallback. The value is 0. |
| `Os2TypoAscender` | `OS/2` `sTypoAscender`. |
| `Os2TypoDescender` | `OS/2` `sTypoDescender`. |
| `Os2WinAscent` | `OS/2` `usWinAscent`. |
| `Os2WinDescent` | `OS/2` `usWinDescent`, negated. |
| `HheaAscender` | `hhea` `ascender`. |
| `HheaDescender` | `hhea` `descender`. |
| `HeadYMax` | `head` `yMax`. A fallback for the ascent. |
| `HeadYMin` | `head` `yMin`. A fallback for the descent. |
| `HeadBounds` | `head` `xMin`, `yMin`, `xMax` and `yMax`. |
| `Os2CapHeight` | `OS/2` `sCapHeight`, version 2 or later. |
| `Os2XHeight` | `OS/2` `sxHeight`, version 2 or later. |
| `CapitalHBounds` | The top of the outline of the glyph for U+0048. A fallback for the cap height. |
| `SmallXBounds` | The top of the outline of the glyph for U+0078. A fallback for the x-height. |
| `FallbackAscent` | The ascent. The last fallback for the cap height. |
| `PostItalicAngle` | `post` `italicAngle`. |

## FontDescriptorFlags

`[Flags] public enum FontDescriptorFlags`

The `/Flags` of a PDF font descriptor, at the bit positions of ISO 32000-1, Table 123.

| Value | Bit | Set when |
| --- | --- | --- |
| `None` | | No flag. |
| `FixedPitch` | 1 | `post` `isFixedPitch` is not 0. |
| `Serif` | 2 | `OS/2` `sFamilyClass` names a serif class, or says nothing and the PANOSE values give a serif style. |
| `Symbolic` | 3 | The `cmap` table has a (3, 0) symbol subtable. |
| `Script` | 4 | `sFamilyClass` is Scripts, or says nothing and the PANOSE family kind is Latin Script. |
| `Nonsymbolic` | 6 | The `cmap` table has no (3, 0) symbol subtable. |
| `Italic` | 7 | `fsSelection` has the italic or oblique bit, `head` `macStyle` has the italic bit, or `post` `italicAngle` is not 0. |
| `AllCap` | 17 | Never set by the library. |
| `SmallCap` | 18 | Never set by the library. |
| `ForceBold` | 19 | Never set by the library. |

The bit numbers count from 1, as ISO 32000-1 does. `FixedPitch` has the value 1, and `ForceBold` has the value
262144.

## FontEmbeddingPermissions

`public readonly struct FontEmbeddingPermissions`

The embedding permissions of the `fsType` field of the `OS/2` table. Get it from
`FontMetrics.EmbeddingPermissions`. Guide: [Embedding permissions](../fonts/font-metrics.md#embedding-permissions).

| Property | Type | Description |
| --- | --- | --- |
| `IsStated` | `bool` | `false` when the font has no `OS/2` table. Then `Usage` is `Installable` and the other values are `false`. |
| `FsType` | `ushort` | The `fsType` field as stored, or 0 when the font has no `OS/2` table. |
| `Usage` | `FontEmbeddingUsage` | The usage permission of bits 1 to 3. When more than one is set, the least restrictive one applies: editable, then preview and print, then restricted. |
| `NoSubsetting` | `bool` | `true` when the font must not be subset before it is embedded. Bit 8. |
| `BitmapEmbeddingOnly` | `bool` | `true` when only the bitmaps of the font can be embedded. Bit 9. |

## FontEmbeddingUsage

`public enum FontEmbeddingUsage`

| Value | Description |
| --- | --- |
| `Installable` | The font can be embedded and installed on the remote system. None of bits 1 to 3 is set. |
| `Restricted` | The font must not be embedded without the permission of its owner. Bit 1. |
| `PreviewAndPrint` | The font can be embedded to view and print a document, and the document must be read-only. Bit 2. |
| `Editable` | The font can be embedded to view, print and edit a document. Bit 3. |
