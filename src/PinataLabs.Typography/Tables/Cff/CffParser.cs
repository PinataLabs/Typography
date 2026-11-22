// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// Parses a Compact Font Format (CFF) font program as described in The Compact Font Format specification (Adobe Technical Note #5176).
/// It reads the first font of the table, and it keeps the table bytes instead of copying the glyph data.
/// </summary>
internal static class CffParser
{
    // Top DICT operators.
    private const int OpCharStrings = 17;
    private const int OpPrivate = 18;
    private const int OpCharstringType = 0x0C06;
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
        ReadOnlySpan<byte> span = data.Span;
        if (span.Length < 4 || span[0] != 1)
        {
            throw new InvalidFontFileException("The CFF table is not a version 1 table.");
        }

        // Header, Name INDEX, Top DICT INDEX, String INDEX, Global Subr INDEX.
        CffIndex.Read(data, span[2], 2, out int position);
        CffIndex topDicts = CffIndex.Read(data, position, 2, out position);
        CffIndex.Read(data, position, 2, out position);
        CffIndex globalSubrs = CffIndex.Read(data, position, 2, out _);

        if (!topDicts.TryGet(0, out ReadOnlyMemory<byte> topDict))
        {
            throw new InvalidFontFileException("The CFF table has no Top DICT.");
        }

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

        CffFontDict topPrivate = privateSize == 0 ? default : ReadPrivateDict(data, privateOffset, privateSize);

        CffFontDict[] fontDicts = [];
        if (fdArrayOffset != 0)
        {
            CffIndex fdArray = CffIndex.Read(data, fdArrayOffset, 2, out _);
            fontDicts = new CffFontDict[fdArray.Count];
            for (int i = 0; i < fontDicts.Length; i++)
            {
                if (!fdArray.TryGet(i, out ReadOnlyMemory<byte> fontDict))
                {
                    throw new InvalidFontFileException("The CFF FDArray is malformed.");
                }

                fontDicts[i] = ReadFontDict(data, fontDict.Span);
            }
        }

        if (fdSelectOffset != 0)
        {
            CffFont.ValidateFdSelect(span, fdSelectOffset, charStrings.Count);
        }

        return new CffFont(data, globalSubrs, charStrings, topPrivate, fontDicts, fdSelectOffset);
    }

    private static CffFontDict ReadFontDict(ReadOnlyMemory<byte> data, ReadOnlySpan<byte> fontDict)
    {
        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(fontDict);
        CffFontDict result = default;
        while (reader.TryRead(operands, out int count, out int op))
        {
            if (op == OpPrivate && count >= 2 && operands[0] != 0)
            {
                result = ReadPrivateDict(data, ToOffset(operands[1], data.Length), ToOffset(operands[0], data.Length));
            }
        }

        return result;
    }

    private static CffFontDict ReadPrivateDict(ReadOnlyMemory<byte> data, int offset, int size)
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

        return new CffFontDict(localSubrs, defaultWidthX, nominalWidthX);
    }

    private static int ToOffset(double value, int length)
    {
        if (!(value >= 0 && value <= length))
        {
            throw new InvalidFontFileException("A CFF offset is out of range.");
        }

        return (int)value;
    }
}
