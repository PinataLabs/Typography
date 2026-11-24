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
- `NormalizationHangul.ttf`, `NormalizationHangulGsub.ttf` and `NormalizationHangulJamo.ttf` have Hangul jamo.
  The first two also have two syllables, and only the second has a GSUB table.
- `NormalizationSpace.ttf` and `NormalizationSpaceBare.ttf` have U+0020 and lack the other spaces.
- `NormalizationMarks.ttf`, `NormalizationMarksGdef.ttf` and `NormalizationMarksKern.ttf` have a base and marks of
  eight scripts. The first has no GPOS and no GDEF table, the second has GDEF glyph classes, and the third has a
  GPOS table that only kerns.
- `NormalizationScripts.ttf` has the first letter of every script of `Scripts.txt` and U+0301.

The reference holds four lists:

- `cases` has the glyph IDs that HarfBuzz gives for each named text.
- `scan` has the glyph IDs for 6427 texts: each character alone in both scan fonts, the two parts of each
  decomposition, the full decomposition, and each pair of combining classes after a base.
- `positioned` has the glyph IDs and where HarfBuzz draws each glyph, for the spaces that a font lacks and for
  marks in a font without GPOS.
- `scripts` has the same for one letter and a mark of each script. What happens to the mark shows the shaper
  that HarfBuzz gives the script.

`NormalizationTests` checks that the library gives the same glyphs in the same places. For `scripts` it names
the six scripts that the library gives another shaper.

To write the fonts and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_normalization_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
