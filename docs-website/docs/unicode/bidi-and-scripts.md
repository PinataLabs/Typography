---
title: Bidirectional text and scripts
description: Split text into paragraphs, resolve its direction with the Unicode Bidirectional Algorithm, order the runs of a line, read the script of a character, and itemize text into runs of one script and one direction.
---

`TextLayout` orders right-to-left and mixed text for you. If you write your own layout, the
namespace `PinataLabs.Typography.Unicode` gives you the same steps:

- `BidiParagraph` runs the [Unicode Bidirectional Algorithm](https://www.unicode.org/reports/tr9/)
  (UAX #9) and gives the runs of each line in visual order.
- `CodePoint.GetScriptClass` gives the script of a character.
- `TextItemizer` cuts text into runs of one script and one direction, which is what a shaper takes.

All indexes on this page are UTF-16 `char` indexes. The positions that
[`LineBreakEnumerator`](./line-breaking.md#convert-a-position-to-a-char-index) gives count code
points, so convert them before you pass a line to these methods.

## Resolve the paragraphs of a text

`BidiParagraph.ResolveAll` splits a text into paragraphs and resolves each one on its own. A
paragraph ends after each paragraph separator (rule P1): LF, CR, CR LF as one separator, U+0085,
U+2029 and U+001C to U+001E. The separator is the last `char` of its paragraph.

```csharp
using PinataLabs.Typography.Unicode;

string text = "abc\nكتب abc";

foreach (BidiParagraph paragraph in BidiParagraph.ResolveAll(text, BidiParagraphDirection.Auto))
{
    Console.WriteLine($"{paragraph.Start} {paragraph.Length} {paragraph.ParagraphLevel}");
}
// 0 4 0
// 4 7 1
```

`BidiParagraphDirection` sets the direction of every paragraph:

| Value | Paragraph level |
| --- | --- |
| `LeftToRight` | 0. |
| `RightToLeft` | 1. |
| `Auto` | From the first strong character of each paragraph, or 0 when the paragraph has none (rules P2 and P3). |

In the example, the first paragraph starts with `a`, so it is left to right. The second starts with
an Arabic letter, so it is right to left.

A `BidiParagraph` has these members:

| Member | Description |
| --- | --- |
| `Start` | The index of the first `char` of the paragraph in the text. |
| `Length` | The number of `char` values in the paragraph, the separator included. |
| `ParagraphLevel` | The embedding level of the paragraph. It is even for left to right and odd for right to left. |
| `Levels` | The embedding level of each `char` of the paragraph, in logical order. |
| `GetVisualRuns(lineStart, lineLength)` | The runs of one line of the paragraph, in visual order. |

The paragraph keeps a copy of the text, so the span that you pass does not have to stay alive.

### Resolve one paragraph

When you know that the text is one paragraph, `BidiParagraph.Resolve` returns it. A paragraph
separator can only be the last `char`, or CR LF the last two.

```csharp
BidiParagraph paragraph = BidiParagraph.Resolve("abc كتب 123", BidiParagraphDirection.LeftToRight);
```

`Resolve` throws `ArgumentException` when the text holds more than one paragraph, such as
`"abc\nكتب"`. Use `ResolveAll` for text that can contain line feeds.

## Order the runs of a line

A paragraph that you wrap has more than one line. Rule L1 of the algorithm applies to each line,
so the order depends on where the line breaks. Break the paragraph into lines first, then call
`GetVisualRuns` for each line. Pass the start and the length of the line as indexes in the text, not
in the paragraph.

```csharp
string line = "abc كتب 123";
BidiParagraph paragraph = BidiParagraph.Resolve(line, BidiParagraphDirection.LeftToRight);

foreach (BidiLevelRun run in paragraph.GetVisualRuns(0, line.Length))
{
    Console.WriteLine($"{run.Start} {run.Length} {run.Level} {run.IsRightToLeft}");
}
// 0 4 0 False   "abc "
// 8 3 2 False   "123"
// 4 4 1 True    "كتب "
```

The runs come leftmost first. The characters in a run are in logical order. Draw the characters of
a run with `IsRightToLeft` set from right to left. In the example, the Arabic word and the number
after it form one right-to-left stretch, so the number is drawn to the left of the word.

Rule L1 gives the whitespace at the end of the line the paragraph level. A space at the end of a
wrapped line is therefore drawn at the end of the line in the paragraph direction, not inside the
run before it.

### Characters that the algorithm removes

Rule X9 removes the embedding and override controls, such as U+202B RIGHT-TO-LEFT EMBEDDING, and
other characters of bidi class BN, such as U+00AD SOFT HYPHEN, from the ordering. Each still has an
entry in `Levels`, but it is in no run, and it ends the run before it.

U+200C ZERO WIDTH NON-JOINER and U+200D ZERO WIDTH JOINER are the exceptions. A shaper must see them
next to the letters they join, so they stay in a run. A joiner goes with the character before it.
When the direction changes at the joiner and the character after it joins to it, as an Arabic letter
does, the joiner goes with the run after it. A joiner at the start of a line goes with the character
after it.

## Read the script of a character

`CodePoint.GetScriptClass` gives the Script property of a character, as
[UAX #24](https://www.unicode.org/reports/tr24/) defines it. The `GetIso15924Code` extension method
gives the four-letter ISO 15924 code of a script.

```csharp
ScriptClass arabic = CodePoint.GetScriptClass(new CodePoint('ك')); // ScriptClass.Arabic
string code = arabic.GetIso15924Code();                            // "Arab"

ScriptClass digit = CodePoint.GetScriptClass(new CodePoint('1'));  // ScriptClass.Common
string common = digit.GetIso15924Code();                           // "Zyyy"
```

Digits, spaces and most punctuation are in the `Common` script. Combining marks are mostly in the
`Inherited` script (`Zinh`). A code point that no script owns, such as an unassigned one, is
`Unknown` (`Zzzz`). The method does not read the Script_Extensions property.

The OpenType script tags that a font uses can differ from the ISO 15924 code.
`OpenTypeScriptTags.GetTags`, in the namespace `PinataLabs.Typography.Tables.AdvancedTypographic`,
gives the tags in the order that the library looks for them in a font:

```csharp
using PinataLabs.Typography.Tables.AdvancedTypographic;

ReadOnlySpan<Tag> tags = OpenTypeScriptTags.GetTags(ScriptClass.Devanagari); // dev3, dev2, deva
```

## Itemize text into runs for a shaper

A shaper takes text of one script and one direction at a time. `TextItemizer.Itemize` resolves the
paragraphs of a text, cuts the bidi runs at each change of script, and returns the runs in visual
order:

```csharp
string text = "Hello مرحبا 2026 Привет";

foreach (ItemizedRun run in TextItemizer.Itemize(text, BidiParagraphDirection.Auto))
{
    Console.WriteLine($"{run.Start} {run.Length} {run.Script} {run.Level}");
}
// 0 6 Latin 0      "Hello "
// 12 4 Common 2    "2026"
// 6 6 Arabic 1     "مرحبا "
// 16 7 Cyrillic 0  " Привет"
```

Each `ItemizedRun` has `Start`, `Length`, `Script`, `Level` and `IsRightToLeft`. A run that has no
letters, such as `2026`, has the script `Common`.

This overload treats each paragraph as one line. For wrapped text, resolve the paragraphs with
`BidiParagraph.ResolveAll`, break them into lines, and itemize each line:

```csharp
ItemizedRun[] runs = TextItemizer.Itemize(paragraph, lineStart, lineLength);
```

The script of a space comes from the bidi run that the space is drawn in, not from the whole
paragraph. In the example, the space between `مرحبا` and `2026` is drawn inside the right-to-left
stretch, so it is part of the Arabic run.

The runs cover the text, except for the characters that rule X9 removes. U+200C and U+200D are
included.

To shape the runs into glyphs, see [Shape text](../text/shaping-text.md).

## See also

- [Unicode types](../api/unicode.md#bidiparagraph) for the API reference.
- [International text](../text/international-text.md#right-to-left-and-bidirectional-text) for how
  `TextLayout` applies the algorithm.
