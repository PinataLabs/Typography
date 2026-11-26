// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tables.Cff;

internal sealed class Cff2Table : Table, ICffTable
{
    internal const string TableName = "CFF2";

    private readonly CffGlyphData[] glyphs;
    private readonly ItemVariationStore store;

    private Cff2Table(CffGlyphData[] glyphs, in ItemVariationStore store)
    {
        this.glyphs = glyphs;
        this.store = store;
    }

    public int GlyphCount => this.glyphs.Length;

    public CffGlyphData GetGlyph(int index)
        => this.glyphs[index];

    public static Cff2Table? Load(FontReader fontReader)
    {
        if (!fontReader.TryGetReaderAtTablePosition(TableName, out BigEndianBinaryReader? binaryReader, out TableHeader? header))
        {
            return null;
        }

        using (binaryReader)
        {
            return Create(binaryReader.ReadBytes((int)header.Length));
        }
    }

    /// <summary>
    /// Loads a CFF2 table from a reader positioned at the start of the table, reading to the end of the stream.
    /// The glyphs are those of the default instance: the <c>blend</c> operators of a glyph scale their deltas by 0.
    /// A variation instance draws a glyph with <see cref="CffGlyphData.WithBlendScalars"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The <see cref="Cff2Table"/>.</returns>
    public static Cff2Table Load(BigEndianBinaryReader reader)
    {
        long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        return Create(reader.ReadBytes((int)remaining));
    }

    /// <summary>
    /// Gets the scalars that the <c>blend</c> operators of a variation instance multiply their deltas with.
    /// </summary>
    /// <param name="coordinates">The normalised F2Dot14 coordinates of the instance, in <c>fvar</c> order.</param>
    /// <returns>For each ItemVariationData of the variation store, the scalars of the regions that it names. A malformed one is <see langword="null"/>.</returns>
    public float[]?[] CreateBlendScalars(ReadOnlySpan<short> coordinates)
        => Cff2Parser.CreateBlendScalars(this.store, this.store.CreateScalars(coordinates));

    private static Cff2Table Create(byte[] data)
    {
        CffGlyphData[] glyphs = Cff2Parser.Load(data, out ItemVariationStore store);
        return new Cff2Table(glyphs, store);
    }
}
