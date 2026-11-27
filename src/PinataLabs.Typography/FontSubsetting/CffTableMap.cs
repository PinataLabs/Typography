// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// Where the parts of a CFF table are, which a writer needs to copy them to new places: the DICTs, the parts that it
/// copies as they are, and for each Font DICT its Private DICT. The reader of the font keeps none of this, because it does
/// not need it to draw a glyph. The map follows the rules of that reader, so both see the same Font DICTs and the same Private DICTs.
/// </summary>
internal sealed class CffTableMap
{
    // The operators that name a place in the table, for the Top DICT, the Font DICT and the Private DICT.
    private const int OpCharset = 15;
    private const int OpEncoding = 16;
    private const int OpPrivate = 18;
    private const int OpFdArray = 0x0C24;
    private const int OpFdSelect = 0x0C25;

    private const int MinHeaderSize = 4;

    private CffTableMap(
        ReadOnlyMemory<byte> header,
        ReadOnlyMemory<byte> name,
        ReadOnlyMemory<byte> topDict,
        ReadOnlyMemory<byte> strings,
        int charset,
        ReadOnlyMemory<byte> charsetData,
        int encoding,
        ReadOnlyMemory<byte> encodingData,
        ReadOnlyMemory<byte> fdSelect,
        bool hasFdArray,
        FontDictMap[] fontDicts)
    {
        this.Header = header;
        this.Name = name;
        this.TopDict = topDict;
        this.Strings = strings;
        this.Charset = charset;
        this.CharsetData = charsetData;
        this.Encoding = encoding;
        this.EncodingData = encodingData;
        this.FdSelect = fdSelect;
        this.HasFdArray = hasFdArray;
        this.FontDicts = fontDicts;
    }

    /// <summary>Gets the header, which is copied as it is.</summary>
    public ReadOnlyMemory<byte> Header { get; }

    /// <summary>Gets the first font name, or an empty memory when the Name INDEX is empty.</summary>
    public ReadOnlyMemory<byte> Name { get; }

    /// <summary>Gets the Top DICT.</summary>
    public ReadOnlyMemory<byte> TopDict { get; }

    /// <summary>Gets the String INDEX with its count and offsets. The writer copies it as it is, so every SID stays valid.</summary>
    public ReadOnlyMemory<byte> Strings { get; }

    /// <summary>Gets the operand of Charset when it names a predefined charset (0, 1 or 2), else 3.</summary>
    public int Charset { get; }

    /// <summary>Gets the charset when the font has its own, else an empty memory.</summary>
    public ReadOnlyMemory<byte> CharsetData { get; }

    /// <summary>Gets the operand of Encoding when it names a predefined encoding (0 or 1), else 2.</summary>
    public int Encoding { get; }

    /// <summary>Gets the encoding when the font has its own, else an empty memory.</summary>
    public ReadOnlyMemory<byte> EncodingData { get; }

    /// <summary>Gets the FDSelect, or an empty memory when the font has none.</summary>
    public ReadOnlyMemory<byte> FdSelect { get; }

    /// <summary>Gets a value indicating whether the font has an FDArray.</summary>
    public bool HasFdArray { get; }

    /// <summary>
    /// Gets the Font DICTs that the font reads. A font without an FDArray has one, whose DICT is empty and whose
    /// Private DICT the Top DICT names.
    /// </summary>
    public FontDictMap[] FontDicts { get; }

    /// <summary>
    /// Finds the parts of a CFF table.
    /// </summary>
    /// <param name="table">The CFF table, which the reader of the font has already read.</param>
    /// <param name="font">The font that the reader gave.</param>
    /// <param name="budget">The budget that the array of Font DICTs is charged to.</param>
    /// <returns>The map.</returns>
    /// <exception cref="InvalidFontFileException">A part is malformed.</exception>
    /// <exception cref="FontProgramTooLargeException">The array does not fit under the cap of the budget.</exception>
    public static CffTableMap Read(ReadOnlyMemory<byte> table, CffFont font, ref LoadBudget budget)
    {
        ReadOnlySpan<byte> span = table.Span;
        int headerSize = span[2];
        if (headerSize < MinHeaderSize)
        {
            throw new InvalidFontFileException("The CFF header is shorter than 4 bytes.");
        }

        CffIndex names = CffIndex.Read(table, headerSize, 2, out int position);
        CffIndex topDicts = CffIndex.Read(table, position, 2, out position);
        int stringsStart = position;
        CffIndex.Read(table, position, 2, out int stringsEnd);

        ReadOnlyMemory<byte> name = default;
        if (names.Count > 0 && !names.TryGet(0, out name))
        {
            throw new InvalidFontFileException("The CFF Name INDEX is malformed.");
        }

        if (!topDicts.TryGet(0, out ReadOnlyMemory<byte> topDict))
        {
            throw new InvalidFontFileException("The CFF table has no Top DICT.");
        }

        int charset = 0;
        int encoding = 0;
        int privateSize = 0;
        int privateOffset = 0;
        int fdArray = 0;
        int fdSelect = 0;
        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(topDict.Span);
        while (reader.TryRead(operands, out int count, out int op))
        {
            switch (op)
            {
                case OpCharset when count >= 1:
                    charset = ToOffset(operands[0], span.Length);
                    break;
                case OpEncoding when count >= 1:
                    encoding = ToOffset(operands[0], span.Length);
                    break;
                case OpPrivate when count >= 2:
                    privateSize = ToOffset(operands[0], span.Length);
                    privateOffset = ToOffset(operands[1], span.Length);
                    break;
                case OpFdArray when count >= 1:
                    fdArray = ToOffset(operands[0], span.Length);
                    break;
                case OpFdSelect when count >= 1:
                    fdSelect = ToOffset(operands[0], span.Length);
                    break;
            }
        }

        // A CID-keyed font has no encoding, and the reader of the font does not look at one that its Top DICT names.
        ReadOnlyMemory<byte> encodingData = default;
        if (font.IsCidKeyed)
        {
            encoding = 0;
        }
        else if (encoding > 1)
        {
            encodingData = Slice(table, encoding, GetEncodingLength(span.Slice(encoding)));
        }

        ReadOnlyMemory<byte> charsetData = charset > 2 ? Slice(table, charset, GetCharsetLength(span.Slice(charset), font.GlyphCount)) : default;
        ReadOnlyMemory<byte> fdSelectData = fdSelect == 0 ? default : Slice(table, fdSelect, GetFdSelectLength(span.Slice(fdSelect), font.GlyphCount));

        FontDictMap[] fontDicts = budget.Allocate<FontDictMap>(font.FontDictCount);
        if (fdArray == 0)
        {
            fontDicts[0] = new FontDictMap(default, ReadPrivate(table, privateSize, privateOffset));
        }
        else
        {
            CffIndex fdIndex = CffIndex.Read(table, fdArray, 2, out _);
            for (int i = 0; i < fontDicts.Length; i++)
            {
                if (!fdIndex.TryGet(i, out ReadOnlyMemory<byte> fontDict))
                {
                    throw new InvalidFontFileException("The CFF FDArray is malformed.");
                }

                fontDicts[i] = ReadFontDict(table, fontDict);
            }
        }

        return new CffTableMap(
            table.Slice(0, headerSize),
            name,
            topDict,
            table.Slice(stringsStart, stringsEnd - stringsStart),
            charset > 2 ? 3 : charset,
            charsetData,
            encoding > 1 ? 2 : encoding,
            encodingData,
            fdSelectData,
            fdArray != 0,
            fontDicts);
    }

    private static FontDictMap ReadFontDict(ReadOnlyMemory<byte> table, ReadOnlyMemory<byte> fontDict)
    {
        int privateSize = 0;
        int privateOffset = 0;
        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(fontDict.Span);
        while (reader.TryRead(operands, out int count, out int op))
        {
            if (op == OpPrivate && count >= 2)
            {
                privateSize = ToOffset(operands[0], table.Length);
                privateOffset = ToOffset(operands[1], table.Length);
            }
        }

        return new FontDictMap(fontDict, ReadPrivate(table, privateSize, privateOffset));
    }

    private static ReadOnlyMemory<byte> ReadPrivate(ReadOnlyMemory<byte> table, int size, int offset)
        => size == 0 ? default : Slice(table, offset, size);

    private static ReadOnlyMemory<byte> Slice(ReadOnlyMemory<byte> table, int offset, int length)
        => (long)offset + length <= table.Length
            ? table.Slice(offset, length)
            : throw new InvalidFontFileException("A part of the CFF table is out of range.");

    // The length of the charset as the reader of the font reads it: format 0 lists the glyphs after glyph 0,
    // and formats 1 and 2 list ranges until they cover the glyphs.
    private static int GetCharsetLength(ReadOnlySpan<byte> charset, int glyphCount)
    {
        if (charset.IsEmpty)
        {
            throw new InvalidFontFileException("The CFF charset is out of range.");
        }

        int format = charset[0];
        if (format == 0)
        {
            return 1 + (2 * (glyphCount - 1));
        }

        if (format is not (1 or 2))
        {
            throw new InvalidFontFileException($"CFF charset format {format} is not supported.");
        }

        int rangeSize = format == 1 ? 3 : 4;
        int position = 1;
        for (int glyph = 1; glyph < glyphCount;)
        {
            if (position + rangeSize > charset.Length)
            {
                throw new InvalidFontFileException("The CFF charset is truncated.");
            }

            glyph += 1 + (format == 1 ? charset[position + 2] : BinaryPrimitives.ReadUInt16BigEndian(charset.Slice(position + 2)));
            position += rangeSize;
        }

        return position;
    }

    // The length of a custom encoding: the codes of the glyphs from glyph 1, then the supplements when the high bit of the format is set.
    private static int GetEncodingLength(ReadOnlySpan<byte> encoding)
    {
        if (encoding.Length < 2)
        {
            throw new InvalidFontFileException("The CFF encoding is truncated.");
        }

        int length = (encoding[0] & 0x7F) switch
        {
            0 => 2 + encoding[1],
            1 => 2 + (2 * encoding[1]),
            _ => throw new InvalidFontFileException("The CFF encoding has a format that is not supported."),
        };

        if ((encoding[0] & 0x80) != 0)
        {
            if (length >= encoding.Length)
            {
                throw new InvalidFontFileException("The CFF encoding is truncated.");
            }

            length += 1 + (3 * encoding[length]);
        }

        return length <= encoding.Length ? length : throw new InvalidFontFileException("The CFF encoding is truncated.");
    }

    // The reader of the font has checked that the FDSelect fits and has format 0 or 3.
    private static int GetFdSelectLength(ReadOnlySpan<byte> fdSelect, int glyphCount)
        => fdSelect[0] == 0 ? 1 + glyphCount : 3 + (3 * BinaryPrimitives.ReadUInt16BigEndian(fdSelect.Slice(1))) + 2;

    private static int ToOffset(double value, int length)
        => value >= 0 && value <= length ? (int)value : throw new InvalidFontFileException("A CFF offset is out of range.");

    /// <summary>
    /// A Font DICT and the Private DICT that it names.
    /// </summary>
    /// <param name="Dict">The Font DICT, or an empty memory for the Top DICT of a font without an FDArray.</param>
    /// <param name="Private">The Private DICT, or an empty memory when the font has none.</param>
    internal readonly record struct FontDictMap(ReadOnlyMemory<byte> Dict, ReadOnlyMemory<byte> Private)
    {
        /// <summary>
        /// Gets a value indicating whether the Private DICT names local subroutines.
        /// </summary>
        public bool HasSubrs => !this.Private.IsEmpty && CffDictRewriter.HasSubrs(this.Private.Span);
    }
}
