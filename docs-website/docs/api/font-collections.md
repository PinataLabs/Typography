---
title: Font collections
description: API reference for FontCollection, SystemFonts, FontFamily, FontDescription and the collection interfaces.
---

Namespace: `PinataLabs.Typography`. Guides: [Loading fonts](../fonts/loading-fonts.md),
[System fonts](../fonts/system-fonts.md), [Font descriptions](../fonts/font-descriptions.md).

## FontCollection

`public sealed class FontCollection : IFontCollection`

A collection of fonts that you load.

| Member | Description |
| --- | --- |
| `FontCollection()` | Makes an empty collection. |
| `Families` | All the families in the collection. |
| `Add(string path)` | Adds the font in a file. Returns its family. |
| `Add(Stream stream)` | Adds the font in a stream. Returns its family. |
| `Add(…, CultureInfo culture)` | As above, and uses `culture` to choose the family name. |
| `Add(…, out FontDescription description)` | As above, and returns the description of the font. |
| `AddCollection(string path)` | Adds every font in a TrueType collection (`.ttc`) file. Returns their families. |
| `AddCollection(Stream stream)` | Adds every font in a TrueType collection stream. |
| `AddCollection(…, CultureInfo culture)` | As above, with names in `culture`. |
| `AddCollection(…, out IEnumerable<FontDescription> descriptions)` | As above, and returns the descriptions. |
| `Get(string name)` | Gets a family by name. Throws `FontFamilyNotFoundException` if there is none. |
| `Get(string name, CultureInfo culture)` | As above, matching names in `culture`. |
| `TryGet(string name, out FontFamily family)` | Gets a family by name. Returns `false` if there is none. |
| `TryGet(string name, CultureInfo culture, out FontFamily family)` | As above, matching names in `culture`. |
| `GetByCulture(CultureInfo culture)` | All the families, with names in `culture`. |

Family names are compared without regard to case.

### FontCollectionExtensions

| Member | Description |
| --- | --- |
| `AddSystemFonts(this FontCollection collection)` | Adds all the fonts from `SystemFonts`. Returns the collection. |
| `AddSystemFonts(this FontCollection collection, Predicate<FontMetrics> match)` | Adds the system fonts for which `match` returns `true`. |

## SystemFonts

`public static class SystemFonts`

The fonts installed on the machine. The library finds them the first time you use this class.

| Member | Description |
| --- | --- |
| `Collection` | The installed fonts, as an `IReadOnlySystemFontCollection`. |
| `Families` | All the installed families. |
| `Get(string name)` | Gets a family by name. Throws `FontFamilyNotFoundException` if it is not installed. |
| `Get(string name, CultureInfo culture)` | As above, matching names in `culture`. |
| `TryGet(string name, out FontFamily family)` | Gets a family by name. Returns `false` if it is not installed. |
| `TryGet(string name, CultureInfo culture, out FontFamily family)` | As above, matching names in `culture`. |
| `GetByCulture(CultureInfo culture)` | All installed families, with names in `culture`. |
| `CreateFont(string name, float size)` | Makes a regular font from an installed family. |
| `CreateFont(string name, float size, FontStyle style)` | Makes a font in a style from an installed family. |
| `CreateFont(string name, CultureInfo culture, float size)` | As above, matching names in `culture`. |
| `CreateFont(string name, CultureInfo culture, float size, FontStyle style)` | As above, matching names in `culture`. |

## FontFamily

`public struct FontFamily : IEquatable<FontFamily>`

A set of fonts that share a family name. Get one from a collection. The `default` value is not a
valid family; its methods throw.

| Member | Description |
| --- | --- |
| `Name` | The family name. |
| `Culture` | The culture that the name is in. |
| `CreateFont(float size)` | Makes a regular font at `size` points. |
| `CreateFont(float size, FontStyle style)` | Makes a font in `style` at `size` points. |
| `GetAvailableStyles()` | The styles in the family. |
| `TryGetMetrics(FontStyle style, out FontMetrics? metrics)` | Gets the metrics of one style. Returns `false` if the family does not have it. |
| `TryGetPaths(out IEnumerable<string> paths)` | Gets the files of the fonts in the family. Returns `false` if they were loaded from streams. |

## FontDescription

`public class FontDescription`

The names and style of a font, read from its `name` table.

| Member | Description |
| --- | --- |
| `static LoadDescription(string path)` | Reads the description of the font in a file. |
| `static LoadDescription(Stream stream)` | Reads the description of the font in a stream. |
| `static LoadFontCollectionDescriptions(string path)` | Reads the descriptions of every font in a `.ttc` file. |
| `static LoadFontCollectionDescriptions(Stream stream)` | Reads the descriptions of every font in a `.ttc` stream. |
| `Style` | The style of the font. |
| `FontNameInvariantCulture` | The full name, for example *Open Sans Bold*. |
| `FontFamilyInvariantCulture` | The family name, for example *Open Sans*. |
| `FontSubFamilyNameInvariantCulture` | The style name, for example *Bold*. |
| `FontName(CultureInfo culture)` | The full name in `culture`. |
| `FontFamily(CultureInfo culture)` | The family name in `culture`. |
| `FontSubFamilyName(CultureInfo culture)` | The style name in `culture`. |
| `GetNameById(CultureInfo culture, KnownNameIds nameId)` | Any name from the `name` table. Returns an empty string if the font does not have it. |

The `InvariantCulture` properties, and the methods when the font has no name in `culture`, return
the US English name if the font has one.

### KnownNameIds

Namespace `PinataLabs.Typography.WellKnownIds`. The standard name IDs:
`CopyrightNotice`, `FontFamilyName`, `FontSubfamilyName`, `UniqueFontID`, `FullFontName`, `Version`,
`PostscriptName`, `Trademark`, `Manufacturer`, `Designer`, `Description`, `VendorUrl`, `DesignerUrl`,
`LicenseDescription`, `LicenseInfoUrl`, `TypographicFamilyName`, `TypographicSubfamilyName`,
`SampleText`. See [Font descriptions](../fonts/font-descriptions.md#read-other-names).

## Interfaces

| Interface | Members |
| --- | --- |
| `IReadOnlyFontCollection` | `Families`, `Get`, `TryGet`, `GetByCulture`. |
| `IFontCollection` | `IReadOnlyFontCollection`, plus `Add` and `AddCollection`. `FontCollection` implements it. |
| `IReadOnlySystemFontCollection` | `IReadOnlyFontCollection`, plus `SearchDirectories`: the folders that were searched for installed fonts. |

Use the interfaces in your own APIs when a method only needs to read fonts, or to accept either a
`FontCollection` or `SystemFonts.Collection`.
