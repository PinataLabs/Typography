# Test tools

## Shared HarfBuzz helpers

`harfbuzz_reference.py` holds what the `generate_*_reference.py` scripts share: `build_font` saves a font of empty glyphs,
`load_font` and `shape_line` shape a line with HarfBuzz and give the glyph IDs, places and advance, and `write_reference`
and `write_unique_rows` write the reference JSON. A script imports it from its own folder, so each one still runs as
`python tests/tools/<name>.py` from the repository root.

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

## Item Variation Store reference

`generate_item_variation_store_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/ItemVariationStore.fonttools.json`,
the fontTools reference for the Item Variation Store reader (issue #124). It reads the two variable test fonts:

- `Teko-Variable.ttf` has the stores of `HVAR`, `MVAR` and GDEF.
- `SourceSans3VF-Upright.otf` has the CFF2 store and the stores of `HVAR`, `MVAR` and GDEF.

For each font the script picks the locations that `CONFIG` lists: some named instances, which include the axis
minimum and the axis maximum, and a user location between them. It normalises each one as the Font Variations Overview
says, with the `avar` segment map and a round to F2Dot14, and records the F2Dot14 coordinates. The library does not
read `fvar` or `avar` yet, so `ItemVariationStoreReferenceTests` gives the reader these coordinates as they are.

For each store at each location the file holds the scalar of every region and the delta of every row, which fontTools
computes with `VarStoreInstancer`. The `HVAR` entry also holds the VarIdx that the advance width map gives to every
glyph, and the `MVAR` entry holds the VarIdx of each value record.

To write the file again, install fontTools 4.68 or later and run this from the repository root:

```
python tests/tools/generate_item_variation_store_reference.py
```

The output is deterministic for one fontTools version. The file records the version that wrote it.

## Font variations reference

`generate_font_variations_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Variations/FontVariations.fonttools.json`,
the fontTools reference for `fvar`, `avar` and the normalised coordinates of a variation instance (issue #125).
It also builds the two small variable fonts that the variation tests need, next to the reference:

- `VariationAxes.ttf` has the axes `wght`, `wdth`, `opsz`, `ital` and a hidden `GRAD` axis, with an `avar` segment map
  for `wght` and `opsz`. Neither `Teko-Variable.ttf` nor `SourceSans3VF-Upright.otf` has an `opsz` axis, so the
  `opsz` tests use this font.
- `VariationSlant.ttf` has the axes `wght` and `slnt` and no `ital` axis, so an italic request has to use `slnt`.

Both fonts have `fvar` and `avar` and no outline variation data, because instances draw the default outlines
until the CFF2 `blend` issue lands. Each glyph has a short glyph program that rounds the top of its box to
the pixel grid, so that hinting changes the outline. The tests use it to check that the default instance and a variation
instance of a font with no `cvar` table both draw hinted.

For these two fonts and the two variable fonts of the test project, the reference holds the axes, the named
instances, the `avar` segment maps, and a list of locations. A location is the user coordinates of every axis and the
F2Dot14 coordinates that the Font Variations Overview, "Coordinate Scales and Normalization", gives for them:
fontTools `normalizeLocation` clamps and normalises each coordinate, `piecewiseLinearMap` applies the `avar` segment map,
and `floatToFixed` rounds the result to F2Dot14. The locations are the default, the minimum and the maximum of each axis,
every axis at its minimum and at its maximum, each named instance, and some user locations between them.
`FontVariationTests` checks that the library gives the same F2Dot14 coordinates.

The script also writes `Teko-Variable.woff` and `Teko-Variable.woff2`, which check that a compressed font loads its
`fvar` and `avar`.

To write the fonts and the reference again, install fontTools 4.68 or later and brotli, and run this from the
repository root:

```
python tests/tools/generate_font_variations_reference.py
```

The output is deterministic for one fontTools version. The reference records the version that wrote it.

## Variation reference helpers

`variation_reference.py` holds what the variation reference scripts share: the F2Dot14 coordinates of a user location
(fontTools `normalizeValue`, the `avar` segment map, and a round to F2Dot14), the list of locations that a script records,
and the JSON layout. A script imports it from its own folder, so each one still runs as `python tests/tools/<name>.py`
from the repository root.

## CFF2 outline reference

`generate_cff2_outline_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Variations/Cff2Outlines.fonttools.json`,
the fontTools reference for the outlines of CFF2 variation instances (issue #126). It reads
`SourceSans3VF-Upright.otf`, whose CFF2 table has a variation store of two `ItemVariationData` subtables, so a glyph blends
with the regions of `vsindex` 0, or with those of `vsindex` 1 after a `vsindex` operator.

The script records 38 glyphs at the default instance (wght 200), at the named instances Regular and Bold, at the axis
maximum (wght 900) and at two user locations between them (wght 350 and 850). The glyphs blend in the charstring, blend
only in a subroutine, switch to `vsindex` 1, and do not blend at all. The script checks that the list has each kind.

For each glyph at each location the file holds what fontTools gives with `TTFont.getGlyphSet(location=..., normalized=True)`:
the outline that a `RecordingPen` records, the exact bounds that a `BoundsPen` finds, and the advance width, which `HVAR`
varies. fontTools gets the F2Dot14 coordinates that the library normalises to, which the file also holds, so both compute
with the same coordinates.

To write the file again, install fontTools 4.68 or later and run this from the repository root:

```
python tests/tools/generate_cff2_outline_reference.py
```

The output is deterministic for one fontTools version. The file records the version that wrote it.

## Metrics variation reference

`generate_metrics_variation_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Variations/MetricsVariations.fonttools.json`,
the fontTools reference for the advances, side bearings and global metrics of variation instances (issue #128).
Neither variable test font has `HVAR` side bearing mappings or `VVAR`, and their `MVAR` varies only the strikeout position
and the heights, so the script builds three small TrueType fonts that have all of them, next to the reference:

- `VariationMetrics.ttf` has the axes `wght` and `wdth` and six glyphs. Its `HVAR` has an advance width map that shares one
  row between glyphs, a left side bearing map that leaves a glyph out, and a right side bearing map. It has `vhea`, `vmtx`
  and a `VVAR` with the matching maps, and an `MVAR` that varies every metric that the library reads: the ascender,
  descender and line gap, the win ascent and descent, the `vhea` metrics, and the subscript, superscript, strikeout and
  underline values. Its `hhea`, typo and win values differ, and its `USE_TYPO_METRICS` bit is clear.
- `VariationMetricsTypo.ttf` is the same font with the `USE_TYPO_METRICS` bit set.
- `VariationMetricsGvar.ttf` is the first font with a `gvar` table (issue #127). Its tuples move the points of each glyph and
  its four phantom points, by deltas that are not those of `HVAR` and `VVAR`, so the font tells which source the library takes.
  `HVAR` and `VVAR` win for the advances, as they do in HarfBuzz, and the library also gives the left and top side bearings that
  a mapping gives, which HarfBuzz does not: it takes both bearings from the varied outline, measured from the phantom points,
  whatever the tables map. The reference records the values of the tables like those of the other fonts, and records under
  `phantom` what the phantom points give: the advances, the left side bearing (the left edge of the varied outline less the
  first phantom point) and the top side bearing. They come from fontTools on a copy of the font that has no `HVAR` and `VVAR`,
  and the script checks the advances, and the bearings, against HarfBuzz. Under `harfbuzz` the reference holds what HarfBuzz gives
  for three edits that the tests make: the font without `vmtx`, where every glyph has the advance height of the font; an advance
  width map of no entries; and an advance height map of no entries, which HarfBuzz reads as no map. The tests break or remove a
  table, or the mapping of a bearing, and expect those values.

The glyphs of the first two fonts have no `gvar` variation, so only their metrics change.

For these three fonts and the two variable fonts of the test project, the file holds, at the axis minimum, a named instance, the
axis maximum and some user locations, the advance width of each glyph that `hmtx` lists and the advance height of each glyph
that `vmtx` lists (`TTFont.getGlyphSet(location=..., normalized=True)`, rounded as `otRound` does), the global metrics, which
are the value of a table plus the rounded `MVAR` delta of its tag, and the `MVAR` delta of every tag. For the built fonts
it also holds the left and top side bearings that the `HVAR` and `VVAR` maps give, which no fontTools function returns:
the script adds the `VarStoreInstancer` delta to the `hmtx` or `vmtx` value.

The script also builds two CFF2 fonts, `VariationCff2Vertical.otf` and `VariationCff2VerticalTsb.otf`, and writes
`Cff2VerticalOrigins.fonttools.json` for them. Both have the axis `wght`, `vhea`, `vmtx` and a `VVAR` with advance heights, and
the tops of their glyphs move with `wght`. The first maps no top side bearing, so the bearing of an instance is the vertical
origin of the default outline (its top plus the `vmtx` bearing) minus the top of the varied outline. The second has a `VVAR`
top side bearing map. The file holds the top and the advance height that fontTools gives, which the script checks against
HarfBuzz, and the bearing that follows. HarfBuzz is no reference for the origin: for a glyph outside `glyf` it ignores the
`vmtx` bearing and centres the glyph in its advance height.

The script also checks that HarfBuzz varies a `vhea` descent stored as a positive number as the library does: it adds the
`MVAR` delta to the stored value, and then negates a sum above zero.

fontTools gets the F2Dot14 coordinates that the library normalises to, which the file holds. The script then checks the advances
and global metrics against HarfBuzz at the same coordinates, and stops when they differ. It also stops when a value is
within 0.0001 of a half but not on it, because the library's single precision could round that one the other way.

To write the fonts and the file again, install fontTools 4.68 or later and uharfbuzz, and run this from the repository root:

```
python tests/tools/generate_metrics_variation_reference.py
```

The output is deterministic for one fontTools version. The file records the version that wrote it.

## TrueType variation reference

`generate_gvar_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Variations/Gvar.fonttools.json`, the fontTools
reference for the outlines, bounds and advances of a TrueType variation instance (issue #127). `Teko-Variable.ttf` is the one
variable test font with TrueType outlines. The script draws it with fontTools `getGlyphSet(location=..., normalized=True)` at the
default instance, the "SemiBold" named instance, the maximum and a user location. Each location is the F2Dot14 coordinates that
the font variations reference gives, as normalised coordinates. fontTools applies the `gvar` deltas with interpolation of
untouched points, the deltas of the component offsets of composite glyphs, and the deltas of the phantom points.

For every glyph the reference holds the control bounds of the varied outline, the advance width that the phantom points give
(`advances`), the advance width that `HVAR` gives (`hvarAdvances`), and the left side bearing. Teko has an `HVAR` table, which a
font program then uses for the advance, so the script reads the phantom point advances from a copy of the font that has none, with
HarfBuzz (install uharfbuzz). HarfBuzz and FreeType give a composite glyph the phantom points of its component that has the
USE_MY_METRICS flag, where fontTools keeps those of the composite, and six glyphs of Teko differ in the two. The `HVAR` advances
come from the font as it is, and the script stops when HarfBuzz and fontTools differ. `HVAR` and the phantom points give the same
advance for every glyph but six composites (Edieresis, Odieresis, Ohungarumlaut, Uhungarumlaut, Ydieresis and ydieresis), away
from the default instance. Each is a mark and a letter that has USE_MY_METRICS, `HVAR` has a delta for the composite, and `gvar`
moves the phantom points of the letter. The script stops when another glyph differs. fontTools takes the bounds of a composite glyph
from its unvaried components, so the script measures the drawn outline instead. For a sample of the glyphs, which has the Basic Latin glyphs, every fourth composite and the
empty glyphs, it also holds the outline as the path commands that the font program fixtures use. The script checks that the first
phantom point of every glyph is the origin, which the left side bearing relies on.

To write the file again, install fontTools 4.68 or later, and run this from the repository root:

```
python tests/tools/generate_gvar_reference.py
```

The output is deterministic for one fontTools version. The reference records the version that wrote it.

## Hinted variable font

`generate_hinted_variable_font.py` builds `tests/PinataLabs.Typography.Tests/Fonts/Variations/HintedVariable.ttf` and writes
`HintedVariable.fonttools.json` next to it (issues #127 and #130). Neither variable test font has a `cvar` table or hinting
instructions that depend on the control values, so this font has them. It has the axes `wght` and `wdth`, with ranges that are powers
of two on both sides of the default, so that every location of the reference is an exact F2Dot14 coordinate. It has 1024 units to the
em, so that a font unit is a whole number of 1/64 pixel at 16 and 32 pixels to the em.

The font has `gvar` tuples that leave points out, cover every point, span an intermediate region and use two axes, composite
glyphs whose components have offsets, a scaled offset, and a placement by points, a glyph with phantom point deltas for the advance and
the vertical advance, a glyph whose first phantom point moves, two composites with a component that has the USE_MY_METRICS flag, a `cvar` table with tuples on each axis and on an intermediate region, an `fpgm` function, a `prep` program that
sets a control value from another, and glyphs whose instructions go to a control value, call the function, or use the instructions
GETVARIATION and GETINFO. The script's own documentation lists each glyph.

The reference holds, at five locations:

- the control values of the instance, from `fontTools.varLib.instancer`, which sums the `cvar` deltas and rounds once;
- the outline, the bounds, the advances and the side bearings of each glyph from fontTools `getGlyphSet`, where fontTools can draw the glyph;
- the advances and extents of each glyph from HarfBuzz, which moves an outline by its first phantom point and takes the phantom points of
  a composite from its USE_MY_METRICS component, as FreeType does and fontTools does not;
- the unhinted outline of each glyph from FreeType at 16384 pixels to the em, which keeps it to 1/1024 of a font unit;
- the hinted outlines of the four hinted glyphs at 16 and 32 pixels to the em, from FreeType with its version 35 interpreter.

The script also builds `HintedVariableMetricsTables.ttf` and writes `HintedVariableMetricsTables.fonttools.json`: the same font with
two glyphs more and an `HVAR` and a `VVAR`, whose advances and bearings are not the deltas of the phantom points. FreeType leaves
the deltas of `gvar` out of the phantom points of a direction when the font has the table of it, so the outline does not move by the
first phantom point, and the advance is the table's. R is a composite whose right edge goes to the second phantom point, and T is a
box whose top and bottom go to the third and fourth. The reference holds the hinted outlines of L, R, T and H, from FreeType, and the
advances and extents from HarfBuzz, which still moves the outline by the first phantom point.

To write the font and the reference again, install fontTools 4.68 or later, `freetype-py` and `uharfbuzz`, and run this from the repository root:

```
python tests/tools/generate_hinted_variable_font.py
```

The output is deterministic for one fontTools and FreeType version. The reference records both.

## GPOS variations reference

`generate_gpos_variations_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Variations/GposVariations.harfbuzz.json`,
the HarfBuzz reference for the GPOS `VariationIndex` deltas of the two variable test fonts (issue #129). `Teko-Variable.ttf`
has 2,385 of these device tables, all in mark anchors, and `SourceSans3VF-Upright.otf` has 6,815, in kerning and in mark
anchors. Teko has no kerning. Neither font has a delta in mark-to-ligature or cursive attachment, so
`generate_gpos_anchor_variations_font.py` builds `GposAnchorVariations.ttf` with a delta of a known size in every kind
of anchor and in value records, and the reference script runs it first.

The script finds the glyph sequences that the device tables of the two fonts position, and keeps an even stride of them,
in four kinds of row:

- `pair`: two glyphs that a PairPos value record with a device moves, from lookups of format 1 and format 2.
- `mark`: a base and a mark that a MarkBasePos anchor with a device attaches.
- `chain`: a base and two marks, where the second attaches to the first. It only moves because the first mark does.
- `static`: a pair that a value record with no device moves, which does not change with the weight.

`GposAnchorVariations.ttf` has the kinds `single`, `mark`, `markmark`, `ligature`, `ligature0` and `cursive`, and the file
holds every text of it. The font has the axis wght from 100 to 900 with the default 400, and one region from 400 to 900,
so that the scalar of a delta is 0.5 at wght 650 and a delta of 1, 3 or 5 is half a unit, one and a half or two and a half.
Each glyph pair meets every pair of deltas, in the sizes 0, 1, -1, 3, -3, 5 and -5.

A text of the two real fonts is in the file when HarfBuzz shapes it to the glyphs that its rules name, so that substitution
does not hide the positioning, and when its positions differ between the default instance and another weight, or for
`static` rows when they do not. HarfBuzz shapes each text at the default instance, at masters and at weights between them,
where a delta is fractional. A glyph holds its ID, the x of its origin from the start of the line, its y offset, its final
advance, its advance before positioning, and whether it is a mark.

What the file says about rounding, which the tests check against HarfBuzz 15.0.0:

- A delta of a value record rounds on its own, and the value record adds it to the whole units of the table.
- A mark attaches at the base anchor minus the mark anchor, each with its fractional delta, rounded once.
- A cursive chain rounds the anchor of each glyph for the advance, and the difference of the anchors once for the offset.
- A half rounds up, so 2.5 is 3 and -2.5 is -2.

The advance before positioning is what HarfBuzz gives for the glyph at that weight, with HVAR. `GposVariationTests` takes the
same number from the library, and compares what GPOS adds to it. Outlines and advances of a variation instance
follow other issues, and the test holds before and after they land.

To write the file again, install fontTools 4.68 or later and uharfbuzz, and run this from the repository root:

```
python tests/tools/generate_gpos_variations_reference.py
```

That run also writes `GposAnchorVariations.ttf`.

The output is deterministic for one fontTools and HarfBuzz version. The file records the HarfBuzz version.

## FeatureVariations font

`generate_feature_variations_font.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Variations/FeatureVariations.ttf` and
its HarfBuzz reference `FeatureVariations.harfbuzz.json` (issue #131). Neither variable test font has a `FeatureVariations`
table, so the script builds a font that has them on both GSUB and GPOS, with fontTools:

- The axes are `wght` (100, 400, 900) and `wdth` (75, 100, 125). The glyphs are empty, so each position and advance comes from
  the glyph ID and the lookups alone. The font has no `HVAR`, `gvar` or `avar`.
- `rvrn` comes from `varLib.featureVars.addFeatureVariations`. wght 0.5 to 1 substitutes `C.rvrn` for `C`, and wdth 0.5 to 1
  substitutes `B.rvrn` for `B`. The two regions overlap, which fontTools turns into three boxes.
- The GPOS feature `kern` kerns `A V` by -100. The records come from `addFeatureVariationsRaw`, and the script edits them.
  The first one that holds decides. wght 0.5 to 1 swaps in a lookup that kerns `A V` by -300. wght 0.2 to 1 and wdth 0.5 to 1
  together keep the lookup of the feature and add one that kerns by -500. wght -1 to -0.5 swaps in one that kerns by -20.
- Four more records, which hold for wght -0.25 to 0.125, use the condition formats of OpenType 1.9 and swap in lookups
  that kern `A V` by -410 to -440: format 2, a value that a GDEF Item Variation Store varies, which the script adds, and
  format 3, 4 and 5, which are AND, OR and NOT of format 1 conditions. Every boundary is a power of 2 fraction, because
  HarfBuzz and fontTools round a value such as 0.1 to F2Dot14 differently in the last bit.

The reference holds the glyph IDs, positions and advance that HarfBuzz gives 10 texts at 96 locations, and the two
kerning texts at 105 locations that split the range of wdth. The locations are on both sides of the boundary of each
condition and on it, because a range includes its ends. `FeatureVariationsTests` shapes each text with the library at the
same location and compares them.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the repository
root:

```
python tests/tools/generate_feature_variations_font.py
```

The output is deterministic for one fontTools and HarfBuzz version. The reference records the HarfBuzz version.

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
- `NormalizationArabicForms.ttf` has every Arabic letter, mark and presentation form, and no GSUB table.
  `NormalizationArabicFallback.ttf` has a few Arabic letters, marks, forms and ligatures.
  `NormalizationArabicFallbackWithoutMedial.ttf` lacks the medial forms, `NormalizationArabicFallbackRlig.ttf` has a
  GSUB table with only `rlig`, and `NormalizationArabicFallbackInit.ttf` has an `init` feature. HarfBuzz uses the
  presentation forms in all but the last.
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

## Arabic fallback table

`generate_arabic_fallback_table.py` writes `ArabicFallbackTable.Generated.cs` in
`src/PinataLabs.Typography/Tables/AdvancedTypographic/Shapers`. When the GSUB table of a font has none of the joining
form features for Arabic, HarfBuzz gives each letter the presentation form that the font has, and then ligates some
of the forms. The table holds the four forms of each letter, from `UnicodeData.txt`, and the ligatures that HarfBuzz
uses. The script finds those ligatures: it shapes each Arabic ligature of `UnicodeData.txt` in a probe font that has
every Arabic letter, mark and presentation form, and keeps the ligatures that HarfBuzz gives.

To write the table again, install fontTools 4.68 or later and uharfbuzz, and run this from the repository root:

```
python tests/tools/generate_arabic_fallback_table.py
```

The output is deterministic for one HarfBuzz version and one `UnicodeData.txt`. The file records the HarfBuzz version.
The Arabic cases of the normalization reference check the table and its use against HarfBuzz.

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

## Universal shaper reference

`generate_universal_shaper_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/UniversalShaper.harfbuzz.json`.

It builds `UniversalShaperJoining.ttf`. The font has every character of the eleven scripts whose letters the
universal shaper of HarfBuzz joins: Adlam, Chorasmian, Hanifi Rohingya, Mandaic, Manichaean, Mongolian, N'Ko,
Old Uyghur, Phags-pa, Psalter Pahlavi and Sogdian. Each character has a glyph for each of the `isol`, `init`, `medi`
and `fina` features. The `joining` list of the reference holds the glyph IDs that HarfBuzz gives for:

- each character alone, after a letter that joins on both sides, before that letter, and between two of them;
- every text of two or three characters, from one character of each joining type and general category of the script;
- a Mongolian letter and a free variation selector. Each feature has a ligature of the two, which HarfBuzz forms
  only when the selector has the feature of the letter.

The font has no other character that is Default_Ignorable_Code_Point. HarfBuzz takes the glyph of such a character
out of its output, and the library keeps it and does not draw it.

`UniversalShaperTests` checks that the library gives the same glyphs.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_universal_shaper_reference.py
```

The output is deterministic for one HarfBuzz version and one set of Unicode data files. The reference records the
HarfBuzz version that wrote it.

## Universal shaper categories

`derive_harfbuzz_use_categories.py` writes `tests/UnicodeTestData/UniversalShapingCategory.harfbuzz.txt`, the
universal shaper (USE) category of every code point in HarfBuzz 15.0.0.

HarfBuzz builds its category table with `gen-use-table.py` and ships it as `src/hb-ot-shaper-use-table.hh`. HarfBuzz
has no API that returns a category, so the script downloads that file at the HarfBuzz tag and runs its lookup for
every code point. The output lists each range whose category is not `O`.

`UniversalShapingCategoryTests` checks that the library gives every code point the same category.

To write the reference again, run this from the repository root. It needs only Python and network access, or the
path of a local copy of `hb-ot-shaper-use-table.hh`:

```
python tests/tools/derive_harfbuzz_use_categories.py
```

The output is deterministic for one HarfBuzz version.

## Syllable reference

`generate_use_syllable_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Syllables.harfbuzz.json`.

It builds two fonts that have no GSUB table:

- `SyllablesBalinese.ttf` has a Balinese letter, two vowel signs, the dotted circle, and the combining grapheme
  joiner, ZWJ and ZWNJ. The universal shaper of HarfBuzz leaves these three out when it finds syllables, so a sign
  after one of them stays in the syllable of the letter before it. The `joiners` rows hold such texts.
- `SyllablesInvalid.ttf` has the characters of `IndicShapingInvalidCluster.txt`. The `invalid` rows hold each
  sequence of that file, which HarfBuzz breaks with a dotted circle.

HarfBuzz shapes with `HB_BUFFER_FLAG_PRESERVE_DEFAULT_IGNORABLES`, so it keeps the joiners in its output as the
library does. `SyllableTests` checks that the library gives the same glyphs and positions.

To write the fonts and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_use_syllable_reference.py
```

The output is deterministic for one HarfBuzz version and one set of Unicode data files. The reference records the
HarfBuzz version that wrote it.

## Shaper choice reference

`generate_shaper_choice_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/ShaperChoice.harfbuzz.json`.
HarfBuzz chooses a shaper from the script of the text and from the script tag that it finds in the GSUB table of
the font. The script builds seven fonts that differ only in the scripts of their GSUB table:

- `ShaperChoiceTag1.ttf` has the first tag that HarfBuzz looks for, for each script of `ScriptTags.harfbuzz.json`,
  such as `dev3`, `mym2` and `latn`. `ShaperChoiceTag2.ttf` has the second tag, such as `dev2` and `mymr`, and
  `ShaperChoiceTag3.ttf` has the third, such as `deva`.
- `ShaperChoiceDFLT.ttf`, `ShaperChoiceLowercaseDflt.ttf` and `ShaperChoiceLatn.ttf` have only `DFLT`, `dflt` or `latn`.
- `ShaperChoiceNone.ttf` has only `zzzz`, which HarfBuzz looks for in no script.

Each font has one glyph for the first letter of every script, a glyph for U+0301, and no GPOS table. Every script of
the GSUB table has the features `nukt`, `pref`, `pres`, `haln` and `isol`, and each of them puts a glyph of its own
after the letter. The features that HarfBuzz applies, and what it does with the mark, show its shaper. For example,
only the universal shaper applies both `nukt` and `pref`, and only the default and Arabic shapers place the mark.

The reference holds the glyph IDs and positions that HarfBuzz gives for the letter and the mark of each script, in
each font that has a tag for the script.

The script also builds `ShaperChoicePositioning.ttf`. Its GSUB and GPOS tables have only `DFLT`, and its GPOS
features `abvm`, `blwm` and `dist` move the letter by 1, 2 and 4 units. Every shaper of HarfBuzz applies the three,
so the reference has each letter 7 units from its place. It has no Hebrew letter for this font: HarfBuzz does not
use the GPOS table of a font that has no `hebr` script for Hebrew text.

`ShaperChoiceTests` checks that the library gives the same.

To write the fonts and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root. Run `derive_harfbuzz_script_tags.py` first when the script tags can have changed:

```
python tests/tools/generate_shaper_choice_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Cluster reference

`generate_cluster_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Clusters.harfbuzz.json`. It holds how
HarfBuzz shapes a sign that has no letter of its script before it.

The script builds two fonts that have no GSUB table, so only the shaper changes the order and the number of the
glyphs:

- `ClustersBalinese.ttf` has Balinese letters and signs, the dotted circle, and the characters of the Basic Latin
  block and some others that have the script Common. HarfBuzz shapes Balinese text with its universal shaper.
- `ClustersDevanagari.ttf` has the same for Devanagari, which HarfBuzz shapes with its Indic shaper.

The reference holds the glyph IDs that HarfBuzz gives, and where it draws each glyph, for:

- each sign alone, before a letter, and after a letter and a space. Such a sign starts a broken cluster, and HarfBuzz
  gives it a dotted circle as its base.
- each sign after each other sign. These pairs have no virama: HarfBuzz joins a virama and some of the signs next to
  it in one cluster, and the library does not. When the first sign goes in front of the base, the sign after it
  goes there too, before the dotted circle.
- each Common character before a vowel sign that goes above or after the base, and before one that goes in front of
  it. The universal shaper takes most of these characters as the base of the sign.

The fonts have no U+2015, U+2022 or U+25FB. They are bases in the Indic shaper of HarfBuzz and not in the library.

`ClusterTests` checks that the library gives the same glyphs in the same places.

To write the fonts and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_cluster_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Default-ignorable reference

`generate_default_ignorable_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/DefaultIgnorables.harfbuzz.json`.
After shaping, HarfBuzz gives each default-ignorable character that GSUB did not substitute no advance and no
offset. It draws the character with the glyph of U+0020 when the font has one, and takes it out of its output
otherwise.

The script builds `DefaultIgnorables.ttf`, which has U+0020, and `DefaultIgnorablesNoSpace.ttf`, which does not.
Both have Latin, Arabic, Mongolian and Sinhala letters and the characters ZWJ, ZWNJ, the Mongolian free variation
selectors, the Mongolian vowel separator, COMBINING GRAPHEME JOINER, SOFT HYPHEN and WORD JOINER, each 600 units
wide. They also have the Hangul fillers and SHORTHAND FORMAT LETTER OVERLAP, which Unicode makes default-ignorable
and HarfBuzz draws with their own glyphs. The `ccmp` feature of the first substitutes WORD JOINER, which HarfBuzz then
keeps. The reference also has
text with ZWJ and ZWNJ in Segoe UI, Noto Sans Devanagari, Noto Sans Bengali and Open Sans, which has no glyph for
ZWJ. It holds the glyph IDs, where HarfBuzz draws each glyph, and the advance of each text.

`DefaultIgnorableTests` checks that the library gives the same glyphs and advance, and that measurement and line
breaking follow the glyphs.

To write the fonts and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_default_ignorable_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Chain backtrack reference

`generate_chain_backtrack_reference.py` builds `tests/PinataLabs.Typography.Tests/Fonts/ChainBacktrack.ttf` and writes
`ChainBacktrack.harfbuzz.json` next to it. A chained context or reverse chaining subtable lists its backtrack glyphs
from the glyph nearest to the input to the farthest one. The `calt` feature of the font has a chained context
subtable of each format and a reverse chaining subtable, each with the backtrack "a b", so each replaces its input
glyph after "a b" and not after "b a". A last reverse chaining subtable covers two glyphs and has a substitute for
only the first, so HarfBuzz leaves the second as it is. The reference holds the glyph IDs that HarfBuzz gives.

`ChainBacktrackTests` checks that the library gives the same glyphs.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_chain_backtrack_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Lookup flags reference

`generate_lookup_flags_reference.py` builds `tests/PinataLabs.Typography.Tests/Fonts/LookupFlags.ttf` and writes
`LookupFlags.harfbuzz.json` next to it. Each case of the font has glyphs of its own: a GPOS chained context lookup of
each format that applies a nested lookup past a mark that IgnoreMarks skips, a mark-to-mark lookup whose mark filtering
set leaves out the mark between the two marks it attaches, a GSUB and a GPOS lookup with IgnoreMarks that cover the
mark that starts the text, a chained context substitution that calls a reverse chaining lookup, and cursive chains of
three glyphs with and without the RightToLeft flag, in left to right and right to left text. fontTools picks the
smallest format of a chained context subtable, so the script makes it pick the format that each case names. The
reference holds the glyph ID and the place of each glyph that HarfBuzz gives.

`LookupFlagsTests` checks that the library puts the same glyphs in the same places.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_lookup_flags_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Text run context reference

`generate_text_run_context_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/TextRunContext.harfbuzz.json`.
A caller of HarfBuzz that styles a word in two runs shapes each run in a buffer of its own and gives the buffer the
text before and after the run as context. HarfBuzz then joins the letters at the boundary as in one run, and applies
no GSUB lookup across it.

For each word and each grapheme boundary, the reference holds the glyph IDs of both runs, left to right. The words
are Arabic in Segoe UI, Dubai and me_quran, and Mongolian in `UniversalShaperJoining.ttf`. `Issues_109` checks that
the library gives the same glyphs when a `TextRun` styles the second part of the word.

To write the reference again, install uharfbuzz and run this from the repository root:

```
python tests/tools/generate_text_run_context_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Thai private use reference

`generate_thai_pua_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/ThaiPua.harfbuzz.json`. When the
GSUB table of a font has no `thai` script, HarfBuzz draws a Thai mark that would clash with its base or with the mark
before it with a glyph of the private use area: a tone mark lower or further left, a vowel below a consonant with a
descender lower, and YO YING and THO THAN without their descender. It looks for the Windows code point first and
then for the Mac one.

The script builds `ThaiPua.ttf`, with the Windows and Mac private use glyphs and no GSUB table, `ThaiPuaMac.ttf`,
with the Mac glyphs only, and `ThaiPuaGsub.ttf`, with both and a GSUB table that has the `thai` script. The reference
holds the glyph IDs and where HarfBuzz draws each glyph for each kind of consonant, a digit and a space, followed by
one or two marks, in these fonts and in `FDArrayTest257.otf`. `ThaiPuaTests` checks that the library gives the same.

To write the fonts and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_thai_pua_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Universal shaper forms reference

`generate_universal_forms_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/UniversalForms.harfbuzz.json`.
The universal shaper of HarfBuzz applies `isol`, `init`, `medi` and `fina` to every script. For a script whose
letters do not join, it gives them by syllable: a run of syllables takes `init` on the first, `medi` on the middle
ones and `fina` on the last, and a syllable alone takes `isol`. A character outside every syllable, such as WORD
JOINER, ends the run.

The script builds `UniversalForms.ttf`, with Balinese, Tibetan and Javanese letters and signs, the space, digits,
ZWJ, ZWNJ, WORD JOINER and the dotted circle. Each of the four features gives every glyph a glyph of its own. The
reference holds the glyph IDs and where HarfBuzz draws each glyph for texts of one to four syllables.
`UniversalShaperTests` checks that the library gives the same.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_universal_forms_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Hebrew marks reference

`generate_hebrew_marks_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/HebrewMarks.harfbuzz.json`. It
holds where HarfBuzz draws Hebrew letters and the GPOS marks on them, in three cases:

- `GposKernAcrossMark`: `Segoeui.ttf` kerns lamed and final mem in a GPOS lookup that ignores marks. HarfBuzz skips
  the qamats between the two letters and kerns them.
- `ThreeMarks`: `TimesNewRoman.ttf` and `Segoeui.ttf` attach qamats to an alef, meteg to the qamats and etnahta to
  the meteg. HarfBuzz attaches a mark to the mark before it when the two marks belong to the same base.
- `KernTableMovesBase`: the script builds `HebrewKernMark.ttf`. It has a `kern` table, and a GPOS table with a
  `mark` feature and no `kern` feature. HarfBuzz kerns with the `kern` table after the GPOS lookups and then places
  each mark from its base, so a mark moves with a base that the `kern` table moved. The font has Latin letters too,
  for the same case in text that runs left to right.

`HebrewMarkTests` checks that the library draws each glyph in the same place.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_hebrew_marks_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Khmer reference

`generate_khmer_reference.py` writes `tests/PinataLabs.Typography.Tests/Fonts/Khmer.harfbuzz.json`, how the Khmer
shaper of HarfBuzz shapes 1350 syllables in `NotoSansKhmer-Regular.ttf`: a consonant with a vowel, with one or two
subscript consonants before the vowel, with a register shifter, with ROBAT and with a sign after the vowel. The
reference holds the glyph IDs and where HarfBuzz draws each glyph. `KhmerTests` checks that the library gives the
same, except for the syllables that #114 tracks.

To write the reference again, install uharfbuzz and run this from the repository root:

```
python tests/tools/generate_khmer_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Mongolian lookups reference

`generate_mongolian_lookups_reference.py` builds `tests/PinataLabs.Typography.Tests/Fonts/MongolianLookups.ttf` and
writes `MongolianLookups.harfbuzz.json` next to it (issue #111). Mongolian Baiti cannot ship in the repository, so
the font copies the four things in it that the library shaped differently from HarfBuzz. Each row of the reference
names one of them:

- `joining`: the Mongolian vowel separator U+180E and the Arabic number sign U+0600 have joining type U, so the
  letters next to them take their isolated forms.
- `context-match`: a chaining subtable that matches with no substitution records, from an `ignore sub` rule, ends
  its lookup.
- `mark-filtering-set`: a lookup with the UseMarkFilteringSet flag skips the marks that are not in its GDEF mark
  glyph set.
- `reverse-chaining`: a reverse chaining lookup runs from the last glyph to the first, skips the glyphs that its
  flags ignore, and substitutes the glyph at its Coverage index.

The reference holds the glyph IDs, where HarfBuzz draws each glyph, and the advance of each text.
`MongolianLookupsTests` checks that the library gives the same glyphs, one test case for each cause.

To write the font and the reference again, install fontTools 4.68 or later and uharfbuzz, and run this from the
repository root:

```
python tests/tools/generate_mongolian_lookups_reference.py
```

The output is deterministic for one HarfBuzz version. The reference records the version that wrote it.

## Font matching fonts

`generate_font_matching_fonts.py` writes the Matching Sans fonts into
`tests/PinataLabs.Typography.Tests/Fonts/Matching`, the static family that the font matching tests use
(issue #123). The family has nine static weights from 100 to 900, three italic faces (300, 400 and 700), two oblique
faces (10 and 20 degrees), and faces with `usWidthClass` 2, 3, 7 and 8 (62.5%, 75%, 125% and 150%), 19 fonts in all.
That is more faces than the four that name ID 1 can group, so the family exists only under the typographic family name.

The fonts are built as the faces of a real family are:

- Regular, Italic, Bold and Bold Italic have name ID 1 "Matching Sans" and no name IDs 16 and 17, so they test the
  fallback to ID 1.
- Every other face has its own name ID 1, such as "Matching Sans Light", and the typographic names ID 16
  "Matching Sans" and ID 17.
- `OS/2.usWeightClass` and `usWidthClass` give the weight and width, `fsSelection` `ITALIC` or `OBLIQUE` gives the
  slope, and `post.italicAngle` gives the angle of an oblique face.
- Glyph `A` has an advance that is different in each face, so a test can tell which face a layout drew.

`FontMatchingTests` adds the files of the faces that a case needs to a `FontCollection`, so each case has its own
family. To write the fonts again, install fontTools 4.68 or later and run this from the repository root:

```
python tests/tools/generate_font_matching_fonts.py
```

The output is deterministic, so a second run changes no file.
