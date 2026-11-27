// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography;

/// <summary>
/// A box in font design units, with the y axis up.
/// </summary>
public readonly struct FontBoundingBox
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FontBoundingBox"/> struct.
    /// </summary>
    /// <param name="xMin">The left edge.</param>
    /// <param name="yMin">The bottom edge.</param>
    /// <param name="xMax">The right edge.</param>
    /// <param name="yMax">The top edge.</param>
    public FontBoundingBox(short xMin, short yMin, short xMax, short yMax)
    {
        this.XMin = xMin;
        this.YMin = yMin;
        this.XMax = xMax;
        this.YMax = yMax;
    }

    /// <summary>
    /// Gets the left edge.
    /// </summary>
    public short XMin { get; }

    /// <summary>
    /// Gets the bottom edge.
    /// </summary>
    public short YMin { get; }

    /// <summary>
    /// Gets the right edge.
    /// </summary>
    public short XMax { get; }

    /// <summary>
    /// Gets the top edge.
    /// </summary>
    public short YMax { get; }
}
