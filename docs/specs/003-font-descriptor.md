# Font descriptor metrics and embedding permissions

`FontMetrics.DescriptorMetrics` gives the values that a PDF `/FontDescriptor` is written from, and
`FontMetrics.EmbeddingPermissions` gives the `OS/2` `fsType` permissions (issue #157). All values are in font design
units (`FontMetrics.UnitsPerEm` to the em). Each value of `FontDescriptorMetrics` has a `MetricSource` that names the
table field, or the fallback, that gave it.

`FontProgram` has no such members. It loads bare CFF and Type 1 programs that have no `OS/2` or `post` table, and its
callers read the program itself.

## Precedence

**Ascent and descent** follow the rules of `HorizontalMetrics`, so a PDF line matches a laid-out line:

1. `OS/2` `sTypoAscender` and `sTypoDescender` when the table exists and `fsSelection` bit 7 (USE_TYPO_METRICS) is set.
2. Otherwise `hhea` `ascender` and `descender`.
3. When either value from the step above is 0 and the `OS/2` table exists: the typo values if either is not 0, else
   `usWinAscent` and `-usWinDescent`.
4. When both are still 0, that is the font states no ascent and descent (an empty `hhea` and no `OS/2` table, or an `OS/2`
   table whose typo and win metrics are 0 as well): `head` `yMax` and `yMin`. The rule looks at the values that the font
   stores. `MVAR` deltas do not decide it, and `head` has no `MVAR` record, so these two values are those of the default design.

The descent is negative. The other values of a variation instance include the `MVAR` deltas of `hasc`, `hdsc`, `hcla` and
`hcld`. The values of the face are the default design.

**Cap height** is `OS/2` `sCapHeight` when the table is version 2 or later and the value the font stores is not 0. The
`OS/2` table of an earlier version has no such field. An `OS/2` table shorter than its version says has no such field
either, and the reader skips the fields that the table length does not cover (86 bytes for version 1, 96 for versions 2 to 4,
100 for version 5). The next source is the top of the glyph for U+0048, then the ascent
(`FallbackAscent`), because a descriptor of a non-symbolic font must state a cap height.

**x-height** is `OS/2` `sxHeight` under the same condition, then the top of the glyph for U+0078, then 0 (`None`),
because the `/XHeight` entry of a descriptor is optional.

The top of a glyph is the `yMax` that the library gives the glyph: the box of its `glyf` header, or the bounds of its
CFF outline. The library finds the glyph with its `cmap` reader, which reads the formats 0, 4, 12 and 14. A variation
instance adds the `MVAR` deltas of `cpht` and `xhgt` to the `OS/2` values. The source is decided from the stored value, before `MVAR`:
a stored 0 stays a font with no value, and `MVAR` gives it none.

**Italic angle** is `post` `italicAngle`, and **bounding box** is `head` `xMin`, `yMin`, `xMax` and `yMax`. Both are those of
the default design, also for a variation instance.

## Flags

The bits are those of ISO 32000-1, Table 123. The library sets a bit only when a table of the font states it.

| Flag | Source |
| --- | --- |
| `FixedPitch` | `post` `isFixedPitch` is not 0. |
| `Serif` | `OS/2` `sFamilyClass` class 1 to 5 or 7. With class 0, PANOSE family kind 2 (Latin Text) with a serif style 2 to 10. |
| `Script` | `sFamilyClass` class 10. With class 0, PANOSE family kind 3 (Latin Script). |
| `Italic` | `fsSelection` bit 0, or bit 9 in an `OS/2` table of version 4 or later (the bit is reserved before), `head` `macStyle` bit 1, or a `post` `italicAngle` that is not 0. |
| `Symbolic` | The `cmap` table has an encoding record for platform 3, encoding 0 (Microsoft Symbol), whatever the format of its subtable. |
| `Nonsymbolic` | The `cmap` table has no such subtable. Exactly one of `Symbolic` and `Nonsymbolic` is set. |
| `AllCap`, `SmallCap`, `ForceBold` | No table states them. The library never sets them. |

`OS/2` `ulCodePageRange` bit 31 (Symbol Character Set) is not used. Fonts that set it also carry the (3, 0) subtable.

## Embedding permissions

`FsType` is the field as stored. `Usage` reads bits 1 to 3. When more than one is set, the least restrictive
permission applies, as the OpenType specification says: editable (bit 3), then preview and print (bit 2), then
restricted (bit 1). With none set the usage is installable. Bit 0 and bits 4 to 7 are reserved and ignored.
`NoSubsetting` is bit 8 and `BitmapEmbeddingOnly` is bit 9. A font with no `OS/2` table states nothing:
`IsStated` is `false`, the usage is installable and both bits are `false`. The caller decides what to do with such a font.

## Verification

`tests/tools/generate_font_descriptor_reference.py` reads every face of the test fonts with fontTools, applies the
precedence above, and writes `FontDescriptor.fonttools.json`. It also builds the fonts in `Fonts/FontDescriptor` for the
cases that the other faces lack. It also writes the values of variation instances of Teko-Variable and SourceSans3VF-Upright
that fontTools.varLib.instancer gives. `FontDescriptorMetricsReferenceTests` compares every value.

## Known limits

- A `glyf` header whose box is wrong gives a wrong cap height or x-height when the font has no `OS/2` value for it.
  `PMINGLIU.ttf` has such headers.
- A font whose only `cmap` subtable has a format that the library does not read, such as format 6, has no glyph for
  U+0048, so its cap height falls back to the ascent.
