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
        Bounds bounds = vector.GetBounds();
        ushort advanceWidth = htmx.GetAdvancedWidth(glyphId);
        short lsb = htmx.GetLeftSideBearing(glyphId);

        IMetricsHeader metrics = isVerticalLayout ? this.VerticalMetrics : this.HorizontalMetrics;
        ushort advancedHeight = (ushort)(metrics.Ascender - metrics.Descender);
        short tsb = (short)(metrics.Ascender - bounds.Max.Y);
        if (vtmx != null)
        {
            advancedHeight = vtmx.GetAdvancedHeight(glyphId);
            tsb = vtmx.GetTopSideBearing(glyphId);
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
