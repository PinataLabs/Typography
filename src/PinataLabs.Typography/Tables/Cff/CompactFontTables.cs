// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.Colr;
using PinataLabs.Typography.Tables.General.Kern;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tables.General.Post;

namespace PinataLabs.Typography.Tables.Cff;

internal sealed class CompactFontTables : IFontTables
{
    public CompactFontTables(
        CMapTable cmap,
        HeadTable head,
        HorizontalHeadTable hhea,
        HorizontalMetricsTable htmx,
        MaximumProfileTable maxp,
        NameTable name,
        OS2Table os2,
        PostTable post,
        ICffTable cff)
    {
        this.Cmap = cmap;
        this.Head = head;
        this.Hhea = hhea;
        this.Htmx = htmx;
        this.Maxp = maxp;
        this.Name = name;
        this.Os2 = os2;
        this.Post = post;
        this.Cff = cff;
    }

    public CMapTable Cmap { get; set; }

    public HeadTable Head { get; set; }

    public HorizontalHeadTable Hhea { get; set; }

    public HorizontalMetricsTable Htmx { get; set; }

    public MaximumProfileTable Maxp { get; set; }

    public NameTable Name { get; set; }

    public OS2Table Os2 { get; set; }

    public PostTable Post { get; set; }

    public GlyphDefinitionTable? Gdef { get; set; }

    public GSubTable? GSub { get; set; }

    public GPosTable? GPos { get; set; }

    public ColrTable? Colr { get; set; }

    public CpalTable? Cpal { get; set; }

    public KerningTable? Kern { get; set; }

    public VerticalHeadTable? Vhea { get; set; }

    public VerticalMetricsTable? Vmtx { get; set; }

    // Tables Related to CFF Outlines
    // +------+----------------------------------+
    // | Tag  | Name                             |
    // +======+==================================+
    // | CFF  | Compact Font Format 1.0          |
    // +------+----------------------------------+
    // | CFF2 | Compact Font Format 2.0          |
    // +------+----------------------------------+
    // | VORG | Vertical Origin (optional table) |
    // +------+----------------------------------+
    public ICffTable Cff { get; set; }
}
