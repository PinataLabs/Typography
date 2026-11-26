// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables.Cff;
using PinataLabs.Typography.Tables.TrueType.Glyphs;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tables.TrueType;

/// <summary>
/// Represents a glyph metric from a particular TrueType font face.
/// </summary>
public class TrueTypeGlyphMetrics : GlyphMetrics
{
    private readonly GlyphTable glyphs;

    // The hinted points of the glyph for each size. An unhinted glyph is drawn from the font data and keeps nothing.
    private readonly ConcurrentDictionary<float, GlyphVector> hintedVectorCache = new();

    internal TrueTypeGlyphMetrics(
        StreamFontMetrics font,
        ushort glyphId,
        CodePoint codePoint,
        GlyphTable glyphs,
        Bounds bounds,
        ushort advanceWidth,
        ushort advanceHeight,
        short leftSideBearing,
        short topSideBearing,
        ushort unitsPerEM,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        GlyphType glyphType = GlyphType.Standard,
        GlyphColor? glyphColor = null)
        : base(
              font,
              glyphId,
              codePoint,
              bounds,
              advanceWidth,
              advanceHeight,
              leftSideBearing,
              topSideBearing,
              unitsPerEM,
              textAttributes,
              textDecorations,
              glyphType,
              glyphColor)
        => this.glyphs = glyphs;

    internal TrueTypeGlyphMetrics(
        StreamFontMetrics font,
        ushort glyphId,
        CodePoint codePoint,
        GlyphTable glyphs,
        Bounds bounds,
        ushort advanceWidth,
        ushort advanceHeight,
        short leftSideBearing,
        short topSideBearing,
        ushort unitsPerEM,
        Vector2 offset,
        Vector2 scaleFactor,
        TextRun textRun,
        GlyphType glyphType = GlyphType.Standard,
        GlyphColor? glyphColor = null)
        : base(
              font,
              glyphId,
              codePoint,
              bounds,
              advanceWidth,
              advanceHeight,
              leftSideBearing,
              topSideBearing,
              unitsPerEM,
              offset,
              scaleFactor,
              textRun,
              glyphType,
              glyphColor)
        => this.glyphs = glyphs;

    /// <inheritdoc/>
    internal override GlyphMetrics CloneForRendering(TextRun textRun, CodePoint codePoint)
        => new TrueTypeGlyphMetrics(
            this.FontMetrics,
            this.GlyphId,
            codePoint,
            this.glyphs,
            this.Bounds,
            this.AdvanceWidth,
            this.AdvanceHeight,
            this.LeftSideBearing,
            this.TopSideBearing,
            this.UnitsPerEm,
            this.Offset,
            this.ScaleFactor,
            textRun,
            this.GlyphType,
            this.GlyphColor);

    /// <summary>
    /// Gets one point of the unhinted glyph in glyph space.
    /// </summary>
    /// <param name="index">The number of the point.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="false"/> if the glyph has no such point.</returns>
    internal bool TryGetPoint(int index, out Vector2 point)
        => this.glyphs.TryGetPoint(this.GlyphId, index, out point);

    /// <inheritdoc/>
    internal override void RenderTo(IGlyphRenderer renderer, Vector2 location, Vector2 offset, GlyphLayoutMode mode, TextOptions options)
    {
        // https://www.unicode.org/faq/unsup_char.html
        if (ShouldSkipGlyphRendering(this.CodePoint))
        {
            return;
        }

        float pointSize = this.TextRun.Font?.Size ?? options.Font.Size;
        float dpi = options.Dpi;

        // The glyph vector is rendered offset to the location.
        // For horizontal text, the offset is always zero but vertical or rotated text
        // will be offset against the location.
        location *= dpi;
        offset *= dpi;
        Vector2 renderLocation = location + offset;
        float scaledPPEM = this.GetScaledSize(pointSize, dpi);

        Matrix3x2 rotation = GetRotationMatrix(mode);
        FontRectangle box = this.GetBoundingBox(mode, renderLocation, scaledPPEM);
        GlyphRendererParameters parameters = new(this, this.TextRun, pointSize, dpi, mode);

        if (renderer.BeginGlyph(in box, in parameters))
        {
            if (!ShouldRenderWhiteSpaceOnly(this.CodePoint))
            {
                if (this.GlyphColor.HasValue && renderer is IColorGlyphRenderer colorSurface)
                {
                    colorSurface.SetColor(this.GlyphColor.Value);
                }

                Vector2 scale = new Vector2(scaledPPEM) / this.ScaleFactor;
                if (this.TryGetHintedVector(scaledPPEM, scale, rotation, options.HintingMode, out GlyphVector hinted))
                {
                    // The hinted points are already at the size and the rotation of the text.
                    TransformingGlyphRenderer sink = new(renderer, renderLocation, Vector2.One, Vector2.Zero, Matrix3x2.Identity);
                    TrueTypeGlyphDecoder.WriteOutline(hinted.ControlPoints, hinted.EndPoints, ref sink);
                }
                else
                {
                    // A malformed glyph draws the part of its outline that comes before the fault.
                    TransformingGlyphRenderer sink = new(renderer, renderLocation, scale, this.Offset * scale, rotation);
                    this.glyphs.TryWriteOutline(this.GlyphId, ref sink);
                }
            }

            this.RenderDecorationsTo(renderer, location, mode, rotation, scaledPPEM);
        }

        renderer.EndGlyph();
    }

    private bool TryGetHintedVector(float scaledPPEM, Vector2 scale, Matrix3x2 rotation, HintingMode hintingMode, out GlyphVector hinted)
    {
        // The control values of the font are those of the default instance, so hinting a varied outline with them snaps
        // its stems to the wrong widths. A variation instance draws unhinted, whatever mode the text asks for.
        if (hintingMode == HintingMode.None || !this.FontMetrics.IsDefaultInstance)
        {
            hinted = default;
            return false;
        }

        hinted = this.hintedVectorCache.GetOrAdd(scaledPPEM, _ =>
        {
            if (!this.glyphs.TryReadPoints(this.GlyphId, out GlyphVector vector))
            {
                return default;
            }

            vector.Bounds = this.Bounds;
            var matrix = Matrix3x2.CreateScale(scale);
            matrix.Translation = this.Offset * scale;
            GlyphVector.TransformInPlace(ref vector, matrix);

            float pixelSize = scaledPPEM / 72F;
            this.FontMetrics.ApplyTrueTypeHinting(hintingMode, this, ref vector, scale, pixelSize);

            // Rotation must happen after hinting.
            GlyphVector.TransformInPlace(ref vector, rotation);
            return vector;
        });

        return hinted.ControlPoints is not null;
    }
}
