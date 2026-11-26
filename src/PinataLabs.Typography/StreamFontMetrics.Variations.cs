// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.Variations;

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

    // HVAR and VVAR with the region scalars of this instance. They are empty for a face, and for a font that lacks the
    // table or has a malformed one, and then the advances and side bearings of hmtx and vmtx stand.
    private readonly MetricsVariation hvar;
    private readonly MetricsVariation vvar;
}
