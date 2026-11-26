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

    internal void ApplyTrueTypeHinting(HintingMode hintingMode, GlyphMetrics metrics, ref GlyphVector glyphVector, Vector2 scaleXY, float pixelSize)
    {
        if (hintingMode == HintingMode.None || this.outlineType != OutlineType.TrueType)
        {
            return;
        }

        TrueTypeFontTables tables = this.trueTypeFontTables!;
        Bounds bounds = glyphVector.Bounds;

        Vector2 pp1 = new(MathF.Round(bounds.Min.X - (metrics.LeftSideBearing * scaleXY.X)), 0);
        Vector2 pp2 = new(MathF.Round(pp1.X + (metrics.AdvanceWidth * scaleXY.X)), 0);
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

                GlyphVector.Hint(ref glyphVector, this.interpreter, pp1, pp2, pp3, pp4);
            }
            catch (Exception)
            {
                // Fonts can contain faulty hint programs (out of range control value, storage or
                // point references, stack overflow, etc). Hinting is an optimization of the outline
                // so we fall back to the unhinted glyph. The glyph vector is only updated after the
                // glyph has been hinted successfully so it is left untouched here.
                // The interpreter state is undefined after a failure so discard it.
                this.interpreter = null;
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
            HasHvar = reader.GetHeader("HVAR") is not null,
            HasVvar = reader.GetHeader("VVAR") is not null,
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
            this.VaryMetrics(tables, glyphId, varied, phantoms, metrics, ref bounds, ref advanceWidth, ref lsb, ref advancedHeight, ref tsb);
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
    // its vertical advance on the y axis. The bounds of a varied glyph are those of its varied points. A side bearing is the
    // distance from a phantom point to the bounds, and an advance is the distance between two phantom points, unless an
    // 'HVAR' or 'VVAR' table gives it. Each value rounds once, after the deltas are summed.
    private void VaryMetrics(
        TrueTypeFontTables tables,
        ushort glyphId,
        in GlyphVector varied,
        PhantomDeltas deltas,
        IMetricsHeader metrics,
        ref Bounds bounds,
        ref ushort advanceWidth,
        ref short lsb,
        ref ushort advanceHeight,
        ref short tsb)
    {
        Bounds header = tables.Glyf.GetHeaderBounds(glyphId);
        float left = header.Min.X - lsb;
        float right = left + advanceWidth;
        float top = tables.Vmtx is null ? metrics.Ascender : header.Max.Y + tsb;
        float bottom = top - advanceHeight;

        left += deltas.Left;
        right += deltas.Right;
        top += deltas.Top;
        bottom += deltas.Bottom;

        // An empty glyph keeps the bounds that it had.
        float xMin = 0;
        float yMax = 0;
        if (varied.ControlPoints.Length > 0)
        {
            Vector2 min = new(float.MaxValue);
            Vector2 max = new(float.MinValue);
            foreach (ControlPoint point in varied.ControlPoints)
            {
                min = Vector2.Min(min, point.Point);
                max = Vector2.Max(max, point.Point);
            }

            bounds = new Bounds(min, max);
            xMin = min.X;
            yMax = max.Y;
        }

        lsb = (short)Math.Clamp(Math.Floor(xMin - left + 0.5), short.MinValue, short.MaxValue);
        tsb = (short)Math.Clamp(Math.Floor(top - yMax + 0.5), short.MinValue, short.MaxValue);
        if (!tables.HasHvar)
        {
            advanceWidth = (ushort)Math.Clamp(Math.Floor(right - left + 0.5), 0, ushort.MaxValue);
        }

        if (!tables.HasVvar)
        {
            advanceHeight = (ushort)Math.Clamp(Math.Floor(top - bottom + 0.5), 0, ushort.MaxValue);
        }
    }
}
