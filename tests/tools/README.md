# Test tools

## Font program fixtures

`generate_font_program_fixtures.py` writes the reference data for the `FontProgram` tests into
`tests/PinataLabs.Typography.Tests/Fonts/FontPrograms`. It builds the small synthetic fonts there
and writes one JSON fixture per font with what fontTools reads from it: glyph names, advances,
the built-in encoding, the FontMatrix, and every glyph outline.

To write the fonts and fixtures again, install fontTools 4.68 or later and run this from the repository root:

```
python tests/tools/generate_font_program_fixtures.py
```

The output is deterministic. A run that changes a file means that the script, fontTools, or a shipped font changed.

## Central baseline reference

`central_baseline/` holds the browser reference for rotated glyphs in vertical mixed layout (issue #50).

- `build_font.py` writes `CentralBaselineTest.ttf` into the test fonts. The font has ascent 1100, descent -200 and line gap 100 at 1000 units per em. Its Latin glyphs fill the ascent to descent range, so the ink centre of a rotated Latin glyph is its central baseline.
- `central-baseline.html` sets "あいうABCえお" in that font at 100px with `writing-mode: vertical-rl` and `text-orientation: mixed`. One column has `line-height: 1`, which is one em wide like the library's column, and one has `line-height: normal`.
- `measure_browser.py` takes a headless screenshot with Edge or Chrome and reads each glyph's ink edges from the pixels. It needs only Python.
- `browser-reference.json` is the output from Edge 158.0.4370.33 on Windows.

To measure again, run this from the repository root:

```
python tests/tools/central_baseline/build_font.py
python tests/tools/central_baseline/measure_browser.py [path-to-browser]
```

In both columns the ink centre of every glyph, upright and rotated, is the column centre to within 0.01px. `VerticalGlyphFormTests.RotatedGlyphCentralBaselineIsAtTheColumnCentre` checks the library against the same layout.

## GSUB lookup order font

`generate_gsub_lookup_order_font.py` builds `tests/PinataLabs.Typography.Tests/Fonts/GsubLookupOrder.ttf`
from feature code with fontTools feaLib. The LookupList of the font has the `vrtr` lookup first, then `liga`,
then `calt`. Two later lookups belong to both `liga` and `ss01`, and a `ccmp` lookup comes before the `rtlm`
lookup that feeds it. `GsubLookupOrderTests` uses the font to check that lookups are applied in LookupList order,
to every glyph that has one of their features, and that the Arabic shaper keeps its stage boundaries.

To write the font again, run this from the repository root:

```
python tests/tools/generate_gsub_lookup_order_font.py
```

The output is deterministic.

## GPOS lookup order font

`generate_gpos_lookup_order_font.py` builds `tests/PinataLabs.Typography.Tests/Fonts/GposLookupOrder.ttf`
from feature code with fontTools feaLib. The LookupList of the font has a `kern` lookup first, then `mkmk`,
then `mark`. Two later lookups belong to both `kern` and `mark`, and the `abvm` and `calt` lookups that
come last attach a mark that the `mark` lookup also attaches.

The script then shapes each test text with HarfBuzz and writes the glyph positions to
`GposLookupOrder.harfbuzz.json`, next to the font. `GposLookupOrderTests` checks that the library gives
the same positions. It does so only when it applies each lookup once, in LookupList order.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this
from the repository root:

```
python tests/tools/generate_gpos_lookup_order_font.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Default LangSys reference

`generate_default_langsys_reference.py` shapes Latin text with HarfBuzz in `NotoSansOgham-Regular.ttf`,
`NotoSansRunic-Regular.ttf` and `DejaVuSans.ttf`, and writes the glyph IDs and positions to
`tests/PinataLabs.Typography.Tests/Fonts/DefaultLangSys.harfbuzz.json`. The `latn` script of the two Noto
fonts has only the `MOL ` and `ROM ` language systems and no default LangSys, so HarfBuzz applies none of
its features when no language is requested.

The script also builds `DfltLangSysRecord.ttf`. Its `arab` script has only a LangSysRecord tagged `dflt` and no
default LangSys, and its `latn` script has both, with different `liga` lookups. HarfBuzz uses the `dflt` record
before the default LangSys, so it joins and kerns the Arabic text and takes `liga` from the `dflt` record.

It builds `RequiredFeature.ttf` too, whose language systems have a required feature, listed and not listed.
HarfBuzz applies a required feature to every glyph. It applies its lookups in the stage of the shaper feature with
the same tag, or else in the first stage, which holds only `rvrn`.

Last, it builds four `ScriptFallback*.ttf` fonts with no `grek` script and a different `liga` and `kern` for each
script. HarfBuzz shapes Greek text in them with the `DFLT` script, else the `dflt` script, else `latn`, and with no
feature when the font has none of the three. Their `vert` is listed only under `arab`, and HarfBuzz takes it from
the whole FeatureList for vertical Han text.

`DefaultLangSysTests` checks that the library gives the same glyphs and positions.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_default_langsys_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## HarfBuzz mark order

`derive_harfbuzz_mark_order.py` writes `src/PinataLabs.Typography.UnicodeTrieGenerator/Rules/HarfBuzzMarkOrder.txt`.
Before GSUB, HarfBuzz sorts each run of combining marks. It does not sort by Canonical_Combining_Class itself:
it moves some classes, and three characters, to another place in the order. The script does not read the HarfBuzz
source. It builds a font with every character of `UnicodeData.txt` that has a non-zero combining class, shapes
each of them after a base with one mark of each class in both orders, and writes the order that HarfBuzz gives.
It also lists the modifier combining marks that the Arabic shaper moves to the start of a run.

The file also says where HarfBuzz places each mark on its base in a font without a GPOS table. The script builds
a second font whose base and mark boxes make each placement give a different offset. It shapes every mark after
a base in that font, and writes how HarfBuzz aligns the marks of each class with the base and the side of the
base they go on. It lists the Thai, Lao and Tibetan characters that HarfBuzz places apart from their class.

The Unicode trie generator reads the file and writes `CombiningClassTrie.Generated.cs` from it.
To write the file again, install fontTools 4.68 or later and uharfbuzz, and run this from the repository root:

```
python tests/tools/derive_harfbuzz_mark_order.py
```

Then run the generator. When it has written the files, it waits for Enter:

```
dotnet run --project src/PinataLabs.Typography.UnicodeTrieGenerator
```

The output is deterministic for one HarfBuzz version and one `UnicodeData.txt`. The file records the HarfBuzz version.

## Normalization reference

`generate_normalization_reference.py` builds the `Normalization*.ttf` fonts in
`tests/PinataLabs.Typography.Tests/Fonts` and writes `Normalization.harfbuzz.json` next to them.

- `Normalization.ttf` has a small character set for named cases. For example it has o, U+0301 and U+00F3, and it
  has T and U+0327 but not U+0162.
- `NormalizationScan.ttf` has every character of `UnicodeData.txt` that has a canonical decomposition, and every
  part of those decompositions. `NormalizationParts.ttf` has the same characters without the ones that decompose.
  The glyphs of both fonts have no outline.
- `NormalizationBase.ttf` has a, e, o, q and U+0323 only. The tests use it as a primary font with
  `Normalization.ttf` as its fallback font. HarfBuzz has no font fallback, so the reference has no case for it.

- `NormalizationHebrew.ttf`, `NormalizationHebrewMark.ttf` and `NormalizationHebrewKern.ttf` have the Hebrew
  letters, points and presentation forms. The first has no GPOS table, the second has a `mark` feature for `hebr`,
  and the third has a `mark` feature that only `latn` lists. A letter and a point compose in the first and third.
- `NormalizationHebrewGposDflt.ttf`, `NormalizationHebrewGposLatn.ttf` and `NormalizationHebrewGposHebr.ttf` have
  Hebrew and Latin letters, marks and a `kern` table. Their GPOS tables have the `DFLT` script, the `latn` script and
  the `hebr` script. HarfBuzz positions Hebrew with GPOS only in the third, and kerns it with the `kern` table and
  places its marks as without GPOS in the other two.
- `NormalizationHangul.ttf`, `NormalizationHangulGsub.ttf` and `NormalizationHangulJamo.ttf` have Hangul jamo.
  The first two also have two syllables, and only the second has a GSUB table.
- `NormalizationDevanagari.ttf` has Devanagari letters and signs and the dotted circle, and no GSUB table.
- `NormalizationSpace.ttf` and `NormalizationSpaceBare.ttf` have U+0020 and lack the other spaces.
- `NormalizationMarks.ttf`, `NormalizationMarksGdef.ttf` and `NormalizationMarksKern.ttf` have a base and marks of
  eight scripts. The first has no GPOS and no GDEF table, the second has GDEF glyph classes, and the third has a
  GPOS table that only kerns.
- `NormalizationMarksLegacyKern.ttf` and `NormalizationMarksBothKern.ttf` have the same glyphs and a `kern` table.
  The second also has the GPOS table that only kerns.
- `NormalizationThai.ttf` and `NormalizationThaiWithoutParts.ttf` have Thai and Lao letters, marks and SARA AM, with
  and without its parts. HarfBuzz splits SARA AM in both.
- `NormalizationLigature.ttf`, `NormalizationLigatureGdef.ttf` and `NormalizationLigatureGdefBase.ttf` ligate f and i,
  f, f and i, and lam and alef across marks, and have no GPOS table. The second and third also have GDEF glyph
  classes, and the third calls the ligatures bases. Without GPOS, HarfBuzz places a mark of a ligature on its own
  component.
- `NormalizationScripts.ttf` has the first letter of every script of `Scripts.txt` and U+0301.
  `NormalizationScriptsKern.ttf` also has a `kern` table with the pair of each letter and itself.

The reference holds five lists:

- `cases` has the glyph IDs that HarfBuzz gives for each named text.
- `scan` has the glyph IDs for 6427 texts: each character alone in both scan fonts, the two parts of each
  decomposition, the full decomposition, and each pair of combining classes after a base.
- `positioned` has the glyph IDs and where HarfBuzz draws each glyph, for the spaces that a font lacks, for
  marks in a font without GPOS, and for text that a `kern` table kerns.
- `scripts` has the same for one letter and a mark of each script. What happens to the mark shows the shaper
  that HarfBuzz gives the script.
- `kernedScripts` has the same for two letters of each script in the font with the `kern` table. Only the shapers
  that place a mark without GPOS kern with that table.

`NormalizationTests` checks that the library gives the same glyphs in the same places. It lays out each
`positioned` text a second time as two text runs.

To write the fonts and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_normalization_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Script tags

`derive_harfbuzz_script_tags.py` writes `tests/PinataLabs.Typography.Tests/Fonts/ScriptTags.harfbuzz.json`. For each
script of `ScriptClass.cs`, it holds the OpenType script tags that HarfBuzz looks for in a font, in the order that
HarfBuzz tries them. For example, Devanagari gives `dev3`, `dev2`, `deva`, N'Ko gives `nko `, and Hiragana gives `kana`.

The script does not read the HarfBuzz source. It builds a probe font with a GSUB script for each candidate tag, where
each script has a required feature that gives a glyph of its own. It shapes text with the ISO 15924 code of the script
as the buffer script, and reads which script HarfBuzz chose. It hides that script and shapes again, until HarfBuzz
chooses `DFLT`.

It also builds `ScriptTags.ttf`, whose scripts are `nko `, `lao `, `vai `, `yi  `, `kana` and `DFLT`, and writes the
glyphs that HarfBuzz gives for text of each script. It copies `NotoSansKannada-Regular.ttf` to
`ScriptTagsKannadaVersion3.ttf` with `knd2` renamed to `knd3`, and writes the glyphs of Kannada text in it. `ScriptTagTests` checks the map of the library and the glyphs
against the reference.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/derive_harfbuzz_script_tags.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
