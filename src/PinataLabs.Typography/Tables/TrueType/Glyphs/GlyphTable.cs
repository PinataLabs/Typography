// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.Woff;

namespace PinataLabs.Typography.Tables.TrueType.Glyphs;

/// <summary>
/// The glyphs of a TrueType font for text layout. It keeps the bytes of the 'glyf' and 'loca' tables
/// and decodes a glyph when asked. A glyph ID outside the font selects glyph 0.
/// </summary>
internal sealed class GlyphTable : Table
{
    internal const string TableName = "glyf";

    private const string LocationTableName = "loca";

    // The number of contours, then the bounding box.
    private const int HeaderSize = 10;

    private readonly TrueTypeGlyphData data;

    public GlyphTable(TrueTypeGlyphData data)
        => this.data = data;

    public int GlyphCount => this.data.GlyphCount;

    public static GlyphTable? Load(FontReader reader)
    {
        if (reader.GetHeader(TableName) is null)
        {
            return null;
        }

        HeadTable head = reader.GetTable<HeadTable>();
        MaximumProfileTable maxp = reader.GetTable<MaximumProfileTable>();
        byte[] glyf = ReadTable(reader, TableName)!;

        // A WOFF2 file holds the two tables as one transformed table.
        if (reader.TableFormat is TableFormat.Woff2)
        {
            Woff2Utils.ReconstructGlyphTables(glyf, out glyf, out byte[] offsets);
            return new GlyphTable(new TrueTypeGlyphData(offsets, glyf, longOffsets: true, maxp.GlyphCount));
        }

        bool longOffsets = head.IndexLocationFormat switch
        {
            HeadTable.IndexLocationFormats.Offset16 => false,
            HeadTable.IndexLocationFormats.Offset32 => true,
            _ => throw new InvalidFontTableException("indexToLocFormat an invalid value", "head"),
        };

        byte[] loca = ReadTable(reader, LocationTableName)
            ?? throw new MissingFontTableException($"Table '{LocationTableName}' is missing", LocationTableName);

        return new GlyphTable(new TrueTypeGlyphData(loca, glyf, longOffsets, maxp.GlyphCount));
    }

    /// <summary>
    /// Gets the bounding box that the font gives a glyph. An empty glyph takes the box of glyph 0.
    /// </summary>
    /// <returns>The bounding box, or <see cref="Bounds.Empty"/> if glyph 0 has none.</returns>
    public Bounds GetBounds(int glyphId)
        => this.TryReadBounds(this.Resolve(glyphId), out Bounds bounds) || this.TryReadBounds(0, out bounds)
            ? bounds
            : Bounds.Empty;

    /// <summary>
    /// Writes the unhinted outline of a glyph in glyph space.
    /// </summary>
    /// <returns><see langword="false"/> if the glyph is malformed. The sink may then hold a part of the outline.</returns>
    public bool TryWriteOutline<TSink>(int glyphId, ref TSink sink)
        where TSink : IGlyphOutlineSink
        => TrueTypeGlyphDecoder.TryWriteOutline(this.data, this.Resolve(glyphId), ref sink);

    /// <summary>
    /// Reads the points of a glyph in glyph space for the hinting interpreter.
    /// </summary>
    /// <returns><see langword="false"/> if the glyph is malformed.</returns>
    public bool TryReadPoints(int glyphId, out GlyphVector vector)
        => TrueTypeGlyphDecoder.TryReadPoints(this.data, this.Resolve(glyphId), out vector);

    /// <summary>
    /// Gets one point of a glyph in glyph space.
    /// </summary>
    /// <returns><see langword="false"/> if the glyph has no such point or is malformed.</returns>
    public bool TryGetPoint(int glyphId, int index, out Vector2 point)
        => TrueTypeGlyphDecoder.TryGetPoint(this.data, this.Resolve(glyphId), index, out point);

    private static byte[]? ReadTable(FontReader reader, string tableName)
    {
        if (!reader.TryGetReaderAtTablePosition(tableName, out BigEndianBinaryReader? binaryReader, out TableHeader? header))
        {
            return null;
        }

        using (binaryReader)
        {
            // A header can declare more bytes than the file has.
            long available = binaryReader.BaseStream.Length - binaryReader.BaseStream.Position;
            return binaryReader.ReadBytes((int)Math.Min(header.Length, Math.Max(available, 0)));
        }
    }

    private ushort Resolve(int glyphId)
        => (uint)glyphId < (uint)this.GlyphCount ? (ushort)glyphId : (ushort)0;

    private bool TryReadBounds(int glyphId, out Bounds bounds)
    {
        if (!this.data.TryGetGlyphData(glyphId, out ReadOnlyMemory<byte> glyph) || glyph.Length < HeaderSize)
        {
            bounds = default;
            return false;
        }

        ReadOnlySpan<byte> box = glyph.Span.Slice(2);
        bounds = new Bounds(
            BinaryPrimitives.ReadInt16BigEndian(box),
            BinaryPrimitives.ReadInt16BigEndian(box.Slice(2)),
            BinaryPrimitives.ReadInt16BigEndian(box.Slice(4)),
            BinaryPrimitives.ReadInt16BigEndian(box.Slice(6)));
        return true;
    }
}
