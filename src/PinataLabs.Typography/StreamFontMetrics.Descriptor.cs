// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading;
using PinataLabs.Typography.Tables;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.CMap;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.WellKnownIds;

namespace PinataLabs.Typography;

internal partial class StreamFontMetrics
{
    private const int PanoseFamilyKind = 0;
    private const int PanoseSerifStyle = 1;
    private const byte PanoseLatinText = 2;
    private const byte PanoseLatinScript = 3;
    private const byte PanoseFirstSerifStyle = 2;
    private const byte PanoseLastSerifStyle = 10;
    private const int ScriptsFamilyClass = 10;

    /// <inheritdoc/>
    public override FontDescriptorMetrics DescriptorMetrics
    {
        get
        {
            // Two threads can each build the metrics. One of them wins, and the metrics are equal and do not change.
            FontDescriptorMetrics? metrics = Volatile.Read(ref this.descriptorMetrics);
            if (metrics is null)
            {
                metrics = this.CreateDescriptorMetrics();
                metrics = Interlocked.CompareExchange(ref this.descriptorMetrics, metrics, null) ?? metrics;
            }

            return metrics;
        }
    }

    /// <inheritdoc/>
    public override FontEmbeddingPermissions EmbeddingPermissions
        => this.FontTables.Os2 is { } os2 ? new(os2.FsType, true) : new(0, false);

    private static bool IsSerif(OS2Table? os2)
    {
        if (os2 is null)
        {
            return false;
        }

        int familyClass = os2.FamilyClass >> 8;
        if (familyClass != 0)
        {
            return familyClass is >= 1 and <= 5 or 7;
        }

        return os2.Panose[PanoseFamilyKind] == PanoseLatinText
            && os2.Panose[PanoseSerifStyle] is >= PanoseFirstSerifStyle and <= PanoseLastSerifStyle;
    }

    private static bool IsScript(OS2Table? os2)
    {
        if (os2 is null)
        {
            return false;
        }

        int familyClass = os2.FamilyClass >> 8;
        return familyClass != 0
            ? familyClass == ScriptsFamilyClass
            : os2.Panose[PanoseFamilyKind] == PanoseLatinScript;
    }

    private static bool HasSymbolCmap(CMapTable cmap)
    {
        foreach (CMapSubTable subTable in cmap.Tables)
        {
            if (subTable.Platform == PlatformIDs.Windows && subTable.Encoding == 0)
            {
                return true;
            }
        }

        return false;
    }

    private FontDescriptorMetrics CreateDescriptorMetrics()
    {
        IFontTables tables = this.FontTables;
        OS2Table? os2 = tables.Os2;
        HeadTable head = tables.Head;

        short ascent = this.horizontalMetrics.Ascender;
        short descent = this.horizontalMetrics.Descender;
        MetricSource ascentSource = this.ascentSource;
        MetricSource descentSource = this.descentSource;
        if (ascent == 0 && descent == 0)
        {
            ascent = VariationRounding.ToInt16(head.Bounds.Max.Y);
            descent = VariationRounding.ToInt16(head.Bounds.Min.Y);
            ascentSource = MetricSource.HeadYMax;
            descentSource = MetricSource.HeadYMin;
        }

        short capHeight = this.os2CapHeight;
        MetricSource capHeightSource = MetricSource.Os2CapHeight;
        if (capHeight == 0)
        {
            (capHeight, capHeightSource) = this.TryGetGlyphTop('H', out short top)
                ? (top, MetricSource.CapitalHBounds)
                : (ascent, MetricSource.FallbackAscent);
        }

        short xHeight = this.os2XHeight;
        MetricSource xHeightSource = MetricSource.Os2XHeight;
        if (xHeight == 0)
        {
            (xHeight, xHeightSource) = this.TryGetGlyphTop('x', out short top)
                ? (top, MetricSource.SmallXBounds)
                : ((short)0, MetricSource.None);
        }

        FontBoundingBox boundingBox = new(
            VariationRounding.ToInt16(head.Bounds.Min.X),
            VariationRounding.ToInt16(head.Bounds.Min.Y),
            VariationRounding.ToInt16(head.Bounds.Max.X),
            VariationRounding.ToInt16(head.Bounds.Max.Y));

        FontDescriptorFlags flags = HasSymbolCmap(tables.Cmap) ? FontDescriptorFlags.Symbolic : FontDescriptorFlags.Nonsymbolic;
        if (tables.Post.IsFixedPitch != 0)
        {
            flags |= FontDescriptorFlags.FixedPitch;
        }

        if (IsSerif(os2))
        {
            flags |= FontDescriptorFlags.Serif;
        }

        if (IsScript(os2))
        {
            flags |= FontDescriptorFlags.Script;
        }

        bool isItalic = (os2 is not null && (os2.FontStyle & (OS2Table.FontStyleSelection.ITALIC | OS2Table.FontStyleSelection.OBLIQUE)) != 0)
            || head.MacStyle.HasFlag(HeadTable.HeadMacStyle.Italic)
            || this.italicAngle != 0;
        if (isItalic)
        {
            flags |= FontDescriptorFlags.Italic;
        }

        return new FontDescriptorMetrics(
            ascent,
            ascentSource,
            descent,
            descentSource,
            capHeight,
            capHeightSource,
            xHeight,
            xHeightSource,
            this.italicAngle,
            boundingBox,
            flags);
    }

    private bool TryGetGlyphTop(char character, out short top)
    {
        top = 0;
        CodePoint codePoint = new(character);
        if (!this.TryGetGlyphId(codePoint, out ushort glyphId) || glyphId == 0)
        {
            return false;
        }

        GlyphMetrics[] metrics = this.GetCachedGlyphMetrics(
            codePoint,
            glyphId,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            false);

        if (metrics.Length == 0 || metrics[0].Bounds.Max.Y <= 0)
        {
            return false;
        }

        top = VariationRounding.ToInt16(metrics[0].Bounds.Max.Y);
        return true;
    }
}
