// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.Colr;
using PinataLabs.Typography.Tables.General.Kern;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tables.General.Post;
using PinataLabs.Typography.Tables.TrueType;
using PinataLabs.Typography.Tables.TrueType.Glyphs;
using PinataLabs.Typography.Tables.TrueType.Hinting;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <content>
/// Contains TrueType specific methods.
/// </content>
internal partial class StreamFontMetrics
{
    private readonly object interpreterLock = new();
    private TrueTypeInterpreter? interpreter;

    // The control values that the interpreter of an instance runs with, which 'cvar' varies. The interpreter lock guards it.
    private short[]? instanceControlValues;

    // Hints the points of a glyph in place. It returns the x of the first phantom point, which is that of the unrounded phantom
    // point if the glyph is not hinted. A glyph of a variation instance moves left by it after hinting, as FreeType does.
    internal float ApplyTrueTypeHinting(HintingMode hintingMode, GlyphMetrics metrics, ref GlyphVector glyphVector, Vector2 scaleXY, float pixelSize, float originX)
    {
        float origin = (originX + metrics.Offset.X) * scaleXY.X;
        if (hintingMode == HintingMode.None || this.outlineType != OutlineType.TrueType)
        {
            return origin;
        }

        TrueTypeFontTables tables = this.trueTypeFontTables!;
        Bounds bounds = glyphVector.Bounds;

        Vector2 pp1 = new(MathF.Round(origin), 0);
        Vector2 pp2 = new(MathF.Round(origin + (metrics.AdvanceWidth * scaleXY.X)), 0);
        Vector2 pp3 = new(0, MathF.Round(bounds.Max.Y + (metrics.TopSideBearing * scaleXY.Y)));
        Vector2 pp4 = new(0, MathF.Round(pp3.Y - (metrics.AdvanceHeight * scaleXY.Y)));

        // The interpreter holds mutable state (stack, zones, storage, scaled control values)
        // so it must only be used by one thread at a time.
        lock (this.interpreterLock)
        {
            try
            {
                if (this.interpreter == null)
                {
                    MaximumProfileTable maxp = tables.Maxp;
                    TrueTypeInterpreter created = new(
                        maxp.MaxStackElements,
                        maxp.MaxStorage,
                        maxp.MaxFunctionDefs,
                        maxp.MaxInstructionDefs,
                        maxp.MaxTwilightPoints,
                        this.coordinates.Values.ToArray());

                    FpgmTable? fpgm = tables.Fpgm;
                    if (fpgm is not null)
                    {
                        created.InitializeFunctionDefs(fpgm.Instructions);
                    }

                    this.interpreter = created;
                }

                PrepTable? prep = tables.Prep;
                float scaleFactor = pixelSize / this.UnitsPerEm;
                this.interpreter.SetControlValueTable(this.GetControlValues(), scaleFactor, pixelSize, prep?.Instructions);

                return GlyphVector.Hint(ref glyphVector, this.interpreter, pp1, pp2, pp3, pp4);
            }
            catch (Exception)
            {
                // Fonts can contain faulty hint programs (out of range control value, storage or
                // point references, stack overflow, etc). Hinting is an optimization of the outline
                // so we fall back to the unhinted glyph. The glyph vector is only updated after the
                // glyph has been hinted successfully so it is left untouched here.
                // The interpreter state is undefined after a failure so discard it.
                this.interpreter = null;
                return origin;
            }
        }
    }

    // A face runs its hinting programs with the values of the 'cvt ' table. An instance adds the 'cvar' deltas for its coordinates,
    // and uses the plain values when the font has no 'cvar' table or its data is malformed.
    internal short[]? GetControlValues()
    {
        TrueTypeFontTables tables = this.trueTypeFontTables!;
        short[]? controlValues = tables.Cvt?.ControlValues;
        if (this.face is null || controlValues is null || tables.Cvar is null)
        {
            return controlValues;
        }

        return this.instanceControlValues ??= tables.Cvar.CreateControlValues(controlValues, this.coordinates.Values) ?? controlValues;
    }

    private static StreamFontMetrics LoadTrueTypeFont(FontReader reader)
    {
        // Load using recommended order for best performance.
        // https://www.microsoft.com/typography/otspec/recom.htm#TableOrdering
        // 'head', 'hhea', 'maxp', OS/2, 'hmtx', LTSH, VDMX, 'hdmx', 'cmap', 'fpgm', 'prep', 'cvt ', 'loca', 'glyf', 'kern', 'name', 'post', 'gasp', PCLT, DSIG
        HeadTable head = reader.GetTable<HeadTable>();
        HorizontalHeadTable hhea = reader.GetTable<HorizontalHeadTable>();
        MaximumProfileTable maxp = reader.GetTable<MaximumProfileTable>();
        OS2Table os2 = reader.GetTable<OS2Table>();
        HorizontalMetricsTable htmx = reader.GetTable<HorizontalMetricsTable>();
        CMapTable cmap = reader.GetTable<CMapTable>();
        FpgmTable? fpgm = reader.TryGetTable<FpgmTable>();
        PrepTable? prep = reader.TryGetTable<PrepTable>();
        CvtTable? cvt = reader.TryGetTable<CvtTable>();
        GlyphTable glyf = reader.GetTable<GlyphTable>();
        KerningTable? kern = reader.TryGetTable<KerningTable>();
        NameTable name = reader.GetTable<NameTable>();
        PostTable post = reader.GetTable<PostTable>();

        VerticalHeadTable? vhea = reader.TryGetTable<VerticalHeadTable>();
        VerticalMetricsTable? vmtx = null;
        if (vhea is not null)
        {
            vmtx = reader.TryGetTable<VerticalMetricsTable>();
        }

        GlyphDefinitionTable? gdef = reader.TryGetTable<GlyphDefinitionTable>();
        GSubTable? gSub = reader.TryGetTable<GSubTable>();
        GPosTable? gPos = reader.TryGetTable<GPosTable>();

        ColrTable? colr = reader.TryGetTable<ColrTable>();
        CpalTable? cpal = reader.TryGetTable<CpalTable>();
        FvarTable? fvar = reader.TryGetTable<FvarTable>();
        GvarTable? gvar = fvar is null ? null : reader.TryGetTable<GvarTable>();
        CvarTable? cvar = fvar is null ? null : reader.TryGetTable<CvarTable>();

        // Only a variable font has instances to vary, so only it needs these.
        HvarTable? hvar = fvar is null ? null : reader.TryGetTable<HvarTable>();
        VvarTable? vvar = fvar is null ? null : reader.TryGetTable<VvarTable>();
        MvarTable? mvar = fvar is null ? null : reader.TryGetTable<MvarTable>();

        TrueTypeFontTables tables = new(cmap, head, hhea, htmx, maxp, name, os2, post, glyf)
        {
            Fpgm = fpgm,
            Prep = prep,
            Cvt = cvt,
            Kern = kern,
            Vhea = vhea,
            Vmtx = vmtx,
            Gdef = gdef,
            GSub = gSub,
            GPos = gPos,
            Colr = colr,
            Cpal = cpal,
            Hvar = hvar,
            Vvar = vvar,
            Mvar = mvar,
            Fvar = fvar,
            Gvar = gvar,
            Cvar = cvar,
        };

        return new StreamFontMetrics(tables);
    }

    private GlyphMetrics CreateTrueTypeGlyphMetrics(
        CodePoint codePoint,
        ushort glyphId,
        GlyphType glyphType,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        bool isVerticalLayout,
        ushort paletteIndex = 0)
    {
        TrueTypeFontTables tables = this.trueTypeFontTables!;
        GlyphTable glyf = tables.Glyf;
        HorizontalMetricsTable htmx = tables.Htmx;
        VerticalMetricsTable? vtmx = tables.Vmtx;

        Bounds bounds = glyf.GetBounds(glyphId);
        ushort advanceWidth = this.hvar.VaryAdvance(glyphId, htmx.GetAdvancedWidth(glyphId));
        short lsb = this.hvar.VaryLeadingBearing(glyphId, htmx.GetLeftSideBearing(glyphId));

        IMetricsHeader metrics = isVerticalLayout ? this.VerticalMetrics : this.HorizontalMetrics;
        ushort advancedHeight = (ushort)(metrics.Ascender - metrics.Descender);
        short tsb = (short)(metrics.Ascender - bounds.Max.Y);
        if (vtmx != null)
        {
            advancedHeight = this.vvar.VaryAdvance(glyphId, vtmx.GetAdvancedHeight(glyphId));
            tsb = this.vvar.VaryLeadingBearing(glyphId, vtmx.GetTopSideBearing(glyphId));
        }

        GlyphVector varied = default;
        if (this.face is not null
            && tables.Gvar is GvarTable gvar
            && glyf.TryReadVariedPoints(glyphId, gvar, this.coordinates.Values, out varied, out PhantomDeltas phantoms))
        {
            this.VaryMetrics(tables, ref varied, phantoms, metrics, ref bounds, ref advanceWidth, ref lsb, ref advancedHeight, ref tsb);
        }

        GlyphColor? color = null;
        if (glyphType == GlyphType.ColrLayer)
        {
            // 0xFFFF is special index meaning use foreground color and thus leave unset
            if (paletteIndex != 0xFFFF)
            {
                CpalTable? cpal = tables.Cpal;
                color = cpal?.GetGlyphColor(0, paletteIndex);
            }
        }

        return new TrueTypeGlyphMetrics(
            this,
            glyphId,
            codePoint,
            glyf,
            bounds,
            advanceWidth,
            advancedHeight,
            lsb,
            tsb,
            this.UnitsPerEm,
            textAttributes,
            textDecorations,
            glyphType,
            color,
            varied);
    }

    // The phantom points of a glyph are the origin and the end of its advance on the x axis, and the top and the bottom of
    // its vertical advance on the y axis. A composite takes those of a component that asks for its metrics, so the glyph
    // that the deltas belong to is not always the glyph itself. FreeType and HarfBuzz move the outline left by the x of the first
    // phantom point so that the origin is at 0, and so does this method, in place. The bounds of a varied glyph are those of
    // its varied points. A side bearing is the distance from a phantom point to the bounds, and an advance is the distance
    // between two phantom points. Each value rounds once, after the deltas are summed. A glyph that has no points keeps its
    // bounds, which are those of glyph 0 if the font gives it none.
    //
    // 'HVAR' and 'VVAR' win over the phantom points, as they do in HarfBuzz. The caller has already varied the advances and
    // bearings through them, and this method keeps what a table gives: an advance when the table is there, and a leading
    // bearing when the table maps it. A table that is malformed is absent.
    private void VaryMetrics(
        TrueTypeFontTables tables,
        ref GlyphVector varied,
        PhantomDeltas deltas,
        IMetricsHeader metrics,
        ref Bounds bounds,
        ref ushort advanceWidth,
        ref short lsb,
        ref ushort advanceHeight,
        ref short tsb)
    {
        int source = deltas.GlyphId;
        Bounds header = tables.Glyf.GetBounds(source);
        float left = header.Min.X - tables.Htmx.GetLeftSideBearing(source);
        float right = left + tables.Htmx.GetAdvancedWidth(source);
        float top = metrics.Ascender;
        float bottom = top - (metrics.Ascender - metrics.Descender);
        if (tables.Vmtx is VerticalMetricsTable vmtx)
        {
            top = header.Max.Y + vmtx.GetTopSideBearing(source);
            bottom = top - vmtx.GetAdvancedHeight(source);
        }

        left += deltas.Left;
        right += deltas.Right;
        top += deltas.Top;
        bottom += deltas.Bottom;

        float xMin = bounds.Min.X - left;
        float yMax = bounds.Max.Y;
        ControlPoint[] points = varied.ControlPoints;
        if (points.Length > 0)
        {
            Vector2 shift = new(left, 0);
            Vector2 min = new(float.MaxValue);
            Vector2 max = new(float.MinValue);
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 point = points[i].Point - shift;
                points[i] = new ControlPoint(point, points[i].OnCurve);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            varied.OriginX = left;
            bounds = new Bounds(min, max);
            xMin = min.X;
            yMax = max.Y;
        }

        if (!this.hvar.HasLeadingBearings)
        {
            lsb = VariationRounding.ToInt16(xMin);
        }

        if (!this.vvar.HasLeadingBearings)
        {
            tsb = VariationRounding.ToInt16(top - yMax);
        }

        if (this.hvar.IsEmpty)
        {
            advanceWidth = VariationRounding.ToUInt16(right - left);
        }

        if (this.vvar.IsEmpty)
        {
            advanceHeight = VariationRounding.ToUInt16(top - bottom);
        }
    }
}
