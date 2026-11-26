// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Cff;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.Colr;
using PinataLabs.Typography.Tables.General.Kern;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tables.General.Post;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <content>
/// Contains CFF specific methods.
/// </content>
internal partial class StreamFontMetrics
{
    private static StreamFontMetrics LoadCompactFont(FontReader reader)
    {
        // Load using recommended order for best performance.
        // https://www.microsoft.com/typography/otspec/recom.htm#TableOrdering
        // 'head', 'hhea', 'maxp', OS/2, 'name', 'cmap', 'post', 'CFF '
        HeadTable head = reader.GetTable<HeadTable>();
        HorizontalHeadTable hhea = reader.GetTable<HorizontalHeadTable>();
        MaximumProfileTable maxp = reader.GetTable<MaximumProfileTable>();
        OS2Table os2 = reader.GetTable<OS2Table>();
        NameTable name = reader.GetTable<NameTable>();
        CMapTable cmap = reader.GetTable<CMapTable>();
        PostTable post = reader.GetTable<PostTable>();

        ICffTable? cff = reader.TryGetTable<Cff1Table>() ?? (ICffTable?)reader.TryGetTable<Cff2Table>();
        if (cff is null)
        {
            throw new InvalidFontFileException("Required CFF or CFF2 table is missing or malformed.");
        }

        // TODO: VORG
        HorizontalMetricsTable htmx = reader.GetTable<HorizontalMetricsTable>();
        VerticalHeadTable? vhea = reader.TryGetTable<VerticalHeadTable>();
        VerticalMetricsTable? vmtx = null;
        if (vhea is not null)
        {
            vmtx = reader.TryGetTable<VerticalMetricsTable>();
        }

        KerningTable? kern = reader.TryGetTable<KerningTable>();

        GlyphDefinitionTable? gdef = reader.TryGetTable<GlyphDefinitionTable>();
        GSubTable? gSub = reader.TryGetTable<GSubTable>();
        GPosTable? gPos = reader.TryGetTable<GPosTable>();

        ColrTable? colr = reader.TryGetTable<ColrTable>();
        CpalTable? cpal = reader.TryGetTable<CpalTable>();
        FvarTable? fvar = reader.TryGetTable<FvarTable>();

        // Only a variable font has instances to vary, so only it needs these.
        HvarTable? hvar = fvar is null ? null : reader.TryGetTable<HvarTable>();
        VvarTable? vvar = fvar is null ? null : reader.TryGetTable<VvarTable>();
        MvarTable? mvar = fvar is null ? null : reader.TryGetTable<MvarTable>();

        CompactFontTables tables = new(cmap, head, hhea, htmx, maxp, name, os2, post, cff)
        {
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
        };

        return new StreamFontMetrics(tables);
    }

    private GlyphMetrics CreateCffGlyphMetrics(
        CodePoint codePoint,
        ushort glyphId,
        GlyphType glyphType,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        bool isVerticalLayout,
        ushort paletteIndex = 0)
    {
        CompactFontTables tables = this.compactFontTables!;
        ICffTable cff = tables.Cff;
        HorizontalMetricsTable htmx = tables.Htmx;
        VerticalMetricsTable? vtmx = tables.Vmtx;

        CffGlyphData vector = cff.GetGlyph(glyphId);
        Bounds defaultBounds = default;
        float[]?[]? blendScalars = this.blendScalars;
        if (blendScalars is not null)
        {
            defaultBounds = vector.GetBounds();
            vector = vector.WithBlendScalars(blendScalars);
        }

        Bounds bounds = vector.GetBounds();
        ushort advanceWidth = this.hvar.VaryAdvance(glyphId, htmx.GetAdvancedWidth(glyphId));
        short lsb = this.hvar.VaryLeadingBearing(glyphId, htmx.GetLeftSideBearing(glyphId));

        // A CFF glyph has no phantom points, and its origin is where its outline puts it. When HVAR does not map the left
        // side bearing, the bearing of a varied outline is where the outline starts, as it is in hmtx at the default instance.
        if (!this.IsDefaultInstance && !this.hvar.HasLeadingBearings)
        {
            lsb = (short)bounds.Min.X;
        }

        IMetricsHeader metrics = isVerticalLayout ? this.VerticalMetrics : this.HorizontalMetrics;
        ushort advancedHeight = (ushort)(metrics.Ascender - metrics.Descender);
        short tsb = (short)(metrics.Ascender - bounds.Max.Y);
        if (vtmx != null)
        {
            advancedHeight = this.vvar.VaryAdvance(glyphId, vtmx.GetAdvancedHeight(glyphId));
            tsb = this.vvar.VaryLeadingBearing(glyphId, vtmx.GetTopSideBearing(glyphId));

            // The vertical origin is where the default outline puts it: the top of that outline plus its top side bearing.
            // When VVAR does not map the top side bearing, the bearing of a varied outline is the distance from that origin to
            // the top of the varied outline, as the left side bearing is above.
            if (blendScalars is not null && !this.vvar.HasLeadingBearings)
            {
                tsb = VariationRounding.ToInt16(defaultBounds.Max.Y + tsb - bounds.Max.Y);
            }
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

        return new CffGlyphMetrics(
            this,
            glyphId,
            codePoint,
            vector,
            bounds,
            advanceWidth,
            advancedHeight,
            lsb,
            tsb,
            this.UnitsPerEm,
            textAttributes,
            textDecorations,
            glyphType,
            color);
    }
}
