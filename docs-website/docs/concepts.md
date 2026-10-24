---
title: Concepts
description: The words that the PinataLabs.Typography documentation uses, such as font family, point, DPI, advance, bounds, glyph and grapheme.
---

This page explains the words that the rest of the documentation uses.

## Fonts

**Font file.** A file that contains one font (`.ttf`, `.otf`, `.woff`, `.woff2`) or more than one
font (`.ttc`, a TrueType collection).

**Font family.** A set of fonts that share a design, for example *Open Sans*. Each font in the
family has a different **style**: regular, bold, italic or bold italic. In the library a family is a
`FontFamily`. You get it from a `FontCollection` or from `SystemFonts`.

**Font.** A family at one size and one style. In the library a font is a `Font`. It is cheap to make
and holds no font data of its own; it points at the data in its family.

**Font metrics.** The numbers in the font file: units per em, ascender, descender, line gap,
underline position and so on. They are in **font units**. `FontMetrics` gives them to you.

**Units per em.** The size of the design grid of the font, often 1000 or 2048. A glyph that is 1024
units wide in a 2048-unit font is half the font size wide.

## Sizes and units

**Point.** The unit of `Font.Size`. One point is 1/72 inch.

**DPI.** Dots per inch, set by `TextOptions.Dpi`. The default is 72. The library converts points to
pixels with this rule:

```text
pixels = points × DPI / 72
```

At 72 DPI, one point is one pixel. All results from `TextMeasurer`, and all coordinates sent to an
`IGlyphRenderer`, are in pixels at the DPI that you set.

**Origin.** The point where the layout starts, set by `TextOptions.Origin`. The default is `(0, 0)`.
The y-axis points down, as in most screen and image coordinate systems.

## Characters and glyphs

**Code point.** One Unicode value, for example U+0041 for `A`. A .NET `string` holds UTF-16 code
units, and one code point can use two of them. The library represents a code point as a
`CodePoint`.

**Grapheme.** What a reader sees as one character. It can be more than one code point, for example
`e` followed by a combining accent, or an emoji with a skin tone. Indexes in a
[`TextRun`](./text/rich-text.md) count graphemes, not `char` values.

**Glyph.** A shape in the font. Shaping maps code points to glyphs. The map is not always one to
one: a ligature replaces two or more characters with one glyph, and some scripts split one character
into more than one glyph.

**Shaping.** The step that picks the glyphs for the text and puts them in position. It applies the
font's OpenType features (GSUB for substitution, GPOS for positioning) and the rules of the script.

## Measurements

**Advance.** How far the pen moves after a glyph. The advance of a string is the logical size of the
text in a line: the sum of the advances in the direction of the line, and the line height in the
other direction. It includes the space that the font puts on each side of a glyph.

**Bounds.** The smallest rectangle that holds the ink of the glyphs. Bounds can start to the left
of the origin (for example an italic `f`), and are usually smaller than the advance.

**Size.** The width and height from the origin to the far edge of the ink. It is what you need to
make a bitmap that the text fits in.

[Measuring text](./text/measuring-text.md) shows the three side by side.

**Line height.** The distance from one baseline to the next. It comes from the font's ascender,
descender and line gap, multiplied by `TextOptions.LineSpacing`.

## Layout

**Layout mode.** The direction of the lines. Horizontal text runs left to right or right to left in
lines from top to bottom. Vertical text runs top to bottom in columns. See
[Vertical text](./text/vertical-text.md).

**Text direction.** Left to right or right to left inside a line. The default, `Auto`, finds it from
the text with the Unicode Bidirectional Algorithm. See
[International text](./text/international-text.md).

**Text box.** The box that the lines are laid out in. Its width is `WrappingLength` when you set it.
`TextAlignment` places each line in the box. `HorizontalAlignment` and `VerticalAlignment` place the
box relative to the origin. See [Wrapping and alignment](./text/wrapping-and-alignment.md).
