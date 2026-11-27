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
    ├── FontProgramTooLargeException
    ├── InvalidFontTableException
    └── MissingFontTableException
```

| Exception | Thrown when | Extra members |
| --- | --- | --- |
| `FontException` | A general font error, for example when you use a `default` `FontFamily`. The base class of the two below. | |
| `FontFamilyNotFoundException` | `Get` or `CreateFont` cannot find a family by name. | `FontFamily`: the name that was not found. `SearchDirectories`: the folders that were searched, for system fonts. |
| `GlyphMissingException` | Not thrown by the library at present. A missing glyph gives the font's missing-glyph box instead. | |
| `InvalidFontFileException` | The data is not a font that the library can read. | |
| `FontProgramTooLargeException` | `FontProgram.Load` or `FontSubsetter.Subset` would retain more memory than the `MaxRetainedBytes` of a `FontProgramOptions`. The font is well formed as far as the load went. See [Limit the memory of a font program](../fonts/font-programs.md#limit-the-memory-of-a-font-program). | `RetainedByteCount`: the bytes that the font program would have retained, or a lower bound when the load stopped before it read all the data. `MaxRetainedBytes`: the limit that was set. |
| `InvalidFontTableException` | A table in the font has data that is not valid. | `Table`: the tag of the table. |
| `MissingFontTableException` | The font has no table that the library needs, such as `cmap` or `head`. | `Table`: the tag of the table. |

The library also throws the standard .NET exceptions: `ArgumentNullException` for a `null`
argument, `ArgumentOutOfRangeException` for a negative `Dpi`, `ArgumentException` for a
`LineSpacing` of `0`, and `FileNotFoundException` and other I/O exceptions when it reads a file.

To load fonts that a user supplies, catch `InvalidFontFileException` (which also catches the
exceptions that derive from it) and show a message:

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
