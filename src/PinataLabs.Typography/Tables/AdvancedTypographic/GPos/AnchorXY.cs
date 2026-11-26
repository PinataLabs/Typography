// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.AdvancedTypographic.GPos;

/// <summary>
/// Represents the anchor coordinates for a given table. The deltas of a variation instance make them fractional,
/// and the positioning rounds the result of its arithmetic, as HarfBuzz does.
/// </summary>
internal readonly struct AnchorXY
{
    public AnchorXY(float x, float y)
    {
        this.XCoordinate = x;
        this.YCoordinate = y;
    }

    /// <summary>
    /// Gets the horizontal value, in design units.
    /// </summary>
    public float XCoordinate { get; }

    /// <summary>
    /// Gets the vertical value, in design units.
    /// </summary>
    public float YCoordinate { get; }

    /// <summary>
    /// Rounds each coordinate to a whole design unit.
    /// </summary>
    /// <returns>The rounded coordinates.</returns>
    public (int XCoordinate, int YCoordinate) Round()
        => (AdvancedTypographicUtils.Round(this.XCoordinate), AdvancedTypographicUtils.Round(this.YCoordinate));
}
