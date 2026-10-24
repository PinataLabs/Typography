---
title: FontRectangle and GlyphColor
description: API reference for FontRectangle, the rectangle type that measurements use, and GlyphColor.
---

Namespace: `PinataLabs.Typography`.

## FontRectangle

`public readonly struct FontRectangle : IEquatable<FontRectangle>`

A rectangle with `float` coordinates. The y-axis points down, so `Top` is less than `Bottom`.

### Make a rectangle

| Member | Description |
| --- | --- |
| `FontRectangle(float x, float y, float width, float height)` | A rectangle at `(x, y)` with a size. |
| `FontRectangle(Vector2 point, Vector2 size)` | A rectangle at `point` with `size`. |
| `static FromLTRB(float left, float top, float right, float bottom)` | A rectangle from its edges. |
| `static Empty` | The rectangle at `(0, 0)` with no size. |

### Properties

| Property | Description |
| --- | --- |
| `X`, `Y` | The top-left corner. |
| `Width`, `Height` | The size. |
| `Location` | `(X, Y)` as a `Vector2`. |
| `Size` | `(Width, Height)` as a `Vector2`. |
| `Left`, `Top`, `Right`, `Bottom` | The edges. |
| `IsEmpty` | `true` if the width or the height is zero or less. |

### Methods

| Method | Description |
| --- | --- |
| `Contains(float x, float y)`, `Contains(Vector2 point)` | `true` if the point is inside. |
| `Contains(in FontRectangle rectangle)` | `true` if the other rectangle is fully inside. |
| `IntersectsWith(in FontRectangle rectangle)` | `true` if the rectangles overlap. |
| `Intersect(in FontRectangle rectangle)` | The overlap of the two rectangles. |
| `static Intersect(in FontRectangle a, in FontRectangle b)` | The overlap of two rectangles. |
| `static Union(in FontRectangle a, in FontRectangle b)` | The smallest rectangle that holds both. |
| `Inflate(float width, float height)`, `Inflate(Vector2 size)` | A rectangle grown by `width` on the left and the right, and by `height` at the top and the bottom. |
| `static Inflate(in FontRectangle rectangle, float x, float y)` | The same, as a static method. |
| `Offset(float dx, float dy)`, `Offset(Vector2 point)` | A rectangle moved by the amounts. |
| `static Center(in FontRectangle rectangle)` | The centre point. |
| `static Transform(in FontRectangle rectangle, Matrix3x2 matrix)` | The bounding box of the rectangle after a transform. |
| `Deconstruct(out float x, out float y, out float width, out float height)` | Lets you write `var (x, y, w, h) = rectangle;`. |

`FontRectangle` is immutable: methods that change it return a new value. It supports `==` and `!=`.

## GlyphColor

`public readonly struct GlyphColor`

The color of one layer of a color glyph, from the font's CPAL table.

| Property | Description |
| --- | --- |
| `Red`, `Green`, `Blue` | The color components, 0 to 255. |
| `Alpha` | The opacity, 0 (transparent) to 255 (opaque). |

`GlyphColor` supports `==`, `!=` and `Equals`.
