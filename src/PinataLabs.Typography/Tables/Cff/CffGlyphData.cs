// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace PinataLabs.Typography.Tables.Cff;

internal readonly struct CffGlyphData
{
    private readonly CffIndex globalSubrs;
    private readonly CffIndex localSubrs;
    private readonly ReadOnlyMemory<byte> charString;
    private readonly float nominalWidthX;
    private readonly int[]? blendRegionCounts;
    private readonly int vsIndex;

    public CffGlyphData(
        ushort glyphIndex,
        CffIndex globalSubrs,
        CffIndex localSubrs,
        float nominalWidthX,
        ReadOnlyMemory<byte> charString,
        int[]? blendRegionCounts = null,
        int vsIndex = 0)
    {
        this.blendRegionCounts = blendRegionCounts;
        this.vsIndex = vsIndex;
        this.GlyphIndex = glyphIndex;
        this.globalSubrs = globalSubrs;
        this.localSubrs = localSubrs;
        this.nominalWidthX = nominalWidthX;
        this.charString = charString;
    }

    public ushort GlyphIndex { get; }

    public Bounds GetBounds()
    {
        CffBoundsFinder finder = default;
        this.WriteOutline(ref finder);
        return finder.GetBounds();
    }

    public void RenderTo(IGlyphRenderer renderer, Vector2 origin, Vector2 scale, Vector2 offset, Matrix3x2 transform)
    {
        TransformingGlyphRenderer adapter = new(renderer, origin, scale, offset, transform);
        this.WriteOutline(ref adapter);
    }

    /// <summary>
    /// Writes the outline of the glyph in glyph space.
    /// </summary>
    /// <typeparam name="TSink">The type of the sink.</typeparam>
    /// <param name="sink">The sink that receives the outline.</param>
    public void WriteOutline<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        using var engine = new CffEvaluationEngine(
            this.charString.Span,
            this.globalSubrs,
            this.localSubrs,
            this.nominalWidthX,
            this.blendRegionCounts,
            this.vsIndex);

        engine.WriteOutline(ref sink);
    }
}
