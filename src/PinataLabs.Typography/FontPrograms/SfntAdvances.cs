// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// The advance widths of the 'hmtx' table, read when a glyph is asked for. Both tables are optional.
/// </summary>
internal readonly struct SfntAdvances
{
    private readonly ReadOnlyMemory<byte> hmtx;
    private readonly int metricCount;

    private SfntAdvances(ReadOnlyMemory<byte> hmtx, int metricCount)
    {
        this.hmtx = hmtx;
        this.metricCount = metricCount;
    }

    /// <summary>
    /// Gets a value indicating whether the font has advance widths.
    /// </summary>
    public bool IsEmpty => this.metricCount == 0;

    public static SfntAdvances Read(SfntDirectory directory)
    {
        if (!directory.TryGetTable(SfntDirectory.Hhea, out ReadOnlyMemory<byte> hhea)
            || !directory.TryGetTable(SfntDirectory.Hmtx, out ReadOnlyMemory<byte> hmtx)
            || hhea.Length < 36)
        {
            return default;
        }

        // numberOfHMetrics is the last field of 'hhea'. Each metric is an advance and a side bearing.
        int metricCount = BinaryPrimitives.ReadUInt16BigEndian(hhea.Span.Slice(34));
        return new SfntAdvances(hmtx, Math.Min(metricCount, hmtx.Length / 4));
    }

    /// <summary>
    /// Gets the advance width of a glyph. A glyph after the last metric has the advance of the last metric.
    /// </summary>
    public bool TryGet(int glyphId, out float advanceWidth)
    {
        if (this.IsEmpty)
        {
            advanceWidth = 0;
            return false;
        }

        int index = Math.Min(glyphId, this.metricCount - 1);
        advanceWidth = BinaryPrimitives.ReadUInt16BigEndian(this.hmtx.Span.Slice(index * 4));
        return true;
    }
}
