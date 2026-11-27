---
title: Unicode types
description: API reference for CodePoint, UnicodeInfo, the span enumerators, line breaking, MemoryExtensions, the Unicode property types and the glyph name tables in PinataLabs.Typography.Unicode.
---

Namespace: `PinataLabs.Typography.Unicode`. Guide:
[Code points and graphemes](../unicode/code-points-and-graphemes.md).

## CodePoint

`public readonly struct CodePoint : IComparable, IComparable<CodePoint>, IEquatable<CodePoint>`

One Unicode scalar value.

### Make a code point

| Member | Description |
| --- | --- |
| `CodePoint(char value)` | From a `char` that is not a surrogate. |
| `CodePoint(char highSurrogate, char lowSurrogate)` | From a surrogate pair. |
| `CodePoint(int value)`, `CodePoint(uint value)` | From a number. Must be a valid scalar value. |
| explicit conversions from `char`, `int`, `uint` | The same, as casts. |
| `static ReplacementChar` | U+FFFD REPLACEMENT CHARACTER. |
| `static IsValid(int value)`, `static IsValid(uint value)` | `true` if the number is a Unicode scalar value (not a surrogate, not above U+10FFFF). |

### Properties

| Property | Description |
| --- | --- |
| `Value` | The number of the code point. |
| `IsAscii` | `true` for U+0000 to U+007F. |
| `IsBmp` | `true` for the Basic Multilingual Plane, U+0000 to U+FFFF. |
| `Plane` | The Unicode plane, 0 to 16. |
| `Utf16SequenceLength` | The number of `char` values needed: 1 or 2. |
| `Utf8SequenceLength` | The number of UTF-8 bytes needed: 1 to 4. |

`CodePoint` supports `==`, `!=`, `<`, `<=`, `>`, `>=` and `CompareTo`. `ToString()` returns the
character.

### Static methods

| Method | Returns |
| --- | --- |
| `GetCodePointCount(ReadOnlySpan<char> source)` | The number of code points in `source`. |
| `GetGeneralCategory(CodePoint)` | The general category, as `UnicodeCategory`. |
| `GetBidiClass(CodePoint)` | A `BidiClass`. |
| `GetLineBreakClass(CodePoint)` | A `LineBreakClass`. |
| `GetGraphemeClusterClass(CodePoint)` | A `GraphemeClusterClass`. |
| `GetVerticalOrientationType(CodePoint)` | A `VerticalOrientationType`. |
| `TryGetBidiMirror(CodePoint, out CodePoint mirror)` | The mirrored character for right-to-left text. |
| `TryGetVerticalMirror(CodePoint, out CodePoint mirror)` | The vertical form of the character. |
| `IsLetter`, `IsDigit`, `IsLetterOrDigit`, `IsNumber`, `IsLower`, `IsUpper`, `IsMark`, `IsPunctuation`, `IsSeparator`, `IsSymbol`, `IsControl`, `IsWhiteSpace` | Tests by general category. |
| `IsNonBreakingSpace`, `IsNewLine`, `IsTabulation`, `IsVariationSelector`, `IsZeroWidthJoiner`, `IsZeroWidthNonJoiner` | Tests for specific characters. |

## UnicodeInfo

`public static class UnicodeInfo`

| Member | Description |
| --- | --- |
| `static string Version` | The version of the Unicode Character Database that the character properties are built from, such as `18.0.0`. |

`Version` is a property and not a constant, so a caller reads the version of the library that is
loaded, not the one it was compiled against. If you test against the Unicode conformance suites,
pin them to this version.

## MemoryExtensions

`public static class MemoryExtensions`

| Method | For | Returns |
| --- | --- | --- |
| `EnumerateCodePoints()` | `Span<char>`, `ReadOnlySpan<char>` | A `SpanCodePointEnumerator`. |
| `EnumerateGraphemes()` | `Span<char>`, `ReadOnlySpan<char>` | A `SpanGraphemeEnumerator`. |
| `GetCodePointCount()` | `string`, `Span<char>`, `ReadOnlySpan<char>` | The number of code points. |
| `GetGraphemeCount()` | `string`, `Span<char>`, `ReadOnlySpan<char>` | The number of graphemes. |

This class has the same name as `System.MemoryExtensions`. The extension methods do not conflict,
because their names are different. If you need to name the class, write it in full.

## SpanCodePointEnumerator and SpanGraphemeEnumerator

`public ref struct SpanCodePointEnumerator`
`public ref struct SpanGraphemeEnumerator`

Enumerators for `foreach`. They do not allocate. `SpanCodePointEnumerator.Current` is a
`CodePoint`; `SpanGraphemeEnumerator.Current` is a `ReadOnlySpan<char>`. Both can also be made
directly from a `ReadOnlySpan<char>`.

## LineBreakEnumerator, LineBreak and LineBreakAlgorithm

`public ref struct LineBreakEnumerator`
`public readonly struct LineBreak`
`public static class LineBreakAlgorithm`

The line breaking algorithm of UAX #14. `new LineBreakEnumerator(text, tailoring)` is an enumerator for
`foreach` over the break opportunities of a `ReadOnlySpan<char>`. It does not allocate. The tailoring is
optional.

| `LineBreak` member | Description |
| --- | --- |
| `PositionWrap` | The index of the code point that starts the next line. It counts code points, not `char` values. |
| `PositionMeasure` | The index to measure the line to: `PositionWrap` without the spaces before it. |
| `Required` | `true` for a mandatory break, such as after a line feed. |

The last opportunity is at the end of the text (LB3), so a text that is not empty gives at least one.

`LineBreakAlgorithm.IsBreakAllowedBetween(before, after, tailoring)` answers a question about one
boundary: is a break allowed where `before` ends and `after` begins? It reads the two spans as one
text, so every rule that reaches across the boundary applies. The result is the one the enumerator
gives at that position of the joined text. See
[Line breaking](../unicode/line-breaking.md).

### LineBreakTailoring

`[Flags] public enum LineBreakTailoring`

| Value | Description |
| --- | --- |
| `None` | The algorithm as UAX #14 publishes it. This is the default. |
| `SouthEastAsianAsIdeographs` | The letters of class SA (Thai, Lao, Khmer, Myanmar) break as ideographs do. SA marks stay combining marks, and so do the Thai and Lao vowels written after their consonant (U+0E30, U+0E32, U+0E33, U+0E45, U+0EB0, U+0EB2, U+0EB3, U+0EBD). No break follows a Thai or Lao vowel written before its consonant (U+0E40 to U+0E44, U+0EC0 to U+0EC4). |
| `HangulAsAlphabetic` | Hangul syllables, conjoining jamo and compatibility jamo (U+3131 to U+318E, U+FFA0 to U+FFDC) break as letters do, so Korean breaks at its spaces. |

A tailoring changes the class of the characters it names and of no other.

## Property types

| Type | Kind | Description |
| --- | --- | --- |
| `BidiClass` | struct | `CharacterType` (a `BidiCharacterType`), `PairedBracketType` (a `BidiPairedBracketType`) and `TryGetPairedBracket`. |
| `BidiCharacterType` | enum | The bidirectional character types of UAX #9, such as `LeftToRight`, `RightToLeft`, `ArabicLetter`, `EuropeanNumber`. |
| `BidiPairedBracketType` | enum | `None`, `Open`, `Close`. |
| `LineBreakClass` | enum | The line-break classes of UAX #14, with their short names, such as `AL`, `BA`, `HY`, `ID`, `SP`. |
| `GraphemeClusterClass` | enum | The grapheme-break property values of UAX #29. |
| `VerticalOrientationType` | enum | The vertical orientation of UAX #50: `Upright`, `Rotate`, `TransformUpright`, `TransformRotate`. |
| `ArabicJoiningClass` | struct | Made from a `CodePoint`. Has `JoiningType` and `JoiningGroup`. |
| `ArabicJoiningType` | enum | `RightJoining`, `LeftJoining`, `DualJoining`, `JoinCausing`, `NonJoining`, `Transparent`. |
| `ArabicJoiningGroup` | enum | The Arabic joining groups, such as `Beh`, `Alef`, `Lam`. |
| `ScriptClass` | enum | The Unicode scripts, such as `Latin`, `Arabic`, `Han`. |
| `IndicSyllabicCategory` | enum | The Indic syllabic categories. |
| `IndicPositionalCategory` | enum | The Indic positional categories. |

## BidiParagraph

`public sealed class BidiParagraph`

A paragraph that the Unicode Bidirectional Algorithm (UAX #9) has resolved. Guide:
[Bidirectional text and scripts](../unicode/bidi-and-scripts.md).

All indexes are UTF-16 code unit indexes in the text that was resolved. The two code units of a
surrogate pair share a level. The paragraph holds a copy of the text.

| Member | Description |
| --- | --- |
| `static Resolve(ReadOnlySpan<char> text, BidiParagraphDirection direction)` | Resolves text that is one paragraph. A paragraph separator can only be the last code unit, or CR LF the last two. Throws `ArgumentException` when the text holds more than one paragraph. |
| `static ResolveAll(ReadOnlySpan<char> text, BidiParagraphDirection direction)` | Splits the text into paragraphs by rule P1 and resolves each on its own. Returns a `BidiParagraph[]` in text order. The paragraphs cover the text. Empty text gives one empty paragraph, and a separator at the end of the text gives no empty paragraph after it. |
| `Start` | The index of the first code unit of the paragraph. It is 0 for `Resolve`. |
| `Length` | The number of code units in the paragraph, its paragraph separator included. |
| `ParagraphLevel` | The embedding level of the paragraph, as a `byte`: even for left to right, odd for right to left. |
| `Levels` | A `ReadOnlySpan<byte>` with the level of each code unit of the paragraph, from `Start`, in logical order. Rule L1 is applied to the whole paragraph as one line. |
| `GetVisualRuns(int lineStart, int lineLength)` | The `BidiLevelRun[]` of one line of the paragraph in visual order, leftmost first. Rule L1 is applied to the line, and rule L2 orders the runs. `lineStart` is an index in the text. Throws `ArgumentOutOfRangeException` when the line is not inside the paragraph or splits a surrogate pair. |

The paragraph separators are LF, CR, CR LF as one separator, U+0085, U+2029 and U+001C to U+001E. A
separator is the last code unit of its paragraph. Both methods throw `ArgumentOutOfRangeException`
when `direction` is not a defined value.

Rule X9 removes the embedding and override controls and the characters of class BN from the
ordering. Each still has an entry in `Levels`, but it is in no run and ends the run before it.
U+200C and U+200D stay in a run, with the character before them, except in these cases:

- The level changes at the joiner, and the character after it is right joining, dual joining or
  join causing. The joiner is in the run after it.
- The joiner is at the start of a line, or after a removed character that is not a joiner. The
  joiner is in the run
  of the character after it.
- No character follows the joiner before the end of the line or the next removed character. The
  joiner is a run of its own.

## BidiLevelRun

`public readonly struct BidiLevelRun : IEquatable<BidiLevelRun>`

| Member | Description |
| --- | --- |
| `BidiLevelRun(int start, int length, byte level)` | Makes a run. |
| `Start` | The index of the first code unit of the run in the text, in logical order. |
| `Length` | The number of code units in the run. |
| `Level` | The embedding level of the run. |
| `IsRightToLeft` | `true` when `Level` is odd. Draw the characters of the run from right to left. |

## BidiParagraphDirection

`public enum BidiParagraphDirection`

| Value | Description |
| --- | --- |
| `LeftToRight` | Paragraph level 0 (rule HL1). |
| `RightToLeft` | Paragraph level 1 (rule HL1). |
| `Auto` | The direction of the first strong character of the paragraph, or level 0 when it has none (rules P2 and P3). |

## TextItemizer and ItemizedRun

`public static class TextItemizer`
`public readonly struct ItemizedRun : IEquatable<ItemizedRun>`

`TextItemizer` cuts text into runs of one script and one direction, in visual order. The runs are
those of `BidiParagraph.GetVisualRuns`, cut at each change of script. The script is found inside
each bidi run. A run that is right to left and holds several scripts gives its pieces right to
left.

| Method | Returns |
| --- | --- |
| `Itemize(ReadOnlySpan<char> text, BidiParagraphDirection direction)` | The runs of each paragraph of the text as one line, in visual order, and the paragraphs in text order. Throws `ArgumentOutOfRangeException` for a direction that is not defined. |
| `Itemize(BidiParagraph paragraph, int lineStart, int lineLength)` | The runs of one line of a resolved paragraph, in visual order. Throws `ArgumentNullException` for a `null` paragraph and `ArgumentOutOfRangeException` for a line that is not inside it. |

The runs cover the text, except the characters that rule X9 removes. U+200C and U+200D are
included.

| `ItemizedRun` member | Description |
| --- | --- |
| `ItemizedRun(int start, int length, ScriptClass script, byte level)` | Makes a run. |
| `Start` | The index of the first code unit of the run in the text, in logical order. |
| `Length` | The number of code units in the run. |
| `Script` | The `ScriptClass` of the run. It is `Common` when the run has no letters of any script. |
| `Level` | The embedding level of the run. |
| `IsRightToLeft` | `true` when `Level` is odd. |

## Scripts

| Member | Description |
| --- | --- |
| `static CodePoint.GetScriptClass(CodePoint codePoint)` | The Script property (`sc`) of the code point from `Scripts.txt`, as UAX #24 defines it. A code point that no script owns is `ScriptClass.Unknown`. The Script_Extensions property is not read. |
| `ScriptClassExtensions.GetIso15924Code(this ScriptClass script)` | The four-letter ISO 15924 code of the script, such as `Latn`, `Arab` or `Hebr`: `Zinh` for `Inherited`, `Zyyy` for `Common` and `Zzzz` for `Unknown`. Throws `ArgumentOutOfRangeException` for a value that is not defined. |

`OpenTypeScriptTags.GetTags(ScriptClass script)`, in `PinataLabs.Typography.Tables.AdvancedTypographic`
of the `PinataLabs.Typography` package, gives the OpenType script tags of a script as a
`ReadOnlySpan<Tag>`, in the order that the library looks for them in a font. A script of the Indic or
Myanmar shaper has the newer tag first, such as `dev3`, `dev2` and then `deva`. Some tags differ from
the ISO 15924 code, such as `nko ` for `Nkoo`. Every script has at least one tag.

## Glyph names

Guide: [Glyph names and encodings](../unicode/glyph-names-and-encodings.md).

### AdobeGlyphList

`public static class AdobeGlyphList`

Maps glyph names to text and code points to glyph names, by the Adobe Glyph List and the Adobe Glyph List
Specification.

| Method | Returns |
| --- | --- |
| `static TryGetUnicode(ReadOnlySpan<char> glyphName, out string text)` | `true` with the text of the name when every component of the name has text. The part after the first period is dropped, and the rest is split at underscores into components. Each component is a name of the list, `uni` and groups of four uppercase hexadecimal digits, or `u` and four to six uppercase hexadecimal digits. `false` when a component has no text. |
| `static TryGetZapfDingbatsUnicode(ReadOnlySpan<char> glyphName, out string text)` | As `TryGetUnicode`, except that the names of the ITC Zapf Dingbats glyph list, `a1` to `a206`, map first. |
| `static GetGlyphName(int codePoint)` | The name that `aglfn.txt` gives the code point, such as `Euro` for U+20AC, or else `uniXXXX` in the Basic Multilingual Plane and `uXXXXX` or `uXXXXXX` outside it. A surrogate or a value outside 0 to 0x10FFFF throws `ArgumentOutOfRangeException`. |

`Omega`, `Delta` and `mu` map to U+2126, U+2206 and U+00B5, as the list gives them. `Omegagreek`, `Deltagreek` and
`mugreek` map to the Greek letters.

### GlyphNameEncodings

`public static class GlyphNameEncodings`

Maps the character codes of a PDF simple font to glyph names, through the encodings of ISO 32000-1 Annex D.

| Method | Returns |
| --- | --- |
| `static GetGlyphName(SimpleFontEncoding encoding, byte code)` | The glyph name that the code selects, or `null` when the encoding gives the code no glyph. |
| `static TryGetCode(SimpleFontEncoding encoding, ReadOnlySpan<char> glyphName, out byte code)` | `true` with the code that selects the glyph. When more than one code selects it, the lowest. The name is compared as it is, with no suffix or `uniXXXX` form resolved. |

Both methods throw `ArgumentOutOfRangeException` for an `encoding` that is not a defined value.

In `WinAnsi`, every unused code above 0x20 is `bullet`, and 0xA0 and 0xAD are `space` and `hyphen`. In `MacRoman`,
0xCA is `space`, and the 15 symbols of Mac OS Roman that Annex D leaves out are present. `ZapfDingbats` has the
codes 0x80 to 0x8D of the built-in encoding of the font.

### SimpleFontEncoding

`public enum SimpleFontEncoding`

| Value | Description |
| --- | --- |
| `Standard` | The Adobe StandardEncoding. |
| `WinAnsi` | WinAnsiEncoding, which is Windows code page 1252. |
| `MacRoman` | MacRomanEncoding, the Mac OS Roman character set. |
| `MacExpert` | MacExpertEncoding, the small capitals, old-style figures and fractions of an expert font. |
| `Symbol` | The built-in encoding of the Symbol font. |
| `ZapfDingbats` | The built-in encoding of the ZapfDingbats font. |
