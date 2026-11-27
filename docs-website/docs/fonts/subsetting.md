---
title: Font subsetting
description: Cut a TrueType or CFF font down to the glyphs that a document uses with FontSubsetter, and keep every glyph at its glyph ID.
---

`FontSubsetter.Subset` cuts a font down to the glyphs that a document uses, so a PDF file can embed a
smaller font. Every glyph keeps its glyph ID. The glyph count stays the same, so a PDF font that embeds the
subset can keep `/CIDToGIDMap /Identity`.

## Supported formats

| Format | Input | Output |
| --- | --- | --- |
| TrueType | An sfnt file with `glyf` and `loca` tables. | An sfnt file. |
| OpenType CFF | An sfnt file with a `CFF ` table. | An sfnt file. |
| Bare CFF | A CFF font, as a PDF embeds it in a `/FontFile3` stream. | A bare CFF font. |

Type 1 fonts, CFF2 fonts, WOFF, WOFF2 and font collections are not supported. `Subset` throws
`InvalidFontFileException` for them.

## Subset a font

Pass the font file and the glyph IDs to keep. The result is the font file of the subset:

```csharp
using PinataLabs.Typography;

byte[] data = File.ReadAllBytes("fonts/OpenSans-Regular.ttf");
ushort[] glyphIds = [43, 72, 79, 82];

byte[] subset = FontSubsetter.Subset(data, glyphIds);
```

Glyph 0, the `.notdef` glyph, is always kept. A glyph ID that the font does not have throws
`ArgumentOutOfRangeException`.

## Get the glyph IDs from shaped text

When your application shapes its text with [`TextShaper`](../api/text-shaper.md), collect the glyph IDs
of the result:

```csharp
FontCollection fonts = new();
Font font = fonts.Add("fonts/OpenSans-Regular.ttf").CreateFont(12);

HashSet<ushort> used = [];
foreach (ShapedGlyph glyph in TextShaper.Shape(font, "Hello, world"))
{
	used.Add(glyph.GlyphId);
}

byte[] data = File.ReadAllBytes("fonts/OpenSans-Regular.ttf");
byte[] subset = FontSubsetter.Subset(data, used);
```

For Open Sans, the subset of these glyphs is 23,716 bytes. The full font is 122,076 bytes.

## What the subset keeps

A kept glyph keeps its data byte for byte, so its outline and its advance do not change. A dropped glyph
stays in the font with no outline. In CFF it is a lone `endchar`, and in TrueType it has no data in `glyf`.

The subset also keeps the glyphs and the code that the kept glyphs need:

- The components of a composite TrueType glyph, at every depth.
- The base and the accent of a CFF glyph that ends with `seac`.
- The global and local subroutines that a kept CFF glyph calls, directly or from another subroutine. A
  subroutine that no kept glyph calls keeps its place in its INDEX, with a lone `return` for a body. So no
  charstring and no subroutine bias changes.

A CFF subset keeps the charset, the encoding, FDSelect and the strings of the font. Glyph names and CIDs stay
the same.

## Choose the tables

An sfnt subset keeps the tables in `FontSubsetOptions.DefaultTables`:

| Tables | Why |
| --- | --- |
| `CFF `, or `glyf` and `loca` | The outlines, of the format of the font only. |
| `cmap`, `OS/2`, `head`, `hhea`, `hmtx`, `maxp`, `name`, `post` | PDF readers and PDF/A validators read them. |
| `cvt `, `fpgm`, `prep` | TrueType hinting runs them. |

Every other table is dropped. That includes `GSUB`, `GPOS` and `GDEF`, which a PDF does not use, and `DSIG`,
whose signature a subset would break. The subset has the checksum of each table and the `head` checksum
adjustment.

To keep other tables, set `FontSubsetOptions.Tables`. This example keeps the default tables without `name`:

```csharp
FontSubsetOptions options = new()
{
	Tables = [.. FontSubsetOptions.DefaultTables.Where(tag => tag != "name")],
};

byte[] subset = FontSubsetter.Subset(data, used, options);
```

Each tag has 4 characters. The tag `CFF ` ends with a space. A tag that the font does not have is ignored. A
tag that is not 4 printable ASCII characters throws `ArgumentException`. The subset always keeps the outline
tables, `head` and `maxp`, so you do not need to name them. A bare CFF font has no tables, so `Tables` does not
apply to it.

## Limit the memory of a subset

`FontSubsetOptions.ProgramOptions` applies the memory limit of a [font program](./font-programs.md#limit-the-memory-of-a-font-program)
to the subsetter:

```csharp
FontSubsetOptions options = new()
{
	ProgramOptions = new FontProgramOptions { MaxRetainedBytes = 32 * 1024 * 1024 },
};

try
{
	byte[] subset = FontSubsetter.Subset(data, used, options);
}
catch (FontProgramTooLargeException ex)
{
	Console.WriteLine($"Needs at least {ex.RetainedByteCount} bytes.");
}
```

The limit counts the length of the font data, the arrays that the subsetter allocates, and the array that it
returns. The subsetter throws `FontProgramTooLargeException` before an allocation that would pass the limit. A
later allocation can need more, so a retry with `RetainedByteCount` as the limit can fail again with a larger
value.

## Check the embedding permissions first

`FontSubsetter` does not read the embedding permissions of the font. Read them yourself from
`FontMetrics.EmbeddingPermissions` before you embed or subset a font:

```csharp
FontEmbeddingPermissions permissions = font.FontMetrics.EmbeddingPermissions;

if (permissions.Usage == FontEmbeddingUsage.Restricted || permissions.BitmapEmbeddingOnly)
{
	// The font does not allow you to embed its outlines.
}
else if (permissions.NoSubsetting)
{
	// Embed the whole font file.
}
else
{
	byte[] subset = FontSubsetter.Subset(data, used);
}
```

A font with no `OS/2` table states no permissions. Then `IsStated` is `false` and `Usage` is
`FontEmbeddingUsage.Installable`. See [Embedding permissions](./font-metrics.md#embedding-permissions).

## Untrusted fonts

The font data can be untrusted, such as a font that a PDF file embeds. For malformed data, and for a kept glyph
whose charstring or components are malformed, `Subset` throws `InvalidFontFileException`, or an exception that
derives from it, and no other type. The work for each kept glyph has the same limits as drawing the glyph with
`FontProgram`. A cycle of composite glyphs ends. `Subset` does not change the data that you pass.

## Related pages

- [Font programs](./font-programs.md) reads the outlines of the subset.
- [FontSubsetter API reference](../api/font-program.md#fontsubsetter).
