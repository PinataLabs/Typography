---
title: Hinting
description: Apply TrueType hinting to fit glyph outlines to the pixel grid for sharper text at small sizes on screens.
---

Hinting adjusts the outline of a glyph so that its stems and edges fall on whole pixels. At small
sizes on a screen, hinted text is sharper. At large sizes, in print, and in vector output such as
PDF and SVG, hinting has little benefit and changes the shapes from the design.

## Turn hinting on

```csharp
TextOptions options = new(font)
{
    HintingMode = HintingMode.Standard,
    Dpi = 96,
};
```

| `HintingMode` | Effect |
| --- | --- |
| `None` (default) | Use the outlines as designed. |
| `Standard` | Run the TrueType hinting instructions in the font. |

Set `Dpi` to the real resolution of the target. Hinting fits the outline to the pixel grid at that
resolution, so the wrong DPI gives the wrong result.

## What hinting applies to

- Only fonts with **TrueType outlines** (`glyf` table) are hinted. The library does not hint CFF and
  CFF2 outlines (`.otf` fonts with PostScript outlines). For those fonts `Standard` has no effect.
- Hinting changes the **outlines** that your [glyph renderer](./glyph-renderers.md) gets. When
  hinting is on, the library also rounds the positions of attached marks (accents) to whole pixels.
  The advances of the glyphs, and so the measurements of the text, do not change.

## Faulty hint programs

Some fonts have hinting instructions with errors. When the instructions of a glyph fail, the
library uses the glyph's outline without hinting, and continues. One bad glyph does not stop the
text from rendering.

## Threads

Hinting is safe to use from more than one thread with the same `Font`. The library runs the hinting
instructions of one font on one thread at a time, so heavy parallel rendering of the same hinted font
can wait on that lock.
