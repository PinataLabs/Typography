// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.Colr;
using PinataLabs.Typography.Tables.General.Kern;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tables.General.Post;
using PinataLabs.Typography.Tables.TrueType.Glyphs;
using PinataLabs.Typography.Tables.TrueType.Hinting;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tables.TrueType;

internal sealed class TrueTypeFontTables : IFontTables
{
    public TrueTypeFontTables(
        CMapTable cmap,
        HeadTable head,
        HorizontalHeadTable hhea,
        HorizontalMetricsTable htmx,
        MaximumProfileTable maxp,
        NameTable name,
        OS2Table os2,
        PostTable post,
        GlyphTable glyph)
    {
        this.Cmap = cmap;
        this.Head = head;
        this.Hhea = hhea;
        this.Htmx = htmx;
        this.Maxp = maxp;
        this.Name = name;
        this.Os2 = os2;
        this.Post = post;
        this.Glyf = glyph;
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

    public HvarTable? Hvar { get; set; }

    public VvarTable? Vvar { get; set; }

    public MvarTable? Mvar { get; set; }

    public FvarTable? Fvar { get; set; }

    public GvarTable? Gvar { get; set; }

    public CvarTable? Cvar { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the font has an 'HVAR' table, which then varies the advances. Without one the
    /// advances come from the phantom points of the 'gvar' table.
    /// </summary>
    public bool HasHvar { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the font has a 'VVAR' table, which then varies the vertical advances.
    /// </summary>
    public bool HasVvar { get; set; }

    // Tables Related to TrueType Outlines
    // +------+-----------------------------------------------+
    // | Tag  | Name                                          |
    // +======+===============================================+
    // | cvt  | Control Value Table (optional table)          |
    // +------+-----------------------------------------------+
    // | fpgm | Font program (optional table)                 |
    // +------+-----------------------------------------------+
    // | glyf | Glyph data                                    |
    // +------+-----------------------------------------------+
    // | loca | Index to location                             |
    // +------+-----------------------------------------------+
    // | prep | CVT Program (optional table)                  |
    // +------+-----------------------------------------------+
    // | gasp | Grid-fitting/Scan-conversion (optional table) |
    // +------+-----------------------------------------------+
    public CvtTable? Cvt { get; set; }

    public FpgmTable? Fpgm { get; set; }

    public GlyphTable Glyf { get; set; }

    public PrepTable? Prep { get; set; }
}
