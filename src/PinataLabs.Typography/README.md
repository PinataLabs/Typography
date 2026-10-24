# PinataLabs.Typography

![A piñata donkey setting metal type on a letterpress, under the word Typography](https://media.githubusercontent.com/media/PinataLabs/Typography/main/assets/logo.png)

[![License: Apache 2.0](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](https://github.com/PinataLabs/Typography/blob/main/LICENSE)
[![Documentation](https://img.shields.io/badge/docs-pinatalabs.github.io-blue.svg)](https://pinatalabs.github.io/Typography/)

PinataLabs.Typography is a cross-platform .NET library that loads fonts and lays out and measures text. It is written in C# and has no native dependencies.

Read the [documentation](https://pinatalabs.github.io/Typography/) for guides, the API reference and troubleshooting.

## Features

- Read font descriptions: name, family, subfamily and other string metadata.
- Load OpenType fonts with CFF1, CFF2 and TrueType outlines.
- Load TrueType fonts and TrueType collections (`.ttc`).
- Load [WOFF](https://www.w3.org/Submission/WOFF/) and [WOFF2](https://www.w3.org/TR/WOFF2) fonts.
- Load all compatible fonts installed on the machine (Windows, Linux and macOS).
- Line breaking based on [UAX #14](https://www.unicode.org/reports/tr14/).
- Left-to-right, right-to-left and bidirectional text.
- Ligatures, and the OpenType glyph substitution ([GSUB](https://learn.microsoft.com/typography/opentype/spec/gsub)) and glyph positioning ([GPOS](https://learn.microsoft.com/typography/opentype/spec/gpos)) features.
- Measure the size, advance and bounds of laid-out text.

## Install

```bash
dotnet add package PinataLabs.Typography
```

Add `--prerelease` while only pre-release versions are published. The library targets `net8.0`, `net10.0` and `net11.0`.

## Quick start

### Read a font description

```csharp
using System.Globalization;
using PinataLabs.Typography;

FontDescription description = FontDescription.LoadDescription("path/to/font.ttf");
string name = description.FontName(CultureInfo.InvariantCulture);
```

### Load fonts into a collection

```csharp
using PinataLabs.Typography;

FontCollection fonts = new();
FontFamily family = fonts.Add("path/to/font.ttf");
fonts.Add("path/to/other-font.woff2");

Font font = family.CreateFont(12);
```

### Use a system font

```csharp
using PinataLabs.Typography;

if (SystemFonts.TryGet("Arial", out FontFamily arial))
{
    Font font = arial.CreateFont(16);
}
```

### Measure text

```csharp
using PinataLabs.Typography;

TextOptions options = new(font);
FontRectangle size = TextMeasurer.MeasureSize("Hello, world!", options);
```

## Contributing

Read the [contribution guide](https://github.com/PinataLabs/Typography/blob/main/.github/CONTRIBUTING.md) before you open an issue or a pull request. Report bugs and suggest changes in [GitHub Issues](https://github.com/PinataLabs/Typography/issues). This project follows the [Code of Conduct](https://github.com/PinataLabs/Typography/blob/main/CODE_OF_CONDUCT.md).

## License and attribution

PinataLabs.Typography is licensed under the [Apache License, Version 2.0](https://github.com/PinataLabs/Typography/blob/main/LICENSE).

This project is derived from [SixLabors.Fonts](https://github.com/SixLabors/Fonts), Copyright (c) Six Labors, licensed under the Apache License, Version 2.0. It was forked at commit `32bef429` and has been modified by PinataLabs. It also includes build files from SixLabors SharedInfrastructure. PinataLabs.Typography is not affiliated with or endorsed by Six Labors. See [NOTICE](https://github.com/PinataLabs/Typography/blob/main/NOTICE) for the full attribution.
