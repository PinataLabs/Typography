# PinataLabs.Typography

A .NET library that loads font files, shapes and lays out text, and hands glyph outlines to a renderer.

## Variable fonts

**Variable font**:
A font whose `fvar` table defines one or more variation axes, so one file holds a continuous range of designs.
_Avoid_: VF, OpenType variations font

**Variation axis**:
One dimension along which a variable font's design varies, named by a four-byte tag such as `wght` or `wdth`, with a minimum, a default and a maximum value.
_Avoid_: Axis of variation, design axis

**User coordinate**:
A position on a variation axis in the axis's own units, as the font declares them in `fvar`, for example 650 on `wght`.
_Avoid_: Design coordinate, axis value

**Normalised coordinate**:
A position on a variation axis mapped to the range -1 to 1, with 0 at the axis default, after the `avar` mapping. All variation data in the font is keyed by normalised coordinates.
_Avoid_: Normalized value, internal coordinate

**Default instance**:
The design a variable font draws when every normalised coordinate is 0. It is what a variable font renders as when no variation is asked for.
_Avoid_: Base instance, master

**Variation instance**:
One fixed set of normalised coordinates applied to a variable font, giving one concrete design with its own outlines and metrics.
_Avoid_: Instance font, variable instance, location

**Named instance**:
A variation instance that the font itself lists in `fvar`, with a name such as "Semibold".
_Avoid_: Predefined instance, preset

## Shaping

**Glyph normalization**:
The step before GSUB that maps the characters of a run to the glyphs that the font has. It decomposes a character that the font lacks into its canonical parts, sorts each run of combining marks, and composes a base and a mark into the precomposed character when the font has it.
_Avoid_: Unicode normalization (NFC and NFD do not look at the font), normalising (that word is for normalised coordinates)
