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

- **Variation axes and named instances.** The library reads the `fvar` and `avar` tables of a variable font.
  `FontMetrics.VariationAxes` lists the axes as `FontVariationAxis`: `Tag`, `MinValue`, `DefaultValue`, `MaxValue`,
  `IsHidden` and `Name(culture)`. `FontMetrics.NamedInstances` lists the instances that `fvar` names as
  `FontNamedInstance`: `Name(culture)` and `Variations`, which has one `FontVariation` for each axis. `FontMetrics.Variations`
  gives the axis values of a face or an instance. All three lists are empty for a static font. `FontVariation` is a tag
  and a value in the user coordinates of the axis. A malformed `fvar` table makes the font load as a static font, and a
  malformed `avar` table, or one for another number of axes, counts as absent. For an `avar` version 2 table the library
  reads the version 1 segment maps. The library normalises a coordinate as the Font Variations Overview says, and
  its F2Dot14 coordinates match fontTools `normalizeLocation` with `avar` for `Teko-Variable.ttf` and
  `SourceSans3VF-Upright.otf` at the minimum, at each named instance and at the maximum. (#125)
- **Variation instances.** The `Font` constructor and `FontFamily.CreateFont` take `params ReadOnlySpan<FontVariation>`
  after the style, and `Font.WithVariations(...)` gives a font of the same family, size and style with other
  variations. `Font.Variations` lists what was asked for. A font ignores a variation whose axis its face lacks, clamps a
  value to the range of the axis, and uses the last value of an axis that repeats. When the face is a variable font,
  the style sets `wght` and `wdth`, an italic or oblique request sets `ital` and `slnt` (an italic request on a font
  with `slnt` and no `ital` axis uses -14), and the size in points sets `opsz`. The explicit variations then win.
  `Font.FontMetrics` is the variation instance, which shares the tables of the face and has its own glyph caches.
  Two fonts whose coordinates normalise to the same F2Dot14 values share one `FontMetrics` while one of them is alive,
  and the instance is collected when both are gone. The face keeps a weak reference to each instance, so a caller that
  creates a font for every draw does not rebuild glyphs, and one that changes the weight every frame does not
  accumulate instances. `FontMetrics.Variations` of an instance gives the values of the request that made it.
  `CreateFont(12)` on Teko is the instance `wght` 400, and `FontStyle.Bold` is `wght` 700. An instance draws the
  positions of the default instance until the GPOS variation data are read. (#125)
- **CFF2 variation instances.** A variation instance of a CFF2 font draws the outlines of its coordinates. A `blend`
  operator adds each delta, scaled by the scalar of its region, to its value, for the regions that the `vsindex` of the
  glyph names, and the bounds of a glyph come from the varied outline. The scalars of an instance are made once, when
  the instance is made. A face draws as before: the outlines, bounds and advances of all 2478 glyphs of
  `SourceSans3VF-Upright.otf` at its default instance match what the library drew before this change. The outlines and
  bounds of 38 glyphs, which blend in the charstring, blend in a subroutine, switch to `vsindex` 1 and do not blend,
  match fontTools `getGlyphSet(location=...)` at the default instance, Regular, Bold, `wght` 900 and two locations
  between them. (#126)
- **Advances and global metrics of variation instances.** A variation instance adds the `HVAR` delta of a glyph to its
  advance width, and the `VVAR` delta to its advance height. When the table maps the left or top side bearing, the
  delta varies it too. A table with no advance mapping uses the glyph ID as its inner index and 0 as its outer
  index. `MVAR` varies the ascender, descender and line gap of `HorizontalMetrics` and `VerticalMetrics`, the subscript
  and superscript sizes and offsets, the strikeout size and position, and the underline position and thickness. `hasc`,
  `hdsc` and `hlgp` vary the `hhea` or typo value that the existing rules choose, as HarfBuzz does, and `hcla` and `hcld`
  vary the win values when the rules fall back to them. The rules look at the values of the font, not at the varied ones.
  A value is rounded once after its delta is added, with a half rounding up as fontTools `otRound` does, and stays in the
  range of its type. A table that is truncated, has another major version, or has a store or mapping that is malformed
  counts as absent, and nothing throws when a glyph is drawn. A font without `fvar` does not load the tables. The right
  side bearing and bottom side bearing mappings are not read, because those bearings follow from the advance, the other
  bearing and the size of the glyph. A CFF2 glyph keeps its origin where the default outline puts it, so without a left
  side bearing mapping its bearing is the left edge of its varied outline, and without a top side bearing mapping its
  bearing is the distance from the vertical origin of the default outline, which is its top plus the `vmtx` bearing, to
  the top of the varied outline. A TrueType glyph that `gvar` varies has its bearing from the phantom points. The win
  ascent and descent are added to their `MVAR` deltas as integers and clamped once, so a value above 32767 does not
  wrap. Advance widths of every glyph that `hmtx` lists match fontTools and HarfBuzz for `Teko-Variable.ttf` and
  `SourceSans3VF-Upright.otf` at the axis minimum, named instances, the axis maximum and between them. The global
  metrics, advance heights and bearings match them for two built fonts that have every mapping and an `MVAR` record for
  every metric. (#128)
- **TrueType variation outlines.** A variation instance of a TrueType font that has a `gvar` table draws the varied
  outline. The new tuple variation store reader decodes the shared and private point numbers, the packed deltas and the
  intermediate regions of the tuples of a glyph. The deltas move the points of a simple glyph, with interpolation of the points
  that a tuple leaves out, and the offsets of the components of a composite glyph, and they move its four phantom points. A
  component that a composite places by points is placed by the varied points. The bounds of a glyph are those of its varied
  points. When the font has no `HVAR` table, the advance width comes from the first two phantom points, and the advance height
  from the other two when the font has no `VVAR` table. The left side bearing and the top side bearing are the distance from a phantom
  point to the varied bounds, and each value rounds once, after the deltas are summed. The outline moves left by the x of the
  first phantom point, so that the origin stays at 0, as it does in HarfBuzz and FreeType, and a hinted glyph moves by that
  point as hinting rounds it. A composite takes the four phantom points of its last component that has the USE_MY_METRICS
  flag, with the deltas of that component, and drops its own. An empty glyph keeps its bounds, so a glyph with no deltas
  keeps its bearings. For `Teko-Variable.ttf` at the default instance, the "SemiBold" named instance, the maximum and a user
  location, the outlines and the bounds match fontTools, and the advances match HarfBuzz. A glyph whose `gvar` data is malformed draws at the default instance, and so does a glyph that has such a
  component. A `gvar` table that is malformed, or has another number of axes than `fvar`, counts as absent. (#127)
- **Hinting for variation instances.** A variation instance of a TrueType font hints its glyphs, with an interpreter of its own
  and the control values of its own. Its control values are those of the `cvt ` table plus the deltas of the `cvar` table for its
  coordinates, summed and rounded once, as fontTools does. A font that has `fpgm` and `prep` tables and no `cvar` table, or a
  `cvar` table that is malformed, hints an instance with the control values of the face. The interpreter runs the
  `GETVARIATION` instruction, which pushes the F2Dot14 coordinates of the instance, one for each axis, and sets the variation
  bit of `GETINFO`, which a font with axes always has. A font without axes treats `GETVARIATION` as unknown. A hinted glyph of
  `HintedVariable.ttf`, whose instructions use the control values and the coordinates, matches the hinted outline of FreeType
  at five locations. (#130)
- **Style range of a variable face.** `FontDescription.StyleRange` of a variable face covers the weights of its `wght`
  axis and the widths of its `wdth` axis. An `ital` axis that reaches 0 adds upright slopes, and one that reaches 1 adds
  italic. A `slnt` axis adds the oblique angles from its negated maximum to its negated minimum. Before, a variable face
  covered the weight and width of its default instance. A part without an axis still comes from the `OS/2` table. (#125)
- **Font weight, width and slope.** `FontWeight` (1 to 1000), `FontWidth` (50 to 200 percent) and `FontSlope`
  (`Normal`, `Italic` or `Oblique(degrees)`, with `FontSlopeKind`) hold the parts of a font style with the ranges of
  the CSS `font-weight`, `font-stretch` and `font-style` properties. `FontWeight` and `FontWidth` have the CSS
  keywords as static properties, such as `FontWeight.Bold` and `FontWidth.Condensed`. The default value of each type
  is its normal value. A constructor clamps a value to the CSS range and rejects `NaN`. (#123)
- **Font matching as in CSS.** `FontFamily.Faces` lists the faces of a family in the order in which they were added,
  and `FontFamily.MatchFace(style)` picks the face that best matches a `FontStyle`, with step 4 of CSS Fonts Level 4
  section 5.2: it narrows the faces by width, then by slope, then by weight. A face that has the asked value matches
  at once. Otherwise a width of 100% or less tries the narrower faces first, and a wider request the wider faces
  first. An italic request tries italic, then oblique (11 degrees and up first), then upright faces. A weight from
  400 to 500 tries the weights up to 500, then the lighter ones, then the heavier ones. A weight below 400 tries the
  lighter weights first, and a weight above 500 the heavier ones first. It always returns a face, so
  `Font.FontMetrics` no longer fails for a family that came from a collection. Of faces that match equally,
  the one that was added first wins. The library does not make a bold or oblique face when the family has none. (#123)
- **`FontStyleRange`.** `FontDescription.StyleRange` gives the weights, widths and slopes that a face covers:
  `MinWeight`, `MaxWeight`, `MinWidth`, `MaxWidth`, `HasNormal`, `HasItalic`, `HasOblique`, `MinObliqueAngle` and
  `MaxObliqueAngle`, with `Contains(style)`. A static face covers one point. The weight is `OS/2.usWeightClass`, with
  1 to 9 of the legacy scale read as hundreds, or 700 or 400 from the `BOLD` bit when the class is 0. The width is
  `usWidthClass` as a percentage, with the table in the OpenType `OS/2` chapter. The slope is italic when
  `fsSelection` has `ITALIC`, and oblique when it has `OBLIQUE` and `OS/2` is version 4 or later, at the negated
  `post.italicAngle` (14 degrees when that is 0). A font without `OS/2` takes bold and italic from `head.macStyle`.
  A renderer that makes a bold or oblique face compares `Font.Style` with `StyleRange`. (#123)
- **`Font.Style`, `WithSize` and `WithStyle`.** `Font.Style` is the style that the font was created with.
  `font.WithSize(size)` and `font.WithStyle(style)` give a font of the same family with another size or style.
  The `Font` constructor and `FontFamily.CreateFont` take the style as an optional parameter, so `CreateFont(12)` is
  regular. (#123)
- **Legacy family name.** `FontDescription.LegacyFontFamily(culture)` gives name ID 1, the family name that groups
  at most four faces. `FontDescription.FontFamily(culture)` now gives the typographic family name (see Changed). (#123)
- **Unicode 18.0.0 scripts and classes.** `ScriptClass` has members for the 16 scripts of Unicode 15.0 to 18.0, with
  their OpenType tags, and they use the universal shaper. `LineBreakClass` gains `AK`, `AP`, `AS`, `HH`, `VF` and `VI`,
  `IndicSyllabicCategory` gains `ReorderingKiller`, and `ArabicJoiningGroup` gains 12 members. (#103)
- **Paint type of font programs.** `FontProgram.PaintType` (`GlyphPaintType.Filled` or `GlyphPaintType.Stroked`)
  and `FontProgram.StrokeWidth` report whether the outlines are filled or stroked, and the stroke width in glyph space
  units. They come from the Type 1 `/PaintType` and `/StrokeWidth` and the CFF Top DICT PaintType and StrokeWidth.
  Only paint type 2 is `Stroked`. TrueType is always `Filled` with width 0. (#83)
- **cmap lookup in font programs.** `FontProgram.TryGetGlyphIdFromCmap(platformId, encodingId, code, out glyphId)`
  maps a code through the named 'cmap' subtable of a TrueType or OpenType CFF program, read in place. It supports
  subtable formats 0, 2, 4, 6, 10, 12 and 13, and uses the first record when several share a platform and encoding.
  It returns `false` for a missing, unsupported or malformed subtable, an unmapped code or glyph 0, and for bare CFF
  and Type 1 programs. (#79)
- **Trimming and Native AOT.** The net8.0 and later assets are marked `IsTrimmable` and `IsAotCompatible`, and the
  trim, AOT and single-file analyzers report no warnings. CI publishes a Native AOT smoke app on Linux that loads
  TrueType, CFF and Type 1 fonts from bytes, writes outlines and measures text. A test fails on any writable static
  field in the library. (#76)
- **Variable font design.** `docs/adr/0001-variable-font-instances.md` proposes the variable font instance API and
  the order of the work. `GLOSSARY.md` defines its terms. (#46)
- **Font program API.** `FontProgram` reads the glyphs of a font by glyph ID, without text layout.
  Use it to draw a font that another format selects and positions, such as a font embedded in a PDF file.
  `FontProgram.Load` takes the font file as `ReadOnlyMemory<byte>` and does not copy it. It accepts
  TrueType fonts and OpenType fonts with a `CFF ` table. `TryWriteOutline` writes the outline of a glyph
  to an `IGlyphOutlineSink` in glyph space, and `FontMatrix` maps glyph space to ems.
  `TryGetGlyphId`, `GetGlyphName` and `TryGetGlyphIdForCid` find glyphs by name and by CID,
  `TryGetBuiltInEncoding` gives the glyph for each of the 256 codes of the encoding that the font carries,
  and `TryGetAdvanceWidth` gives the advance width. A font program holds no state that changes after load,
  so one instance can serve many threads. Malformed data makes `Load` throw `InvalidFontFileException`.
  After load, a malformed glyph makes a method return `false`. (#71, #72)
- **Type 1 font programs.** `FontProgram.Load` reads a Type 1 font as a PFB file, a PFA file, or the stream of
  a PDF `/FontFile`, and `Format` gives `FontProgramFormat.Type1`. It finds the encrypted part itself, so the
  `/Length1`, `/Length2` and `/Length3` of the PDF stream do not need to be correct. Glyph 0 is `.notdef`, and the
  other glyphs follow in the order of the `/CharStrings` dictionary. A font without `.notdef` gets an empty one.
  `TryGetBuiltInEncoding` gives the glyphs of its `/Encoding`, StandardEncoding or its own. A Type 1 program does not
  keep the data that it was loaded from. It keeps a decrypted copy of the part that holds the glyphs, and
  `RetainedByteCount` counts that copy and its arrays, not the data. (#73)
- **Memory bound for font programs.** `FontProgram.RetainedByteCount` gives the bytes that a font program
  keeps reachable: the length of the data that was passed to `Load` when the program keeps it, plus the arrays
  that the program allocated.
  The value is fixed at load, and it does not change when the program is used. A TrueType program allocates no array.
  `FontProgram.Load(data, options)` takes a `FontProgramOptions` with `MaxRetainedBytes`. It checks the limit before
  each allocation and before it parses anything, and it throws `FontProgramTooLargeException`, a subclass of
  `InvalidFontFileException`, instead of retaining more. The exception carries `RetainedByteCount` and `MaxRetainedBytes`.
  Use this to keep one budget for the fonts of a document. (#75)
- **Bare CFF font programs.** `FontProgram.Load` reads a bare CFF font, as a PDF embeds it in a `/FontFile3`
  stream with `/Subtype /Type1C` or `/CIDFontType0C`. Name-keyed and CID-keyed fonts load, and the FontMatrix of
  each Font DICT is folded into the outlines of its glyphs. The format comes from the CFF header, and `Format` is
  `FontProgramFormat.Cff`. Advance widths come from the charstrings. A bare CFF2 font throws
  `InvalidFontFileException`. (#70)
- **Stripped OpenType fonts.** `FontProgram.MissingOptionalTables` and `FontProgram.MalformedOptionalTables`
  name the optional sfnt tables (`name`, `OS/2`, `post`, `cmap`, `hhea`, `hmtx`) that a TrueType or OpenType
  font lacks or that were ignored because they are damaged. Font subsets without these tables, such as the
  subsets in PDF files, load for their outlines. A damaged optional table is treated as absent and never fails
  `Load`. (#77)
- **Limits for untrusted fonts.** The `FontProgram` remarks state the limits on charstring stacks, subroutine
  nesting, operators per glyph, composite depth and components, points per outline, CFF INDEX and DICT data,
  Font DICTs, glyph counts and Type 1 subroutines, and the memory that `Load` and the glyph methods allocate.
  `tests/PinataLabs.Typography.Fuzz` is a SharpFuzz harness over the TrueType, CFF, bare CFF and Type 1
  loaders, and a seeded mutation test in the test suite checks the same rules on each build. (#74)
- **netstandard2.1 target.** The library targets `netstandard2.1` again, so a netstandard2.1
  project such as a Unity package can depend on it. On this target the package depends on
  `System.Runtime.CompilerServices.Unsafe`. The other targets keep no dependencies.
  The public API is the same on every target. (#69)

### Changed

- **`FontStyle` is a weight, a width and a slope.** The flags enum is now a struct with a `FontWeight`, a `FontWidth`
  and a `FontSlope`. `FontStyle.Regular`, `Bold`, `Italic` and `BoldItalic` are static properties with the same
  names and meanings, so `CreateFont(12, FontStyle.Bold)` still compiles, and `default(FontStyle)` is
  `FontStyle.Regular`. Code that combines styles with `|` or calls `HasFlag` stops compiling, so each place shows up
  for review: use `WithWeight`, `WithWidth` and `WithSlope`. (#123)
- **Variable fonts draw as variation instances.** On a variable face, `Font.FontMetrics` is a variation instance, so
  `FontFamily.CreateFont(12)` on Teko or Source Sans 3 draws their `wght` 400 and no longer their default instance at
  the lightest weight. Ask for `FontWeight.Light` on Teko, or `FontWeight.ExtraLight` on Source Sans 3, to get the
  default instance, which is the face itself. Two instances of one face give different `GlyphRendererParameters`, and
  `GlyphRendererParameters.FontMetrics` is the instance. `Font.Name` is the family name and the subfamily name of the
  named instance that the font draws, such as "Teko SemiBold", and for any other coordinates it is the full name of the
  face. A text layout creates each fallback font with the size, the style and the variations of
  the font of the text. (#125)
- **A font takes the best face by weight, width and slope.** `Font.FontMetrics` is the face that
  `FontFamily.MatchFace(font.Style)` gives. A family with static Regular and Bold faces renders as before. A
  request that no face has now goes to the nearest weight, width or slope in the CSS order, where before it fell back
  to the exact Italic, then Bold, then Regular. A face is bold or light by `usWeightClass` and not by the `BOLD`
  bit of `fsSelection`, so a font whose class and bit disagree is matched by its class. (#123)
- **`GlyphRendererParameters` identifies the font by its metrics.** The new `FontMetrics` property is the metrics
  object that owns the glyph, and two `GlyphRendererParameters` are equal only when they hold the same object.
  Before, `Font` (the full font name in upper case) and `FontStyle` identified the font, so two fonts that had the
  same name and came from different files gave equal parameters, and a renderer that cached outlines by the
  parameters could draw the glyph of the wrong font. `FontMetrics` is the same object that `Font.FontMetrics` and
  `FontFamily.Faces` give, also for a font that is added from a file path. (#123)
- **Families are grouped by the typographic family name.** `FontDescription.FontFamily(culture)`,
  `FontDescription.FontFamilyInvariantCulture`, `FontFamily.Name` and the lookups of `FontCollection` and
  `SystemFonts` now use name ID 16, and fall back to name ID 1 for a font that has no ID 16. A family with more than
  four weights is one family, and `Teko-Variable.ttf` is the family "Teko" where it was "Teko Light". A font that
  has an ID 16 that differs from its ID 1 is no longer found under its ID 1, so `Get("Teko Light")` fails.
  Use `FontDescription.LegacyFontFamily(culture)` for the old name. In a culture, a name in the language of the
  culture beats a name in another language, and in one language ID 16 beats ID 1. Faces are grouped by their
  invariant family name, so faces whose names are in different languages stay in one family, and any of their
  localized names finds the family. (#123)
- **Unicode 18.0.0.** All Unicode data moves from Unicode 14.0.0 to 18.0.0, the version that HarfBuzz 15.0.0 uses:
  scripts, general category, line break, East Asian width, bidi, grapheme break, vertical orientation, Arabic joining,
  Indic categories, emoji and canonical mappings. Line breaking follows the UAX #14 rules of Unicode 18.0.0, including
  the quotation, hyphen, East Asian and Aksara rules added since Unicode 15.1, so lines can break in new places.
  Grapheme clusters follow rule GB9c of UAX #29. A line can break after leading spaces, as UAX #14 allows, so an
  indented first word that does not fit can move to the next line. (#103)
- **The shaper depends on the script tag that the font provides.** A version 3 Indic tag such as `dev3` gives the
  universal shaper. `DFLT` or `latn` gives the default shaper to Indic, Myanmar and universal scripts, as does `mymr`
  for Myanmar and `DFLT` for Syriac, as in HarfBuzz 15.0.0. The normalization, the `kern` table, fallback mark
  placement and GPOS follow the chosen shaper. (#101)
- **Characters are composed and decomposed to the glyphs that a font has.** Before GSUB, text layout maps the
  characters of each run to the glyphs that the font has, as the normalization step of HarfBuzz does. A base and a mark
  give the precomposed glyph when the font has it, so `o` + U+0301 draws ó. A character that the font lacks is drawn
  from its canonical parts when the font has them, so U+0162 draws as T + U+0327. Each run of combining marks is
  sorted in the order that HarfBuzz uses. The Indic, universal and Myanmar shapers decompose first, and the Arabic
  shaper moves modifier marks such as U+0654, as HarfBuzz 15.0.0 does. `TextMeasurer.TryMeasureCharacter*` report the
  glyphs after this step, so `o` + U+0301 gives one entry for U+00F3, and U+0162 gives two entries, T and U+0327. (#95)
- **One TrueType decoder.** Text layout draws TrueType glyphs with the decoder that `FontProgram` uses, and it
  no longer decodes the `glyf` table when it loads a font. A loaded font keeps the bytes of `glyf` and `loca`.
  OpenSans-Regular keeps about 295 KB after load, down from 585 KB. A contour starts at its first on-curve point,
  so an `IGlyphRenderer` gets the same curves in fewer calls. A malformed `glyf` no longer fails the load. A
  malformed glyph draws up to the fault. (#80)
- **`vrtr` in vertical mixed layout.** Glyphs that are rotated in `VerticalMixedLeftRight` and
  `VerticalMixedRightLeft` layouts now get the OpenType `vrtr` feature. The font's `vrtr` glyphs are
  designed to be rotated by the layout engine, so they are still rotated. Fonts without `vrtr` are
  not affected. (#51)

### Fixed

- A CFF2 font with a malformed variation store loads, where a bad store used to fail the whole load. A glyph with a
  `blend` operator fails only when the region count it needs cannot be read. A wrong store length or a malformed region
  list does not matter, and a malformed subtable fails only the glyphs that use it. A store with a bad header counts as
  absent. (#124)
- `FontMetrics.TryGetGlyphMetrics` reports the text decorations that the caller passed. Before, a glyph reported the
  decorations of whichever caller asked for it first. (#106)
- Letters on both sides of a `TextRun` boundary join as in one run: each run is shaped with up to five characters of
  the text around it as context, as a HarfBuzz caller does. No GSUB lookup applies across the boundary. (#109)
- Default-ignorable characters such as ZWJ, ZWNJ, the Mongolian free variation selectors and the vowel separator are
  drawn with the glyph of U+0020 and no advance after shaping, or left out when the font has no U+0020, as in
  HarfBuzz. SOFT HYPHEN, COMBINING GRAPHEME JOINER and the selectors no longer take their own advance. A character
  that GSUB substituted keeps its glyph. Line breaking does not change. The Hangul fillers and the shorthand format
  controls keep their own glyph and advance, as in HarfBuzz. (#110)
- The universal shaper finds syllables from the HarfBuzz 15.0.0 categories, as HarfBuzz does. A combining grapheme
  joiner, a ZWJ, or a ZWNJ before a mark stays in the syllable around it. A vowel sign that goes before its base
  moves in front of a space, a punctuation mark or a symbol before it, as in a HarfBuzz symbol cluster. A letter that
  a reordering moves to the place of a ZWJ or ZWNJ keeps its advance. (#108)
- In a font whose GSUB table has no `thai` script, Thai marks use the private use glyphs of Windows and Mac Thai fonts
  where they would clash with their base or another mark, as in HarfBuzz. (#111)
- The universal shaper gives the syllables of every script `isol`, `init`, `medi` or `fina` by their place in a run
  of syllables, as HarfBuzz does. Before, only the scripts whose letters join got these features. (#111)
- Two signs moved in front of a dotted circle each keep their advance. (#111)
- The Khmer basic features run in one stage, in the order of the lookup list, as in HarfBuzz. (#111)
- GPOS pair positioning finds the second glyph past the glyphs that the lookup flags skip, so a lookup that ignores
  marks kerns the letters on both sides of a mark. GPOS mark-to-mark attaches a mark to a mark of the same base, as
  in HarfBuzz. A GPOS mark moves with a base that the `kern` table moved. (#111)
- Mongolian in Mongolian Baiti shapes as in HarfBuzz. The format characters that ArabicShaping.txt lists as joining
  type U, such as U+180E and U+0600, keep it. A context subtable that matches ends its lookup, even when it has no
  substitution records. A lookup skips the marks outside its GDEF mark filtering set, whose offsets are now read as
  32-bit. Reverse chaining substitution runs from the last glyph to the first, follows the lookup flags, and
  substitutes the one glyph at its Coverage index. A glyph that it has no substitute for stays. (#111)
- Chained context and reverse chaining lookups match a backtrack of two or more glyphs from the glyph nearest to the
  input, as the OpenType specification orders it. Before, they matched it in the reverse order.
- Adlam, Chorasmian, Hanifi Rohingya, Mandaic, Manichaean, Mongolian, N'Ko, Old Uyghur, Phags-pa, Psalter Pahlavi
  and Sogdian use the universal shaper, which now joins their letters. Phags-pa letters had no joining type. (#102)
- In the universal shaper, a sign after a space, a digit, a punctuation mark or a symbol takes that character as its
  base. The dotted circle of a broken cluster goes before the sign in the Indic and universal shapers. Every shaper
  applies `abvm` and `blwm`, and applies `dist` to horizontal text only, and the universal shaper applies `haln`.
  Text such as U+1B34 U+1B44 no longer throws. The Myanmar shaper no longer draws the dotted circle of a lone vowel
  sign E on top of the sign. (#102)
- Hebrew is positioned with GPOS only when the GPOS table has the `hebr` script. Otherwise it is kerned with the `kern`
  table and its marks are placed without GPOS, as in HarfBuzz. (#102)
- In a font without GPOS, a mark of a ligature is placed on its own component. GSUB gives the marks of a ligature their
  component as HarfBuzz does. (#102)
- Thai SARA AM and Lao AM split into their mark and vowel, and the mark moves before the marks above the base. (#102)
- Arabic in a font without the GSUB joining forms uses the presentation forms of the font and their ligatures, as
  HarfBuzz does. (#102)
- A glyph shared by several code points, such as U+00A0 and U+0020 on the space glyph or the .notdef glyph of missing
  characters, reports the code point that the text holds. Before, it reported whichever code point the font met first,
  which also changed line breaking at a no-break space and whether a missing character was drawn. (#105)
- Text in N'Ko, Lao, Vai, Yi, Hiragana and Katakana or Hiragana uses the `nko `, `lao `, `vai `, `yi  ` and `kana`
  scripts of the font. Before, shaping looked for `nkoo`, `laoo`, `vaii`, `yiii`, `hira` and `hrkt`, which fonts do not
  use, and fell back to `DFLT`. Indic scripts try the version 3 tags such as `dev3` and `knd3` before the version 2 and
  old tags, and the Indic shaper treats a version 3 tag as new-spec. A test checks the tags of every script against
  HarfBuzz 15.0.0. (#98)
- A space that the font lacks (U+00A0, U+2000 to U+200A, U+202F, U+205F, U+3000) is drawn with the glyph of U+0020 and
  the width of that space, and U+2011 with the glyph of U+2010, as in HarfBuzz. Line breaking does not change. (#99)
- A Hebrew letter and point compose to the presentation form that the font has when the font has no GPOS `mark`
  feature for Hebrew. A meteg or a mark below the base moves before a sheva or hiriq that follows a patah or qamats. (#99)
- The shaper of each script runs in a font without a GSUB table, so Hangul jamo compose and syllables decompose there.
  The Indic shaper also reorders each syllable there, and gives a broken syllable a dotted circle.
  A Hangul run that does not start the text composes and decomposes the correct glyphs. (#99)
- In a font without a GPOS table, a mark takes no advance and is placed on its base from the glyph boxes, as in
  HarfBuzz. A mark of Mongolian, N'Ko, Phags-pa, Mandaic, Manichaean or Psalter Pahlavi text only loses its advance.
  With a GPOS table and no GDEF glyph classes, the glyph of a non-spacing mark takes no advance. (#99)
- The `kern` table of a font kerns as in HarfBuzz. Each glyph of a pair takes half of the kerning and the second
  glyph moves, a pair is two glyphs with nothing but marks between them, and right-to-left text looks for the pair
  in the order of the line. Only text of the default, Arabic, Hebrew and Hangul shapers is kerned, and only when the
  GPOS table has no `kern` feature for the script. Before, the kerning moved the glyphs after the pair, and it also
  applied when the GPOS `kern` feature had no pair for the text. (#99)
- Text runs that have the same font position its glyphs once. Before, each text run applied the GPOS adjustments and
  the mark offsets of the font again. (#99)
- Dives Akuru and 29 more scripts use the universal shaper, as in HarfBuzz. (#99)
- The required feature of the chosen language system is applied to every glyph, as HarfBuzz does. GSUB applies it
  in the stage of the shaper feature with the same tag, or else in the first stage. `KerningMode.None` still turns off
  a required `kern` or `vkrn` feature, which HarfBuzz does not. (#93)
- When a font has no script for the text, shaping uses its `DFLT` script, else `dflt`, else `latn`, and applies no
  features when it has none of them. Before, the first script in the font was used. Vertical text takes `vert` from any
  script in the font when the chosen language system lacks it, as HarfBuzz does. (#94)
- `TextMeasurer` measures text from a layout at the zero origin, so `MeasureAdvance`, `MeasureSize` and the character
  advances and sizes no longer depend on `TextOptions.Origin`. Before, a large origin could make a measured extent one
  pixel larger. `MeasureBounds` and `TryMeasureCharacterBounds` still return absolute bounds, now the zero-origin
  bounds moved by the origin. (#96)
- Shaping uses one language system of a script, as HarfBuzz does: the language system tagged `dflt` when the script
  has one, and otherwise the default language system. A script with neither gets none of its features. Before, it
  applied the features of every language system together, so Latin text in fonts such as Noto Sans Ogham and
  Noto Sans Runic was kerned and got `locl` substitutions that HarfBuzz does not apply. This affects GSUB and GPOS. (#91)
- `TextMeasurer.MeasureAdvance` and `MeasureSize` no longer add a pixel when float error puts an extent just over a
  whole pixel. A one-em column at line spacing 1.5 now measures 150, not 151, in every vertical mode, and the same
  holds for horizontal line heights and advances. A real fractional extent still rounds up. (#90)
- GPOS lookups are applied in LookupList order across all the positioning features that are active on a glyph, as
  the OpenType specification requires and as HarfBuzz does. Each lookup is applied once, even when more than one active
  feature refers to it. A font whose lookup order differs from the old feature order can position differently. In
  DejaVu Sans, whose `mkmk` lookups come before its `mark` lookups, a second combining mark now attaches to the base,
  as it does in HarfBuzz. (#88)
- In `VerticalMixedLeftRight` and `VerticalMixedRightLeft`, `TextMeasurer.MeasureAdvance` no longer reports a column
  wider than the one laid out when it holds rotated glyphs. It now matches the width of the upright column. Rendering
  does not change. (#87)
- GSUB lookups are applied in LookupList order across all the features that are active in a shaping stage, as the
  OpenType specification requires. Before, each feature was applied in turn, in the order that the shaper planned
  it. A font whose `vrtr` lookup precedes its `liga` lookup now gives a rotated ligature its `vrtr` form in vertical
  mixed layout. A font whose lookup order differs from the old feature order can shape differently. (#85)
- In `VerticalMixedLeftRight` and `VerticalMixedRightLeft` layout, rotated glyphs sit on the CSS central baseline:
  the midpoint of the ascent and descent of the font is on the column centre, as in browsers. Before, a rotated glyph
  was off by half the line gap of the font. Fonts with no line gap do not change. (#50)
- The built-in encoding of a TrueType font program finds a code in a format 4 'cmap' subtable by binary search
  instead of a scan of every segment. (#79)
- A TrueType composite that places many components on high point numbers fails within the outline budget,
  instead of reading hundreds of millions of points. (#74)
- A CFF font program keeps at most 256 Font DICT records, the most that FDSelect can select. Before, 65,535
  empty Font DICTs in 65 KB of data made the program keep 6.36 MB. (#74)
- TrueType outlines in text layout no longer contain a zero-length line before each curve. (#80)
- Composite TrueType glyphs in text layout place a component by point numbers when the font asks for that,
  and scale a component offset when `SCALED_COMPONENT_OFFSET` is set. (#80)
- Hinted WOFF2 text matches the hinted TrueType font. The WOFF2 reader read the bounding box bitmap at the
  wrong length and gave some glyphs the instructions of other glyphs. (#80)
- Text layout of TrueType fonts keeps no per-thread state. (#80)
- **CFF glyph outlines.** Text layout draws these CFF glyphs correctly now: a glyph that uses a
  `flex`, `hflex`, `hflex1` or `flex1` operator, a glyph with fractional coordinates, and an accented glyph
  that an `endchar` with four arguments builds from two other glyphs. In a CID-keyed font, each glyph
  uses the subroutines and the FontMatrix of its own Font DICT. Layout draws a glyph at the size that its
  FontMatrix and the units per em of the `head` table give, so a FontMatrix other than 1/unitsPerEm
  changes the size. The arithmetic operators of a charstring take their operands in the right order.
- **Malformed CFF fonts.** A CFF table or glyph that is malformed makes layout throw
  `InvalidFontFileException`. Before, it threw several exception types, and a subroutine that called
  itself stopped the process with a stack overflow. An accented glyph whose `endchar` names a base or
  accent glyph that the font does not have draws without the two glyphs, as before. A charstring in a
  `CFF ` table with more than 48 arguments on its stack, the Type 2 limit, is malformed, and so is an
  outline of more than a million segments.
- **CID-keyed CFF fonts.** A CID-keyed font with an FDArray keeps its Private DICTs in the Font DICTs,
  so the font loads even when the Private entry of its Top DICT is out of range. Before, such a font made
  layout throw `InvalidFontFileException`.
- **Bounds of an empty CFF glyph.** A CFF glyph without an outline, such as a space, has a width and
  a height of 0 on every runtime. Before, the width was 1 on .NET 10 and later.
- **`vrt2` in vertical mixed layout.** A `vrt2` feature requested in `TextOptions.FeatureTags` is no
  longer applied to glyphs that are rotated in vertical mixed layout. Its glyphs are pre-rotated, so
  before, these glyphs were rotated twice. `vrt2` is still never applied by default. (#51)

### Removed

These members are removed in #123. Each entry names its replacement.

- `Font.IsBold` and `Font.IsItalic`: use `Font.Style` for the style that was asked for. Neither says what the face
  has, because a weight of 600 needs a threshold to be bold and no standard sets one.
- `Font(Font prototype, FontStyle style)`, `Font(Font prototype, float size)` and
  `Font(Font prototype, float size, FontStyle style)`: use `prototype.WithStyle(style)`, `prototype.WithSize(size)`,
  or `prototype.WithSize(size).WithStyle(style)`.
- `Font(FontFamily family, float size)` and `Font(FontFamily family, float size, FontStyle style)`: use
  `Font(FontFamily family, float size, FontStyle style = default)`. Code that calls either one still compiles.
- `FontFamily.CreateFont(float size)` and `FontFamily.CreateFont(float size, FontStyle style)`: use
  `FontFamily.CreateFont(float size, FontStyle style = default)`. Code that calls either one still compiles.

- `GlyphRendererParameters.Font` (a `string`): use `GlyphRendererParameters.FontMetrics`. For the name, read
  `FontMetrics.Description.FontNameInvariantCulture`.
- `GlyphRendererParameters.FontStyle`: use `GlyphRendererParameters.FontMetrics`. For the style of the face, read
  `FontMetrics.Description.StyleRange`.
- `FontDescription.Style`: use `FontDescription.StyleRange`, which holds the range of styles that the face covers.
- `FontFamily.GetAvailableStyles()`: use `FontFamily.Faces`, and read the `Description.StyleRange` of each face.
- `FontFamily.TryGetMetrics(FontStyle style, out FontMetrics? metrics)`: use `FontFamily.MatchFace(FontStyle style)`,
  which returns the best face and never fails for a family that has a face.
- `FontStyle.Regular = 0`, `Bold = 1`, `Italic = 2` and `BoldItalic = 3` as members of the flags enum: they are now
  static properties of the `FontStyle` struct with the same names.

## [0.1.0] - 2026-10-26

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
