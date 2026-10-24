---
title: Exceptions
description: API reference for the exceptions that PinataLabs.Typography throws.
---

Namespace: `PinataLabs.Typography`.

```text
Exception
├── FontException
│   ├── FontFamilyNotFoundException
│   └── GlyphMissingException
└── InvalidFontFileException
    ├── InvalidFontTableException
    └── MissingFontTableException
```

| Exception | Thrown when | Extra members |
| --- | --- | --- |
| `FontException` | A general font error, for example when you use a `default` `FontFamily`. The base class of the two below. | |
| `FontFamilyNotFoundException` | `Get` or `CreateFont` cannot find a family by name. | `FontFamily`: the name that was not found. `SearchDirectories`: the folders that were searched, for system fonts. |
| `GlyphMissingException` | Not thrown by the library at present. A missing glyph gives the font's missing-glyph box instead. | |
| `InvalidFontFileException` | The data is not a font that the library can read. | |
| `InvalidFontTableException` | A table in the font has data that is not valid. | `Table`: the tag of the table. |
| `MissingFontTableException` | The font has no table that the library needs, such as `cmap` or `head`. | `Table`: the tag of the table. |

The library also throws the standard .NET exceptions: `ArgumentNullException` for a `null`
argument, `ArgumentOutOfRangeException` for a negative `Dpi`, `ArgumentException` for a
`LineSpacing` of `0`, and `FileNotFoundException` and other I/O exceptions when it reads a file.

To load fonts that a user supplies, catch `InvalidFontFileException` (which also catches the two
table exceptions) and show a message:

```csharp
try
{
    fonts.Add(path);
}
catch (InvalidFontFileException ex)
{
    Console.WriteLine($"{Path.GetFileName(path)} is not a usable font: {ex.Message}");
}
```
