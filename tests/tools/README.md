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

## GSUB lookup order font

`generate_gsub_lookup_order_font.py` builds `tests/PinataLabs.Typography.Tests/Fonts/GsubLookupOrder.ttf`
from feature code with fontTools feaLib. The LookupList of the font has the `vrtr` lookup first, then `liga`,
then `calt`. `GsubLookupOrderTests` uses the font to check that lookups are applied in LookupList order.

To write the font again, run this from the repository root:

```
python tests/tools/generate_gsub_lookup_order_font.py
```

The output is deterministic.
