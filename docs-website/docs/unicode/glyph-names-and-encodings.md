---
title: Glyph names and encodings
description: Map the character codes of a PDF simple font to glyph names through the standard encodings, and map glyph names to text and back with the Adobe Glyph List.
---

A PDF simple font, such as a Type 1 or TrueType font with an `/Encoding`, selects a glyph by a one-byte
character code. The encoding gives the code a glyph name, and the Adobe Glyph List gives the name its text.
Two static classes in `PinataLabs.Typography.Unicode` hold these tables:

- `GlyphNameEncodings` maps a character code to a glyph name in a standard encoding, and back.
- `AdobeGlyphList` maps a glyph name to text, and a code point to a glyph name.

Both are in the `PinataLabs.Typography.Unicode` package, which has no font code. The tables are sorted arrays,
so the library builds no dictionary at start-up.

## Get the glyph name of a code

`GlyphNameEncodings.GetGlyphName` gives the glyph name that a code selects in a `SimpleFontEncoding`:

```csharp
using PinataLabs.Typography.Unicode;

string? name = GlyphNameEncodings.GetGlyphName(SimpleFontEncoding.WinAnsi, 0x80); // "Euro"
string? quote = GlyphNameEncodings.GetGlyphName(SimpleFontEncoding.Standard, 0x27); // "quoteright"
```

The method returns `null` when the encoding gives the code no glyph.

| `SimpleFontEncoding` | The encoding |
| --- | --- |
| `Standard` | The Adobe StandardEncoding, the default encoding of a Type 1 font. |
| `WinAnsi` | WinAnsiEncoding, which is Windows code page 1252. |
| `MacRoman` | MacRomanEncoding, the Mac OS Roman character set. |
| `MacExpert` | MacExpertEncoding: the small capitals, old-style figures and fractions of an expert font. |
| `Symbol` | The built-in encoding of the Symbol font. |
| `ZapfDingbats` | The built-in encoding of the ZapfDingbats font. |

The tables follow ISO 32000-1 Annex D, and also its notes:

- In `WinAnsi`, every unused code above 0x20 is `bullet`, and 0xA0 and 0xAD are `space` and `hyphen`.
- In `MacRoman`, 0xCA is `space`. `MacRoman` also has the 15 symbols of Mac OS Roman that Annex D leaves out,
  such as `notequal` and `infinity`, because PDF readers resolve them.
- `ZapfDingbats` has the codes 0x80 to 0x8D of the built-in encoding of the font.

To apply a `/Differences` array, start from the glyph names of the base encoding and replace the names that
the array gives.

## Get the code of a glyph name

`GlyphNameEncodings.TryGetCode` is the other way. Use it to write text in a standard encoding:

```csharp
if (GlyphNameEncodings.TryGetCode(SimpleFontEncoding.WinAnsi, "space", out byte code))
{
	Console.WriteLine($"0x{code:X2}"); // 0x20
}
```

When more than one code selects the glyph, as 0x20 and 0xA0 select `space` in `WinAnsi`, you get the lowest
code. The method compares the name as it is, so it does not resolve a suffix such as `.alt` or a `uniXXXX`
name.

An encoding that `SimpleFontEncoding` does not define makes both methods throw
`ArgumentOutOfRangeException`.

## Get the text of a glyph name

`AdobeGlyphList.TryGetUnicode` maps a glyph name to text, as the Adobe Glyph List Specification says. Use it to
extract text from a PDF:

```csharp
if (AdobeGlyphList.TryGetUnicode("Euro", out string text))
{
	Console.WriteLine(text); // €
}
```

The method reads a name in these steps:

1. It drops the part after the first period, so `a.alt` maps as `a`.
2. It splits the rest at each underscore into components, so `f_f_i` maps to `ffi`.
3. It maps each component through the first form that applies:
   - A name in the Adobe Glyph List, such as `Euro`.
   - `uni` and one or more groups of four uppercase hexadecimal digits, such as `uni20AC`.
   - `u` and four to six uppercase hexadecimal digits, such as `u1F600`.

Lowercase digits, as in `uni20ac`, and surrogate code points do not map.

The method returns `false` when a component has no text, such as the `oops` of `f_oops`. The specification
maps such a component to the empty string. The method returns `false` instead, so that you can use another
source, such as the `cmap` table of the font.

A few names map to a compatibility character, as the list gives them: `Omega` is U+2126 OHM SIGN, `Delta` is
U+2206 INCREMENT, and `mu` is U+00B5 MICRO SIGN. `Omegagreek`, `Deltagreek` and `mugreek` are the Greek letters.
To get the letters, normalize the text. U+2126 becomes U+03A9 under NFC, and U+00B5 becomes U+03BC under NFKC.
U+2206 has no normalization to U+0394.

### Names of the ZapfDingbats font

The ZapfDingbats font names its glyphs `a1` to `a206`, and these names are not in the Adobe Glyph List. For a
ZapfDingbats font, use `TryGetZapfDingbatsUnicode`. It reads the ITC Zapf Dingbats glyph list first, then
maps as `TryGetUnicode` does:

```csharp
AdobeGlyphList.TryGetUnicode("a1", out _);                         // false
AdobeGlyphList.TryGetZapfDingbatsUnicode("a1", out string dingbat); // true, U+2701
```

## Get the glyph name of a code point

`AdobeGlyphList.GetGlyphName` gives the preferred glyph name of a code point. Use it to write the names of a
`/Differences` array:

```csharp
AdobeGlyphList.GetGlyphName(0x20AC);  // "Euro"
AdobeGlyphList.GetGlyphName(0x4E00);  // "uni4E00"
AdobeGlyphList.GetGlyphName(0x1F600); // "u1F600"
```

The name is the one that the Adobe Glyph List for New Fonts (`aglfn.txt`) gives the code point. A code point
without such a name gets `uniXXXX` in the Basic Multilingual Plane, and `uXXXXX` or `uXXXXXX` outside it, with
uppercase digits. A surrogate, or a value outside 0 to 0x10FFFF, throws `ArgumentOutOfRangeException`.

## Use the names with a font program

A [`FontProgram`](../fonts/font-programs.md) finds a glyph by name. To draw code 0x80 of a WinAnsi font:

```csharp
using PinataLabs.Typography;
using PinataLabs.Typography.Unicode;

string? name = GlyphNameEncodings.GetGlyphName(SimpleFontEncoding.WinAnsi, 0x80);
if (name is not null && program.TryGetGlyphId(name, out ushort glyphId))
{
	program.TryWriteOutline(glyphId, ref path);
}
```

## Related pages

- [Font programs](../fonts/font-programs.md).
- [Unicode types API reference](../api/unicode.md#glyph-names).
