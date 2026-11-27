# PinataLabs.Typography.Unicode

[![License: Apache 2.0 and BSD-3-Clause](https://img.shields.io/badge/license-Apache%202.0%20%2B%20BSD--3--Clause-blue.svg)](https://github.com/PinataLabs/Typography/blob/main/NOTICE)
[![Documentation](https://img.shields.io/badge/docs-pinatalabs.github.io-blue.svg)](https://pinatalabs.github.io/Typography/)

PinataLabs.Typography.Unicode is a .NET library of Unicode character properties. It contains no font code. It is the Unicode half of [PinataLabs.Typography](https://www.nuget.org/packages/PinataLabs.Typography). The bidirectional text, line-breaking, script and grapheme algorithms that back its layout live here, and public APIs for them will follow.

Use this package on its own when you need the character properties, code point and grapheme enumeration, and the Unicode version, and not fonts.

## Features

- `CodePoint`: a Unicode scalar value with its general category and its bidi class, line-break class, grapheme-cluster class and vertical orientation.
- Enumerate the code points and grapheme clusters of a span of text.
- `UnicodeInfo.Version`: the Unicode version of the property data.
- `GlyphNameEncodings`: the PDF Standard, WinAnsi, MacRoman and MacExpert encodings and the Symbol and ZapfDingbats built-in encodings, as code to glyph name.
- `AdobeGlyphList`: glyph name to Unicode by the Adobe Glyph List specification, and Unicode to glyph name.
- `BidiParagraph`: the Unicode Bidirectional Algorithm (UAX #9). It gives the embedding level of each code unit of a paragraph, and the runs of a line in visual order.
- `CodePoint.GetScriptClass` and `ScriptClassExtensions.GetIso15924Code`: the script of a code point (UAX #24) and its ISO 15924 code.
- `TextItemizer`: cuts a paragraph into runs of one script and one direction, in visual order, for a shaper.
- The property data behind them, built from the [Unicode Character Database](https://www.unicode.org/ucd/).

## Install

```bash
dotnet add package PinataLabs.Typography.Unicode
```

Add `--prerelease` while only pre-release versions are published. The library targets `netstandard2.1`, `net8.0`, `net10.0` and `net11.0`.

## Unicode version

The property data is built from one version of the [Unicode Character Database](https://www.unicode.org/ucd/), which the library reports:

```csharp
using PinataLabs.Typography.Unicode;

string unicodeVersion = UnicodeInfo.Version; // "18.0.0"
```

A release that changes the Unicode version says so in the [changelog](https://github.com/PinataLabs/Typography/blob/main/CHANGELOG.md). If you test against the Unicode conformance suites, pin them to this version.

## Versioning

PinataLabs.Typography and PinataLabs.Typography.Unicode are released together with one version number. PinataLabs.Typography reads internals of this assembly, so a PinataLabs.Typography package depends on exactly the PinataLabs.Typography.Unicode package of the same version.

## Contributing

Read the [contribution guide](https://github.com/PinataLabs/Typography/blob/main/.github/CONTRIBUTING.md) before you open an issue or a pull request. Report bugs and suggest changes in [GitHub Issues](https://github.com/PinataLabs/Typography/issues). This project follows the [Code of Conduct](https://github.com/PinataLabs/Typography/blob/main/CODE_OF_CONDUCT.md).

## License and attribution

PinataLabs.Typography.Unicode is licensed under the [Apache License, Version 2.0](https://github.com/PinataLabs/Typography/blob/main/LICENSE).

This project is derived from [SixLabors.Fonts](https://github.com/SixLabors/Fonts), Copyright (c) Six Labors, licensed under the Apache License, Version 2.0. It was forked at commit `32bef429` and has been modified by PinataLabs. PinataLabs.Typography.Unicode is not affiliated with or endorsed by Six Labors. See [NOTICE](https://github.com/PinataLabs/Typography/blob/main/NOTICE) for the full attribution.
