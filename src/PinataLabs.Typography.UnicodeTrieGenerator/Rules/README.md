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

To regenerate the files in `src/PinataLabs.Typography.Unicode/Resources`, run this from the repository root:

```
dotnet run --project src/PinataLabs.Typography.UnicodeTrieGenerator
```

The generator reads the Unicode version from the first line of each of these files (such as `# LineBreak-18.0.0.txt`), and from the `# Version: 18.0.0` line of `emoji-data.txt`. It fails, before it writes any file, when they differ. `UnicodeData.txt` has no version header and `DerivedAge.txt` is not in the set, and the HarfBuzz files name no Unicode version, so those are not checked. Replace them with the rest. It writes the version to `UnicodeInfo.Generated.cs`, which is what `UnicodeInfo.Version` reports. To move to a new Unicode version, replace the files together with the conformance suites in `tests/UnicodeTestData`, regenerate, and say in `CHANGELOG.md` that the Unicode version changed.
