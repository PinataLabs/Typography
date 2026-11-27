---
title: FontProgram
description: API reference for FontProgram, FontProgramOptions, FontProgramFormat, GlyphPaintType, OptionalFontTables, IGlyphOutlineSink, FontSubsetter and FontSubsetOptions.
---

Namespace: `PinataLabs.Typography`. Guides: [Font programs](../fonts/font-programs.md) and
[Font subsetting](../fonts/subsetting.md).

## FontProgram

`public sealed class FontProgram`

The glyph outlines of one font, read by glyph ID without text layout. An instance has no state that changes
after load, so one instance can serve many threads at once.

### Load

| Method | Description |
| --- | --- |
| `static Load(ReadOnlyMemory<byte> data)` | Loads a font program. The format comes from the data. |
| `static Load(ReadOnlyMemory<byte> data, FontProgramOptions options)` | Loads a font program, and refuses one that would retain more than `options.MaxRetainedBytes`. A `null` `options` throws `ArgumentNullException`. |

The formats are an sfnt file with a `glyf` table, an sfnt file with a `CFF ` table, a bare CFF font (name-keyed
or CID-keyed), and a Type 1 font as a PFB file, a PFA file or a PDF `/FontFile` stream. WOFF, WOFF2, font
collections and CFF2 are not supported.

A TrueType or CFF program keeps `data` and reads it on each call. It does not copy it, so do not change or reuse
the memory while the program is in use. A Type 1 program does not keep `data` after `Load` returns.

For malformed or unsupported data, `Load` throws `InvalidFontFileException`, or an exception that derives from
it, and no other type. A missing or damaged optional table does not fail the load.

### Properties

| Property | Type | Description |
| --- | --- | --- |
| `Format` | `FontProgramFormat` | The format of the outlines. |
| `IsCidKeyed` | `bool` | `true` for a CID-keyed CFF font. Such a font has no glyph names. |
| `GlyphCount` | `int` | The number of glyphs. Glyph IDs run from 0 to `GlyphCount - 1`. |
| `FontMatrix` | `Matrix3x2` | The matrix from glyph space to ems, for every glyph. For TrueType it scales by one over the units per em. For CFF it is the FontMatrix of the Top DICT, and for Type 1 the `/FontMatrix`. |
| `PaintType` | `GlyphPaintType` | How to paint the outlines. TrueType is always `Filled`. CFF and Type 1 are `Stroked` for paint type 2, and `Filled` for every other value. |
| `StrokeWidth` | `float` | The line width for `Stroked` outlines, in glyph space units. 0 when the font gives none, gives a negative or infinite width, or is TrueType. |
| `MissingOptionalTables` | `OptionalFontTables` | The optional sfnt tables that the font does not have. |
| `MalformedOptionalTables` | `OptionalFontTables` | The optional sfnt tables that the font has but that are too damaged to use. They count as absent. |
| `RetainedByteCount` | `long` | The bytes that the program keeps reachable: the length of `data` when the program keeps it, plus 24 bytes and the size of the elements for each array that it allocated. Fixed at load. |

`MissingOptionalTables` and `MalformedOptionalTables` never name the same table. They are
`OptionalFontTables.None` for a bare CFF program and a Type 1 program, which have no sfnt tables.

### Methods

| Method | Returns |
| --- | --- |
| `TryWriteOutline<TSink>(ushort glyphId, ref TSink sink)` where `TSink : IGlyphOutlineSink` | `true` when it wrote the unhinted outline of the glyph to `sink`, in glyph space with y up. `false` when the glyph ID is out of range or the glyph data is malformed. After `false`, the sink can hold a part of the outline. A glyph without an outline writes nothing and returns `true`. |
| `TryGetAdvanceWidth(ushort glyphId, out float advanceWidth)` | `true` with the advance width in glyph space. It comes from `hmtx`, or from the charstring of a CFF font without `hmtx`, or from the `hsbw` or `sbw` of a Type 1 charstring. |
| `TryGetGlyphId(ReadOnlySpan<char> glyphName, out ushort glyphId)` | `true` when a glyph has the name. The comparison is ordinal, and the lowest glyph ID wins. Always `false` for a CID-keyed font. |
| `GetGlyphName(ushort glyphId)` | The name of the glyph, or `null` when the glyph has no name or the ID is out of range. |
| `TryGetGlyphIdForCid(int cid, out ushort glyphId)` | `true` when the font has a glyph for the CID. A CID-keyed font maps the CID through its charset. Any other font maps a CID to the glyph with the same ID. |
| `TryGetGlyphIdFromCmap(ushort platformId, ushort encodingId, int code, out ushort glyphId)` | `true` when the `cmap` subtable of the platform and encoding maps `code` to a glyph other than glyph 0. Reads subtable formats 0, 2, 4, 6, 10, 12 and 13. Uses the first record when more than one has the same platform and encoding. Always `false` for a bare CFF program and a Type 1 program. |
| `TryGetBuiltInEncoding(Span<ushort> glyphIds)` | `true` when the font has an encoding. It writes the glyph ID of each of the 256 codes, or 0 for a code without a glyph. `false`, with nothing written, for a CID-keyed font, a TrueType font without a (3, 0) or (1, 0) `cmap` subtable, and a Type 1 font without an `/Encoding`. A span with fewer than 256 elements throws `ArgumentException`. |

After a program loads, no method throws for malformed font data. Only `GetGlyphName` allocates, for the string
that it returns.

### Limits

These limits apply to one glyph:

| Limit | Value |
| --- | --- |
| Charstring operand stack | CFF: 48 operands. Type 1: 24 operands. A charstring that pushes more fails. |
| Subroutine nesting | CFF and Type 1: 10 levels. |
| Charstring operators | CFF: 1,048,576. Type 1: 100,000. The count includes the operators of subroutines and of the two components of a `seac` glyph. A `seac` component cannot be a `seac` glyph. |
| Composite glyphs | TrueType: 16 levels of nesting, and 4,096 components for each outline. |
| Points | TrueType and CFF: 1,000,000 calls to the sink for each outline. Type 1: at most 300,000. |

These limits apply to the font data:

| Limit | Value |
| --- | --- |
| CFF INDEX | At most 65,535 objects. `Load` refuses an INDEX whose offsets are past the end of the table. |
| CFF DICT | At most 48 operands for each operator. A real number has at most 64 characters. |
| CFF Font DICTs | At most 256 are read, because FDSelect selects a Font DICT with one byte. |
| Glyph count | TrueType: the smaller of the `maxp` count and the `loca` entries less one. CFF: the CharStrings INDEX count. Type 1: the `/CharStrings` entries in the data, at most 65,536. A Type 1 glyph name has at most 255 bytes. |
| Type 1 subroutines | The declared `/Subrs` count is at most the length of the decrypted private part. |

`RetainedByteCount` is at most the data length for TrueType, 3 times the data length plus 32 KiB for CFF, and
12 times the data length plus 32 KiB for Type 1. The objects of the program itself are not counted. They take
less than 320 bytes on 64-bit CoreCLR.

## FontProgramOptions

`public sealed class FontProgramOptions`

| Property | Type | Description |
| --- | --- | --- |
| `MaxRetainedBytes` | `long?` | The most bytes that the program may retain, as `RetainedByteCount` counts them, or `null` for no limit. `Load` throws `FontProgramTooLargeException` instead of retaining more. A negative value throws `ArgumentOutOfRangeException`. |

`Load` checks the limit before it parses anything and before each allocation.

## FontProgramFormat

`public enum FontProgramFormat`

| Value | Description |
| --- | --- |
| `TrueType` | Quadratic outlines in a `glyf` table. |
| `Cff` | Cubic outlines in Type 2 charstrings, from an sfnt `CFF ` table or a bare CFF font. |
| `Type1` | Cubic outlines in Type 1 charstrings, from a PFB file, a PFA file or a PDF `/FontFile` stream. |

## GlyphPaintType

`public enum GlyphPaintType`

| Value | Description |
| --- | --- |
| `Filled` | Closed contours to fill. Paint type 0 of Type 1 and CFF, and the only type of TrueType. |
| `Stroked` | Centrelines to stroke with `StrokeWidth`, and not to fill. Paint type 2 of Type 1 and CFF. |

## OptionalFontTables

`[Flags] public enum OptionalFontTables`

The sfnt tables that a font program does not need to read outlines.

| Value | Table |
| --- | --- |
| `None` | No table. |
| `Name` | `name`. A font program never reads it. |
| `Os2` | `OS/2`. A font program never reads it. |
| `Post` | `post`, which gives the glyph names of a TrueType font. |
| `Cmap` | `cmap`, which gives the built-in encoding of a TrueType font. |
| `Hhea` | `hhea`, which says how many advance widths `hmtx` holds. |
| `Hmtx` | `hmtx`, which holds the advance widths. |

A table is malformed when its header is truncated, or when the structure that reading it needs does not fit in
its data. `hhea` is malformed when it is shorter than 36 bytes or says that the font has no metrics, and `hmtx`
when it is shorter than one metric. A font has advance widths from these tables only when both are usable.

## IGlyphOutlineSink

`public interface IGlyphOutlineSink`

Receives one glyph outline as path commands in glyph space, with y up. Each contour is a `MoveTo`, then
segments, then a `ClosePath`.

| Method | Description |
| --- | --- |
| `MoveTo(Vector2 point)` | Starts a contour at `point`. |
| `LineTo(Vector2 point)` | Adds a straight segment to `point`. |
| `QuadraticBezierTo(Vector2 control, Vector2 point)` | Adds a quadratic Bézier segment. TrueType outlines use it. |
| `CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)` | Adds a cubic Bézier segment. CFF and Type 1 outlines use it. |
| `ClosePath()` | Closes the contour. |

## FontSubsetter

`public static class FontSubsetter`

Cuts a font down to the glyphs that a document uses. Every glyph keeps the glyph ID that it had.

| Method | Returns |
| --- | --- |
| `static Subset(ReadOnlyMemory<byte> font, IReadOnlyCollection<ushort> glyphIds, FontSubsetOptions? options = null)` | The subset font file, in the format of `font`. |

The formats are an sfnt file with `glyf` and `loca` tables, an sfnt file with a `CFF ` table, and a bare CFF
font. Type 1, CFF2, WOFF, WOFF2 and font collections are not supported.

- Glyph 0 is always kept. The subset also keeps the components of composite TrueType glyphs, the base and the
  accent of CFF `seac` glyphs, and the subroutines that the kept CFF glyphs call.
- A dropped glyph stays in the font with no outline: a lone `endchar` in CFF, and no data in `glyf`.
- A subroutine that no kept glyph calls keeps its place in its INDEX, with a lone `return` for a body.
- A CFF subset keeps the charset, the encoding, FDSelect and the strings, so glyph names and CIDs do not change.
- An sfnt subset has the tables of `FontSubsetOptions.Tables`, the checksum of each table, and the `head`
  checksum adjustment.

`Subset` does not change `font`, and it does not read the embedding permissions of the font.

| Exception | Thrown when |
| --- | --- |
| `ArgumentNullException` | `glyphIds` is `null`. |
| `ArgumentOutOfRangeException` | A glyph ID is not the ID of a glyph of the font. |
| `ArgumentException` | A table tag in `options` is not 4 printable ASCII characters. |
| `InvalidFontFileException` | The data is malformed or not in a supported format, or the data of a kept glyph is malformed. |
| `FontProgramTooLargeException` | The subsetter would retain more than the `MaxRetainedBytes` of `options.ProgramOptions`. |

## FontSubsetOptions

`public sealed class FontSubsetOptions`

| Property | Type | Description |
| --- | --- | --- |
| `static DefaultTables` | `IReadOnlyCollection<string>` | The tables that a subset keeps when `Tables` is `null`: `CFF `, `OS/2`, `cmap`, `cvt `, `fpgm`, `glyf`, `head`, `hhea`, `hmtx`, `loca`, `maxp`, `name`, `post` and `prep`. A subset keeps only the outline tables of its own format. |
| `Tables` | `IReadOnlyCollection<string>?` | The tags of the tables to keep, each of 4 characters. A tag that the font does not have is ignored. A tag that you name is kept, whatever the format of the outlines. `null` keeps `DefaultTables`. The subset always keeps the outline tables, `head` and `maxp`. It does not apply to a bare CFF font. |
| `ProgramOptions` | `FontProgramOptions?` | The memory limit of the subsetter, or `null` for no limit. It counts the length of the font data, the arrays that the subsetter allocates, and the array that `Subset` returns. |
