# 002: Font program API

Status: accepted for implementation. Issues: #71, #72 (PR 1), #75 (PR 2), #73 (PR 3), then #70, #74, #77, #80 (PR 4). Later: #76.

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
    // and sfnt 'OTTO' with a 'CFF ' table. PR 3: Type 1 (PFB, PFA, PDF /FontFile).
    // Rejects WOFF, WOFF2, TTC, CFF2 and anything else.
    // TrueType and CFF retain `data` and read it on every call. They do not copy. The caller must
    // not mutate or recycle the buffer while the font is reachable. Type 1 does not retain `data`.
    // Throws InvalidFontFileException, and no other type, for unsupported or malformed data.
    public static FontProgram Load(ReadOnlyMemory<byte> data);

    public FontProgramFormat Format { get; }
    public bool IsCidKeyed { get; }          // CID-keyed CFF. Such a program has no glyph names.
    public int GlyphCount { get; }           // glyph IDs are 0 .. GlyphCount - 1
    public Matrix3x2 FontMatrix { get; }     // glyph space -> em. TrueType Scale(1/unitsPerEm);
                                             // CFF Top DICT FontMatrix, Type 1 /FontMatrix (default 0.001).
                                             // Holds for every glyph (see "One matrix").

    // Unhinted outline in glyph space, y up. Each contour is MoveTo, segments, ClosePath.
    // An empty glyph writes nothing and returns true. False for an out-of-range ID or
    // malformed glyph data; the sink may then hold a partial outline. Never throws for bad data.
    public bool TryWriteOutline<TSink>(ushort glyphId, ref TSink sink) where TSink : IGlyphOutlineSink;

    public bool TryGetAdvanceWidth(ushort glyphId, out float advanceWidth); // hmtx, else charstring width (Type 1: hsbw/sbw)
    public bool TryGetGlyphId(ReadOnlySpan<char> glyphName, out ushort glyphId); // ordinal; lowest ID wins
    public string? GetGlyphName(ushort glyphId);                                // null when unnamed
    public bool TryGetGlyphIdForCid(int cid, out ushort glyphId); // charset when CID-keyed, else identity

    // Fills glyphIds[0..256) with the glyph for each code under the program's own encoding,
    // 0 where unencoded. CFF: Standard, Expert or custom (formats 0/1, supplements) through the
    // charset. TrueType: cmap (3,0) by low byte, else (1,0). Type 1: /Encoding, StandardEncoding or
    // the font's own array. False and nothing written when the program has none (CID-keyed CFF,
    // no such cmap, no /Encoding). Throws ArgumentException if glyphIds.Length < 256.
    public bool TryGetBuiltInEncoding(Span<ushort> glyphIds);

    // #77. Optional sfnt tables that the font lacks, and those it has but that were ignored as damaged.
    // Fixed at load, no retained array. The two sets never overlap. Type 1 and bare CFF report None.
    public OptionalFontTables MissingOptionalTables { get; }
    public OptionalFontTables MalformedOptionalTables { get; }
}

[Flags]
public enum OptionalFontTables { None = 0, Name = 1, Os2 = 2, Post = 4, Cmap = 8, Hhea = 16, Hmtx = 32 }

public interface IGlyphOutlineSink
{
    void MoveTo(Vector2 point);
    void LineTo(Vector2 point);
    void QuadraticBezierTo(Vector2 control, Vector2 point);
    void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point);
    void ClosePath();
}

public enum FontProgramFormat { TrueType = 0, Cff = 1, Type1 = 2 } // Type1 added by PR 3
```

PR 2 (#75) adds the following. The guarantee covers `FontProgram` only. The layout API keeps a cache that grows after load and has no such bound.

```csharp
public sealed class FontProgramOptions { public long? MaxRetainedBytes { get; init; } } // negative throws
public static FontProgram Load(ReadOnlyMemory<byte> data, FontProgramOptions options);
public long RetainedByteCount { get; } // on FontProgram, fixed at load, never changes
public sealed class FontProgramTooLargeException : InvalidFontFileException
{
    public long RetainedByteCount { get; } // bytes the font would retain; loads with this as the cap, except when only data.Length or the Type 1 decrypted buffer was checked
    public long MaxRetainedBytes { get; }
}
```

`RetainedByteCount` is `data.Length` when the program retains the input buffer, plus for each array the program allocated, 24 bytes of array header plus the size of its elements. A slice of a larger array keeps the whole array alive, and only the slice is counted. A TrueType program has no array. A CFF program has the charset (`ushort[GlyphCount]`) and one `CffFontDict[]` with one record per Font DICT, up to 256. The objects of the program (the `FontProgram`, its TrueType, CFF or Type 1 program and the parsed CFF font) are not counted. They are a fixed amount per font: a load allocated under 320 bytes for them on 64-bit CoreCLR, and callers can add 1024 bytes per font. The element and record sizes are `Unsafe.SizeOf` on the running runtime. Only the 24-byte array header is the 64-bit CoreCLR figure, and another runtime (Mono, IL2CPP, 32-bit) may differ by a few bytes per array. Every array on the load path comes from one internal `LoadBudget`, which charges it before it allocates and throws when the charge passes the cap. For TrueType and CFF, `data.Length` is charged first, so a font larger than the cap is refused before any parsing. A CFF font charges its charset and its Font DICT array in one call (`LoadBudget.Reserve`) after it reads both counts, so every other refusal carries the full retained size, and a load with that size as the cap succeeds. A refusal on the data length alone carries `data.Length`, a lower bound for a CFF font. A test sums the arrays reachable from a loaded program by reflection, plus `data.Length` when the program reaches the input, and compares the sum with the property, so a cached field that a later change adds fails it. Bare CFF (#70) adds nothing public. `Load` takes data as bare CFF 1 when it has at least 4 bytes, major version 1, a header size of at least 4 and an offset size of 1 to 4. No sfnt tag, Type 1 marker, TTC or WOFF signature starts with byte 1, so the order of the checks does not change the result. Bare CFF2 data (major version 2) is rejected. A bare CFF program keeps `data` like an OpenType CFF program, its advance widths come from the charstrings, and it reports no optional tables.

PR 3 (#73) adds `FontProgramFormat.Type1` and nothing else public. `Load` takes Type 1 when the data starts with a PFB segment marker (`0x80 0x01`) or with `%!` after any PostScript white space (space, tab, CR, LF, form feed, NUL). No sfnt tag starts with either, so the order of the checks does not change the result. The loader finds the `eexec` section itself, so a PDF `/FontFile` stream loads whatever its `/Length1`, `/Length2` and `/Length3` say, and `Load` takes no lengths. A Type 1 program does not retain the input. It owns one buffer, the decrypted private part with each charstring decrypted in place, and arrays of offsets into it:

- the private part: `byte[]`, its length known from the located eexec section before decryption (binary bytes, or hex digits / 2, less the 4 random bytes);
- the glyph charstrings: two `int[GlyphCount]` (offset, length);
- the subroutines: two `int[highest /Subrs index present + 1]`, sized from the entries that the data holds, not the declared count, and a declared count above the length of the private part refused;
- the glyph names: `int[GlyphCount]` offsets into the private part, `byte[GlyphCount]` lengths, and `ushort[GlyphCount]` glyph IDs sorted by name bytes then by ID, which name lookups search, so a name defined twice finds its lowest ID;
- the built-in encoding: `ushort[256]`, only when the font has an `/Encoding`.

`GlyphCount` comes from the `/CharStrings` entries that the data holds, counted before any array is allocated, not from the declared dictionary size. The decrypted buffer is charged before it is allocated, so a cap below it refuses the font before decryption, and that refusal carries the size of the buffer, a lower bound of the full size. The loader then reads the `/Subrs` and `/CharStrings` counts and charges every other array in one call before it allocates any of them, so every other refusal carries the full retained size, and a load with that size as the cap succeeds. The cleartext header lives on the stack. Only a PFB that splits its cleartext over several segments makes a transient copy of the cleartext, which is no larger than the input.

Type 1 glyph IDs: glyph 0 is `.notdef`, then the other glyphs in `/CharStrings` order. A font without `.notdef` gets an empty synthesized one at glyph 0, with no outline and advance 0. `TryGetGlyphIdForCid` is identity and `IsCidKeyed` is false. The font name, `/PaintType` and `/FontBBox` are not exposed.

## Load-bearing decisions

1. **Borrow, don't decode.** The font keeps the caller's bytes and a few arrays sized from header counts (for CFF, the charset: 2 bytes per glyph). TrueType allocates no arrays. Nothing is decoded per glyph at load and nothing is cached afterwards. The retained size is therefore exact, fixed at load and known before allocation, which is what PR 2 needs.
2. **One sink is the internal outline protocol too.** `CffEvaluationEngine` and the TrueType decoder write to `IGlyphOutlineSink` in glyph space. `TransformingGlyphRenderer` becomes the adapter from the sink to `IGlyphRenderer` for layout, and `CffBoundsFinder` implements the sink. For CFF there is one outline producer, shared by layout and `FontProgram`. TrueType has one too (#80). `TrueTypeGlyphData` holds the `loca` and `glyf` views, and `TrueTypeProgram` and the layout `GlyphTable` both pass it to `TrueTypeGlyphDecoder`. Layout streams an unhinted glyph to `TransformingGlyphRenderer`. For a hinted glyph the decoder reads the points into arrays (`TryReadPoints`), the interpreter moves them, and the decoder writes their outline (`WriteOutline`) with the contour writer that the streaming path uses. Layout keeps the bytes of `glyf` and `loca`, not decoded glyphs. A WOFF2 font rebuilds those bytes from its transformed table at load.
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
    TrueTypeGlyphData.cs      'loca' and 'glyf' views; finds the data of a glyph (#80)
    TrueTypeGlyphDecoder.cs   streams glyf to the sink, no allocation; composites depth <= 16;
                              also reads point arrays for hinting (#80)
    SfntOptionalTables.cs     finds and checks post, cmap, hhea, hmtx, name, OS/2 (#77)
    TransformedSink.cs
    MacGlyphNames.cs          the 258 standard Macintosh glyph names (post formats 1 and 2)
    LoadBudget.cs             charges each retained array before it is allocated (PR 2)
    NullGlyphOutlineSink.cs   discards an outline, for a charstring run only for its width
    Type1/                    PR 3: Type1Program, eexec decryption, cleartext and private-part
                              parser, tokenizer, Type 1 charstring engine (flex OtherSubrs, seac)
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
- Limits: CFF 48 operands, 10 subroutine levels, 1,048,576 operators per glyph. Type 1: 24, 10 and 100,000. TrueType: 16 composite levels and 4,096 components per outline. Every outline: 1,000,000 sink calls, and a TrueType point lookup charges each point it reads. CFF reads at most 256 Font DICTs. The `FontProgram` remarks list the data limits and the allocation bounds (#74).
- An optional sfnt table is malformed when its header is truncated or the structure that reading it needs does not fit; it is then treated as absent (#77).
- Text layout draws a malformed TrueType glyph up to the fault. A glyph ID outside the font draws glyph 0 (#80).
- A CFF 1 charstring holds at most 48 arguments on its stack, the Type 2 limit. CFF2 keeps its limit of 513.
- An outline makes at most 1,000,000 sink calls. Past that, `TryWriteOutline` returns `false` and layout treats the glyph as malformed. The TrueType decoder counts a simple glyph before it writes it, as its point count plus three calls per contour, an upper bound.

## Delivery (PR 4)

- #80: the TrueType decoder reads points for hinting, then layout draws through it; the `GlyphVector` loaders and `[ThreadStatic]` state are deleted.
- #77: `SfntOptionalTables` and the two diagnostic properties; tests strip tables from real fonts with a test-only sfnt rewriter.
- #70: the bare CFF sniff in `Load`; fontTools fixtures for the 'CFF ' tables of the shipped OTF test fonts, read as bare bytes.
- #74: the two bound fixes, the limits in the `FontProgram` remarks, `tests/PinataLabs.Typography.Fuzz` (SharpFuzz with libFuzzer, plus an in-process runner) and `FontProgramMutationTests`, which share one oracle.

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
