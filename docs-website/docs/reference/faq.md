---
title: FAQ
description: Answers to common questions about PinataLabs.Typography.
---

## Does the library draw text into images?

No. It lays out text and gives you the glyph outlines. Your code, or another library, turns the
outlines into pixels, PDF or SVG. See [Glyph renderers](../rendering/glyph-renderers.md).

## Does it need FreeType, HarfBuzz or another native library?

No. Font parsing, shaping, bidirectional layout, line breaking and hinting are all written in C#.

## Which font formats can it read?

TrueType (`.ttf`), OpenType with CFF and CFF2 outlines (`.otf`), TrueType collections (`.ttc`),
WOFF and WOFF2. See [Loading fonts](../fonts/loading-fonts.md#supported-formats).

## Does it support variable fonts?

Only in part. The library loads a variable font and draws the outlines of its default instance.
It does not read the variation tables (`fvar`, `gvar`, `HVAR` and others), so you cannot choose a
weight or width on the font's axes.

## Can it make a bold or italic font when the family has none?

No. It uses the nearest style that the family has. It does not make synthetic styles.

## Can I turn off ligatures?

Not at present. Standard ligatures (`liga`) are always applied when the font has them. You can turn
off kerning with `KerningMode.None`. See [OpenType features](../text/opentype-features.md).

## Does it hyphenate words?

No. Lines break at the places that UAX #14 allows. Lines can also break after a soft hyphen
(U+00AD) in the text, but the library does not draw a hyphen there: soft hyphens are never drawn.

## Is it safe to use from more than one thread?

Yes, with care. Fonts and filled collections can be shared; options and renderers should not be.
See [Platforms and deployment](./platforms.md#thread-safety).

## How is it related to SixLabors.Fonts?

It is a fork of SixLabors.Fonts, taken at commit `32bef429`, with new namespaces, newer target
frameworks and further changes. See
[Migrating from SixLabors.Fonts](./migrating-from-sixlabors-fonts.md).

## Where do I report a bug?

In [GitHub Issues](https://github.com/PinataLabs/Typography/issues). Include the version, the font
(or a link to it), the text, the `TextOptions` and what you expected. See
[Contributing](./contributing.md).
