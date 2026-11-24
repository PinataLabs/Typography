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
`DefaultLangSysTests` checks that the library gives the same glyphs and positions.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_default_langsys_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.
