---
title: Font matching
description: How a FontFamily picks the face for a weight, width and slope, with the CSS Fonts Level 4 matching rules.
---

A `FontFamily` can have many faces: Regular, Bold, Light Italic, Condensed, and others. A `Font` asks
for a style. The library picks the face of the family that best matches that style, with the rules
that CSS Fonts Level 4 gives in section 5.2, "Matching font styles". Browsers, DirectWrite and Core
Text use the same rules, so a style picks the same face in your application as it does in a web page.

## Ask for a style

A `FontStyle` is a weight, a width and a slope:

| Part | Type | Range | CSS property |
| --- | --- | --- | --- |
| Weight | `FontWeight` | 1 to 1000. `FontWeight.Normal` is 400 and `FontWeight.Bold` is 700. | `font-weight` |
| Width | `FontWidth` | 50% to 200%. `FontWidth.Normal` is 100% and `FontWidth.Condensed` is 75%. | `font-stretch` |
| Slope | `FontSlope` | `Normal`, `Italic`, or `Oblique(degrees)` from -90 to 90. | `font-style` |

`FontStyle.Regular`, `Bold`, `Italic` and `BoldItalic` are the four common styles. To ask for
another style, change one part of a style:

```csharp
using PinataLabs.Typography;

FontStyle semiBold = FontStyle.Regular.WithWeight(FontWeight.SemiBold);
FontStyle condensedItalic = FontStyle.Italic.WithWidth(FontWidth.Condensed);
FontStyle oblique = FontStyle.Regular.WithSlope(FontSlope.Oblique(10));
FontStyle weight350 = FontStyle.Regular.WithWeight(new FontWeight(350));

Font font = family.CreateFont(12, semiBold);
```

The constructors clamp a value to its range, so `new FontWeight(1200)` is 1000. A value that is not a
number throws `ArgumentOutOfRangeException`. The default value of each type is its normal value, and
`default(FontStyle)` is `FontStyle.Regular`.

## See which styles a family has

`FontFamily.Faces` lists the faces of a family, in the order in which you added them. The
`Description.StyleRange` of each face gives the weights, widths and slopes that it covers:

```csharp
foreach (FontMetrics face in family.Faces)
{
    FontStyleRange range = face.Description.StyleRange;
    Console.WriteLine(
        $"{face.Description.FontSubFamilyNameInvariantCulture}: " +
        $"weight {range.MinWeight.Value} to {range.MaxWeight.Value}, " +
        $"width {range.MinWidth.Percent}% to {range.MaxWidth.Percent}%, " +
        $"italic {range.HasItalic}");
}
```

A static face covers one weight, one width and one slope. The library reads them from the font:

- The weight is `OS/2.usWeightClass`.
- The width is `OS/2.usWidthClass`, as a percentage.
- The slope is italic when `OS/2.fsSelection` has the `ITALIC` bit. It is oblique when
  `fsSelection` has the `OBLIQUE` bit and the `OS/2` table is version 4 or later. The angle comes from
  `post.italicAngle`, or is 14 degrees when that is 0. Otherwise the slope is normal.

A font without an `OS/2` table takes bold and italic from `head.macStyle`.

A variable face covers the range of its axes: the weights of its `wght` axis, the widths of its
`wdth` axis, and the slopes that its `ital` and `slnt` axes reach. See
[Variable fonts](./variable-fonts.md).

## How the library picks a face

`FontFamily.MatchFace(style)` gives the face that a `Font` of that style uses. It narrows the faces in
three steps: by width, then by slope, then by weight. Each step keeps only the faces that are nearest
to the request for that part, and the next step chooses among them. A face whose range contains the
requested value is an exact match for that part.

When no face has the requested value, the search goes to the nearest value in the direction that
CSS gives:

| Part | Request | Order of search |
| --- | --- | --- |
| Width | 100% or less | Narrower faces, then wider faces. |
| Width | More than 100% | Wider faces, then narrower faces. |
| Slope | Italic | Italic faces, then oblique faces from 11 degrees up, then oblique faces below 11 degrees, then upright faces. |
| Slope | Oblique of 11 degrees or more | More oblique faces, then less oblique faces, then italic faces, then upright faces. |
| Slope | Oblique of less than 11 degrees | Less oblique faces, then more oblique faces, then italic faces, then upright faces. |
| Slope | Normal | Upright faces, then oblique faces, then italic faces. |
| Weight | Less than 400 | Lighter faces, then heavier faces. |
| Weight | 400 to 500 | Faces up to 500, then lighter faces, then heavier faces. |
| Weight | More than 500 | Heavier faces, then lighter faces. |

When two faces match equally well, the face that you added first wins.

`MatchFace` always returns a face for a family that has one. So a font of any style draws text,
even when the family has only one face.

## An example

A family has three faces: Regular (400), Bold (700) and Italic (400, italic).

```csharp
FontCollection fonts = new();
fonts.Add("fonts/MySans-Regular.ttf");
fonts.Add("fonts/MySans-Bold.ttf");
fonts.Add("fonts/MySans-Italic.ttf");
FontFamily family = fonts.Get("My Sans");

FontMetrics face = family.MatchFace(FontStyle.Regular.WithWeight(FontWeight.SemiBold));
Console.WriteLine(face.Description.FontSubFamilyNameInvariantCulture); // Bold
```

| Request | Face | Why |
| --- | --- | --- |
| `Regular` | Regular | Exact match. |
| `SemiBold` (600) | Bold | A weight above 500 tries heavier faces first. |
| `Medium` (500) | Regular | No face from 500 to 500, so the search goes to lighter faces. |
| `Light` (300) | Regular | No face is lighter than 300, so the search goes to heavier faces. |
| `BoldItalic` | Italic | The slope step keeps only the Italic face, before the weight step. |

## The library does not make bold or italic glyphs

When the family does not have the style that you ask for, the font uses the nearest face as it is.
The library does not make a synthetic bold or oblique face. In the example, `BoldItalic` draws the
Italic face at weight 400.

`Font.Style` is the style that you asked for. `Font.FontMetrics` is the face that the font draws. To
see whether the face has the style, use `FontStyleRange.Contains`:

```csharp
Font font = family.CreateFont(12, FontStyle.BoldItalic);
bool exact = font.FontMetrics.Description.StyleRange.Contains(font.Style);
```

A renderer that makes its own bold or oblique glyphs can use this test to decide when to do it.

## Change the style of a font

`Font.WithStyle` makes a font of the same family and size in another style. The new font matches its
face again:

```csharp
Font regular = family.CreateFont(12);
Font bold = regular.WithStyle(FontStyle.Bold);
```

`SystemFonts.CreateFont(name, size, style)` matches a face of an installed family in the same way.
See [System fonts](./system-fonts.md).
