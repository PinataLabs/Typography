---
title: Overview
slug: /
description: PinataLabs.Typography is a cross-platform .NET library that loads fonts and lays out and measures text. It is written in C# and has no native dependencies.
---

PinataLabs.Typography is a .NET library that loads fonts and lays out and measures text. It is
written in C# and has no native dependencies, so it runs the same way on Windows, Linux and macOS.

The library does not draw pixels. It tells you where each glyph goes, how big the text is, and the
outline of each glyph. You send those outlines to your own drawing code, for example a PDF writer, an
image library or a GPU renderer.

## What you can do

- **Load fonts** from files, streams and the fonts installed on the machine. The library reads
  TrueType (`.ttf`), OpenType with CFF1 and CFF2 outlines (`.otf`), TrueType collections (`.ttc`),
  and [WOFF](https://www.w3.org/Submission/WOFF/) and [WOFF2](https://www.w3.org/TR/WOFF2) web
  fonts. See [Loading fonts](./fonts/loading-fonts.md) and [System fonts](./fonts/system-fonts.md).
- **Read font information** such as the family name, the style, the version and the copyright,
  without loading the whole font. See [Font descriptions](./fonts/font-descriptions.md).
- **Measure text**: its size, its advance, its ink bounds, the number of lines, and the position of
  each character. See [Measuring text](./text/measuring-text.md).
- **Lay out paragraphs**: wrap lines at a width, align and justify them, set line spacing and tab
  width. Lines break by the rules of [UAX #14](https://www.unicode.org/reports/tr14/). See
  [Wrapping and alignment](./text/wrapping-and-alignment.md).
- **Mix styles in one string**: change the font, underline, strike out, or raise and lower parts of
  the text. See [Rich text](./text/rich-text.md).
- **Set text in many scripts**: right-to-left and bidirectional text, joined Arabic letters,
  Indic, Hangul and other complex scripts, and fallback fonts for characters the main font does not
  have. See [International text](./text/international-text.md) and
  [Fallback fonts](./text/fallback-fonts.md).
- **Set vertical text** for Chinese, Japanese and Korean. See [Vertical text](./text/vertical-text.md).
- **Use OpenType features**: ligatures, kerning, small capitals, old-style figures and more, from
  the font's [GSUB](https://learn.microsoft.com/typography/opentype/spec/gsub) and
  [GPOS](https://learn.microsoft.com/typography/opentype/spec/gpos) tables. See
  [OpenType features](./text/opentype-features.md).
- **Get glyph outlines** as lines and Bézier curves, including the layers of color emoji. See
  [Glyph renderers](./rendering/glyph-renderers.md) and [Color fonts](./rendering/color-fonts.md).

## How the parts fit together

```text
 FontCollection / SystemFonts      ->  FontFamily  ->  Font (family + size + style)
                                                          |
                                                          v
 "your text"  +  TextOptions (font, wrapping, alignment, direction, features, ...)
                                                          |
                         +--------------------------------+-------------------------------+
                         v                                                                v
                   TextMeasurer                                                    TextRenderer
         sizes, bounds, line count, per-character positions          glyph outlines sent to your IGlyphRenderer
```

1. You get a `FontFamily` from a `FontCollection` that you fill, or from `SystemFonts`.
2. You create a `Font` from the family. A font is a family at one size and one style.
3. You put the font in a `TextOptions` object, and set how the text is laid out.
4. You give the text and the options to `TextMeasurer` to measure it, or to `TextRenderer` to get
   the glyph outlines.

[Concepts](./concepts.md) explains the words that the rest of these pages use, such as *point*,
*DPI*, *advance* and *bounds*.

## Where to go next

- [Installation](./installation.md): add the library to your project.
- [Getting started](./getting-started.md): load a font and measure a string in a few lines.
- [API reference](./api/index.md): every public type, grouped by what it does.

## Origin

PinataLabs.Typography is derived from [SixLabors.Fonts](https://github.com/SixLabors/Fonts). It was
forked at commit `32bef429` and has been changed by PinataLabs. It is not affiliated with or
endorsed by Six Labors. If you use SixLabors.Fonts today, read
[Migrating from SixLabors.Fonts](./reference/migrating-from-sixlabors-fonts.md).
