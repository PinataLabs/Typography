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
    private readonly Matrix3x2? glyphMatrix;
    private readonly CffFont? font;
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
        this.glyphMatrix = null;
        this.font = null;
    }

    public CffGlyphData(ushort glyphIndex, CffFont font, CffIndex globalSubrs, in CffFontDict fontDict, ReadOnlyMemory<byte> charString)
        : this(glyphIndex, globalSubrs, fontDict.LocalSubrs, fontDict.NominalWidthX, charString)
    {
        this.font = font;
        this.defaultWidthX = fontDict.DefaultWidthX;
        this.transform = fontDict.Transform;
        this.glyphMatrix = fontDict.GlyphMatrix ?? font.FontMatrix;
    }

    private CffGlyphData(in CffGlyphData glyph, Matrix3x2? transform)
    {
        this = glyph;
        this.transform = transform;
    }

    public ushort GlyphIndex { get; }

    /// <summary>
    /// Gets a copy of the glyph that writes its outline in the units of the 'head' table, as text layout
    /// expects, instead of in the glyph space of the font.
    /// </summary>
    /// <param name="unitsPerEm">The units per em of the 'head' table.</param>
    /// <returns>The <see cref="CffGlyphData"/>.</returns>
    public CffGlyphData InUnitsPerEm(ushort unitsPerEm)
    {
        if (this.glyphMatrix is not Matrix3x2 matrix)
        {
            return this;
        }

        Matrix3x2 units = matrix * Matrix3x2.CreateScale(unitsPerEm);
        return new CffGlyphData(in this, IsNearIdentity(units) ? null : units);
    }

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
    /// <returns><see langword="false"/> if the charstring is malformed.</returns>
    public bool TryGetAdvanceWidth(out float advanceWidth)
    {
        Span<float> stack = stackalloc float[CffEvaluationEngine.StackSize];
        Span<float> transient = stackalloc float[CffEvaluationEngine.TransientSize];
        CffEvaluationEngine engine = this.CreateEngine(stack, transient);
        bool result = engine.TryGetWidth(out advanceWidth);
        advanceWidth *= this.transform?.M11 ?? 1;
        return result;
    }

    /// <summary>
    /// Writes the outline of the glyph in the glyph space of the font.
    /// </summary>
    /// <typeparam name="TSink">The type of the sink.</typeparam>
    /// <param name="sink">The sink that receives the outline.</param>
    /// <returns><see langword="false"/> if the charstring is malformed. The sink may then hold a partial outline.</returns>
    public bool TryWriteOutline<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        Span<float> stack = stackalloc float[CffEvaluationEngine.StackSize];
        Span<float> transient = stackalloc float[CffEvaluationEngine.TransientSize];
        CffEvaluationEngine engine = this.CreateEngine(stack, transient);
        if (this.transform is not Matrix3x2 matrix)
        {
            return engine.TryWriteOutline(ref sink);
        }

        TransformedSink<TSink> transformed = new(sink, matrix);
        bool result = engine.TryWriteOutline(ref transformed);
        sink = transformed.Inner;
        return result;
    }

    private void WriteOutline<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (!this.TryWriteOutline(ref sink))
        {
            throw new InvalidFontFileException($"The charstring of CFF glyph {this.GlyphIndex} is malformed.");
        }
    }

    // A FontMatrix of 0.001 and 1000 units per em can multiply to a little more or less than 1.
    private static bool IsNearIdentity(Matrix3x2 matrix)
    {
        const float tolerance = 1e-6F;
        return MathF.Abs(matrix.M11 - 1) < tolerance
            && MathF.Abs(matrix.M12) < tolerance
            && MathF.Abs(matrix.M21) < tolerance
            && MathF.Abs(matrix.M22 - 1) < tolerance
            && MathF.Abs(matrix.M31) < tolerance
            && MathF.Abs(matrix.M32) < tolerance;
    }

    private CffEvaluationEngine CreateEngine(Span<float> stack, Span<float> transient)
        => new(
            this.charString.Span,
            this.globalSubrs,
            this.localSubrs,
            this.nominalWidthX,
            this.defaultWidthX,
            stack,
            transient,
            this.font,
            this.blendRegionCounts,
            this.vsIndex);
}
