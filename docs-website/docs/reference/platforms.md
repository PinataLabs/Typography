---
title: Platforms and deployment
description: Supported .NET versions and operating systems, fonts in containers and on servers, and thread safety.
---

## .NET versions

The package contains builds for `netstandard2.1`, `net8.0`, `net10.0` and `net11.0`. A project that
targets .NET 9 uses the `net8.0` build. A project on another runtime that supports .NET Standard 2.1,
such as Mono or Unity, uses the `netstandard2.1` build. The library does not support .NET Framework,
because .NET Framework does not implement .NET Standard 2.1.

## Trimming and Native AOT

The `net8.0`, `net10.0` and `net11.0` builds are marked trimmable and AOT-compatible. You can publish
an application that uses the library with `PublishTrimmed` or `PublishAot`, and the library adds no
trim or AOT warnings. The repository's CI publishes a Native AOT test application on each change and
fails if the library reports a warning.

The `netstandard2.1` build has no trim annotations. A trimmed or AOT application uses a `net8.0` or
later build, so this does not affect it.

## Operating systems

The library is pure C# with no native code, so it runs wherever .NET runs. The repository's CI
builds and tests it on Windows and Linux.

The only part that depends on the operating system is [`SystemFonts`](../fonts/system-fonts.md),
which looks for installed fonts in the standard places for Windows, Linux, macOS and Android. On
other platforms `SystemFonts` is empty, and you load fonts into a `FontCollection` yourself.

## Containers and servers

Most container images have no fonts installed. Plan for that:

- Ship the fonts that your application needs, and load them with
  [`FontCollection.Add`](../fonts/loading-fonts.md). Then the output is the same on every machine.
- Embed fonts as resources and load them from streams, if you do not want loose font files.
- Load fonts once at startup and reuse the collection.

## Thread safety

- `Font`, `FontFamily`, `FontMetrics` and a `FontCollection` that you have finished filling can be
  shared between threads. Many threads can measure and render with the same font at the same time.
- Do not add fonts to a `FontCollection` while other threads read from it.
- Many threads can lay out and measure text with one `TextOptions`, including its `TextRuns`.
  Layout works on copies of your runs and does not change them. `TextOptions` and `TextRun` are
  mutable, so do not change them while another thread uses them.
- A copy of a run keeps its runtime type. If you derive from `TextRun`, your renderer sees the derived
  type in `GlyphRendererParameters.TextRun`, with your data. Do not rely on it being the same instance.
- `TextRenderer` and your `IGlyphRenderer` hold state. Use each one on one thread at a time.

[Hinting](../rendering/hinting.md#threads) is thread-safe. It runs the hinting program of one font on
one thread at a time.

## Licences of fonts

The library reads any font file that you give it. It does not enforce the font's embedding or
licence flags. You can read the embedding flags yourself from
[`FontMetrics.EmbeddingPermissions`](../fonts/font-metrics.md#embedding-permissions). Make sure that the licence of each
font that you ship, or that you embed in documents, allows that use. [Font descriptions](../fonts/font-descriptions.md#read-other-names)
shows how to read a font's licence text.
