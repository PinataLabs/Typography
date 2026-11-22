# 002: Font program API

Status: accepted for implementation. Issues: #71, #72 (PR 1), #75 (PR 2), #73 (PR 3). Later: #70, #74, #76.

## Goal

PdfPinata draws the font programs that PDFs embed. It selects glyphs itself, through `/Encoding`, `/Differences`, CIDs and `/CIDToGIDMap`, and positions them with the PDF's own widths. It needs the outline of glyph *n*, the matrix from glyph space to ems, glyph lookup by name and by CID, the program's built-in encoding, and a memory cost it can bound. It renders documents in parallel, so a font must hold no shared mutable state.

The layout API (`FontCollection`, `FontMetrics`, `TextRenderer`) cannot serve this. It parses every table eagerly, it requires tables that PDF subsets omit, it decodes all of `glyf` at load, and it keeps a glyph cache that grows after load. A Type 1 program cannot become a `FontMetrics` without synthesised tables.

## Usage

```csharp
FontProgram font = FontProgram.Load(fontFileBytes); // ReadOnlyMemory<byte>, not copied

Span<ushort> codeToGlyph = stackalloc ushort[256];
if (!font.TryGetBuiltInEncoding(codeToGlyph))
{
    codeToGlyph.Clear();
}

foreach ((byte code, string name) in differences)
{
    if (font.TryGetGlyphId(name, out ushort gid))
    {
        codeToGlyph[code] = gid;
    }
}

var path = new PdfPathBuilder(font.FontMatrix * textMatrix); // caller's struct or class
if (!font.TryWriteOutline(codeToGlyph[code], ref path))
{
    path.Reset(); // unknown or malformed glyph
}

// Type 0 font with a CFF descendant. Identity for programs that are not CID-keyed.
if (font.TryGetGlyphIdForCid(cid, out ushort g))
{
    font.TryWriteOutline(g, ref path);
}
```

## Public surface

Namespace `PinataLabs.Typography`.

```csharp
public sealed class FontProgram
{
    // Sniffs the format from the bytes, never from the caller. PR 1: sfnt with glyf (TrueType)
    // and sfnt 'OTTO' with a 'CFF ' table. Rejects WOFF, WOFF2, TTC, CFF2 and anything else.
    // Retains `data` and reads it on every call. Does not copy. The caller must not mutate
    // or recycle the buffer while the font is reachable.
    // Throws InvalidFontFileException, and no other type, for unsupported or malformed data.
    public static FontProgram Load(ReadOnlyMemory<byte> data);

    public FontProgramFormat Format { get; }
    public bool IsCidKeyed { get; }          // CID-keyed CFF. Such a program has no glyph names.
    public int GlyphCount { get; }           // glyph IDs are 0 .. GlyphCount - 1
    public Matrix3x2 FontMatrix { get; }     // glyph space -> em. TrueType Scale(1/unitsPerEm);
                                             // CFF Top DICT FontMatrix (default 0.001).
                                             // Holds for every glyph (see "One matrix").

    // Unhinted outline in glyph space, y up. Each contour is MoveTo, segments, ClosePath.
    // An empty glyph writes nothing and returns true. False for an out-of-range ID or
    // malformed glyph data; the sink may then hold a partial outline. Never throws for bad data.
    public bool TryWriteOutline<TSink>(ushort glyphId, ref TSink sink) where TSink : IGlyphOutlineSink;

    public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth); // hmtx, else charstring width
    public bool TryGetGlyphId(ReadOnlySpan<char> glyphName, out ushort glyphId); // ordinal; lowest ID wins
    public string? GetGlyphName(ushort glyphId);                                // null when unnamed
    public bool TryGetGlyphIdForCid(int cid, out ushort glyphId); // charset when CID-keyed, else identity

    // Fills glyphIds[0..256) with the glyph for each code under the program's own encoding,
    // 0 where unencoded. CFF: Standard, Expert or custom (formats 0/1, supplements) through the
    // charset. TrueType: cmap (3,0) by low byte, else (1,0). False and nothing written when
    // the program has none (CID-keyed CFF, no such cmap). Throws ArgumentException if glyphIds.Length < 256.
    public bool TryGetBuiltInEncoding(Span<ushort> glyphIds);
}

public interface IGlyphOutlineSink
{
    void MoveTo(Vector2 point);
    void LineTo(Vector2 point);
    void QuadraticBezierTo(Vector2 control, Vector2 point);
    void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point);
    void ClosePath();
}

public enum FontProgramFormat { TrueType = 0, Cff = 1 } // PR 3 adds Type1 = 2
```

PR 2 (#75) adds the following. The guarantee covers `FontProgram` only. The layout API keeps a cache that grows after load and has no such bound.

```csharp
public sealed class FontProgramOptions { public long? MaxRetainedBytes { get; init; } } // negative throws
public static FontProgram Load(ReadOnlyMemory<byte> data, FontProgramOptions options);
public long RetainedByteCount { get; } // on FontProgram, fixed at load, never changes
public sealed class FontProgramTooLargeException : InvalidFontFileException
{
    public long RetainedByteCount { get; } // bytes retained at the allocation that was refused, a lower bound
    public long MaxRetainedBytes { get; }
}
```

`RetainedByteCount` is `data.Length`, plus for each array the program allocated, 24 bytes of array header plus the size of its elements. A slice of a larger array keeps the whole array alive, and only the slice is counted. A TrueType program has no array. A CFF program has the charset (`ushort[GlyphCount]`) and one `CffFontDict[]` with one record per Font DICT. The objects of the program (a fixed amount per font) are not counted. The array header and the Font DICT record size are 64-bit runtime figures. Every array on the load path comes from one internal `LoadBudget`, which charges it before it allocates and throws when the charge passes the cap. `data.Length` is charged first, so a font larger than the cap is refused before any parsing. A test sums the arrays reachable from a loaded program by reflection and compares the sum with the property, so a cached field that a later change adds fails it. PR 3 (#73) adds `FontProgramFormat.Type1` and nothing else. Bare CFF (#70) adds nothing public.

## Load-bearing decisions

1. **Borrow, don't decode.** The font keeps the caller's bytes and a few arrays sized from header counts (for CFF, the charset: 2 bytes per glyph). TrueType allocates no arrays. Nothing is decoded per glyph at load and nothing is cached afterwards. The retained size is therefore exact, fixed at load and known before allocation, which is what PR 2 needs.
2. **One sink is the internal outline protocol too.** `CffEvaluationEngine` and the TrueType decoder write to `IGlyphOutlineSink` in glyph space. `TransformingGlyphRenderer` becomes the adapter from the sink to `IGlyphRenderer` for layout, and `CffBoundsFinder` implements the sink. For CFF there is one outline producer, shared by layout and `FontProgram`. TrueType layout keeps the `GlyphVector` loaders, because hinting needs point arrays; the TrueType decoder serves `FontProgram` only. Sharing one TrueType producer is tracked in #80.
3. **One matrix.** A CID-keyed CFF can give each font DICT its own FontMatrix. The CFF program folds each FD matrix, relative to the top one, into that glyph's outline, so `FontMatrix` holds for every glyph. Parity tests compare after applying the matrix.
4. **`ushort` glyph IDs, `int` CIDs, distinct method names.** Passing a CID where a glyph ID belongs needs a cast.
5. **Errors.** Load: one exception type, `InvalidFontFileException`. Glyph time: a `false` return, never an exception, so one bad glyph costs only itself.
6. **No shared mutable state.** No statics that change, no `[ThreadStatic]`, no `ArrayPool`, no static `Random`. Scratch lives on the stack.
7. **PDF policy stays in PdfPinata.** No `.notdef` substitution, no "CID equals GID for name-keyed CFF under a CIDFont" beyond the identity rule above, no symbolic-font cmap rules.

## Portability rules (netstandard2.1, Unity IL2CPP, NativeAOT)

- `FontProgram` is sealed and has no virtual members. It dispatches with a closed type switch over its internal program object, because a generic virtual method with struct type arguments is a problem for IL2CPP and AOT.
- netstandard2.1 has no `ref` fields. Engines pass `ref TSink` through their methods; they do not store it.
- Composite TrueType glyphs accumulate one `Matrix3x2` and use one non-nested `TransformedSink<TSink>`. Never nest wrapper types per recursion level.

## Internal shape

```
src/PinataLabs.Typography/
  FontProgram.cs, FontProgramFormat.cs, IGlyphOutlineSink.cs   public
  FontPrograms/
    SfntDirectory.cs          bounds-checked table directory over memory
    FontDataReader.cs         big-endian cursor; load-time reads throw InvalidFontFileException,
                              glyph-time reads are Try* and never throw
    TrueTypeProgram.cs        head, maxp, loca, glyf required; hhea+hmtx, post, cmap optional
    TrueTypeGlyphDecoder.cs   streams glyf to the sink, no allocation; composites depth <= 16
    TransformedSink.cs
    MacGlyphNames.cs          the 258 standard Macintosh glyph names (post formats 1 and 2)
  Tables/Cff/
    CffParser.cs              refactored in place: parses over memory into CffIndex views, keeps
                              FontMatrix (top and per FD), per-FD widths and subrs, charset as SID
                              or CID, predefined charsets and encodings, encoding formats 0/1 with
                              supplements. One CFF 1 parser for layout and FontProgram.
    CffIndex.cs               INDEX as offsets into memory
    CffEvaluationEngine.cs    writes to the sink; reports the charstring width; fixes flex;
                              4-argument endchar (seac); subr depth <= 10; no static Random
```

## Delivery (PR 1)

Small commits, each green:

1. Characterization tests for today's CFF and TrueType outlines through layout, so the refactor has a net.
2. Delete the unused `GlyphNameMap` and the never-read `CffGlyphData.GlyphName` path.
3. Move `CffEvaluationEngine` output to `IGlyphOutlineSink`; `TransformingGlyphRenderer` becomes the adapter. Behaviour-preserving.
4. `CffIndex` over memory replaces `byte[][]`.
5. CFF parsing fixes, each with a fontTools-referenced test.
6. Type 2 fixes: `flex`, seac `endchar`, subr depth limit.
7. `post` format 2 string indexing fix.
8. `FontProgram`, `SfntDirectory`, `TrueTypeProgram`, `TrueTypeGlyphDecoder`, the CFF program path, PublicAPI and CHANGELOG.
9. fontTools parity tests: every glyph of a TrueType font with composites, SourceSansPro-Regular.otf and FDArrayTest257.otf (CID), plus names and built-in encodings.

Reference data comes from a checked-in Python generator under `tests/tools/` that uses fontTools (`RecordingPen`, `TTFont`, `cffLib`) and writes JSON fixtures. Fixtures for features no shipped test font exercises (Expert charset and encoding, custom encoding with supplements, seac, flex, per-FD FontMatrix) are built with fontTools `fontBuilder` by the same script.

## Implementation notes

The implementation made these local choices where the spec was silent.

- A Font DICT matrix is concatenated with the Top DICT matrix when the top has one.
- Text layout scales a CFF glyph by its matrix (the Font DICT matrix, else the font matrix) times the units per em of `head`, and skips the scale when the product is the identity. `FontProgram` reports the matrix instead.
- A (3,0) cmap lookup tries the code, then 0xF000, 0xF100 and 0xF200.
- CFF encoding format 0 treats code 0 as a real code, as Adobe TN5176 says. fontTools does not.
- A seac component that is missing makes the glyph return `false`. Text layout draws such a glyph without its components instead of throwing, so one glyph does not fail the whole text.
- `Load` dispatches strictly on the sfnt tag.
- A CharstringType other than 2 is rejected.
- Limits: about one million operators per charstring and 4096 components per outline.
- A CFF 1 charstring holds at most 48 arguments on its stack, the Type 2 limit. CFF2 keeps its limit of 513.
- An outline makes at most 1,000,000 sink calls. Past that, `TryWriteOutline` returns `false` and layout treats the glyph as malformed. The TrueType decoder counts a simple glyph before it writes it, as its point count plus three calls per contour, an upper bound.

## Rejected alternatives

- Expose outlines from `FontMetrics` or `StreamFontMetrics`: eager decode, required layout tables, a growing cache, no home for Type 1.
- Reuse `IGlyphRenderer` as the sink: layout-only members and `GlyphRendererParameters` leak into a layout-free API.
- Public abstract `FontProgram` with per-format subclasses: exposes format to callers and invites format-specific members.
- Return a path object as well as the sink: two ways to do one task, and an allocation per call.
- Copy or decode at load: exact counting is simpler but TrueType costs 3-6 times the raw size.
- A sorted name index: costs 2 bytes per glyph in every font. Name lookup runs at most 256 times per font, so a scan is enough.
- A per-glyph matrix: every caller would branch on it to serve a case that is identity in practice.
- Replace `CffParser` wholesale: largest blast radius on layout; the in-place refactor reaches the same single parser in verifiable steps.

## Open

- Unicode cmap (3,1) lookup for non-symbolic TrueType in PDFs is not covered by #71 or #72. Tracked as its own issue.
- Type 1 glyph ID order: `.notdef` first, then `/CharStrings` order. fontTools keys Type 1 glyphs by name, so PR 3 pins and documents this.
