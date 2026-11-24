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
