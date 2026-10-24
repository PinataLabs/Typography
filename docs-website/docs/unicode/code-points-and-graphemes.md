---
title: Code points and graphemes
description: Count and enumerate Unicode code points and grapheme clusters, and read Unicode properties such as line-break class, bidi class and script.
---

The library includes the Unicode data that it uses for layout. The namespace
`PinataLabs.Typography.Unicode` makes some of it public, so you can use the same rules in your own
code. This is useful, for example, to compute grapheme indexes for a
[`TextRun`](../text/rich-text.md), or to move a caret one character at a time.

## Three ways to count

A .NET `string` is a sequence of UTF-16 code units (`char`). A code point outside the Basic
Multilingual Plane, such as most emoji, uses two of them. A grapheme, what a reader sees as one
character, can be several code points.

```csharp
using PinataLabs.Typography.Unicode;

// e + combining acute accent, x, thumbs up + medium skin tone
string text = "e\u0301x\U0001F44D\U0001F3FD";

int chars = text.Length;                  // 7
int codePoints = text.GetCodePointCount(); // 5
int graphemes = text.GetGraphemeCount();   // 3
```

The counting methods are extension methods for `string`, `Span<char>` and `ReadOnlySpan<char>`.

## Enumerate graphemes and code points

`EnumerateGraphemes` returns each grapheme as a `ReadOnlySpan<char>`. `EnumerateCodePoints` returns
each `CodePoint`. Neither allocates.

```csharp
foreach (ReadOnlySpan<char> grapheme in text.AsSpan().EnumerateGraphemes())
{
    Console.WriteLine(grapheme.ToString());
}

foreach (CodePoint codePoint in text.AsSpan().EnumerateCodePoints())
{
    Console.WriteLine($"U+{codePoint.Value:X4}");
}
```

Graphemes follow the extended grapheme cluster rules of
[UAX #29](https://www.unicode.org/reports/tr29/).

### Find the grapheme index of a `char` index

`TextRun.Start` and `TextRun.End` count graphemes. To convert from a `char` index, count the
graphemes before it:

```csharp
static int GraphemeIndex(string text, int charIndex)
    => text.AsSpan(0, charIndex).GetGraphemeCount();

string text = "Café 👍🏽 ok";
int start = GraphemeIndex(text, text.IndexOf("ok", StringComparison.Ordinal)); // 7
```

This works when `charIndex` is at the start of a grapheme.

## The CodePoint type

`CodePoint` is a value type for one Unicode scalar value. Make one from a `char`, a surrogate pair or
an `int`:

```csharp
CodePoint a = new('A');
CodePoint smile = new(0x1F600);
CodePoint fromPair = new('\uD83D', '\uDE00'); // Also U+1F600.

Console.WriteLine(smile.IsBmp);               // False
Console.WriteLine(smile.Utf16SequenceLength); // 2
Console.WriteLine(CodePoint.IsValid(0xD800)); // False: a lone surrogate is not a scalar value.
```

`CodePoint.ReplacementChar` is U+FFFD.

## Unicode properties

Static methods on `CodePoint` read the Unicode properties of a code point:

| Method | Returns |
| --- | --- |
| `GetGeneralCategory` | The general category, as `System.Globalization.UnicodeCategory`. |
| `GetBidiClass` | The bidirectional class and paired-bracket data, as a `BidiClass`. |
| `GetLineBreakClass` | The line-break class from UAX #14, as a `LineBreakClass`. |
| `GetGraphemeClusterClass` | The grapheme-break property from UAX #29, as a `GraphemeClusterClass`. |
| `GetVerticalOrientationType` | The vertical orientation from UAX #50, as a `VerticalOrientationType`. |
| `TryGetBidiMirror` | The mirrored character, for example `)` for `(`. |
| `TryGetVerticalMirror` | The vertical form of a character, if it has one. |

And tests:

`IsLetter`, `IsDigit`, `IsLetterOrDigit`, `IsNumber`, `IsLower`, `IsUpper`, `IsMark`,
`IsPunctuation`, `IsSeparator`, `IsSymbol`, `IsControl`, `IsWhiteSpace`, `IsNonBreakingSpace`,
`IsNewLine`, `IsTabulation`, `IsVariationSelector`, `IsZeroWidthJoiner`, `IsZeroWidthNonJoiner`.

```csharp
CodePoint hyphen = new('-');
LineBreakClass lineBreak = CodePoint.GetLineBreakClass(hyphen); // LineBreakClass.HY

BidiClass bidi = CodePoint.GetBidiClass(new CodePoint('('));
if (bidi.TryGetPairedBracket(out CodePoint closing))
{
    Console.WriteLine(closing); // )
}
```

`new ArabicJoiningClass(codePoint)` gives the Arabic joining type and joining group of a code
point. The namespace also has the `ScriptClass`, `IndicSyllabicCategory` and
`IndicPositionalCategory` enumerations. The shapers use them inside the library; there is no public
method that returns them for a code point. See [Unicode types](../api/unicode.md).
