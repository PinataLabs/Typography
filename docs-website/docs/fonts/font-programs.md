---
title: Font programs
description: Read glyph outlines, glyph names, CIDs and built-in encodings by glyph ID with FontProgram, for TrueType, CFF and Type 1 fonts, including the fonts that PDF files embed.
---

`FontProgram` reads the glyphs of one font by glyph ID, without text layout. Use it when another format
selects and places the glyphs, such as a PDF file that embeds a font and gives its own widths. A
`FontProgram` gives you the outline of a glyph, the matrix from glyph space to ems, glyph lookup by name, by
CID and by `cmap`, and the encoding that the font carries.

For text layout, use a [`FontCollection`](./loading-fonts.md) instead. `FontProgram` does not shape,
measure or render text.

## Supported formats

| Format | Where it comes from | `Format` |
| --- | --- | --- |
| TrueType | An sfnt file with a `glyf` table, or a PDF `/FontFile2` stream. | `FontProgramFormat.TrueType` |
| OpenType CFF | An sfnt file with a `CFF ` table, or a PDF `/FontFile3` stream with `/Subtype /OpenType`. | `FontProgramFormat.Cff` |
| Bare CFF | A PDF `/FontFile3` stream with `/Subtype /Type1C` or `/CIDFontType0C`. Name-keyed and CID-keyed fonts load. | `FontProgramFormat.Cff` |
| Type 1 | A PFB file, a PFA file, or a PDF `/FontFile` stream. | `FontProgramFormat.Type1` |

The library reads the format from the data. A Type 1 font is found from its data alone, so the
`/Length1`, `/Length2` and `/Length3` entries of a PDF stream do not need to be correct.

WOFF, WOFF2, font collections (`.ttc`) and CFF2 are not supported. `Load` throws
`InvalidFontFileException` for them.

## Load a font program

`FontProgram.Load` takes the font file as `ReadOnlyMemory<byte>`:

```csharp
using PinataLabs.Typography;

byte[] data = File.ReadAllBytes("fonts/OpenSans-Regular.ttf");
FontProgram program = FontProgram.Load(data);

Console.WriteLine($"{program.Format}, {program.GlyphCount} glyphs"); // TrueType, 1086 glyphs
```

Glyph IDs run from 0 to `GlyphCount - 1`.

A TrueType or CFF program keeps the memory that you pass and reads it on each call. It does not copy
it. Do not change or reuse the array while you use the program. A Type 1 program keeps a decrypted copy
of the part that holds its glyphs, and does not keep your memory after `Load` returns.

A font program has no state that changes after `Load`. One instance can serve many threads at once.

## Draw the outline of a glyph

`TryWriteOutline` sends the unhinted outline of a glyph to a sink that you write. The sink implements
`IGlyphOutlineSink`. Each contour is a `MoveTo`, then segments, then a `ClosePath`. TrueType outlines use
`QuadraticBezierTo`, and CFF and Type 1 outlines use `CubicBezierTo`.

This sink writes the outline as SVG path data:

```csharp
using System.Numerics;
using System.Text;
using PinataLabs.Typography;

struct SvgPath : IGlyphOutlineSink
{
	public SvgPath()
	{
	}

	public StringBuilder Data { get; } = new();

	public void MoveTo(Vector2 point) => this.Data.Append($"M{point.X} {point.Y}");

	public void LineTo(Vector2 point) => this.Data.Append($"L{point.X} {point.Y}");

	public void QuadraticBezierTo(Vector2 control, Vector2 point)
		=> this.Data.Append($"Q{control.X} {control.Y} {point.X} {point.Y}");

	public void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
		=> this.Data.Append($"C{control1.X} {control1.Y} {control2.X} {control2.Y} {point.X} {point.Y}");

	public void ClosePath() => this.Data.Append('Z');
}
```

Pass the sink by reference:

```csharp
SvgPath path = new();
if (program.TryWriteOutline(glyphId, ref path))
{
	Console.WriteLine(path.Data); // M1117 0L937 464L351 464L172 0L0 0...
}
```

The points are in glyph space, with y pointing up. A glyph without an outline, such as a space, writes
nothing and returns `true`. The method returns `false` when the glyph ID is out of range or the glyph data
is malformed. After `false`, the sink can hold a part of the outline.

`TryWriteOutline` is generic over the sink type, so a struct sink is not boxed.

## Convert glyph space to ems

`FontMatrix` maps glyph space to ems, and it holds for every glyph of the program:

- For TrueType, it scales by one over the units per em.
- For CFF, it is the `FontMatrix` of the Top DICT.
- For Type 1, it is the `/FontMatrix` of the font.

Both CFF and Type 1 scale by 0.001 by default.

```csharp
Vector2 inEms = Vector2.Transform(point, program.FontMatrix);
```

Multiply by your own text matrix to place the glyph on the page. In a CID-keyed CFF font, a Font DICT can
have a matrix of its own. The library writes the glyphs of that Font DICT in the glyph space of
`FontMatrix`, so one matrix holds for all of them.

## Get the advance width

`TryGetAdvanceWidth` gives the advance width of a glyph in glyph space:

```csharp
if (program.TryGetAdvanceWidth(glyphId, out float advance))
{
	float advanceInEms = advance * program.FontMatrix.M11;
}
```

The width comes from the `hmtx` table. A CFF font without `hmtx`, such as a bare CFF font, gives the width
of its charstring. A Type 1 font gives the width of the `hsbw` or `sbw` of its charstring. A PDF usually
gives its own widths, so you may not need this method.

## Find a glyph by name

`TryGetGlyphId` finds the glyph with a name, and `GetGlyphName` gives the name of a glyph:

```csharp
if (program.TryGetGlyphId("A", out ushort glyphId))
{
	Console.WriteLine(program.GetGlyphName(glyphId)); // A
}
```

The comparison is ordinal. When more than one glyph has the name, you get the lowest glyph ID. The names
come from the `post` table of a TrueType font, the charset of a CFF font, and the `/CharStrings` of a Type 1
font. A CID-keyed CFF font has no glyph names, so `TryGetGlyphId` returns `false` and `GetGlyphName`
returns `null` for it.

Use `TryGetGlyphId` to apply the `/Differences` array of a PDF font. To turn a glyph name into text, see
[Glyph names and encodings](../unicode/glyph-names-and-encodings.md).

## Find a glyph by CID

`IsCidKeyed` is `true` for a CID-keyed CFF font, such as a PDF `/CIDFontType0C` font. Such a font names its
glyphs by CID. `TryGetGlyphIdForCid` maps a CID to a glyph ID through the charset of the font:

```csharp
if (program.TryGetGlyphIdForCid(cid, out ushort glyphId))
{
	program.TryWriteOutline(glyphId, ref path);
}
```

A program that is not CID-keyed maps each CID to the glyph with the same ID. That is the
`/CIDToGIDMap /Identity` of a PDF Type 0 font.

## Read the built-in encoding

`TryGetBuiltInEncoding` fills a span of 256 glyph IDs, one for each character code of the encoding that the
font carries. A code without a glyph gets 0:

```csharp
Span<ushort> codeToGlyph = stackalloc ushort[256];
if (!program.TryGetBuiltInEncoding(codeToGlyph))
{
	codeToGlyph.Clear(); // The font has no encoding of its own.
}
```

| Format | The encoding comes from |
| --- | --- |
| CFF | The Standard, Expert or custom encoding of the font. |
| TrueType | The (3, 0) `cmap` subtable by the low byte of each code, or else the (1, 0) subtable. |
| Type 1 | The `/Encoding` of the font: StandardEncoding, or the array of glyph names that the font defines. |

The method returns `false`, and writes nothing, for a CID-keyed font, a TrueType font without either
subtable, and a Type 1 font without an `/Encoding`. The span must have at least 256 elements, or the method
throws `ArgumentException`.

## Look up a code in the cmap table

`TryGetGlyphIdFromCmap` maps a character code through the `cmap` subtable of one platform and encoding:

```csharp
// Platform 3 (Windows), encoding 1 (Unicode BMP).
if (program.TryGetGlyphIdFromCmap(3, 1, 0x20AC, out ushort euro))
{
	Console.WriteLine(euro);
}
```

The method reads subtable formats 0, 2, 4, 6, 10, 12 and 13 in place. When more than one encoding record has
the same platform and encoding, it uses the first one. It returns `false` when the subtable is missing,
unsupported or malformed, when the code has no glyph, and when the code maps to glyph 0. A bare CFF font and
a Type 1 font have no `cmap` table, so the method always returns `false` for them.

## Filled and stroked outlines

`PaintType` says how to paint the outlines. Most fonts are `GlyphPaintType.Filled`. A CFF or Type 1 font
with paint type 2 is `GlyphPaintType.Stroked`: its outlines are centrelines, and you stroke them with
`StrokeWidth` instead of filling them.

```csharp
if (program.PaintType == GlyphPaintType.Stroked)
{
	float lineWidthInEms = program.StrokeWidth * program.FontMatrix.M11;
}
```

`StrokeWidth` is in glyph space units. A TrueType font is always `Filled`, with a `StrokeWidth` of 0.

## Fonts with missing or damaged tables

A font subset in a PDF file often leaves out tables that drawing glyphs does not need. `Load` accepts such a
font, and a missing optional table counts as absent. Two properties tell you what was missing or ignored:

| Property | Holds |
| --- | --- |
| `MissingOptionalTables` | The optional tables that the font does not have. |
| `MalformedOptionalTables` | The optional tables that the font has, but that are too damaged to use. |

Both are `OptionalFontTables` flags: `Name`, `Os2`, `Post`, `Cmap`, `Hhea` and `Hmtx`. A table is in one
property or the other, never both. A damaged optional table never makes `Load` fail.

A missing table takes away what it gives. A TrueType font without `post` has no glyph names. A font without
usable `hhea` and `hmtx` tables has no advance widths from them:

```csharp
if (program.MissingOptionalTables.HasFlag(OptionalFontTables.Post))
{
	Console.WriteLine("This font has no glyph names.");
}
```

A TrueType font needs `head`, `maxp`, `loca` and `glyf`, and an OpenType CFF font needs `CFF `. A font
without one of them, or with a damaged one, fails to load. A bare CFF font and a Type 1 font have no sfnt
tables, so both properties are `OptionalFontTables.None` for them.

## Load fonts from untrusted files

A font program can read untrusted data, such as a font that a PDF file embeds:

- `Load` throws only `InvalidFontFileException`, or an exception that derives from it, for malformed data.
- After `Load`, no method throws for malformed data. A method returns `false` or `null`, so one damaged glyph
  fails alone.
- The work for one glyph has limits. For example, a CFF charstring runs at most 1,048,576 operators, and a
  TrueType composite glyph nests at most 16 levels deep.

The API reference for [`FontProgram`](../api/font-program.md) lists every limit.

These guarantees apply to `FontProgram` and `FontSubsetter` only, not to the text layout API.

## Limit the memory of a font program

`RetainedByteCount` gives the bytes that a font program keeps reachable. It is the length of the data when
the program keeps it, plus the arrays that the program allocated. The value is fixed at `Load`, because the
program never caches.

| Format | `RetainedByteCount` is at most |
| --- | --- |
| TrueType | The data length. A TrueType program allocates no array. |
| CFF | 3 times the data length plus 32 KiB. |
| Type 1 | 12 times the data length plus 32 KiB. A Type 1 program does not keep the data, so the data length is not counted. |

To refuse fonts that would keep too much memory, pass a `FontProgramOptions` with `MaxRetainedBytes`:

```csharp
FontProgramOptions options = new() { MaxRetainedBytes = 16 * 1024 * 1024 };

try
{
	FontProgram program = FontProgram.Load(data, options);
}
catch (FontProgramTooLargeException ex)
{
	Console.WriteLine($"Needs {ex.RetainedByteCount} bytes, the limit is {ex.MaxRetainedBytes}.");
}
```

`Load` checks the limit before it parses the font and before each allocation, so a font that is too large is
refused before the work is done. `FontProgramTooLargeException` derives from `InvalidFontFileException`, so a
`catch` for `InvalidFontFileException` also catches it.

To keep one budget for all the fonts of a document, subtract the `RetainedByteCount` of each program from the
limit of the next load. The objects of the program itself are not counted. Add 1024 bytes for each font to
cover them.

## Related pages

- [Font subsetting](./subsetting.md) cuts a font down to the glyphs that a document uses.
- [Glyph names and encodings](../unicode/glyph-names-and-encodings.md) maps PDF character codes to glyph
  names, and glyph names to text.
- [FontProgram API reference](../api/font-program.md).
