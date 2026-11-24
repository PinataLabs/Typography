---
status: proposed
---

# Variable font instances

Issue #46 asks how a caller picks a variation instance of a variable font, and how the instance changes outlines, metrics and positioning. Today the library draws every variable font at its default instance. Nothing reads `fvar`, `avar`, `gvar`, `HVAR`, `VVAR` or `MVAR`. `Cff2Parser.ReadRegionCounts` reads only the region count of each `ItemVariationData`, so `CffEvaluationEngine.Blend` can pop and drop the deltas. `GlyphDefinitionTable` reads `itemVarStoreOffset` and never uses it. `AnchorFormat3` and `ValueRecord` keep their device offsets and never resolve them. `GPosTable` and `GSubTable` read `featureVariationsOffset` and leave a TODO.

We decide the following. A caller passes user coordinates to `FontFamily.CreateFont` as `FontVariation` values. The library normalises them through `fvar` and `avar` into an immutable set of normalised coordinates. It then hands the caller's `Font` a separate `StreamFontMetrics` for that variation instance, which shares the parsed tables of the face and owns its own glyph caches. The first release applies CFF2 `blend`, `gvar` (with phantom points), `HVAR`, `VVAR`, `MVAR`, and the GPOS `VariationIndex` deltas stored in the GDEF Item Variation Store. One Item Variation Store reader serves CFF2, `HVAR`, `VVAR`, `MVAR` and GDEF. A non-default instance renders unhinted until `cvar` support lands. `FontProgram` stays at the default instance.

The two variable test fonts decide most of the scope. `Teko-Variable.ttf` (TrueType, `wght` 300 to 700) and `SourceSans3VF-Upright.otf` (CFF2, `wght` 200 to 900) both have `avar`, `HVAR`, `MVAR` and a GDEF 1.3 Item Variation Store. Their GPOS tables hold 2,385 and 6,815 `VariationIndex` device tables. Neither has GSUB or GPOS `FeatureVariations`, and neither has `cvar`. The counts come from fontTools 4.68.0 over the files in `tests/PinataLabs.Typography.Tests/Fonts/`.

Spec 001 requires an approved proposal for every public API change. Accepting this ADR is that approval for the API in decision 1, and each implementing PR still updates `PublicAPI.Unshipped.txt` and `CHANGELOG.md`.

## Decisions

### 1. Callers pass user coordinates when they create a `Font`

The public surface is a value type keyed by axis tag, two read-only descriptions on `FontMetrics`, and overloads that take variations where a `Font` is made.

```csharp
public readonly struct FontVariation : IEquatable<FontVariation>
{
	public FontVariation(Tag axis, float value); // value in user coordinates, e.g. 650 for wght
	public Tag Axis { get; }
	public float Value { get; }
}

public sealed class FontVariationAxis
{
	public Tag Tag { get; }
	public float MinValue { get; }
	public float DefaultValue { get; }
	public float MaxValue { get; }
	public bool IsHidden { get; }                    // fvar HIDDEN_AXIS flag
	public string Name(CultureInfo culture);         // fvar axisNameID through the name table
}

public sealed class FontNamedInstance
{
	public string Name(CultureInfo culture);         // fvar subfamilyNameID through the name table
	public IReadOnlyList<FontVariation> Variations { get; } // one entry per axis
}

// FontMetrics (abstract, internal constructor, so new abstract members break no one)
public abstract IReadOnlyList<FontVariationAxis> VariationAxes { get; }  // empty for a static font
public abstract IReadOnlyList<FontNamedInstance> NamedInstances { get; } // empty for a static font

// FontFamily
public Font CreateFont(float size, params ReadOnlySpan<FontVariation> variations);
public Font CreateFont(float size, FontStyle style, params ReadOnlySpan<FontVariation> variations);

// Font
public Font(Font prototype, params ReadOnlySpan<FontVariation> variations);
public IReadOnlyList<FontVariation> Variations { get; } // as requested, after duplicate removal
```

A named instance needs no API of its own. The caller passes `instance.Variations` to `CreateFont`. The rules for a request follow the CSS `font-variation-settings` property, so they match what web developers already expect:

- The library ignores a tag that the font has no axis for.
- It clamps a value to the axis minimum and maximum.
- When a tag repeats, the last value wins.
- On a static font, the variations have no effect and `Font.FontMetrics` is the face itself.

`Tag` is the existing public `PinataLabs.Typography.Tables.AdvancedTypographic.Tag`, which already has `Tag.Parse("wght")`.

Rejected alternatives:

- **Variations on `TextOptions`.** `TextOptions` applies to a whole layout call, and `TextRun` already carries a `Font` per run. A per-run variation is a per-run `Font`. A second channel would also split the glyph cache key across two objects.
- **A mutable `Font.SetVariation`.** `Font` is immutable today and is shared across threads through `TextOptions`. A setter makes every layout racy.
- **`Dictionary<string, float>` or `(string, float)` tuples.** These allow a tag longer than four characters and need the tag parsed on every lookup. `Tag` is already public and already validated.
- **`CreateFont(size, FontNamedInstance)`.** A named instance is a list of `FontVariation` values, so a second overload doubles the surface for no new capability.
- **Each named instance listed as a style in `FontCollection`.** `FontFamily` keys its faces by `FontStyle`, which has four values. A font with nine named weights cannot fit.

### 2. Bold and Italic map to `wght` and `ital` only when no static face matches

`Font.LoadInstanceInternal` falls back to the regular face when the family has no face for the requested style. Today a family that holds one variable font therefore draws `FontStyle.Bold` at the default weight. Under this decision the library picks the face as it does today. If the picked face's own style lacks `Bold` or `Italic` that the caller asked for, and the face has the matching axis, the library adds a variation, unless the caller already passed one for that axis:

- `Bold` adds `wght` 700, clamped to the axis range.
- `Italic` adds `ital` 1.

A static bold face in the same family still wins, so existing callers see no change. `slnt` is not mapped, because `FontStyle` has no oblique value. `wdth` is not mapped, because `FontStyle` has no width.

`Font.IsBold` and `Font.IsItalic` read `FontMetrics.Description.Style`. A variation instance shares the face's `FontDescription`, so the instance reports the face's `OS/2` style bits. `IsBold` and `IsItalic` become true when the instance's `wght` is 600 or more, or its `ital` is 1. This is the one behaviour change for an existing call, and it is listed under open questions.

Rejected alternatives:

- **No mapping.** `CreateFont(12, FontStyle.Bold)` on a variable family keeps drawing the default weight with no error. That is the current silent failure.
- **Always map, even when a static bold face exists.** A family that ships static faces next to a variable one would change its rendering for existing callers.
- **Map `Italic` to `slnt` when the face has no `ital` axis.** A slant of an upright design is an oblique, not an italic. CSS keeps the two apart, and so does this decision.

### 3. A variation instance is a `StreamFontMetrics` that shares the face's tables

Normalisation follows the OpenType Font Variations Overview, section "Coordinate Scales and Normalization". The library clamps each user coordinate to the axis range and maps it to -1, 0 or 1 at the minimum, default and maximum. It applies the `avar` segment maps (OpenType `avar` table, version 1), then converts each result to F2Dot14 exactly as steps 1 to 5 of that section say. The result is `NormalizedCoordinates`, an internal immutable type that holds one `short` per `fvar` axis, in `fvar` order. Two requests that normalise to the same F2Dot14 values are the same instance. Equality is exact, so the value works as a dictionary key.

When every coordinate is 0, the request is the default instance, and `Font.FontMetrics` returns the face itself. A default font costs nothing extra.

For any other coordinates, the face creates a `StreamFontMetrics` through a private constructor that takes the face and the coordinates. The new object references the same `TrueTypeFontTables` or `CompactFontTables`. Those are immutable after load and already shared across threads. The instance owns everything that depends on the coordinates:

- the varied global metrics (`MVAR`), computed once in its constructor;
- the region scalars for each Item Variation Store, computed once;
- its own `glyphCache` and `colorGlyphCache`, because a cached `GlyphMetrics` holds varied bounds and advances;
- its own `TrueTypeInterpreter` and lock, for when `cvar` support lands.

`FileFontMetrics` forwards instance creation to its lazy `StreamFontMetrics`. `FontMetrics` gains one internal abstract method that returns the instance for a set of variations.

The face keeps a `ConcurrentDictionary<NormalizedCoordinates, WeakReference<StreamFontMetrics>>`. `Font` holds its instance strongly through its existing `Lazy<FontMetrics?>`. Two `Font` objects with equal coordinates share one instance and one glyph cache while either is alive. When every such `Font` is gone, the collector frees the instance, and the next insert removes dead entries. If two threads race on the first lookup, both build an instance and one wins. Both are correct, so the race costs one extra object and needs no lock.

Varied values that the public types store as integers are rounded once, after all deltas are summed. That covers `GlyphMetrics.AdvanceWidth` (`ushort`), `LeftSideBearing` (`short`) and the `HorizontalMetrics` fields (`short`). Outline points stay `float`.

Memory per instance is small and fixed, apart from the caches. It is the coordinates (2 bytes per axis), the region scalars (4 bytes per region per store) and the metric fields. The glyph cache grows with the glyphs drawn, as it does for a static face today. For a varied TrueType glyph, the `GlyphMetrics` computes its varied points once and keeps them, the same way `TrueTypeGlyphMetrics.hintedVectorCache` keeps hinted points. Without that cache, every draw would decode `gvar` and run the IUP step again.

Rejected alternatives:

- **The instance owned by the `Font` alone, with no cache on the face.** Callers often call `family.CreateFont(size)` per draw. A static `Font` shares the face's glyph cache, so that pattern is cheap today. A per-`Font` instance would rebuild every glyph on every draw.
- **A strong, unbounded dictionary on the face.** A UI that animates `wght` creates a new instance for every frame, and none would ever be freed.
- **A fixed-size LRU on the face.** It evicts instances that live `Font` objects still use, so two `Font` objects with equal coordinates can hold two copies. The weak reference ties lifetime to use, with no size to tune.
- **A wrapper `FontMetrics` that decorates the face.** `FontMetrics` has 30 abstract members. Most of them would forward unchanged, and `StreamFontMetrics` would still need the coordinates inside, to vary glyphs and GPOS. A second constructor on the same class is less code.
- **Coordinates kept as `float`.** The variation data is defined over F2Dot14 coordinates. Float keys make equal requests miss each other in the dictionary, and they let the results drift from fontTools and HarfBuzz in the last bit.

### 4. The first release varies outlines, advances, global metrics and GPOS

| Data | First release | Why |
| --- | --- | --- |
| `fvar`, `avar` version 1 | Yes | Needed for any instance. |
| CFF2 `blend` and `vsindex` | Yes | `CffEvaluationEngine.Blend` already parses the operands. It now adds the scaled deltas instead of dropping them. |
| `gvar` with IUP, composite component offsets and the four phantom points | Yes | Needed for any TrueType instance. |
| `HVAR` and `VVAR` | Yes | Both test fonts have `HVAR`. The Font Variations Overview, section "Variation Data Tables and Miscellaneous Requirements", requires `HVAR` in a CFF2 variable font, because CFF2 has no phantom points. Without `HVAR`, a TrueType font varies its advances through the `gvar` phantom points. |
| `MVAR` | Yes | Varies ascender, descender, line gap, underline, strikeout, subscript and superscript. Both test fonts have it. |
| GDEF Item Variation Store and GPOS `VariationIndex` device tables | Yes | Kerning and mark positions in both test fonts depend on them. Without them, kerning stays at the default weight while outlines change. |
| `cvar` | No, follow-up | See decision 6. |
| GSUB and GPOS `FeatureVariations` | No, follow-up | Neither test font has them. They select alternate lookups, a separate concern from deltas. |
| `avar` version 2 | No | A version 2 table is read for its version 1 segment maps only, and its extra data is ignored. |
| COLR version 1 variations, `STAT`, `BASE`, `JSTF` | No | The library has no consumer for them today. |

The varied bounds of a glyph come from its varied outline. They do not come from the `glyf` header or from the default CFF2 charstring run. When `HVAR` has no side-bearing mapping, a TrueType glyph's left side bearing is the varied `xMin` less the varied first phantom point.

Malformed variation data follows the policy that #77 set for optional tables. A malformed `fvar` makes the font load as static, with no axes. A malformed `avar`, `HVAR`, `VVAR` or `MVAR` is treated as absent. Malformed `gvar` data for one glyph draws that glyph at the default instance. Nothing throws at glyph time.

Rejected alternatives:

- **Outlines only, with metrics and GPOS later.** A bold instance would draw bold glyphs at regular advances and regular kerning, so text would overlap. Advances must vary with outlines, or the first release renders wrong text.
- **Include `FeatureVariations`.** That means instance-aware lookup selection in `GSubTable` and `GPosTable`, which have no instance today. Neither test font exercises it. A separate issue keeps the first release reviewable.

### 5. One Item Variation Store reader, and one tuple variation store reader

The OpenType chapter "OpenType font variations common table formats" defines two delta formats. The Item Variation Store serves `HVAR`, `VVAR`, `MVAR`, GDEF and the CFF2 `VariationStore`. The tuple variation store serves `gvar` and `cvar`. Each gets one internal reader.

`ItemVariationStore` reads over `ReadOnlyMemory<byte>`, as `CffIndex` and `TrueTypeGlyphData` already do. At load it checks the `VariationRegionList` and the `ItemVariationData` headers. It copies no delta rows, which follows spec 002's "borrow, don't decode" decision. It handles the `LONG_WORDS` flag in `wordDeltaCount`, a `DeltaSetIndexMap` in formats 0 and 1, and the CFF2 form, which has a length prefix and `ItemVariationData` with no rows. For each instance it computes one `float` scalar per region, using the Font Variations Overview section "Algorithm for Interpolation of Instance Values". It then answers `GetDelta(outer, inner)` from those scalars. A CFF2 `blend` asks for the scalars of the regions that the current `vsindex` names. `Cff2Parser.ReadRegionCounts` is deleted.

`TupleVariationStore` reads the `gvar` glyph variation data and the `cvar` data. It decodes shared and private point numbers, packed deltas, and intermediate tuples. `gvar` adds IUP inference for the points that a tuple leaves out.

Rejected alternatives:

- **A reader per table.** Five copies of region-scalar math and of the bounds checks that the fuzzing work (#74) hardened. A fix in one copy would miss the others.
- **Decode every delta row at load.** This is simpler to index, but it costs memory in proportion to the delta data for every font, including fonts drawn only at the default instance.

### 6. A non-default instance renders unhinted until `cvar` support lands

`StreamFontMetrics.ApplyTrueTypeHinting` runs the `prep` program over the `cvt ` values from the default instance. A varied outline under default control values snaps stems to the default widths and distorts the glyph. So in the first release, a variation instance treats `HintingMode.Standard` as `HintingMode.None`. The default instance is the face itself and stays hinted as it is today.

A follow-up issue adds `cvar`. The instance builds its own `TrueTypeInterpreter` and passes `SetControlValueTable` the `cvt ` values plus the `cvar` deltas for its coordinates. It reuses the `TupleVariationStore` reader from decision 5, so it depends on the `gvar` issue. A font with `fpgm` and `prep` but no `cvar` then hints with the default control values.

Rejected alternatives:

- **Hint varied outlines with default control values in the first release.** The output is visibly wrong for any font whose instructions depend on stem widths.
- **Ship `cvar` in the first release.** It adds the interpreter's per-instance lifetime to a release that is already large. Neither test font has `cvar` to test it against.

### 7. `FontProgram` stays at the default instance

`FontProgram` serves PdfPinata, which draws the fonts that PDFs embed. A PDF positions glyphs with its own `/Widths`, and PDF has no font dictionary entry that selects a variation instance. So `FontProgram` gains nothing from variations. `FontProgram.Load` keeps rejecting CFF2, and `TrueTypeProgram` keeps ignoring `gvar`.

If a caller needs it later, the shape is additive. A `FontProgram.WithVariations(ReadOnlySpan<FontVariation>)` method would return a new `FontProgram` that shares the input buffer. It would keep its normalised coordinates and region scalars as arrays that `LoadBudget` charges, so `RetainedByteCount` stays exact and fixed at creation. It would cache nothing and read deltas from the buffer on every call. The `ItemVariationStore` and `TupleVariationStore` readers in decision 5 read over memory, so they already fit that shape.

Rejected alternative:

- **Add variation support to `FontProgram` in the first release.** No PDF needs it, and it would widen the exact-memory contract of spec 002 to cover per-instance state that has no caller.

## Consequences

- `Font.FontMetrics` can return a different `StreamFontMetrics` from the one in `FontCollection`. Code that compares `FontMetrics` references to identify a face must compare against the face, not against the instance. `FontCollection` itself stores faces only.
- `GPosTable` lookups already receive the `FontMetrics` they run for. The GPOS issue loads each `VariationIndex` device as an `(outer, inner)` pair at parse time and asks that `FontMetrics` for the delta. Non-variable `Device` tables (ppem deltas) stay ignored, as they are today.
- The TrueType decoder streams a glyph straight from `glyf` today. A varied glyph needs every point at once for IUP, so it takes the point-array path that hinting uses (`TrueTypeGlyphDecoder.TryReadPoints`, then `WriteOutline`). A composite glyph applies its deltas to component offsets before it reads its components.
- `README.md` says the library loads CFF2. After this work it can also say that it renders variable fonts at any instance.

## Open questions for the maintainer

1. Should `Font.IsBold` and `Font.IsItalic` follow the instance's `wght` and `ital` (decision 2)? This is the one change to an existing member's result. The alternative keeps them on the face's `OS/2` bits, so a `wght` 800 instance reports `IsBold == false`.
2. Is 700 the right `wght` for `FontStyle.Bold`, and 600 the right bold threshold for `IsBold`? 700 matches CSS `font-weight: bold` and the `OS/2` `usWeightClass` for Bold. The threshold of 600 is a choice, not a standard.
3. Should `Font.Name` return the named instance's name when the coordinates match a named instance? This ADR leaves `Font.Name` as the face's name.
4. Is `params ReadOnlySpan<FontVariation>` acceptable on the `netstandard2.1` target? It needs C# 13, which `LangVersion` `preview` gives. A Unity project on an older compiler then sees an ordinary `ReadOnlySpan` parameter. `IEnumerable<FontVariation>` overloads would work on every compiler but allocate.

## Proposed follow-up issues

Each issue takes its reference values from fontTools (`TTFont.getGlyphSet(location=...)`, `fontTools.varLib.instancer`), generated by a script under `tests/tools/`, as the `FontProgram` fixtures are. Each tests `Teko-Variable.ttf` and `SourceSans3VF-Upright.otf` at the axis minimum, at a named instance and at the axis maximum.

### A. Item Variation Store reader

Add `ItemVariationStore` and `DeltaSetIndexMap` over `ReadOnlyMemory<byte>`, with region scalars and `GetDelta(outer, inner)`. Support `LONG_WORDS`, map formats 0 and 1, and the CFF2 store form. Replace `Cff2Parser.ReadRegionCounts`. Add fuzz coverage, because the reader handles untrusted offsets.
Depends on: nothing.

### B. `fvar` and `avar` parsing, normalisation, and the public variation API

Parse `fvar` and `avar` version 1, and add `NormalizedCoordinates`. Add `FontVariation`, `FontVariationAxis`, `FontNamedInstance`, `FontMetrics.VariationAxes` and `NamedInstances`, and the `CreateFont` and `Font` overloads. Add the instance constructor on `StreamFontMetrics`, the weak-valued instance cache, the `FontStyle` mapping, and unhinted rendering for instances. Instances draw the default outlines until C and D land. Update `PublicAPI.Unshipped.txt` and `CHANGELOG.md`.
Depends on: nothing. It can run beside A.

### C. CFF2 `blend` for variation instances

Make `CffEvaluationEngine.Blend` add the scaled deltas for the instance's region scalars, and pass the instance into the CFF glyph path. Glyph bounds come from the varied charstring run. Test that advances, bounds and outlines match fontTools for `SourceSans3VF-Upright.otf`.
Depends on: A, B.

### D. `gvar` outlines and phantom points

Add `TupleVariationStore`, and apply `gvar` deltas with IUP to simple glyphs, composite component offsets and the four phantom points. Varied glyphs take the point-array path and cache their points per `GlyphMetrics`. When there is no `HVAR`, advances and side bearings come from the phantom points. Bounds come from the varied points.
Depends on: B.

### E. `HVAR`, `VVAR` and `MVAR`

Vary the advances and side bearings through `HVAR` and `VVAR` when present, and the `MVAR` global metrics in the instance constructor. Round once after summing deltas.
Depends on: A, B. Coordinate with D on which source wins for TrueType advances.

### F. GDEF variation store and GPOS device deltas

Read the GDEF 1.3 `itemVarStoreOffset` into an `ItemVariationStore`. Load each `VariationIndex` device in `ValueRecord` and `AnchorFormat3` as `(outer, inner)`, and add the instance's delta in GPOS positioning. Test kerning pairs at two weights for both test fonts.
Depends on: A, B.

### G. `cvar` and hinting for variation instances

Give each instance its own `TrueTypeInterpreter`, with `cvt ` values varied by `cvar`, and turn hinting back on for instances. A font without `cvar` hints with the default control values. Implement the `GETVARIATION` and `GETINFO` variation bits in the interpreter, which must return the exact F2Dot14 coordinates. Needs a hinted variable test font with `cvar` under an open licence.
Depends on: D.

### H. GSUB and GPOS `FeatureVariations`

Parse the `FeatureVariations` tables, and select the substitute feature tables whose condition sets match the instance's coordinates. Covers `rvrn`. Needs a test font that uses `FeatureVariations`.
Depends on: B.
