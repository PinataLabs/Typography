// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Numerics;
using PinataLabs.Typography.FontPrograms;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// Parses a Compact Font Format (CFF) font program as described in The Compact Font Format specification (Adobe Technical Note #5176).
/// It reads the first font of the table, and it keeps the table bytes instead of copying the glyph data.
/// </summary>
internal static class CffParser
{
    // Top DICT operators.
    private const int OpCharset = 15;
    private const int OpEncoding = 16;
    private const int OpCharStrings = 17;
    private const int OpPrivate = 18;
    private const int OpCharstringType = 0x0C06;
    private const int OpFontMatrix = 0x0C07;
    private const int OpRos = 0x0C1E;
    private const int OpFdArray = 0x0C24;
    private const int OpFdSelect = 0x0C25;

    // Private DICT operators.
    private const int OpSubrs = 19;
    private const int OpDefaultWidthX = 20;
    private const int OpNominalWidthX = 21;

    /// <summary>
    /// Parses a CFF table.
    /// </summary>
    /// <param name="data">The bytes of the table. The font keeps them.</param>
    /// <returns>The <see cref="CffFont"/>.</returns>
    /// <exception cref="InvalidFontFileException">The table is malformed or not supported.</exception>
    public static CffFont Load(ReadOnlyMemory<byte> data)
    {
        LoadBudget budget = new(0, null);
        return Load(data, ref budget);
    }

    /// <summary>
    /// Parses a CFF table. Each array that the font keeps is charged to the budget before it is allocated.
    /// </summary>
    /// <param name="data">The bytes of the table. The font keeps them.</param>
    /// <param name="budget">The budget of the load.</param>
    /// <returns>The <see cref="CffFont"/>.</returns>
    /// <exception cref="InvalidFontFileException">The table is malformed or not supported.</exception>
    /// <exception cref="FontProgramTooLargeException">The font does not fit under the cap of the budget.</exception>
    public static CffFont Load(ReadOnlyMemory<byte> data, ref LoadBudget budget)
    {
        ReadOnlySpan<byte> span = data.Span;
        if (span.Length < 4 || span[0] != 1)
        {
            throw new InvalidFontFileException("The CFF table is not a version 1 table.");
        }

        // Header, Name INDEX, Top DICT INDEX, String INDEX, Global Subr INDEX.
        CffIndex.Read(data, span[2], 2, out int position);
        CffIndex topDicts = CffIndex.Read(data, position, 2, out position);
        CffIndex strings = CffIndex.Read(data, position, 2, out position);
        CffIndex globalSubrs = CffIndex.Read(data, position, 2, out _);

        if (!topDicts.TryGet(0, out ReadOnlyMemory<byte> topDict))
        {
            throw new InvalidFontFileException("The CFF table has no Top DICT.");
        }

        Matrix3x2? topMatrix = null;
        int charsetOffset = 0;
        int encodingOffset = 0;
        bool isCidKeyed = false;
        int charStringsOffset = 0;
        int privateSize = 0;
        int privateOffset = 0;
        int fdArrayOffset = 0;
        int fdSelectOffset = 0;

        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(topDict.Span);
        while (reader.TryRead(operands, out int count, out int op))
        {
            switch (op)
            {
                case OpFontMatrix when count >= 6:
                    topMatrix = ToMatrix(operands);
                    break;
                case OpCharset when count >= 1:
                    charsetOffset = ToOffset(operands[0], span.Length);
                    break;
                case OpEncoding when count >= 1:
                    encodingOffset = ToOffset(operands[0], span.Length);
                    break;
                case OpRos:
                    isCidKeyed = true;
                    break;
                case OpCharStrings when count >= 1:
                    charStringsOffset = ToOffset(operands[0], span.Length);
                    break;
                case OpPrivate when count >= 2:
                    privateSize = ToOffset(operands[0], span.Length);
                    privateOffset = ToOffset(operands[1], span.Length);
                    break;
                case OpFdArray when count >= 1:
                    fdArrayOffset = ToOffset(operands[0], span.Length);
                    break;
                case OpFdSelect when count >= 1:
                    fdSelectOffset = ToOffset(operands[0], span.Length);
                    break;
                case OpCharstringType when count >= 1 && operands[0] != 2:
                    throw new InvalidFontFileException("Only Type 2 charstrings are supported in a CFF table.");
            }
        }

        CffIndex charStrings = charStringsOffset == 0 ? default : CffIndex.Read(data, charStringsOffset, 2, out _);
        if (charStrings.Count == 0)
        {
            throw new InvalidFontFileException("The CFF table has no glyph data.");
        }

        // The default FontMatrix scales 1000 units to the em.
        Matrix3x2 fontMatrix = topMatrix ?? Matrix3x2.CreateScale(0.001F);

        // A font that is not CID-keyed has one Font DICT: its Top DICT.
        CffIndex fdArray = fdArrayOffset == 0 ? default : CffIndex.Read(data, fdArrayOffset, 2, out _);

        // Both counts are known before either array exists, so a refusal reports the whole size.
        int fontDictCount = fdArrayOffset == 0 ? 1 : fdArray.Count;
        budget.Reserve(LoadBudget.SizeOf<CffFontDict>(fontDictCount) + LoadBudget.SizeOf<ushort>(charStrings.Count));
        CffFontDict[] fontDicts = budget.Allocate<CffFontDict>(fontDictCount);
        ushort[] charset = budget.Allocate<ushort>(charStrings.Count);
        if (fdArrayOffset == 0)
        {
            fontDicts[0] = privateSize == 0 ? default : ReadPrivateDict(data, privateOffset, privateSize, null, null);
        }
        else
        {
            for (int i = 0; i < fontDicts.Length; i++)
            {
                if (!fdArray.TryGet(i, out ReadOnlyMemory<byte> fontDict))
                {
                    throw new InvalidFontFileException("The CFF FDArray is malformed.");
                }

                fontDicts[i] = ReadFontDict(data, fontDict.Span, topMatrix, fontMatrix);
            }
        }

        if (fdSelectOffset != 0)
        {
            CffFont.ValidateFdSelect(span, fdSelectOffset, charStrings.Count);
        }

        ReadCharset(span, charsetOffset, charset, isCidKeyed);

        return new CffFont(data, strings, globalSubrs, charStrings, charset, isCidKeyed, fontMatrix, encodingOffset, fontDicts, fdSelectOffset);
    }

    /// <summary>
    /// Reads the charset into the array: for each glyph its SID, or its CID in a CID-keyed font. Glyph 0 is always 0.
    /// </summary>
    private static void ReadCharset(ReadOnlySpan<byte> data, int offset, ushort[] charset, bool isCidKeyed)
    {
        int glyphCount = charset.Length;
        if (offset <= 2)
        {
            // Offsets 0, 1 and 2 name the predefined charsets. They do not apply to a CID-keyed font,
            // which then maps each CID to the glyph with the same ID.
            ReadOnlySpan<ushort> predefined = offset switch
            {
                1 when !isCidKeyed => CffPredefinedTables.ExpertCharset,
                2 when !isCidKeyed => CffPredefinedTables.ExpertSubsetCharset,
                _ => default,
            };

            int identity = isCidKeyed ? glyphCount : offset == 0 ? CffPredefinedTables.IsoAdobeCharsetLength : 0;
            for (int i = 0; i < glyphCount; i++)
            {
                charset[i] = i < predefined.Length ? predefined[i] : i < identity ? (ushort)i : (ushort)0;
            }

            return;
        }

        ReadOnlySpan<byte> table = data.Slice(offset);
        if (table.IsEmpty)
        {
            throw new InvalidFontFileException("The CFF charset is out of range.");
        }

        int format = table[0];
        int position = 1;
        int glyph = 1;
        switch (format)
        {
            case 0:
                if (table.Length < 1 + (2L * (glyphCount - 1)))
                {
                    throw new InvalidFontFileException("The CFF charset is truncated.");
                }

                for (; glyph < glyphCount; glyph++, position += 2)
                {
                    charset[glyph] = BinaryPrimitives.ReadUInt16BigEndian(table.Slice(position));
                }

                break;

            case 1:
            case 2:
                // Ranges of consecutive values: the first value, then the number of values that follow it.
                int rangeSize = format == 1 ? 3 : 4;
                while (glyph < glyphCount)
                {
                    if (position + rangeSize > table.Length)
                    {
                        throw new InvalidFontFileException("The CFF charset is truncated.");
                    }

                    int first = BinaryPrimitives.ReadUInt16BigEndian(table.Slice(position));
                    int left = format == 1 ? table[position + 2] : BinaryPrimitives.ReadUInt16BigEndian(table.Slice(position + 2));
                    position += rangeSize;
                    for (int i = 0; i <= left && glyph < glyphCount; i++)
                    {
                        charset[glyph++] = (ushort)(first + i);
                    }
                }

                break;

            default:
                throw new InvalidFontFileException($"CFF charset format {format} is not supported.");
        }
    }

    private static CffFontDict ReadFontDict(ReadOnlyMemory<byte> data, ReadOnlySpan<byte> fontDict, Matrix3x2? topMatrix, Matrix3x2 fontMatrix)
    {
        int privateSize = 0;
        int privateOffset = 0;
        Matrix3x2? glyphMatrix = null;
        Matrix3x2? transform = null;

        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(fontDict);
        while (reader.TryRead(operands, out int count, out int op))
        {
            if (op == OpPrivate && count >= 2)
            {
                privateSize = ToOffset(operands[0], data.Length);
                privateOffset = ToOffset(operands[1], data.Length);
            }
            else if (op == OpFontMatrix && count >= 6)
            {
                // The matrix of a Font DICT applies first. The Top DICT matrix follows when the font has one.
                // The font reports one matrix for every glyph, so keep what is left after that matrix is removed.
                Matrix3x2 matrix = ToMatrix(operands);
                if (topMatrix is Matrix3x2 top)
                {
                    matrix *= top;
                }

                glyphMatrix = matrix;
                if (matrix != fontMatrix && Matrix3x2.Invert(fontMatrix, out Matrix3x2 inverse))
                {
                    transform = matrix * inverse;
                }
            }
        }

        return privateSize == 0
            ? new CffFontDict(default, 0, 0, glyphMatrix, transform)
            : ReadPrivateDict(data, privateOffset, privateSize, glyphMatrix, transform);
    }

    private static CffFontDict ReadPrivateDict(ReadOnlyMemory<byte> data, int offset, int size, Matrix3x2? glyphMatrix, Matrix3x2? transform)
    {
        if ((long)offset + size > data.Length)
        {
            throw new InvalidFontFileException("A CFF Private DICT is out of range.");
        }

        CffIndex localSubrs = default;
        float defaultWidthX = 0;
        float nominalWidthX = 0;

        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(data.Span.Slice(offset, size));
        while (reader.TryRead(operands, out int count, out int op))
        {
            if (count < 1)
            {
                continue;
            }

            switch (op)
            {
                case OpSubrs:
                    // The offset is relative to the start of the Private DICT.
                    localSubrs = CffIndex.Read(data, offset + ToOffset(operands[0], data.Length), 2, out _);
                    break;
                case OpDefaultWidthX:
                    defaultWidthX = (float)operands[0];
                    break;
                case OpNominalWidthX:
                    nominalWidthX = (float)operands[0];
                    break;
            }
        }

        return new CffFontDict(localSubrs, defaultWidthX, nominalWidthX, glyphMatrix, transform);
    }

    private static Matrix3x2 ToMatrix(ReadOnlySpan<double> operands)
        => new(
            (float)operands[0],
            (float)operands[1],
            (float)operands[2],
            (float)operands[3],
            (float)operands[4],
            (float)operands[5]);

    private static int ToOffset(double value, int length)
    {
        if (!(value >= 0 && value <= length))
        {
            throw new InvalidFontFileException("A CFF offset is out of range.");
        }

        return (int)value;
    }
}
