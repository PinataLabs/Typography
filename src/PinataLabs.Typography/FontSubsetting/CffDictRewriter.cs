// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// Copies a CFF DICT with new values for the operators that hold an offset into the table.
/// Each new value takes 5 bytes whatever it is, so the length of the DICT does not depend on the offsets,
/// and the offsets can be found with one pass over the table.
/// </summary>
internal static class CffDictRewriter
{
    // The operators of Adobe Technical Note #5176, Table 9 and Table 23.
    private const int Charset = 15;
    private const int Encoding = 16;
    private const int CharStrings = 17;
    private const int Private = 18;
    private const int Subrs = 19;
    private const int FdArray = 0x0C24;
    private const int FdSelect = 0x0C25;

    /// <summary>
    /// The bytes that one operand takes: the marker 29 and a 32-bit number.
    /// </summary>
    private const int OperandSize = 5;

    /// <summary>
    /// Copies a DICT, and replaces the operands of the operators that hold an offset.
    /// An operator that the reader of the font ignores for lack of operands is copied as it is.
    /// </summary>
    /// <param name="source">The DICT.</param>
    /// <param name="kind">The kind of the DICT.</param>
    /// <param name="offsets">The new values.</param>
    /// <param name="destination">Receives the new DICT, or is empty to measure it.</param>
    /// <returns>The length of the new DICT.</returns>
    /// <exception cref="InvalidFontFileException">The DICT is malformed.</exception>
    public static int Rewrite(ReadOnlySpan<byte> source, CffDictKind kind, in CffDictOffsets offsets, Span<byte> destination)
    {
        bool write = !destination.IsEmpty;
        int length = 0;
        int entryStart = 0;
        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(source);
        while (reader.TryRead(operands, out int count, out int op))
        {
            int entryEnd = reader.Position;
            if (TryGetReplacement(kind, op, count, in offsets, out int first, out int second, out int operandCount))
            {
                length += WriteOperand(write ? destination.Slice(length) : default, first);
                if (operandCount == 2)
                {
                    length += WriteOperand(write ? destination.Slice(length) : default, second);
                }

                if (write)
                {
                    if (op >= 0x0C00)
                    {
                        destination[length] = 12;
                        destination[length + 1] = (byte)op;
                    }
                    else
                    {
                        destination[length] = (byte)op;
                    }
                }

                length += op >= 0x0C00 ? 2 : 1;
            }
            else
            {
                if (write)
                {
                    source.Slice(entryStart, entryEnd - entryStart).CopyTo(destination.Slice(length));
                }

                length += entryEnd - entryStart;
            }

            entryStart = entryEnd;
        }

        return length;
    }

    /// <summary>
    /// Finds the operand of the Subrs operator of a Private DICT: the offset of the local Subrs INDEX from the start of the DICT.
    /// The reader of the font takes the last Subrs operator that has an operand.
    /// </summary>
    /// <param name="privateDict">The Private DICT.</param>
    /// <param name="offset">Receives the operand.</param>
    /// <returns><see langword="true"/> if the DICT names local subroutines.</returns>
    /// <exception cref="InvalidFontFileException">The DICT is malformed.</exception>
    public static bool TryGetSubrsOffset(ReadOnlySpan<byte> privateDict, out double offset)
    {
        offset = 0;
        bool found = false;
        Span<double> operands = stackalloc double[CffDictReader.MaxOperands];
        CffDictReader reader = new(privateDict);
        while (reader.TryRead(operands, out int count, out int op))
        {
            if (op == Subrs && count >= 1)
            {
                offset = operands[0];
                found = true;
            }
        }

        return found;
    }

    private static bool TryGetReplacement(CffDictKind kind, int op, int count, in CffDictOffsets offsets, out int first, out int second, out int operandCount)
    {
        first = 0;
        second = 0;
        operandCount = 1;
        switch (kind)
        {
            case CffDictKind.Top when op == Charset && count >= 1:
                first = offsets.Charset;
                return true;
            case CffDictKind.Top when op == Encoding && count >= 1:
                first = offsets.Encoding;
                return true;
            case CffDictKind.Top when op == CharStrings && count >= 1:
                first = offsets.CharStrings;
                return true;
            case CffDictKind.Top when op == FdArray && count >= 1:
                first = offsets.FdArray;
                return true;
            case CffDictKind.Top when op == FdSelect && count >= 1:
                first = offsets.FdSelect;
                return true;
            case CffDictKind.Top or CffDictKind.FontDict when op == Private && count >= 2:
                first = offsets.PrivateSize;
                second = offsets.PrivateOffset;
                operandCount = 2;
                return true;
            case CffDictKind.Private when op == Subrs && count >= 1:
                first = offsets.Subrs;
                return true;
            default:
                return false;
        }
    }

    private static int WriteOperand(Span<byte> destination, int value)
    {
        if (!destination.IsEmpty)
        {
            destination[0] = 29;
            BinaryPrimitives.WriteInt32BigEndian(destination.Slice(1), value);
        }

        return OperandSize;
    }
}
