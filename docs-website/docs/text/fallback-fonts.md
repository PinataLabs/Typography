---
title: Fallback fonts
description: Use fallback font families to show characters that the main font does not have, such as emoji, symbols and other scripts.
---

No font has every character. When the main font has no glyph for a character, the library can take
the glyph from another font. You list those fonts in `TextOptions.FallbackFontFamilies`.

## Set fallback families

```csharp
using PinataLabs.Typography;

FontCollection fonts = new();
Font latin = fonts.Add("fonts/OpenSans-Regular.ttf").CreateFont(16);
FontFamily emoji = fonts.Add("fonts/TwemojiMozilla.ttf");
FontFamily japanese = fonts.Add("fonts/NotoSansJP-Regular.otf");

TextOptions options = new(latin)
{
    FallbackFontFamilies = [japanese, emoji],
};

FontRectangle size = TextMeasurer.MeasureSize("Hello 世界 😀", options);
```

For each character, the library uses the first font in this order that has a glyph for it:

1. The font of the run: `TextOptions.Font`, or the `Font` of the [`TextRun`](./rich-text.md) that
   the character is in.
2. Each family in `FallbackFontFamilies`, in list order.

The library makes each fallback font at the same size and the same requested style as
`TextOptions.Font`.

Each fallback font is shaped with its own metrics and its own OpenType tables, so a Japanese
fallback font gets correct Japanese shaping inside Latin text.

## When no font has the character

If no font in the list has the character, the library uses the missing-glyph box (glyph 0) of the
main font. The glyph comes to your renderer with `GlyphType.Fallback`, so you can find characters
that the text cannot show:

```csharp
public bool BeginGlyph(in FontRectangle bounds, in GlyphRendererParameters parameters)
{
    if (parameters.GlyphType == GlyphType.Fallback)
    {
        Console.WriteLine($"No font has {parameters.CodePoint}");
    }

    return true;
}
```

## Choose fallback fonts

- Put the font that covers the most missing characters first. The library checks the list in
  order for each character.
- Prefer fonts that are made to work together, such as the Noto family. Their sizes and baselines
  match, so the mixed text looks even.
- For emoji, use a color font such as Twemoji or Noto Color Emoji with COLR glyphs. See
  [Color fonts](../rendering/color-fonts.md).
- On a server, ship the fallback fonts with your application. Do not rely on the fonts installed
  on the machine. See [System fonts](../fonts/system-fonts.md#containers-and-servers).
