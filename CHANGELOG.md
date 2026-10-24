# Changelog

All notable changes to PinataLabs.Typography are recorded in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

PinataLabs.Typography is a fork of an Apache-2.0 project, taken at commit `32bef429`.
See `NOTICE` for the original project and its copyright.
The entries below describe the changes from that commit.

## Public API changes

The public API is recorded in `src/PinataLabs.Typography/PublicAPI.Shipped.txt`.
`Microsoft.CodeAnalysis.PublicApiAnalyzers` checks it, and the build fails when the
public surface does not match the PublicAPI files.

An additive or breaking public API change needs all of these:

1. A proposal that the maintainer approves.
2. A commit of its own.
3. A matching change to `src/PinataLabs.Typography/PublicAPI.Unshipped.txt`.
4. An entry in this file.

## [Unreleased]

### Added

- **Myanmar shaper.** Myanmar text now has its own shaper, which follows the Microsoft
  OpenType specification for Myanmar. It splits text into syllables, moves the kinzi after the base,
  and moves medial RA and vowel sign E before it. A broken syllable gets a dotted circle.
  Before, Myanmar text had no script-specific processing, so these marks stayed in logical order.
  The shaper uses the `mym2` script tag, and falls back to `mymr`. (#30)
- **CFF2 fonts.** Fonts with a `CFF2` outline table, such as variable OpenType/CFF2 fonts, now load
  and render. They are supported at the default instance only. `blend` operators use their default
  values, and the variation axes (`fvar`, `avar`, `HVAR`, `MVAR`) are ignored. Before, loading
  such a font threw `NotSupportedException`. (#21)

### Changed

- **Rename.** The package and assembly are now `PinataLabs.Typography`.
  Every namespace from the original project is mapped 1:1 to `PinataLabs.Typography[.X]`.
  The type and member names do not change.
- **Retargeting.** The library targets `net8.0`, `net10.0` and `net11.0`.
  The old targets `netcoreapp3.1`, `netstandard2.1` and `netstandard2.0` are removed.
  The package no longer depends on `System.Memory`, `System.Buffers`,
  `System.Numerics.Vectors` or `System.Runtime.CompilerServices.Unsafe`.
- **AppContext switch rename.** The switch that disables native system font enumeration
  is now `Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration`.
  The original project's switch name has no effect.
- **No strong naming.** The assembly is not strong-named. Its public key token is null,
  so code that references it by the old strong name must be rebuilt.
- **Text run end.** `TextRun.End` is now always exclusive. A run whose `End` is one less
  than the text length no longer covers the last character, which now uses the default font.
  Code that set `End` to `length - 1` to work around the old behaviour must set it to `length`. (#22)
- **Slash line breaks.** When a line must wrap and a word contains `/`, the line breaks at an
  earlier space if there is one, not after the slash. A URL now moves to the next line whole.
  A single long word with no earlier break opportunity still breaks after the slash. (#35)
- **Build layout.** The MSBuild settings now live only in the `Directory.Build.props` files,
  and `build/msbuild` and the `SharedInfrastructure` shared project are gone. Build output uses
  the SDK artifacts layout: `artifacts/bin/<Project>/<config>_<tfm>/` and `artifacts/package/<config>/`.
  This does not change the package.
- **Hinting concurrency.** TrueType hinting for one font now runs one glyph at a time.
  Threads that hint the same font wait for each other. (#27)
- **`TextDirection` sets the base direction only.** `LeftToRight` and `RightToLeft` set the
  paragraph direction, as CSS `direction` does. The bidirectional algorithm still runs, so `"abc"`
  with `RightToLeft` stays `abc` and no longer comes out as `cba`. Unicode direction controls now
  work with a fixed direction. `TextAlignment.Start` and `End` follow the paragraph direction. (#40)

### Fixed

- **Tab width.** `TextOptions.TabWidth` now takes effect for fonts that have no glyph for U+0009.
  A tab is `TabWidth` times the advance of the space glyph, in horizontal and vertical layout.
  `-1` still uses the font's tab glyph advance. A font with no space glyph uses a quarter of
  the em as the space width. (#39)
- **Vertical mixed layout.** A `Tr` (transform-rotate) character is laid out upright only when the font
  has a vertical form for it, from the `vert` feature or a cmap presentation form. Other substitutions,
  such as `ccmp` or `locl`, no longer make it upright. A `Tr` character with no vertical form is rotated and
  shaped with the horizontal features, including `kern`. (#49)

### Removed

These public members existed only for the removed targets. They were type forwarders
that the shared-infrastructure polyfills added to the assembly. They forwarded the
`System.*` types to the runtime, which still supplies them on every new target.
Your code can use them from the runtime as before; only the forwarders in this
assembly are gone. The names use the PublicAPI file format.

`System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute`
(forwarded on `netcoreapp3.1`, `netstandard2.1` and `netstandard2.0`):

```text
System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute
System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute.ExcludeFromCodeCoverageAttribute() -> void
```

`System.HashCode` (forwarded on `netcoreapp3.1` and `netstandard2.1`):

```text
System.HashCode
System.HashCode.Add<T>(T value) -> void
System.HashCode.Add<T>(T value, System.Collections.Generic.IEqualityComparer<T>? comparer) -> void
System.HashCode.HashCode() -> void
System.HashCode.ToHashCode() -> int
override System.HashCode.Equals(object? obj) -> bool
override System.HashCode.GetHashCode() -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5, T6, T7, T8>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7, T8 value8) -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5, T6, T7>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6, T7 value7) -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5, T6>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5, T6 value6) -> int
static System.HashCode.Combine<T1, T2, T3, T4, T5>(T1 value1, T2 value2, T3 value3, T4 value4, T5 value5) -> int
static System.HashCode.Combine<T1, T2, T3, T4>(T1 value1, T2 value2, T3 value3, T4 value4) -> int
static System.HashCode.Combine<T1, T2, T3>(T1 value1, T2 value2, T3 value3) -> int
static System.HashCode.Combine<T1, T2>(T1 value1, T2 value2) -> int
static System.HashCode.Combine<T1>(T1 value1) -> int
```

`System.MathF` (forwarded on `netcoreapp3.1` and `netstandard2.1`; `BitDecrement`, `BitIncrement`,
`CopySign`, `FusedMultiplyAdd`, `ILogB`, `Log2`, `MaxMagnitude`, `MinMagnitude` and `ScaleB`
on `netcoreapp3.1` only):

```text
System.MathF
const System.MathF.E = 2.7182817 -> float
const System.MathF.PI = 3.1415927 -> float
static System.MathF.Abs(float x) -> float
static System.MathF.Acos(float x) -> float
static System.MathF.Acosh(float x) -> float
static System.MathF.Asin(float x) -> float
static System.MathF.Asinh(float x) -> float
static System.MathF.Atan(float x) -> float
static System.MathF.Atan2(float y, float x) -> float
static System.MathF.Atanh(float x) -> float
static System.MathF.BitDecrement(float x) -> float
static System.MathF.BitIncrement(float x) -> float
static System.MathF.Cbrt(float x) -> float
static System.MathF.Ceiling(float x) -> float
static System.MathF.CopySign(float x, float y) -> float
static System.MathF.Cos(float x) -> float
static System.MathF.Cosh(float x) -> float
static System.MathF.Exp(float x) -> float
static System.MathF.Floor(float x) -> float
static System.MathF.FusedMultiplyAdd(float x, float y, float z) -> float
static System.MathF.IEEERemainder(float x, float y) -> float
static System.MathF.ILogB(float x) -> int
static System.MathF.Log(float x) -> float
static System.MathF.Log(float x, float y) -> float
static System.MathF.Log10(float x) -> float
static System.MathF.Log2(float x) -> float
static System.MathF.Max(float x, float y) -> float
static System.MathF.MaxMagnitude(float x, float y) -> float
static System.MathF.Min(float x, float y) -> float
static System.MathF.MinMagnitude(float x, float y) -> float
static System.MathF.Pow(float x, float y) -> float
static System.MathF.Round(float x) -> float
static System.MathF.Round(float x, System.MidpointRounding mode) -> float
static System.MathF.Round(float x, int digits) -> float
static System.MathF.Round(float x, int digits, System.MidpointRounding mode) -> float
static System.MathF.ScaleB(float x, int n) -> float
static System.MathF.Sign(float x) -> int
static System.MathF.Sin(float x) -> float
static System.MathF.Sinh(float x) -> float
static System.MathF.Sqrt(float x) -> float
static System.MathF.Tan(float x) -> float
static System.MathF.Tanh(float x) -> float
static System.MathF.Truncate(float x) -> float
```

### Fixed

#### Line breaking and layout

- `TextOptions.TextRuns` is now normalised before layout. `Start` and `End` are clamped to the grapheme
  count, and runs that are empty after clamping are dropped. Overlapping and nested runs no longer throw
  or lose the outer run's font: the run later in the list wins for the overlap and the earlier run
  continues after it. (#23)

- A number is no longer split between digits when a comma, colon or other punctuation
  appears earlier in the text. For example, `(1999)` stays together in "Super, Smash Bros (1999)". (#28)
- `WordBreaking.BreakWord` no longer splits a token after a colon when the token fits on the next line. (#28)
- `WordBreaking.BreakAll` with a wrapping length narrower than the first glyph no longer throws
  `ArgumentOutOfRangeException`. (#35)
- Text with a long run of leading spaces before a long word no longer throws during layout. (#35)
- A line that exactly fills the wrapping length and is followed by a newline no longer gets
  an extra blank line. Consecutive newlines now give the correct number of blank lines. (#35)
- A mandatory newline no longer stops an earlier word on the same line from wrapping. (#35)
- In horizontal layout, CJK brackets such as `【】「」` are no longer drawn in their vertical forms. (#34)
- In vertical layout, upright glyphs are centred in the column. Kana, `、` and `ー` were off-centre. (#34)
- The default shaper now checks the correct glyph range when it decides whether to apply `vert`. (#34)
- Fonts that have no `vert` feature still get vertical presentation forms from the cmap in vertical layout. (#34)
- Right-to-left text with `TextAlignment.Start` and a `WrappingLength` is aligned to the right edge
  of the wrapping box. It was at the left edge. (#42)

#### Shaping and font fallback

- A fallback font is shaped with its own tables, not with the tables of the primary font.
  Kannada and Khmer fallback text now shapes the same as when the font is the primary font. (#31)
- The universal shaper no longer throws `NullReferenceException` for some scripts, such as
  Tai Viet and Sinhala, when the text does not start at the beginning of a run. (#31)
- GPOS mark attachment (lookup types 4, 5 and 6) no longer throws `NullReferenceException`
  when a font leaves an anchor empty. The attachment is skipped. (#29)
- A mark array with an empty anchor offset is read correctly. It no longer throws
  "anchorFormat identifier is invalid". (#29)

#### Hinting

- Rendering text from several threads with `HintingMode.Standard` no longer throws errors
  from the TrueType interpreter. (#27)
- A hint program that fails (for example, by reading past the end of the CVT) no longer throws.
  That glyph is drawn without hinting. (#27)

#### Malformed fonts

- A corrupt cmap format 4 subtable no longer throws `IndexOutOfRangeException`.
  The glyph is reported as not found. (#26)
- A font that declares CFF outlines but has no CFF or CFF2 table is rejected at load time with
  `InvalidFontFileException`, not a `NullReferenceException` at first use. (#26)
- Corrupt GPOS pair adjustment data (lookup type 2, format 1) no longer throws `IndexOutOfRangeException`. (#26)
- Composite glyphs that refer to themselves, or that form a cycle, no longer cause a stack overflow.
  Composite nesting is limited to 16 levels, and components with an out-of-range glyph id are ignored. (#26)
