// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

namespace PinataLabs.Typography.Tables.Cff;

internal struct CffGlyphData
{
    private readonly byte[][] globalSubrBuffers;
    private readonly byte[][] localSubrBuffers;
    private readonly byte[] charStrings;
    private readonly int nominalWidthX;
    private readonly int[]? blendRegionCounts;
    private readonly int vsIndex;

    public CffGlyphData(
        ushort glyphIndex,
        byte[][] globalSubrBuffers,
        byte[][] localSubrBuffers,
        int nominalWidthX,
        byte[] charStrings,
        int[]? blendRegionCounts = null,
        int vsIndex = 0)
    {
        this.blendRegionCounts = blendRegionCounts;
        this.vsIndex = vsIndex;
        this.GlyphIndex = glyphIndex;
        this.globalSubrBuffers = globalSubrBuffers;
        this.localSubrBuffers = localSubrBuffers;
        this.nominalWidthX = nominalWidthX;
        this.charStrings = charStrings;
    }

    public readonly ushort GlyphIndex { get; }

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
            this.charStrings,
            this.globalSubrBuffers,
            this.localSubrBuffers,
            this.nominalWidthX,
            this.blendRegionCounts,
            this.vsIndex);

        engine.WriteOutline(ref sink);
    }
}
