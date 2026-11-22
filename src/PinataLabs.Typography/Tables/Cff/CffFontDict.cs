// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// The values of a Private DICT that charstring evaluation needs.
/// </summary>
internal readonly struct CffFontDict
{
    public CffFontDict(CffIndex localSubrs, float defaultWidthX, float nominalWidthX)
    {
        this.LocalSubrs = localSubrs;
        this.DefaultWidthX = defaultWidthX;
        this.NominalWidthX = nominalWidthX;
    }

    public CffIndex LocalSubrs { get; }

    public float DefaultWidthX { get; }

    public float NominalWidthX { get; }
}
