---
title: Font descriptions
description: Read the name, family, style, version, copyright and other metadata of a font without loading it for layout.
---

A `FontDescription` holds the strings in the font's `name` table: the family name, the full name, the
style, the version, the copyright and others. You can read it without adding the font to a
collection, which is fast. Use it to build a font picker, check licences, or index a folder of fonts.

## Read the description of a font file

```csharp
using PinataLabs.Typography;

FontDescription description = FontDescription.LoadDescription("fonts/OpenSans-Regular.ttf");

Console.WriteLine(description.FontNameInvariantCulture);          // Open Sans Regular
Console.WriteLine(description.FontFamilyInvariantCulture);        // Open Sans
Console.WriteLine(description.FontSubFamilyNameInvariantCulture); // Regular
Console.WriteLine(description.Style);                             // Regular
```

`LoadDescription` also takes a `Stream`.

## Read every font in a collection file

For a `.ttc` file, `LoadFontCollectionDescriptions` returns one description for each font in it:

```csharp
foreach (FontDescription d in FontDescription.LoadFontCollectionDescriptions("fonts/Helvetica.ttc"))
{
    Console.WriteLine(d.FontNameInvariantCulture);
}
```

## Read other names

`GetNameById` reads any of the standard names. `KnownNameIds` lists them:

```csharp
using System.Globalization;
using PinataLabs.Typography.WellKnownIds;

CultureInfo culture = CultureInfo.InvariantCulture;
string version = description.GetNameById(culture, KnownNameIds.Version);          // Version 3.000
string copyright = description.GetNameById(culture, KnownNameIds.CopyrightNotice);
string license = description.GetNameById(culture, KnownNameIds.LicenseDescription);
```

| `KnownNameIds` value | Contains |
| --- | --- |
| `CopyrightNotice` | The copyright notice. |
| `FontFamilyName` | The family name, for example *Open Sans*. |
| `FontSubfamilyName` | The style name, for example *Bold Italic*. |
| `UniqueFontID` | A unique identifier for the font. |
| `FullFontName` | The family and style together, for example *Open Sans Bold*. |
| `Version` | The version string. |
| `PostscriptName` | The PostScript name, with no spaces. |
| `Trademark` | The trademark notice. |
| `Manufacturer` | The name of the font vendor. |
| `Designer` | The name of the designer. |
| `Description` | A description of the font. |
| `VendorUrl` | The website of the vendor. |
| `DesignerUrl` | The website of the designer. |
| `LicenseDescription` | A description of the licence. |
| `LicenseInfoUrl` | Where to read the licence. |
| `TypographicFamilyName` | The family name for fonts with more than the four basic styles. |
| `TypographicSubfamilyName` | The style name that goes with `TypographicFamilyName`. |
| `SampleText` | Sample text chosen by the designer. |

If the font does not have the name, the result is an empty string.

## Names in other languages

`FontName`, `FontFamily`, `FontSubFamilyName` and `GetNameById` take a `CultureInfo`. They return
the name in that language if the font has it. If it does not, they return the US English name, then
the first Windows name, then the first name of any kind. The `InvariantCulture` properties use the
same order, so they return the US English name when the font has one.

```csharp
string name = description.FontFamily(CultureInfo.GetCultureInfo("zh-CN"));
```

## Get the description of a loaded font

A font that you already loaded has its description in its metrics:

```csharp
FontDescription description = font.FontMetrics.Description;
```
