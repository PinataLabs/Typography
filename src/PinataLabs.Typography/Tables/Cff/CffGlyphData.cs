// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography.Tables.Cff;

internal readonly struct CffGlyphData
{
    private readonly CffIndex globalSubrs;
    private readonly CffIndex localSubrs;
    private readonly ReadOnlyMemory<byte> charString;
    private readonly float nominalWidthX;
    private readonly float defaultWidthX;
    private readonly Matrix3x2? transform;
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
        this.defaultWidthX = 0;
        this.transform = null;
    }

    public CffGlyphData(ushort glyphIndex, CffIndex globalSubrs, in CffFontDict fontDict, ReadOnlyMemory<byte> charString)
        : this(glyphIndex, globalSubrs, fontDict.LocalSubrs, fontDict.NominalWidthX, charString)
    {
        this.defaultWidthX = fontDict.DefaultWidthX;
        this.transform = fontDict.Transform;
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
    /// Gets the advance width that the charstring gives, in the glyph space of the font.
    /// </summary>
    /// <param name="advanceWidth">The advance width.</param>
    /// <returns><see langword="true"/>.</returns>
    public bool TryGetAdvanceWidth(out float advanceWidth)
    {
        NullSink sink = default;
        using CffEvaluationEngine engine = this.CreateEngine();
        engine.WriteOutline(ref sink);
        advanceWidth = engine.Width * (this.transform?.M11 ?? 1);
        return true;
    }

    /// <summary>
    /// Writes the outline of the glyph in the glyph space of the font.
    /// </summary>
    /// <typeparam name="TSink">The type of the sink.</typeparam>
    /// <param name="sink">The sink that receives the outline.</param>
    public void WriteOutline<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        using CffEvaluationEngine engine = this.CreateEngine();
        if (this.transform is not Matrix3x2 matrix)
        {
            engine.WriteOutline(ref sink);
            return;
        }

        TransformedSink<TSink> transformed = new(sink, matrix);
        engine.WriteOutline(ref transformed);
        sink = transformed.Inner;
    }

    private CffEvaluationEngine CreateEngine()
        => new(
            this.charString.Span,
            this.globalSubrs,
            this.localSubrs,
            this.nominalWidthX,
            this.defaultWidthX,
            this.blendRegionCounts,
            this.vsIndex);

    private struct NullSink : IGlyphOutlineSink
    {
        public readonly void MoveTo(Vector2 point)
        {
        }

        public readonly void LineTo(Vector2 point)
        {
        }

        public readonly void QuadraticBezierTo(Vector2 control, Vector2 point)
        {
        }

        public readonly void CubicBezierTo(Vector2 control1, Vector2 control2, Vector2 point)
        {
        }

        public readonly void ClosePath()
        {
        }
    }
}
