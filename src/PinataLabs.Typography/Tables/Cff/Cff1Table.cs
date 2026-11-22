// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.General;

namespace PinataLabs.Typography.Tables.Cff;

internal sealed class Cff1Table : Table, ICffTable
{
    internal const string TableName = "CFF "; // 4 chars

    private readonly ushort unitsPerEm;

    public Cff1Table(CffFont font, ushort unitsPerEm)
    {
        this.Font = font;
        this.unitsPerEm = unitsPerEm;
    }

    public CffFont Font { get; }

    public int GlyphCount => this.Font.GlyphCount;

    public CffGlyphData GetGlyph(int index)
    {
        // An out-of-range glyph id resolves to glyph 0 (.notdef).
        if ((uint)index >= (uint)this.GlyphCount)
        {
            index = 0;
        }

        if (!this.Font.TryGetGlyph(index, out CffGlyphData glyph))
        {
            throw new InvalidFontFileException($"The data of CFF glyph {index} is malformed.");
        }

        return glyph.InUnitsPerEm(this.unitsPerEm);
    }

    public static Cff1Table? Load(FontReader fontReader)
    {
        if (!fontReader.TryGetReaderAtTablePosition(TableName, out BigEndianBinaryReader? binaryReader, out TableHeader? header))
        {
            return null;
        }

        using (binaryReader)
        {
            CffFont font = CffParser.Load(binaryReader.ReadBytes((int)header.Length));
            return new(font, fontReader.GetTable<HeadTable>().UnitsPerEm);
        }
    }
}
