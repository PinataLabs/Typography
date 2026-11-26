// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <content>
/// Contains what a variation instance computes from its coordinates, once, when it is made.
/// </content>
internal partial class StreamFontMetrics
{
    // For the glyphs of a CFF2 variation instance, the scalars of the regions that each ItemVariationData of the
    // variation store names. A blend operator multiplies its deltas with them. It is null for a face and for any
    // font that has no CFF2 table.
    private readonly float[]?[]? blendScalars;
}
