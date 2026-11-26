// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Threading;
using PinataLabs.Typography.Tables;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Tables.Cff;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.Colr;
using PinataLabs.Typography.Tables.General.Kern;
using PinataLabs.Typography.Tables.General.Post;
using PinataLabs.Typography.Tables.TrueType;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography;

/// <summary>
/// <para>
/// Represents a font face with metrics, which is a set of glyphs with a specific style (regular, italic, bold etc).
/// </para>
/// <para>The font source is a stream.</para>
/// </summary>
internal partial class StreamFontMetrics : FontMetrics
{
    private readonly TrueTypeFontTables? trueTypeFontTables;
    private readonly CompactFontTables? compactFontTables;
    private readonly OutlineType outlineType;

    // The GSUB table of the font, or an empty one, so that the shapers also run for a font without GSUB.
    private readonly GSubTable substitution;
    private ArabicFallbackPlan? arabicFallbackPlan;

    // https://docs.microsoft.com/en-us/typography/opentype/spec/otff#font-tables
    // The caches hold one entry for a glyph, its attributes and its orientation, so they cannot grow past the
    // glyphs of the font. A cached entry reports the code point and the text decorations that first asked for the
    // glyph. A caller that gives metrics out gives them those of its text: see GlyphMetrics.WithText and
    // GlyphMetrics.CloneForRendering.
    private readonly ConcurrentDictionary<(ushort Id, TextAttributes Attributes, bool IsVerticalLayout), GlyphMetrics[]> glyphCache;
    private readonly ConcurrentDictionary<(ushort Id, TextAttributes Attributes, bool IsVerticalLayout), GlyphMetrics[]>? colorGlyphCache;
    private readonly FontDescription description;
    private readonly HorizontalMetrics horizontalMetrics;
    private readonly VerticalMetrics verticalMetrics;
    private ushort unitsPerEm;
    private float scaleFactor;
    private short subscriptXSize;
    private short subscriptYSize;
    private short subscriptXOffset;
    private short subscriptYOffset;
    private short superscriptXSize;
    private short superscriptYSize;
    private short superscriptXOffset;
    private short superscriptYOffset;
    private short strikeoutSize;
    private short strikeoutPosition;
    private short underlinePosition;
    private short underlineThickness;
    private float italicAngle;
    private FontMetrics? owner;

    // A face of a variable font keeps weak references to the variation instances that are alive, so that fonts that ask
    // for the same coordinates share one instance and its glyph caches, and an instance that no font uses is collected.
    // A static font and an instance have none.
    private readonly ConcurrentDictionary<NormalizedCoordinates, WeakReference<StreamFontMetrics>>? instances;

    // The face that an instance varies, which is null for a face. An instance shares the tables of its face.
    private readonly StreamFontMetrics? face;
    private readonly NormalizedCoordinates coordinates;
    private readonly IReadOnlyList<FontVariation> variations;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamFontMetrics"/> class.
    /// </summary>
    /// <param name="tables">The True Type font tables.</param>
    internal StreamFontMetrics(TrueTypeFontTables tables)
    {
        this.trueTypeFontTables = tables;
        this.outlineType = OutlineType.TrueType;
        this.substitution = tables.GSub ?? GSubTable.CreateEmpty();
        this.description = new FontDescription(tables.Name, tables.Os2, tables.Head, tables.Post.ItalicAngle, tables.Fvar);
        this.glyphCache = new();
        if (tables.Colr is not null)
        {
            this.colorGlyphCache = new();
        }

        (HorizontalMetrics HorizontalMetrics, VerticalMetrics VerticalMetrics) metrics = this.Initialize(tables);
        this.horizontalMetrics = metrics.HorizontalMetrics;
        this.verticalMetrics = metrics.VerticalMetrics;
        this.coordinates = tables.Fvar?.DefaultCoordinates ?? default;
        this.variations = tables.Fvar?.DefaultVariations ?? [];
        this.instances = tables.Fvar is null ? null : new();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamFontMetrics"/> class.
    /// </summary>
    /// <param name="tables">The Compact Font tables.</param>
    internal StreamFontMetrics(CompactFontTables tables)
    {
        this.compactFontTables = tables;
        this.outlineType = OutlineType.CFF;
        this.substitution = tables.GSub ?? GSubTable.CreateEmpty();
        this.description = new FontDescription(tables.Name, tables.Os2, tables.Head, tables.Post.ItalicAngle, tables.Fvar);
        this.glyphCache = new();
        if (tables.Colr is not null)
        {
            this.colorGlyphCache = new();
        }

        (HorizontalMetrics HorizontalMetrics, VerticalMetrics VerticalMetrics) metrics = this.Initialize(tables);
        this.horizontalMetrics = metrics.HorizontalMetrics;
        this.verticalMetrics = metrics.VerticalMetrics;
        this.coordinates = tables.Fvar?.DefaultCoordinates ?? default;
        this.variations = tables.Fvar?.DefaultVariations ?? [];
        this.instances = tables.Fvar is null ? null : new();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamFontMetrics"/> class as a variation instance of a face.
    /// The instance shares the parsed tables of the face, which do not change after they load, and owns what depends on
    /// the coordinates, which for now is the glyph caches.
    /// </summary>
    /// <param name="face">The face to vary.</param>
    /// <param name="coordinates">The normalised coordinates of the instance. They are not all 0.</param>
    /// <param name="variations">The user coordinates of the request that made the instance.</param>
    private StreamFontMetrics(StreamFontMetrics face, NormalizedCoordinates coordinates, IReadOnlyList<FontVariation> variations)
    {
        this.face = face;
        this.coordinates = coordinates;
        this.variations = variations;
        this.trueTypeFontTables = face.trueTypeFontTables;
        this.compactFontTables = face.compactFontTables;
        this.outlineType = face.outlineType;
        this.substitution = face.substitution;
        this.description = face.Owner.Description;
        this.glyphCache = new();
        if (face.colorGlyphCache is not null)
        {
            this.colorGlyphCache = new();
        }

        this.horizontalMetrics = face.horizontalMetrics;
        this.verticalMetrics = face.verticalMetrics;
        this.HeadFlags = face.HeadFlags;
        this.unitsPerEm = face.unitsPerEm;
        this.scaleFactor = face.scaleFactor;
        this.subscriptXSize = face.subscriptXSize;
        this.subscriptYSize = face.subscriptYSize;
        this.subscriptXOffset = face.subscriptXOffset;
        this.subscriptYOffset = face.subscriptYOffset;
        this.superscriptXSize = face.superscriptXSize;
        this.superscriptYSize = face.superscriptYSize;
        this.superscriptXOffset = face.superscriptXOffset;
        this.superscriptYOffset = face.superscriptYOffset;
        this.strikeoutSize = face.strikeoutSize;
        this.strikeoutPosition = face.strikeoutPosition;
        this.underlinePosition = face.underlinePosition;
        this.underlineThickness = face.underlineThickness;
        this.italicAngle = face.italicAngle;
    }

    /// <summary>
    /// Gets or sets the metrics that the caller sees in place of this object. A <see cref="FileFontMetrics"/>
    /// loads this object on first use, and names itself here, so that <see cref="GlyphRendererParameters"/>
    /// reports the face that <see cref="Font.FontMetrics"/> gives. It is this object when nothing wraps it.
    /// </summary>
    internal FontMetrics Owner
    {
        get => this.owner ?? this;
        set => this.owner = value;
    }

    public HeadTable.HeadFlags HeadFlags { get; private set; }

    /// <summary>
    /// Gets a value indicating whether these metrics are the face and not a variation instance of it.
    /// </summary>
    internal bool IsDefaultInstance => this.face is null;

    /// <inheritdoc/>
    public override FontDescription Description => this.description;

    /// <inheritdoc/>
    public override ushort UnitsPerEm => this.unitsPerEm;

    /// <inheritdoc/>
    public override float ScaleFactor => this.scaleFactor;

    /// <inheritdoc/>
    public override HorizontalMetrics HorizontalMetrics => this.horizontalMetrics;

    /// <inheritdoc/>
    public override VerticalMetrics VerticalMetrics => this.verticalMetrics;

    /// <inheritdoc/>
    public override short SubscriptXSize => this.subscriptXSize;

    /// <inheritdoc/>
    public override short SubscriptYSize => this.subscriptYSize;

    /// <inheritdoc/>
    public override short SubscriptXOffset => this.subscriptXOffset;

    /// <inheritdoc/>
    public override short SubscriptYOffset => this.subscriptYOffset;

    /// <inheritdoc/>
    public override short SuperscriptXSize => this.superscriptXSize;

    /// <inheritdoc/>
    public override short SuperscriptYSize => this.superscriptYSize;

    /// <inheritdoc/>
    public override short SuperscriptXOffset => this.superscriptXOffset;

    /// <inheritdoc/>
    public override short SuperscriptYOffset => this.superscriptYOffset;

    /// <inheritdoc/>
    public override short StrikeoutSize => this.strikeoutSize;

    /// <inheritdoc/>
    public override short StrikeoutPosition => this.strikeoutPosition;

    /// <inheritdoc/>
    public override short UnderlinePosition => this.underlinePosition;

    /// <inheritdoc/>
    public override short UnderlineThickness => this.underlineThickness;

    /// <inheritdoc/>
    public override float ItalicAngle => this.italicAngle;

    /// <inheritdoc/>
    public override IReadOnlyList<FontVariationAxis> VariationAxes => this.description.Fvar?.Axes ?? [];

    /// <inheritdoc/>
    public override IReadOnlyList<FontNamedInstance> NamedInstances => this.description.Fvar?.NamedInstances ?? [];

    /// <inheritdoc/>
    public override IReadOnlyList<FontVariation> Variations => this.variations;

    /// <inheritdoc/>
    internal override NormalizedCoordinates Coordinates => this.coordinates;

    /// <inheritdoc/>
    internal override FontMetrics Face => this.face?.Owner ?? this.Owner;

    /// <inheritdoc/>
    internal override FontMetrics GetInstance(NormalizedCoordinates coordinates, ReadOnlySpan<double> userCoordinates)
    {
        if (this.face is not null)
        {
            return this.face.GetInstance(coordinates, userCoordinates);
        }

        FvarTable? fvar = this.description.Fvar;
        if (this.instances is null || fvar is null || coordinates.IsDefault)
        {
            return this.Owner;
        }

        if (this.instances.TryGetValue(coordinates, out WeakReference<StreamFontMetrics>? existing) && existing.TryGetTarget(out StreamFontMetrics? alive))
        {
            return alive;
        }

        // Two threads that miss together each make an instance, and the second to add its own finds the first.
        // Both instances are correct, so the race costs an object.
        StreamFontMetrics created = new(this, coordinates, fvar.CreateVariations(userCoordinates));
        return this.AddInstance(coordinates, created);
    }

    /// <inheritdoc/>
    internal override bool TryGetGlyphId(CodePoint codePoint, out ushort glyphId)
        => this.TryGetGlyphId(codePoint, null, out glyphId, out bool _);

    /// <inheritdoc/>
    internal override bool TryGetGlyphId(CodePoint codePoint, CodePoint? nextCodePoint, out ushort glyphId, out bool skipNextCodePoint)
    {
        CMapTable cmap = this.outlineType == OutlineType.TrueType
            ? this.trueTypeFontTables!.Cmap
            : this.compactFontTables!.Cmap;

        return cmap.TryGetGlyphId(codePoint, nextCodePoint, out glyphId, out skipNextCodePoint);
    }

    /// <inheritdoc/>
    internal override bool TryGetGlyphClass(ushort glyphId, [NotNullWhen(true)] out GlyphClassDef? glyphClass)
    {
        GlyphDefinitionTable? gdef = this.outlineType == OutlineType.TrueType
            ? this.trueTypeFontTables!.Gdef
            : this.compactFontTables!.Gdef;

        glyphClass = null;
        return gdef is not null && gdef.TryGetGlyphClass(glyphId, out glyphClass);
    }

    /// <inheritdoc/>
    internal override bool TryGetMarkAttachmentClass(ushort glyphId, [NotNullWhen(true)] out GlyphClassDef? markAttachmentClass)
    {
        GlyphDefinitionTable? gdef = this.outlineType == OutlineType.TrueType
            ? this.trueTypeFontTables!.Gdef
            : this.compactFontTables!.Gdef;

        markAttachmentClass = null;
        return gdef is not null && gdef.TryGetMarkAttachmentClass(glyphId, out markAttachmentClass);
    }

    /// <inheritdoc/>
    internal override bool IsInMarkGlyphSet(ushort markGlyphSet, ushort glyphId)
    {
        GlyphDefinitionTable? gdef = this.outlineType == OutlineType.TrueType
            ? this.trueTypeFontTables!.Gdef
            : this.compactFontTables!.Gdef;

        return gdef?.MarkGlyphSetsTable?.Covers(markGlyphSet, glyphId) ?? false;
    }

    /// <inheritdoc/>
    public override bool TryGetGlyphMetrics(
        CodePoint codePoint,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        LayoutMode layoutMode,
        ColorFontSupport support,
        [NotNullWhen(true)] out IReadOnlyList<GlyphMetrics>? metrics)
    {
        // We return metrics for the special glyph representing a missing character, commonly known as .notdef.
        this.TryGetGlyphId(codePoint, out ushort glyphId);
        GlyphMetrics[] cached = this.GetCachedGlyphMetrics(codePoint, glyphId, textAttributes, textDecorations, layoutMode, support, false);
        metrics = WithText(cached, codePoint, textDecorations);
        return cached.Length > 0;
    }

    /// <inheritdoc/>
    internal override IReadOnlyList<GlyphMetrics> GetGlyphMetrics(
        CodePoint codePoint,
        ushort glyphId,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        LayoutMode layoutMode,
        ColorFontSupport support,
        bool hasVerticalForm = false)
        => this.GetCachedGlyphMetrics(codePoint, glyphId, textAttributes, textDecorations, layoutMode, support, hasVerticalForm);

    /// <inheritdoc />
    internal override IReadOnlyList<CodePoint> GetAvailableCodePoints()
    {
        CMapTable cmap = this.outlineType == OutlineType.TrueType
            ? this.trueTypeFontTables!.Cmap
            : this.compactFontTables!.Cmap;

        return cmap.GetAvailableCodePoints();
    }

    /// <inheritdoc/>
    internal override GSubTable GetGSubTable() => this.substitution;

    /// <inheritdoc/>
    internal override ArabicFallbackPlan GetArabicFallbackPlan()
    {
        // Two threads can each build the plan. One of them wins, and the plans are equal and do not change.
        ArabicFallbackPlan? plan = Volatile.Read(ref this.arabicFallbackPlan);
        if (plan is null)
        {
            plan = ArabicFallbackPlan.Create(this);
            plan = Interlocked.CompareExchange(ref this.arabicFallbackPlan, plan, null) ?? plan;
        }

        return plan;
    }

    /// <inheritdoc/>
    internal override bool HasMarkPositioning(ScriptClass script)
    {
        GPosTable? gpos = this.outlineType == OutlineType.TrueType
            ? this.trueTypeFontTables!.GPos
            : this.compactFontTables!.GPos;

        return gpos?.HasFeature(script, GPosTable.MarkTag) == true;
    }

    /// <inheritdoc/>
    internal override void ApplySubstitution(GlyphSubstitutionCollection collection)
        => this.substitution.ApplySubstitution(this, collection);

    /// <inheritdoc/>
    internal override bool TryGetKerningOffset(ushort previousId, ushort currentId, out Vector2 vector)
    {
        bool isTTF = this.outlineType == OutlineType.TrueType;
        KerningTable? kern = isTTF
            ? this.trueTypeFontTables!.Kern
            : this.compactFontTables!.Kern;

        if (kern is null)
        {
            vector = default;
            return false;
        }

        return kern.TryGetKerningOffset(previousId, currentId, out vector);
    }

    /// <inheritdoc/>
    internal override void UpdatePositions(GlyphPositioningCollection collection)
    {
        bool isTTF = this.outlineType == OutlineType.TrueType;
        GPosTable? gpos = isTTF
            ? this.trueTypeFontTables!.GPos
            : this.compactFontTables!.GPos;

        FallbackMarkPositioner.ZeroMarksBeforeKerning(this, collection, gpos);
        gpos?.TryUpdatePositions(this, collection);

        // TODO: I don't think we should disable kerning here.
        if (collection.TextOptions.KerningMode != KerningMode.None)
        {
            KerningTable? kern = isTTF
                ? this.trueTypeFontTables!.Kern
                : this.compactFontTables!.Kern;

            kern?.UpdatePositions(this, collection, gpos);
        }

        FallbackMarkPositioner.PlaceMarksAfterKerning(this, collection, gpos);
        collection.HideDefaultIgnorables(this);
    }

    /// <summary>
    /// Reads a <see cref="StreamFontMetrics"/> from the specified stream.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>a <see cref="StreamFontMetrics"/>.</returns>
    public static StreamFontMetrics LoadFont(string path)
    {
        using FileStream fs = File.OpenRead(path);
        var reader = new FontReader(fs);
        return LoadFont(reader);
    }

    /// <summary>
    /// Reads a <see cref="StreamFontMetrics"/> from the specified stream.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="offset">Position in the stream to read the font from.</param>
    /// <returns>a <see cref="StreamFontMetrics"/>.</returns>
    public static StreamFontMetrics LoadFont(string path, long offset)
    {
        using FileStream fs = File.OpenRead(path);
        fs.Position = offset;
        return LoadFont(fs);
    }

    /// <summary>
    /// Reads a <see cref="StreamFontMetrics"/> from the specified stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>a <see cref="StreamFontMetrics"/>.</returns>
    public static StreamFontMetrics LoadFont(Stream stream)
    {
        var reader = new FontReader(stream);
        return LoadFont(reader);
    }

    internal static StreamFontMetrics LoadFont(FontReader reader)
    {
        if (reader.OutlineType == OutlineType.TrueType)
        {
            return LoadTrueTypeFont(reader);
        }
        else
        {
            return LoadCompactFont(reader);
        }
    }

    private (HorizontalMetrics HorizontalMetrics, VerticalMetrics VerticalMetrics) Initialize<T>(T tables)
        where T : IFontTables
    {
        HeadTable head = tables.Head;
        HorizontalHeadTable hhea = tables.Hhea;
        VerticalHeadTable? vhea = tables.Vhea;
        OS2Table os2 = tables.Os2;
        PostTable post = tables.Post;

        this.HeadFlags = head.Flags;
        this.unitsPerEm = head.UnitsPerEm;
        this.scaleFactor = this.unitsPerEm * 72F; // 72 * UnitsPerEm means 1pt = 1px
        this.subscriptXSize = os2.SubscriptXSize;
        this.subscriptYSize = os2.SubscriptYSize;
        this.subscriptXOffset = os2.SubscriptXOffset;
        this.subscriptYOffset = os2.SubscriptYOffset;
        this.superscriptXSize = os2.SuperscriptXSize;
        this.superscriptYSize = os2.SuperscriptYSize;
        this.superscriptXOffset = os2.SuperscriptXOffset;
        this.superscriptYOffset = os2.SuperscriptYOffset;
        this.strikeoutSize = os2.StrikeoutSize;
        this.strikeoutPosition = os2.StrikeoutPosition;
        this.underlinePosition = post.UnderlinePosition;
        this.underlineThickness = post.UnderlineThickness;
        this.italicAngle = post.ItalicAngle;

        HorizontalMetrics horizontalMetrics = InitializeHorizontalMetrics(hhea, vhea, os2);
        VerticalMetrics verticalMetrics = InitializeVerticalMetrics(horizontalMetrics, vhea);
        return (horizontalMetrics, verticalMetrics);
    }

    private static HorizontalMetrics InitializeHorizontalMetrics(HorizontalHeadTable hhea, VerticalHeadTable? vhea, OS2Table os2)
    {
        short ascender;
        short descender;
        short lineGap;
        short lineHeight;
        short advanceWidthMax;
        short advanceHeightMax;

        // https://www.microsoft.com/typography/otspec/recom.htm#tad
        // We use the same approach as FreeType for calculating the the global  ascender, descender,  and
        // height of  OpenType fonts for consistency.
        //
        // 1.If the OS/ 2 table exists and the fsSelection bit 7 is set (USE_TYPO_METRICS), trust the font
        //   and use the Typo* metrics.
        // 2.Otherwise, use the HorizontalHeadTable "hhea" table's metrics.
        // 3.If they are zero and the OS/ 2 table exists,
        //    - Use the OS/ 2 table's sTypo* metrics if they are non-zero.
        //    - Otherwise, use the OS / 2 table's usWin* metrics.
        bool useTypoMetrics = os2.FontStyle.HasFlag(OS2Table.FontStyleSelection.USE_TYPO_METRICS);
        if (useTypoMetrics)
        {
            ascender = os2.TypoAscender;
            descender = os2.TypoDescender;
            lineGap = os2.TypoLineGap;
            lineHeight = (short)(ascender - descender + lineGap);
        }
        else
        {
            ascender = hhea.Ascender;
            descender = hhea.Descender;
            lineGap = hhea.LineGap;
            lineHeight = (short)(ascender - descender + lineGap);
        }

        if (ascender == 0 || descender == 0)
        {
            if (os2.TypoAscender != 0 || os2.TypoDescender != 0)
            {
                ascender = os2.TypoAscender;
                descender = os2.TypoDescender;
                lineGap = os2.TypoLineGap;
                lineHeight = (short)(ascender - descender + lineGap);
            }
            else
            {
                ascender = (short)os2.WinAscent;
                descender = (short)-os2.WinDescent;
                lineHeight = (short)(ascender - descender);
            }
        }

        advanceWidthMax = (short)hhea.AdvanceWidthMax;
        advanceHeightMax = vhea == null ? lineHeight : vhea.AdvanceHeightMax;

        return new()
        {
            Ascender = ascender,
            Descender = descender,
            LineGap = lineGap,
            LineHeight = lineHeight,
            AdvanceWidthMax = advanceWidthMax,
            AdvanceHeightMax = advanceHeightMax
        };
    }

    private static VerticalMetrics InitializeVerticalMetrics(HorizontalMetrics metrics, VerticalHeadTable? vhea)
    {
        VerticalMetrics verticalMetrics = new()
        {
            Ascender = metrics.Ascender,
            Descender = metrics.Descender,
            LineGap = metrics.LineGap,
            LineHeight = metrics.LineHeight,
            AdvanceWidthMax = metrics.AdvanceWidthMax,
            AdvanceHeightMax = metrics.AdvanceHeightMax,
            Synthesized = true
        };

        if (vhea is null)
        {
            return verticalMetrics;
        }

        short ascender = vhea.Ascender;

        // Always negative due to the grid orientation.
        short descender = (short)(vhea.Descender > 0 ? -vhea.Descender : vhea.Descender);
        short lineGap = vhea.LineGap;
        short lineHeight = (short)(ascender - descender + lineGap);

        verticalMetrics.Ascender = ascender;
        verticalMetrics.Descender = descender;
        verticalMetrics.LineGap = lineGap;
        verticalMetrics.LineHeight = lineHeight;
        verticalMetrics.Synthesized = false;

        return verticalMetrics;
    }

    /// <summary>
    /// Reads a <see cref="StreamFontMetrics"/> from the specified stream.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <returns>a <see cref="StreamFontMetrics"/>.</returns>
    public static StreamFontMetrics[] LoadFontCollection(string path)
    {
        using FileStream fs = File.OpenRead(path);
        return LoadFontCollection(fs);
    }

    /// <summary>
    /// Reads a <see cref="StreamFontMetrics"/> from the specified stream.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <returns>a <see cref="StreamFontMetrics"/>.</returns>
    public static StreamFontMetrics[] LoadFontCollection(Stream stream)
    {
        long startPos = stream.Position;
        var reader = new BigEndianBinaryReader(stream, true);
        var ttcHeader = TtcHeader.Read(reader);
        var fonts = new StreamFontMetrics[(int)ttcHeader.NumFonts];

        for (int i = 0; i < ttcHeader.NumFonts; ++i)
        {
            stream.Position = startPos + ttcHeader.OffsetTable[i];
            fonts[i] = LoadFont(stream);
        }

        return fonts;
    }

    // Adds an instance to the table of the face, or gives the instance that a racing thread added first. An entry whose
    // instance was collected is replaced. Each add also removes the entries of collected instances, so the table does not
    // grow when an application asks for a new weight every frame.
    private StreamFontMetrics AddInstance(NormalizedCoordinates coordinates, StreamFontMetrics created)
    {
        ConcurrentDictionary<NormalizedCoordinates, WeakReference<StreamFontMetrics>> table = this.instances!;
        WeakReference<StreamFontMetrics> reference = new(created);
        while (true)
        {
            if (table.TryAdd(coordinates, reference))
            {
                break;
            }

            if (table.TryGetValue(coordinates, out WeakReference<StreamFontMetrics>? existing))
            {
                if (existing.TryGetTarget(out StreamFontMetrics? alive))
                {
                    return alive;
                }

                if (table.TryUpdate(coordinates, reference, existing))
                {
                    break;
                }
            }
        }

        foreach (KeyValuePair<NormalizedCoordinates, WeakReference<StreamFontMetrics>> entry in table)
        {
            if (!entry.Value.TryGetTarget(out _))
            {
                ((ICollection<KeyValuePair<NormalizedCoordinates, WeakReference<StreamFontMetrics>>>)table).Remove(entry);
            }
        }

        return created;
    }

    private static (ushort Id, TextAttributes Attributes, bool IsVerticalLayout) CreateCacheKey(
        CodePoint codePoint,
        ushort glyphId,
        TextAttributes textAttributes,
        LayoutMode layoutMode,
        bool hasVerticalForm)
        => (glyphId, textAttributes, AdvancedTypographicUtils.IsVerticalGlyph(codePoint, layoutMode, hasVerticalForm));

    // A glyph that several code points share, such as the space glyph of a no-break space or .notdef, or that is
    // asked for with other text decorations, has its cached metrics copied for every caller but the first.
    private static GlyphMetrics[] WithText(GlyphMetrics[] cached, CodePoint codePoint, TextDecorations textDecorations)
    {
        if (cached.Length == 0 || (cached[0].CodePoint == codePoint && cached[0].TextDecorations == textDecorations))
        {
            return cached;
        }

        GlyphMetrics[] metrics = new GlyphMetrics[cached.Length];
        for (int i = 0; i < cached.Length; i++)
        {
            metrics[i] = cached[i].WithText(codePoint, textDecorations);
        }

        return metrics;
    }

    private GlyphMetrics[] GetCachedGlyphMetrics(
        CodePoint codePoint,
        ushort glyphId,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        LayoutMode layoutMode,
        ColorFontSupport support,
        bool hasVerticalForm)
    {
        (ushort Id, TextAttributes Attributes, bool IsVerticalLayout) key = CreateCacheKey(codePoint, glyphId, textAttributes, layoutMode, hasVerticalForm);
        if (support == ColorFontSupport.MicrosoftColrFormat
            && this.TryGetColoredMetrics(codePoint, key, textDecorations, out GlyphMetrics[]? colored))
        {
            return colored;
        }

        if (this.glyphCache.TryGetValue(key, out GlyphMetrics[]? metrics))
        {
            return metrics;
        }

        // A glyph id of zero means that no glyph was found in this face for the codepoint.
        GlyphType glyphType = glyphId == 0 ? GlyphType.Fallback : GlyphType.Standard;
        return this.glyphCache.GetOrAdd(key, [this.CreateGlyphMetrics(codePoint, glyphId, glyphType, textAttributes, textDecorations, key.IsVerticalLayout)]);
    }

    private bool TryGetColoredMetrics(
        CodePoint codePoint,
        (ushort Id, TextAttributes Attributes, bool IsVerticalLayout) key,
        TextDecorations textDecorations,
        [NotNullWhen(true)] out GlyphMetrics[]? metrics)
    {
        ColrTable? colr = this.outlineType == OutlineType.TrueType
            ? this.trueTypeFontTables!.Colr
            : this.compactFontTables!.Colr;

        if (colr == null || this.colorGlyphCache == null)
        {
            metrics = null;
            return false;
        }

        if (!this.colorGlyphCache.TryGetValue(key, out metrics))
        {
            Span<LayerRecord> layers = colr.GetLayers(key.Id);
            GlyphMetrics[] created = layers.Length == 0 ? [] : new GlyphMetrics[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                LayerRecord layer = layers[i];
                created[i] = this.CreateGlyphMetrics(codePoint, layer.GlyphId, GlyphType.ColrLayer, key.Attributes, textDecorations, key.IsVerticalLayout, layer.PaletteIndex);
            }

            metrics = this.colorGlyphCache.GetOrAdd(key, created);
        }

        return metrics.Length > 0;
    }

    private GlyphMetrics CreateGlyphMetrics(
        CodePoint codePoint,
        ushort glyphId,
        GlyphType glyphType,
        TextAttributes textAttributes,
        TextDecorations textDecorations,
        bool isVerticalLayout,
        ushort paletteIndex = 0)
        => this.outlineType switch
        {
            OutlineType.TrueType => this.CreateTrueTypeGlyphMetrics(codePoint, glyphId, glyphType, textAttributes, textDecorations, isVerticalLayout, paletteIndex),
            OutlineType.CFF => this.CreateCffGlyphMetrics(codePoint, glyphId, glyphType, textAttributes, textDecorations, isVerticalLayout, paletteIndex),
            _ => throw new NotSupportedException(),
        };
}
