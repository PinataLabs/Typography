The Unicode files are Unicode 18.0.0. HarfBuzz 15.0.0 uses the same version.

Files sourced from:

- https://www.unicode.org/Public/18.0.0/ucd/ : `ArabicShaping.txt`, `BidiBrackets.txt`, `BidiMirroring.txt`,
  `Blocks.txt`, `CompositionExclusions.txt`, `DerivedCoreProperties.txt`, `EastAsianWidth.txt`,
  `IndicPositionalCategory.txt`, `IndicSyllabicCategory.txt`, `LineBreak.txt`, `PropertyValueAliases.txt`,
  `ScriptExtensions.txt`, `Scripts.txt`, `UnicodeData.txt` and `VerticalOrientation.txt`
- https://www.unicode.org/Public/18.0.0/ucd/extracted/ : `DerivedBidiClass.txt` and `DerivedGeneralCategory.txt`
- https://www.unicode.org/Public/18.0.0/ucd/auxiliary/ : `GraphemeBreakProperty.txt`
- https://www.unicode.org/Public/18.0.0/ucd/emoji/ : `emoji-data.txt`
- https://github.com/harfbuzz/harfbuzz/tree/15.0.0/src/ms-use : `IndicPositionalCategory-Additional.txt`,
  `IndicSyllabicCategory-Additional.txt` and `IndicShapingInvalidCluster.txt`. They come from
  https://github.com/microsoft/font-tools, and HarfBuzz keeps them up to date with each Unicode version.

`HarfBuzzMarkOrder.txt` is not a Unicode file. `tests/tools/derive_harfbuzz_mark_order.py` writes it from the output of HarfBuzz.

To regenerate the files in `src/PinataLabs.Typography/Unicode/Resources`, run this from the repository root:

```
dotnet run --project src/PinataLabs.Typography.UnicodeTrieGenerator
```
