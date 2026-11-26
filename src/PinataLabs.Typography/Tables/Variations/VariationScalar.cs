// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Variations;

/// <summary>
/// The scalar of one axis of a region or a tuple, which the Font Variations Overview, "Algorithm for Interpolation of
/// Instance Values", defines. A region of an Item Variation Store and a tuple of a tuple variation store both give a
/// start, a peak and an end for each axis, and multiply the scalars of their axes.
/// </summary>
internal static class VariationScalar
{
    /// <summary>
    /// Gets how much one axis scales the deltas of a region or a tuple.
    /// </summary>
    /// <param name="start">The F2Dot14 coordinate where the region starts.</param>
    /// <param name="peak">The F2Dot14 coordinate where the region has its full effect.</param>
    /// <param name="end">The F2Dot14 coordinate where the region ends.</param>
    /// <param name="coordinate">The F2Dot14 coordinate of the instance on the axis.</param>
    /// <returns>1 for an axis that does not take part or is at its peak, 0 outside the region, and the fraction between.</returns>
    public static float Get(int start, int peak, int end, int coordinate)
    {
        // An axis with no peak, a peak outside its range, or a range that spans 0 does not take part.
        if (peak == 0 || start > peak || peak > end || (start < 0 && end > 0) || coordinate == peak)
        {
            return 1;
        }

        if (coordinate <= start || end <= coordinate)
        {
            return 0;
        }

        return coordinate < peak
            ? (float)(coordinate - start) / (peak - start)
            : (float)(end - coordinate) / (end - peak);
    }
}
