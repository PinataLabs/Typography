// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.FontPrograms;

internal static class StrokeWidth
{
    /// <summary>
    /// Narrows a declared stroke width to a float. A negative, non-finite or out of range value, which no stroke can use, becomes 0.
    /// </summary>
    /// <param name="declared">The number that the font declares.</param>
    /// <returns>The width.</returns>
    public static float Sanitize(double declared)
    {
        float width = (float)declared;
        return float.IsFinite(width) && width > 0 ? width : 0;
    }
}
