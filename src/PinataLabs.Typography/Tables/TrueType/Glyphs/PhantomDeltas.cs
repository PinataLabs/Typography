// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.TrueType.Glyphs;

/// <summary>
/// What the 'gvar' table adds to the four phantom points of a glyph at a variation instance. The phantom points are the
/// left and right side of the advance on the x axis, and the top and bottom of the vertical advance on the y axis. The
/// other coordinate of each is 0 and the table does not move it into the outline.
/// </summary>
/// <param name="Left">The x delta of the first phantom point, which is the origin of the glyph.</param>
/// <param name="Right">The x delta of the second phantom point, which is the end of the advance.</param>
/// <param name="Top">The y delta of the third phantom point, which is the top of the vertical advance.</param>
/// <param name="Bottom">The y delta of the fourth phantom point, which is the bottom of the vertical advance.</param>
internal readonly record struct PhantomDeltas(float Left, float Right, float Top, float Bottom);
