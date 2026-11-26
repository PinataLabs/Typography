---
status: proposed
---

# Variable font instances and font matching

Issue #46 asks how a caller picks a variation instance of a variable font, and how the instance changes outlines, metrics and positioning. Breaking the public API is allowed, so this ADR records the design we would build if variable fonts had been a requirement from the start. It does not record the smallest change to today's API.

## Context

Today the library draws every variable font at its default instance. Nothing reads `fvar`, `avar`, `gvar`, `HVAR`, `VVAR` or `MVAR`. `Cff2Parser.ReadRegionCounts` reads only the region count of each `ItemVariationData`, so `CffEvaluationEngine.Blend` can pop and drop the deltas. `GlyphDefinitionTable` reads `itemVarStoreOffset` and never uses it. `AnchorFormat3` and `ValueRecord` keep their device offsets and never resolve them. `GPosTable` and `GSubTable` read `featureVariationsOffset` and leave a TODO.

Four facts about the current API shape the decisions below.

1. `FontStyle` is a flags enum with four values. `FontCollection` matches a face by exact `FontStyle` equality. `FontDescription.Style` comes from the `OS/2` `fsSelection` bits or `head.macStyle`. A family therefore holds at most four useful faces, and a face can only say "bold" or "not bold".
2. A family's name comes from `name` ID 1, the legacy family name. For `Teko-Variable.ttf` that name is "Teko Light", so `collection.Get("Teko")` fails today.
3. Both variable test fonts have their default instance at the lightest weight. `Teko-Variable.ttf` has `wght` 300 to 700 with default 300. `SourceSans3VF-Upright.otf` has `wght` 200 to 900 with default 200. A design that treats "Regular" as "the default instance" draws Light and ExtraLight text when the caller asks for regular text.
4. `GlyphRendererParameters` is the key that `IGlyphRenderer.BeginGlyph` documents as "uniquely represents a version of a glyph". It holds the font name in upper case and the `FontStyle`. Two instances of one variable font produce equal keys, so a renderer that caches outlines by this key draws the wrong weight.

The two variable test fonts decide most of the scope. Both fonts have `avar`, `HVAR`, `MVAR`, `STAT` and a GDEF 1.3 Item Variation Store. Their GPOS tables hold 2,385 and 6,815 `VariationIndex` device tables. Neither has GSUB or GPOS `FeatureVariations`, and neither has `cvar` or an `opsz` axis. The counts and names come from fontTools 4.68 over the files in `tests/PinataLabs.Typography.Tests/Fonts/`.

Spec 001 requires an approved proposal for every public API change. Accepting this ADR is that approval for decisions 1 to 4. Each implementing PR still updates `PublicAPI.Unshipped.txt` and `CHANGELOG.md`.

## Decision summary

- `FontStyle` becomes a value type of weight, width and slope, with the CSS ranges. Faces declare the range of styles that they cover, and a variable face covers the range of its axes.
- A family is grouped by the typographic family name, `name` ID 16, with a fallback to ID 1.
- `FontFamily` picks a face with the CSS Fonts Level 4 font matching algorithm. When the face is variable, the requested style sets `wght`, `wdth`, `ital` and `slnt`. The point size sets `opsz`. Explicit `FontVariation` values set any axis and override the values from the style and the size.
- A variation instance is a `StreamFontMetrics` that shares the parsed tables of its face and owns its own caches. Faces keep weak references to their live instances.
- `GlyphRendererParameters` identifies the font by the instance object, not by the font name and style.
- The first release varies outlines, advances, global metrics and GPOS. One Item Variation Store reader and one tuple variation store reader serve every table.
- A non-default instance renders unhinted until `cvar` support lands. `FontProgram` stays at the default instance.

## Decisions

### 1. `FontStyle` is weight, width and slope

The flags enum is replaced by a value type. Its parts follow CSS Fonts Level 4, which is what DirectWrite, Core Text and browsers also use.

```csharp
public readonly struct FontWeight : IEquatable<FontWeight>, IComparable<FontWeight>
{
	public FontWeight(float value);             // 1 to 1000, as CSS font-weight
	public float Value { get; }
	public static FontWeight Thin { get; }       // 100
	public static FontWeight Light { get; }      // 300
	public static FontWeight Normal { get; }     // 400
	public static FontWeight Medium { get; }     // 500
	public static FontWeight SemiBold { get; }   // 600
	public static FontWeight Bold { get; }       // 700
	public static FontWeight Black { get; }      // 900
	// and the other CSS keywords
}

public readonly struct FontWidth : IEquatable<FontWidth>, IComparable<FontWidth>
{
	public FontWidth(float percent);            // 50 to 200, as CSS font-stretch
	public float Percent { get; }
	public static FontWidth Condensed { get; }   // 75
	public static FontWidth Normal { get; }      // 100
	public static FontWidth Expanded { get; }    // 125
	// and the other CSS keywords
}

public enum FontSlopeKind { Normal, Italic, Oblique }

public readonly struct FontSlope : IEquatable<FontSlope>
{
	public static FontSlope Normal { get; }
	public static FontSlope Italic { get; }
	public static FontSlope Oblique(float degrees = 14);  // -90 to 90, as CSS oblique <angle>
	public FontSlopeKind Kind { get; }
	public float ObliqueAngle { get; }
}

public readonly struct FontStyle : IEquatable<FontStyle>
{
	public FontStyle(FontWeight weight, FontWidth width, FontSlope slope);
	public FontWeight Weight { get; }
	public FontWidth Width { get; }
	public FontSlope Slope { get; }
	public FontStyle WithWeight(FontWeight weight);
	public FontStyle WithWidth(FontWidth width);
	public FontStyle WithSlope(FontSlope slope);
	public static FontStyle Regular { get; }     // 400, 100%, normal
	public static FontStyle Bold { get; }        // 700, 100%, normal
	public static FontStyle Italic { get; }      // 400, 100%, italic
	public static FontStyle BoldItalic { get; }  // 700, 100%, italic
}
```

The default value of each type is valid and means "normal": `default(FontStyle)` equals `FontStyle.Regular`. `FontWeight` and `FontWidth` store their value as an offset from 400 and from 100 to get this. The constructors clamp to the CSS ranges.

Each face declares the styles it covers as a `FontStyleRange` on `FontDescription`. `FontStyleRange` holds a minimum and a maximum weight, a minimum and a maximum width, and the slopes the face has. `FontDescription.Style` is deleted. The library reads the range as follows:

- A static face covers one point. The weight is `OS/2.usWeightClass`. The width is `OS/2.usWidthClass`, mapped to a percentage through the table in the `OS/2` chapter of the OpenType specification. The slope is italic when `fsSelection` has `ITALIC`. It is oblique at the negated `post.italicAngle` when `fsSelection` has `OBLIQUE`. Otherwise it is normal. A font without `OS/2` falls back to the `head.macStyle` bits, as it does today.
- A variable face covers the range of its `wght` and `wdth` axes. An `ital` axis whose maximum is 1 adds italic to the slopes. A `slnt` axis adds oblique angles from the negated axis maximum to the negated axis minimum. A face without one of these axes takes that part from `OS/2`, as a static face does.

`FontStyle.Bold`, `FontStyle.Italic` and `FontStyle.BoldItalic` keep their names, so `CreateFont(12, FontStyle.Bold)` still compiles and still means bold. Code that combines flags with `|` or calls `HasFlag` stops compiling, which shows every place that needs review.

Rejected alternatives:

- **Keep the flags enum and map Bold to `wght` 700.** This was the first draft of this ADR. It cannot ask for weight 300, for a condensed width, or for an oblique slope. It cannot hold a family with nine static weights either, because `FontCollection` matches on exact `FontStyle` equality.
- **Separate `weight`, `width` and `slope` parameters on every overload.** This needs three parameters on `Font`, on `FontFamily.CreateFont` and on `SystemFonts.CreateFont`, and two more overloads each time a part is added. One value type keeps the overloads at one parameter.
- **A new name such as `FontFaceStyle`.** Existing callers that write `FontStyle.Bold` would break for no reason. Keeping the name keeps the common call compiling with the same meaning.
- **`usWeightClass` only, with no `fvar` ranges.** A variable face would cover one weight, its default, and the font matching in decision 3 would never pick it for any other weight.

### 2. A family is grouped by the typographic family name

`FontDescription.FontFamily(culture)` reads `name` ID 16, the typographic family name, and falls back to ID 1 when the font has no ID 16. `FontCollection`, `SystemFontCollection` and `FontFamily.Name` use that name. `Teko-Variable.ttf` then belongs to the family "Teko", and the static faces of a family with more than four weights join one family.

`FontDescription` gains `LegacyFontFamily(culture)` to read ID 1, for callers that need the four-style name.

Rejected alternatives:

- **Keep ID 1.** "Teko Light" is the family of a font that draws every weight from 300 to 700. Weight then becomes part of the family name, and decision 3 cannot pick a weight.
- **Accept ID 1 as an alias in `FontCollection.Get`.** `Get("Teko Light")` would return the family "Teko", and `CreateFont(12)` on it would draw weight 400, not Light. One name for each family is less surprising.
- **Group by the WWS family name, `name` ID 21.** ID 21 exists only in families that have styles other than weight, width and slope, such as optical sizes. Few fonts have it, and ID 16 already gives the family that CSS and DirectWrite users expect.

### 3. `FontFamily` picks a face with the CSS font matching algorithm

The library matches a `FontStyle` to a face with the algorithm in CSS Fonts Level 4, section 5.2 "Matching font styles", step 4. It narrows the faces by width, then by slope, then by weight. A face whose `FontStyleRange` contains the requested value matches exactly. Otherwise the library measures the distance to the nearest end of the face's range, and the CSS rules decide the search direction. For weight, a request between 400 and 500 tries the weights up to 500 first, then lighter weights, then heavier weights. A request below 400 tries lighter weights first. A request above 500 tries heavier weights first. An italic request tries italic faces, then oblique faces, then normal faces.

The algorithm always returns a face when the family has one. `Font.FontMetrics` therefore no longer throws "Font instance not found" for a family that came from a collection.

When the picked face is variable, the library sets the axes from the request, clamped to each axis range:

- `wght` takes the requested weight.
- `wdth` takes the requested width.
- `ital` takes 1 for an italic request on a face with an `ital` axis, and 0 otherwise.
- `slnt` takes the negated oblique angle for an oblique request, or -14 for an italic request on a face with `slnt` and no `ital`. Otherwise it takes 0.
- `opsz` takes the font size in points. The OpenType `opsz` axis is defined in points, and `Font.Size` is in points.

Explicit `FontVariation` values from decision 4 override these values axis by axis. CSS applies `font-variation-settings` last for the same reason.

`CreateFont(12)` on "Teko" therefore draws `wght` 400, and `CreateFont(12, FontStyle.Bold)` draws `wght` 700. The default instance, at `wght` 300, is what a caller gets by asking for `FontWeight.Light`.

The library does not synthesize bold or oblique glyphs. A renderer that wants synthesis compares `Font.Style` with `Font.FontMetrics.Description.StyleRange`, as decision 4 shows.

`FontFamily` changes to match:

```csharp
// FontFamily
public IReadOnlyList<FontMetrics> Faces { get; }
public FontMetrics MatchFace(FontStyle style);   // the face, before any variation is applied
public Font CreateFont(float size, FontStyle style = default, params ReadOnlySpan<FontVariation> variations);
public bool TryGetPaths(out IEnumerable<string> paths);
```

`GetAvailableStyles()` and `TryGetMetrics(FontStyle, out FontMetrics?)` are deleted. A list of style points cannot describe a face that covers a range, and exact-match lookup is what decision 3 replaces.

Rejected alternatives:

- **"Regular" means the default instance.** Both test fonts would draw Light or ExtraLight text for a regular request.
- **Use a static bold face when there is one, and map to `wght` only when there is not.** This was the first draft of this ADR. Two code paths decide the face, and the result for a request depends on which faces happen to be installed. CSS matching gives one rule for static and variable faces.
- **No mapping of italic to `slnt`.** CSS Fonts Level 4 lets an italic request fall back to an oblique face, and the library follows the same rule so that results match browsers.
- **Set `opsz` from CSS pixels, as browsers do.** The OpenType specification defines `opsz` in points. The library has no pixel unit, because `Font.Size` is in points and DPI is a render-time value.

### 4. Callers pass axis values on `Font`, and `Font` is immutable

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

// FontMetrics (abstract, internal constructor)
public abstract IReadOnlyList<FontVariationAxis> VariationAxes { get; }  // empty for a static font
public abstract IReadOnlyList<FontNamedInstance> NamedInstances { get; } // empty for a static font
public abstract IReadOnlyList<FontVariation> Variations { get; }         // this instance's user coordinates, one per axis

// Font
public Font(FontFamily family, float size, FontStyle style = default, params ReadOnlySpan<FontVariation> variations);
public FontStyle Style { get; }                         // as requested
public IReadOnlyList<FontVariation> Variations { get; } // as requested, after duplicate removal
public Font WithSize(float size);
public Font WithStyle(FontStyle style);
public Font WithVariations(params ReadOnlySpan<FontVariation> variations);
```

The four prototype constructors on `Font` are deleted, and the three `With` methods replace them. `Font.IsBold` and `Font.IsItalic` are deleted. `Font.Style` gives the request, and `Font.FontMetrics.Variations` gives the axis values of the instance that the font draws. The internal `RequestedStyle` becomes the public `Style`.

`Font.Name` gives the family name and the named instance's subfamily name when the instance's coordinates equal a named instance, for example "Teko SemiBold". Otherwise it gives the face's full name, `name` ID 4, as it does today.

A named instance needs no API of its own. The caller passes `instance.Variations` to `CreateFont`. The rules for a request follow the CSS `font-variation-settings` property:

- The library ignores a tag that the font has no axis for.
- It clamps a value to the axis minimum and maximum.
- When a tag repeats, the last value wins.
- On a static font, the variations have no effect, and `Font.FontMetrics` is the face itself.

`TextLayout` creates each fallback font with the primary font's size, `Style` and `Variations`. A fallback font ignores the tags it has no axis for, as a browser does.

`params ReadOnlySpan<FontVariation>` needs C# 13, which the library's `LangVersion` gives. A caller on an older compiler sees an ordinary `ReadOnlySpan<FontVariation>` parameter and passes an array, which converts to the span. `Font` copies the values into its own array, so the span does not outlive the call.

`Tag` is the existing public `PinataLabs.Typography.Tables.AdvancedTypographic.Tag`, which already has `Tag.Parse("wght")`.

Rejected alternatives:

- **Variations on `TextOptions`.** `TextOptions` applies to a whole layout call, and `TextRun` already carries a `Font` for each run. A variation for one run is a `Font` for that run. A second channel would also split the glyph cache key across two objects.
- **A mutable `Font.SetVariation`.** `Font` is shared across threads through `TextOptions`. A setter makes every layout racy.
- **`Dictionary<string, float>` or `(string, float)` tuples.** These allow a tag longer than four characters and need the tag parsed on every lookup. `Tag` is already public and already checked.
- **`CreateFont(size, FontNamedInstance)`.** A named instance is a list of `FontVariation` values, so a second overload adds methods with no new capability.
- **Keep `IsBold` and `IsItalic` and compute them from the instance.** A weight of 600 would need a threshold to call it bold, and no standard sets one. `Font.Style` and `FontMetrics.Variations` give the real values.

### 5. A variation instance is a `StreamFontMetrics` that shares the face's tables

Normalisation follows the OpenType Font Variations Overview, section "Coordinate Scales and Normalization". The library clamps each user coordinate to the axis range and maps it to -1, 0 or 1 at the minimum, default and maximum. It applies the `avar` segment maps from `avar` version 1, then converts each result to F2Dot14 as steps 1 to 5 of that section say. The result is `NormalizedCoordinates`, an internal immutable type that holds one `short` per `fvar` axis, in `fvar` order. Two requests that normalise to the same F2Dot14 values are the same instance. Equality is exact, so the value works as a dictionary key.

When every coordinate is 0, the request is the default instance, and `Font.FontMetrics` returns the face itself.

For any other coordinates, the face creates a `StreamFontMetrics` through a private constructor that takes the face and the coordinates. The new object references the same `TrueTypeFontTables` or `CompactFontTables`. Those tables are immutable after load and already shared across threads. The instance owns everything that depends on the coordinates:

- the varied global metrics from `MVAR`, computed once in its constructor;
- the region scalars for each Item Variation Store, computed once;
- its own `glyphCache` and `colorGlyphCache`, because a cached `GlyphMetrics` holds varied bounds and advances;
- its own `TrueTypeInterpreter` and lock, for when `cvar` support lands.

The instance keeps an internal reference to its face. `Font.TryGetPath` and `FontFamily.TryGetPaths` read the path from the face, so they keep working for an instance of a `FileFontMetrics` face. `FileFontMetrics` forwards instance creation to its lazy `StreamFontMetrics`. `FontMetrics` gains one internal abstract method that returns the instance for a set of normalised coordinates.

The face keeps a `ConcurrentDictionary<NormalizedCoordinates, WeakReference<StreamFontMetrics>>`. `Font` holds its instance strongly through its existing `Lazy<FontMetrics?>`. Two `Font` objects with equal coordinates share one instance and one glyph cache while either is alive. When every such `Font` is gone, the collector frees the instance, and the next insert removes dead entries. If two threads race on the first lookup, both build an instance and one wins. Both are correct, so the race costs one extra object and needs no lock.

Decision 3 makes non-default instances the common case. A regular request on either test font is an instance. The per-instance cost is therefore part of the normal path, and the face's dictionary is what keeps the `family.CreateFont(size)` per draw pattern cheap.

Varied values that the public types store as integers are rounded once, after all deltas are summed. That rule covers `GlyphMetrics.AdvanceWidth` (`ushort`), `LeftSideBearing` (`short`) and the `HorizontalMetrics` fields (`short`). Outline points stay `float`.

Memory per instance is small and fixed, apart from the caches. It is the coordinates, at 2 bytes per axis, the region scalars, at 4 bytes per region per store, and the metric fields. The glyph cache grows with the glyphs drawn, as it does for a static face today. A varied TrueType `GlyphMetrics` computes its varied points once and keeps them, the same way `TrueTypeGlyphMetrics.hintedVectorCache` keeps hinted points. Without that cache, every draw would decode `gvar` and run the IUP step again.

Rejected alternatives:

- **The instance owned by the `Font` alone, with no cache on the face.** Callers often call `family.CreateFont(size)` per draw. A per-`Font` instance would rebuild every glyph on every draw.
- **A strong, unbounded dictionary on the face.** A UI that animates `wght` creates a new instance for every frame, and none would ever be freed.
- **A fixed-size LRU on the face.** It evicts instances that live `Font` objects still use, so two `Font` objects with equal coordinates can hold two copies. The weak reference ties lifetime to use, with no size to tune.
- **A wrapper `FontMetrics` that decorates the face.** `FontMetrics` has more than 30 abstract members. Most would forward unchanged, and `StreamFontMetrics` would still need the coordinates inside to vary glyphs and GPOS. A second constructor on the same class is less code.
- **Coordinates kept as `float`.** The variation data is defined over F2Dot14 coordinates. Float keys make equal requests miss each other in the dictionary, and they let the results drift from fontTools and HarfBuzz in the last bit.

### 6. `GlyphRendererParameters` identifies the font by its instance

`GlyphRendererParameters.Font` (a `string`) and `GlyphRendererParameters.FontStyle` are deleted. The struct gains `FontMetrics FontMetrics`, the instance that owns the glyph, and compares it by reference.

Two `Font` objects that share an instance share the reference, because decision 5 keeps one live instance for each set of coordinates. A renderer's cache that holds a `GlyphRendererParameters` keeps the instance alive, so the reference stays stable while the cache entry exists. Different instances of one face produce different keys.

The reference key also fixes a defect that exists today without variations. Two different fonts with the same full name, such as two versions of one family loaded from different files, produce equal keys now.

Rejected alternatives:

- **Add the normalised coordinates to the key and keep the name.** The coordinates are internal, and the name still collides across files.
- **A public instance ID number.** It adds a counter and a second identity to keep in step with the object. The object reference is already unique.

### 7. The first release varies outlines, advances, global metrics and GPOS

| Data | First release | Why |
| --- | --- | --- |
| `fvar`, `avar` version 1 | Yes | Needed for any instance. |
| CFF2 `blend` and `vsindex` | Yes | `CffEvaluationEngine.Blend` already parses the operands. It now adds the scaled deltas instead of dropping them. |
| `gvar` with IUP, composite component offsets and the four phantom points | Yes | Needed for any TrueType instance. |
| `HVAR` and `VVAR` | Yes | Both test fonts have `HVAR`. The Font Variations Overview, section "Variation Data Tables and Miscellaneous Requirements", requires `HVAR` in a CFF2 variable font, because CFF2 has no phantom points. Without `HVAR`, a TrueType font varies its advances through the `gvar` phantom points. |
| `MVAR` | Yes | It varies the ascender, descender, line gap, underline, strikeout, subscript and superscript. Both test fonts have it. |
| GDEF Item Variation Store and GPOS `VariationIndex` device tables | Yes | Kerning and mark positions in both test fonts depend on them. Without them, kerning stays at the default weight while outlines change. |
| `opsz` from the point size | Yes | It is part of decision 3. No test font has `opsz`, so the tests build one with fontTools. |
| `cvar` | No, follow-up | See decision 8. |
| GSUB and GPOS `FeatureVariations` | No, follow-up | Neither test font has them. They select alternate lookups, which is a separate concern from deltas. |
| `avar` version 2 | No | The library reads a version 2 table for its version 1 segment maps only, and it ignores the extra data. |
| `STAT` | No | Decision 1 takes face ranges from `fvar`, which is enough for font matching. |
| COLR version 1 variations, `BASE`, `JSTF` | No | The library has no consumer for them today. |

The varied bounds of a glyph come from its varied outline. They do not come from the `glyf` header or from the default CFF2 charstring run. When `HVAR` has no side-bearing mapping, a TrueType glyph's left side bearing is the varied `xMin` less the varied first phantom point.

Malformed variation data follows the policy that #77 set for optional tables. A malformed `fvar` makes the font load as static, with no axes. The library treats a malformed `avar`, `HVAR`, `VVAR` or `MVAR` as absent. Malformed `gvar` data for one glyph draws that glyph at the default instance. Nothing throws at glyph time.

Rejected alternatives:

- **Outlines only, with metrics and GPOS later.** A bold instance would draw bold glyphs at regular advances and regular kerning, so text would overlap. Advances must vary with outlines, or the first release renders wrong text.
- **Include `FeatureVariations`.** That means lookup selection for each instance in `GSubTable` and `GPosTable`, which have no instance today. Neither test font uses it. A separate issue keeps the first release reviewable.

### 8. One Item Variation Store reader, and one tuple variation store reader

The OpenType chapter "OpenType font variations common table formats" defines two delta formats. The Item Variation Store serves `HVAR`, `VVAR`, `MVAR`, GDEF and the CFF2 `VariationStore`. The tuple variation store serves `gvar` and `cvar`. Each format gets one internal reader.

`ItemVariationStore` reads over `ReadOnlyMemory<byte>`, as `CffIndex` and `TrueTypeGlyphData` already do. At load it checks the `VariationRegionList` and the `ItemVariationData` headers. It copies no delta rows, which follows the "borrow, don't decode" decision in spec 002. It handles the `LONG_WORDS` flag in `wordDeltaCount`, a `DeltaSetIndexMap` in formats 0 and 1, and the CFF2 form, which has a length prefix and `ItemVariationData` with no rows. For each instance it computes one `float` scalar per region, with the algorithm in the Font Variations Overview section "Algorithm for Interpolation of Instance Values". It then answers `GetDelta(outer, inner)` from those scalars. A CFF2 `blend` asks for the scalars of the regions that the current `vsindex` names. `Cff2Parser.ReadRegionCounts` is deleted.

`TupleVariationStore` reads the `gvar` glyph variation data and the `cvar` data. It decodes shared and private point numbers, packed deltas and intermediate tuples. `gvar` adds IUP for the points that a tuple leaves out.

Rejected alternatives:

- **A reader per table.** There would be five copies of the region scalar math and of the bounds checks that the fuzzing work in #74 hardened. A fix in one copy would miss the others.
- **Decode every delta row at load.** Indexing is simpler, but every font pays memory in proportion to its delta data, including fonts drawn only at the default instance.

### 9. A non-default instance renders unhinted until `cvar` support lands

`StreamFontMetrics.ApplyTrueTypeHinting` runs the `prep` program over the `cvt ` values of the default instance. A varied outline under default control values snaps stems to the default widths and distorts the glyph. So in the first release, a variation instance treats `HintingMode.Standard` as `HintingMode.None`. The default instance is the face itself and stays hinted.

A follow-up issue adds `cvar`. Each instance builds its own `TrueTypeInterpreter` and passes `SetControlValueTable` the `cvt ` values plus the `cvar` deltas for its coordinates. The issue reuses the `TupleVariationStore` reader from decision 8, so it depends on the `gvar` issue. A font with `fpgm` and `prep` but no `cvar` then hints with the default control values.

Rejected alternatives:

- **Hint varied outlines with default control values in the first release.** The output is visibly wrong for any font whose instructions depend on stem widths.
- **Ship `cvar` in the first release.** It adds the interpreter's lifetime for each instance to a release that is already large. Neither test font has `cvar` to test it against.

### 10. `FontProgram` stays at the default instance

`FontProgram` serves PdfPinata, which draws the fonts that PDFs embed. A PDF positions glyphs with its own `/Widths`, and a PDF font dictionary has no entry that selects a variation instance. `FontProgram.Load` keeps rejecting CFF2, and `TrueTypeProgram` keeps ignoring `gvar`.

If a caller needs instances later, the change adds API and breaks nothing. A `FontProgram.WithVariations(ReadOnlySpan<FontVariation>)` method would return a new `FontProgram` that shares the input buffer. It would keep its normalised coordinates and region scalars as arrays that `LoadBudget` charges, so `RetainedByteCount` stays exact and fixed at creation. It would cache nothing and read deltas from the buffer on every call. The readers in decision 8 read over memory, so they already fit that shape.

Rejected alternative:

- **Add variation support to `FontProgram` in the first release.** No PDF needs it, and it would widen the exact-memory contract of spec 002 to cover per-instance state that has no caller.

## Consequences

- Every public member that takes or returns the old `FontStyle` changes. These are the `Font` constructors, `Font.IsBold`, `Font.IsItalic`, `FontDescription.Style`, `FontFamily.CreateFont`, `FontFamily.GetAvailableStyles`, `FontFamily.TryGetMetrics`, `GlyphRendererParameters.Font`, `GlyphRendererParameters.FontStyle` and both `SystemFonts.CreateFont` overloads that take a style. `CHANGELOG.md` lists each one with its replacement.
- A static font's rendering changes only when its family name changes under decision 2, or when CSS matching picks a different face than exact matching did. A family with static Regular and Bold faces renders as it does today.
- `Font.FontMetrics` can return a different `StreamFontMetrics` from the one in `FontCollection`. Code that compares `FontMetrics` references to identify a face must compare faces, not instances. `FontCollection` itself stores faces only.
- `GPosTable` lookups already receive the `FontMetrics` they run for. The GPOS issue loads each `VariationIndex` device as an `(outer, inner)` pair at parse time and asks that `FontMetrics` for the delta. Non-variable `Device` tables, which hold deltas per ppem, stay ignored.
- The TrueType decoder streams a glyph straight from `glyf` today. A varied glyph needs every point at once for IUP, so it takes the point-array path that hinting uses: `TrueTypeGlyphDecoder.TryReadPoints`, then `WriteOutline`. A composite glyph applies its deltas to the component offsets before it reads its components.
- `README.md` says the library loads CFF2. After this work it can say that it renders variable fonts at any instance and matches faces as CSS does.

## Follow-up issues

Each issue takes its reference values from fontTools (`TTFont.getGlyphSet(location=...)` and `fontTools.varLib.instancer`), through a script under `tests/tools/`, as the `FontProgram` fixtures do. Each variation issue tests `Teko-Variable.ttf` and `SourceSans3VF-Upright.otf` at the axis minimum, at a named instance and at the axis maximum.

### A. #123: `FontStyle`, typographic family names and CSS font matching

Replace the `FontStyle` enum with the value types from decision 1, and add `FontStyleRange` from `OS/2` for static faces. Group families by `name` ID 16, add `FontDescription.LegacyFontFamily`, and implement CSS matching in `FontFamily.MatchFace`. Add `FontFamily.Faces`, and add `Font.Style` and the `With` methods. Delete `GetAvailableStyles`, `TryGetMetrics`, `IsBold`, `IsItalic` and the prototype constructors. Replace the `GlyphRendererParameters` key with the `FontMetrics` reference from decision 6. Static fonts only. Test the matching against the examples in CSS Fonts Level 4, section 5.2.
Depends on: nothing.

### B. #124: Item Variation Store reader

Add `ItemVariationStore` and `DeltaSetIndexMap` over `ReadOnlyMemory<byte>`, with region scalars and `GetDelta(outer, inner)`. Support `LONG_WORDS`, map formats 0 and 1, and the CFF2 store form. Replace `Cff2Parser.ReadRegionCounts`. Add fuzz coverage, because the reader handles untrusted offsets.
Depends on: nothing. It can run beside A.

### C. #125: `fvar` and `avar` parsing, normalisation, instances and the variation API

Parse `fvar` and `avar` version 1, and add `NormalizedCoordinates`. Add `FontVariation`, `FontVariationAxis`, `FontNamedInstance`, `FontMetrics.VariationAxes`, `NamedInstances` and `Variations`, and the `variations` parameters on `Font` and `CreateFont`. Extend `FontStyleRange` with the `fvar` ranges, and set `wght`, `wdth`, `ital`, `slnt` and `opsz` from the match. Add the instance constructor on `StreamFontMetrics`, the weak-valued instance cache, the face reference for `TryGetPath`, fallback fonts that carry variations, `Font.Name` for named instances, and unhinted rendering for instances. Instances draw the default outlines until D and E land.
Depends on: A.

### D. #126: CFF2 `blend` for variation instances

Make `CffEvaluationEngine.Blend` add the scaled deltas for the instance's region scalars, and pass the instance into the CFF glyph path. Glyph bounds come from the varied charstring run. Test that advances, bounds and outlines match fontTools for `SourceSans3VF-Upright.otf`.
Depends on: B, C.

### E. #127: `gvar` outlines and phantom points

Add `TupleVariationStore`, and apply `gvar` deltas with IUP to simple glyphs, composite component offsets and the four phantom points. Varied glyphs take the point-array path and cache their points per `GlyphMetrics`. When there is no `HVAR`, advances and side bearings come from the phantom points. Bounds come from the varied points.
Depends on: C.

### F. #128: `HVAR`, `VVAR` and `MVAR`

Vary the advances and side bearings through `HVAR` and `VVAR` when present, and the `MVAR` global metrics in the instance constructor. Round once after summing deltas.
Depends on: B, C. Agree with E on which source wins for TrueType advances.

### G. #129: GDEF variation store and GPOS device deltas

Read the GDEF 1.3 `itemVarStoreOffset` into an `ItemVariationStore`. Load each `VariationIndex` device in `ValueRecord` and `AnchorFormat3` as `(outer, inner)`, and add the instance's delta in GPOS positioning. Test kerning pairs at two weights for both test fonts.
Depends on: B, C.

### H. #130: `cvar` and hinting for variation instances

Give each instance its own `TrueTypeInterpreter`, with `cvt ` values varied by `cvar`, and turn hinting back on for instances. A font without `cvar` hints with the default control values. Implement the `GETVARIATION` instruction and the variation bits of `GETINFO` in the interpreter, which must return the exact F2Dot14 coordinates. This needs a hinted variable test font with `cvar` under an open licence.
Depends on: E.

### I. #131: GSUB and GPOS `FeatureVariations`

Parse the `FeatureVariations` tables, and select the substitute feature tables whose condition sets match the instance's coordinates. This covers `rvrn`. It needs a test font that uses `FeatureVariations`.
Depends on: C.
