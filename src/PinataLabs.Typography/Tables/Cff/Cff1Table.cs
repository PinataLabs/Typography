// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tables.Cff;

internal sealed class Cff1Table : Table, ICffTable
{
    internal const string TableName = "CFF "; // 4 chars

    public Cff1Table(CffFont font) => this.Font = font;

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

        return glyph;
    }

    public static Cff1Table? Load(FontReader fontReader)
    {
        if (!fontReader.TryGetReaderAtTablePosition(TableName, out BigEndianBinaryReader? binaryReader, out TableHeader? header))
        {
            return null;
        }

        using (binaryReader)
        {
            return new(CffParser.Load(binaryReader.ReadBytes((int)header.Length)));
        }
    }
}
