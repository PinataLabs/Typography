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
    private readonly float[]?[]? blendScalars;
    private readonly int vsIndex;

    public CffGlyphData(
        ushort glyphIndex,
        CffIndex globalSubrs,
        CffIndex localSubrs,
        float nominalWidthX,
        ReadOnlyMemory<byte> charString,
        float[]?[]? blendScalars = null,
        int vsIndex = 0)
    {
        this.blendScalars = blendScalars;
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

    private CffGlyphData(in CffGlyphData glyph, float[]?[] blendScalars)
    {
        this = glyph;
        this.blendScalars = blendScalars;
    }

    public ushort GlyphIndex { get; }

    /// <summary>
    /// Gets a copy of a CFF2 glyph that blends at the coordinates of a variation instance. The glyph that the table gives
    /// blends with scalars of 0, which is the default instance.
    /// </summary>
    /// <param name="blendScalars">
    /// For each ItemVariationData of the variation store, the scalars of the regions that it names, as
    /// <see cref="Cff2Table.CreateBlendScalars"/> gives them. A subtable that the store lacks is <see langword="null"/>.
    /// </param>
    /// <returns>The <see cref="CffGlyphData"/>.</returns>
    public CffGlyphData WithBlendScalars(float[]?[] blendScalars)
        => new(in this, blendScalars);

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
        => this.TryWriteOutline(ref sink, out _);

    // Layout throws for a malformed glyph. A seac without its base or accent draws without them instead,
    // as it did before layout drew seac components, so one such glyph does not fail the whole text.
    private void WriteOutline<TSink>(ref TSink sink)
        where TSink : IGlyphOutlineSink
    {
        if (!this.TryWriteOutline(ref sink, out bool missingSeacComponent) && !missingSeacComponent)
        {
            throw new InvalidFontFileException($"The charstring of CFF glyph {this.GlyphIndex} is malformed.");
        }
    }

    private bool TryWriteOutline<TSink>(ref TSink sink, out bool missingSeacComponent)
        where TSink : IGlyphOutlineSink
    {
        Span<float> stack = stackalloc float[CffEvaluationEngine.StackSize];
        Span<float> transient = stackalloc float[CffEvaluationEngine.TransientSize];
        CffEvaluationEngine engine = this.CreateEngine(stack, transient);
        bool result;
        if (this.transform is not Matrix3x2 matrix)
        {
            result = engine.TryWriteOutline(ref sink);
        }
        else
        {
            TransformedSink<TSink> transformed = new(sink, matrix);
            result = engine.TryWriteOutline(ref transformed);
            sink = transformed.Inner;
        }

        missingSeacComponent = engine.MissingSeacComponent;
        return result;
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
            this.blendScalars,
            this.vsIndex);
}
