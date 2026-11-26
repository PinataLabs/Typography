---
title: Loading fonts
description: Load fonts from files and streams into a FontCollection, including TrueType collections and WOFF and WOFF2 web fonts.
---

A `FontCollection` holds the fonts that your application loads. You add font files to it, then get
a `FontFamily` from it by name.

## Supported formats

| Format | Extensions | Notes |
| --- | --- | --- |
| TrueType | `.ttf` | TrueType (`glyf`) outlines. |
| OpenType | `.otf` | CFF1 and CFF2 outlines. CFF2 fonts render at their default instance. |
| TrueType collection | `.ttc` | More than one font in one file. Use `AddCollection`. |
| WOFF | `.woff` | Web font, compressed with zlib. |
| WOFF2 | `.woff2` | Web font, compressed with Brotli. |

The library reads the format from the file data, not from the extension.

## Add a font file

`Add` reads one font and returns the family that it belongs to:

```csharp
using PinataLabs.Typography;

FontCollection fonts = new();
FontFamily family = fonts.Add("fonts/OpenSans-Regular.ttf");
```

Add each style of a family as its own file. The fonts join the same family when their typographic
family names are the same (`name` ID 16, or ID 1 for a font that has no ID 16). A family can have
any number of weights, widths and slopes:

```csharp
fonts.Add("fonts/OpenSans-Regular.ttf");
fonts.Add("fonts/OpenSans-Bold.ttf");
fonts.Add("fonts/OpenSans-Italic.ttf");

FontFamily openSans = fonts.Get("Open Sans");
Font bold = openSans.CreateFont(12, FontStyle.Bold);
```

## Add a font from a stream

Use a stream when the font is an embedded resource, comes from a network, or is in memory:

```csharp
using Stream stream = typeof(Program).Assembly.GetManifestResourceStream("MyApp.Fonts.Brand.woff2")!;
FontFamily brand = fonts.Add(stream);
```

The library reads the font data that it needs from the stream before `Add` returns. You can close
the stream after the call.

## Add a TrueType collection

A `.ttc` file holds more than one font. `AddCollection` adds all of them and returns their
families:

```csharp
IEnumerable<FontFamily> families = fonts.AddCollection("fonts/NotoSansCJK.ttc");
```

## Get the description of what you added

Each `Add` and `AddCollection` method has an overload with an `out` parameter. It gives you the
`FontDescription` of the font that was added, with its name and style:

```csharp
FontFamily family = fonts.Add("fonts/OpenSans-Bold.ttf", out FontDescription description);
Console.WriteLine($"{description.FontNameInvariantCulture} ({description.StyleRange.MinWeight})");
```

See [Font descriptions](./font-descriptions.md) for what a description contains.

## Find a family

| Method | When the family is not found |
| --- | --- |
| `Get(name)` | Throws `FontFamilyNotFoundException`. |
| `TryGet(name, out family)` | Returns `false`. |
| `Families` | Lists every family in the collection. |
| `GetByCulture(culture)` | Lists every family, with names in the language of `culture`. |

Names are compared without regard to case:

```csharp
if (fonts.TryGet("open sans", out FontFamily family))
{
    Font font = family.CreateFont(14);
}
```

## Names in other languages

A font can store its names in more than one language. A Japanese font, for example, can have an
English family name and a Japanese one. Each method that adds or finds a family has an overload that
takes a `CultureInfo`. The library uses that culture to choose the name:

```csharp
using System.Globalization;

CultureInfo japanese = CultureInfo.GetCultureInfo("ja-JP");
FontFamily family = fonts.Add("fonts/NotoSansJP-Regular.otf", japanese);
Console.WriteLine(family.Name); // The Japanese family name, if the font has one.
```

Without a culture, the library uses the invariant culture. Then it uses the US English name if the
font has one. See [Font descriptions](./font-descriptions.md#names-in-other-languages) for the full
order.

## Create a font

A `FontFamily` makes `Font` objects. A font is the family at one size, in points, and one style:

```csharp
Font regular = family.CreateFont(12);
Font boldItalic = family.CreateFont(12, FontStyle.BoldItalic);
```

A style is a weight, a width and a slope, so you can ask for any of them:

```csharp
Font light = family.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.Light));
Font condensed = family.CreateFont(12, FontStyle.Regular.WithWidth(FontWidth.Condensed));
Font oblique = family.CreateFont(12, FontStyle.Regular.WithSlope(FontSlope.Oblique(10)));
```

You can also make a font from another font:

```csharp
Font larger = regular.WithSize(18);                   // Same family and style, new size.
Font italic = regular.WithStyle(FontStyle.Italic);    // Same family and size, new style.
```

`family.Faces` lists the faces that the family has, and the `Description.StyleRange` of each face says
which weights, widths and slopes it covers. The font uses the face that best matches the style, which
`family.MatchFace(style)` also gives. If the family does not have the style, the face is the nearest one
in the order that CSS Fonts Level 4 gives for matching fonts: width first, then slope, then weight. A
weight from 400 to 500 tries the weights up to 500, then the lighter ones, then the heavier ones. A
lighter weight tries the lighter ones first, and a heavier weight the heavier ones first.
The library does not make a synthetic bold or italic. Compare `font.Style` with
`font.FontMetrics.Description.StyleRange` to see whether the face has the style you asked for.

## Add the system fonts to your collection

`AddSystemFonts` copies the fonts installed on the machine into your collection, so you can use your
own fonts and the installed ones from one place:

```csharp
FontCollection fonts = new();
fonts.Add("fonts/Brand-Regular.ttf");
fonts.AddSystemFonts();
```

Pass a filter to add only some of them:

```csharp
fonts.AddSystemFonts(metrics => metrics.Description.FontFamilyInvariantCulture.StartsWith("Noto"));
```

See [System fonts](./system-fonts.md) for where the library finds installed fonts.

## Find the file of a font

When a font was loaded from a file, `TryGetPath` tells you which file:

```csharp
if (font.TryGetPath(out string? path))
{
    Console.WriteLine(path);
}
```

`FontFamily.TryGetPaths` lists the files of all the styles in a family. A font loaded from a stream
has no path, so both methods return `false` for it.

## Reuse collections

When you add a font from a **file**, the library reads only the font's names at once. It reads the
rest of the file the first time you use the font, and keeps the result. When you add a font from a
**stream**, the library reads the whole font during `Add`.

Either way, loading a font costs time and memory. Make your collections once, for example at
startup, and reuse them. Do not load the same file again for each piece of text.
