// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff;

internal sealed class Cff2Table : Table, ICffTable
{
    internal const string TableName = "CFF2";

    private readonly CffGlyphData[] glyphs;

    public Cff2Table(CffGlyphData[] glyphs) => this.glyphs = glyphs;

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
            return new(Cff2Parser.Load(binaryReader.ReadBytes((int)header.Length)));
        }
    }

    /// <summary>
    /// Loads a CFF2 table from a reader positioned at the start of the table, reading to the end of the stream.
    /// CFF2 fonts are loaded at their default instance: <c>blend</c> operators use their default values and the
    /// variation axes are ignored.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The <see cref="Cff2Table"/>.</returns>
    public static Cff2Table Load(BigEndianBinaryReader reader)
    {
        long remaining = reader.BaseStream.Length - reader.BaseStream.Position;
        return new(Cff2Parser.Load(reader.ReadBytes((int)remaining)));
    }
}
