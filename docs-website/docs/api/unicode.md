---
title: Unicode types
description: API reference for CodePoint, UnicodeInfo, the span enumerators, line breaking, MemoryExtensions and the Unicode property types in PinataLabs.Typography.Unicode.
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
[Line breaks](../unicode/code-points-and-graphemes.md#line-breaks).

### LineBreakTailoring

`[Flags] public enum LineBreakTailoring`

| Value | Description |
| --- | --- |
| `None` | The algorithm as UAX #14 publishes it. This is the default. |
| `SouthEastAsianAsIdeographs` | The letters of class SA (Thai, Lao, Khmer, Myanmar) break as ideographs do. SA marks stay combining marks. |
| `HangulAsAlphabetic` | Hangul syllables and jamo break as letters do, so Korean breaks at its spaces. |

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
