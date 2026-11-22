// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// Reads the operators of a CFF DICT in order, each with its operands.
/// </summary>
internal ref struct CffDictReader
{
    /// <summary>
    /// The largest number of operands that an operator can have.
    /// </summary>
    public const int MaxOperands = 48;

    private readonly ReadOnlySpan<byte> data;
    private int position;

    public CffDictReader(ReadOnlySpan<byte> data)
    {
        this.data = data;
        this.position = 0;
    }

    /// <summary>
    /// Reads the next operator.
    /// </summary>
    /// <param name="operands">Receives the operands. It must hold <see cref="MaxOperands"/> values.</param>
    /// <param name="count">The number of operands.</param>
    /// <param name="op">The operator. A two-byte operator is <c>0x0C00 | second byte</c>.</param>
    /// <returns><see langword="false"/> at the end of the DICT.</returns>
    /// <exception cref="InvalidFontFileException">The DICT is malformed.</exception>
    public bool TryRead(scoped Span<double> operands, out int count, out int op)
    {
        count = 0;
        while (this.position < this.data.Length)
        {
            byte b0 = this.data[this.position++];
            double value;
            if (b0 <= 21)
            {
                op = b0 == 12 ? 0x0C00 | this.ReadByte() : b0;
                return true;
            }
            else if (b0 == 28)
            {
                value = (short)((this.ReadByte() << 8) | this.ReadByte());
            }
            else if (b0 == 29)
            {
                value = (this.ReadByte() << 24) | (this.ReadByte() << 16) | (this.ReadByte() << 8) | this.ReadByte();
            }
            else if (b0 == 30)
            {
                value = this.ReadReal();
            }
            else if (b0 is >= 32 and <= 246)
            {
                value = b0 - 139;
            }
            else if (b0 is >= 247 and <= 250)
            {
                value = ((b0 - 247) * 256) + this.ReadByte() + 108;
            }
            else if (b0 is >= 251 and <= 254)
            {
                value = (-(b0 - 251) * 256) - this.ReadByte() - 108;
            }
            else
            {
                throw new InvalidFontFileException($"Invalid CFF DICT byte {b0}.");
            }

            if (count == operands.Length)
            {
                throw new InvalidFontFileException("A CFF DICT operator has too many operands.");
            }

            operands[count++] = value;
        }

        op = -1;
        return false;
    }

    private static void Append(Span<char> text, ref int length, char c)
    {
        if (length < text.Length)
        {
            text[length++] = c;
        }
    }

    private byte ReadByte()
    {
        if (this.position >= this.data.Length)
        {
            throw new InvalidFontFileException("A CFF DICT is truncated.");
        }

        return this.data[this.position++];
    }

    private double ReadReal()
    {
        Span<char> text = stackalloc char[64];
        int length = 0;
        bool done = false;
        while (!done)
        {
            int b = this.ReadByte();
            for (int i = 0; i < 2 && !done; i++)
            {
                int nibble = i == 0 ? b >> 4 : b & 0xF;
                switch (nibble)
                {
                    case <= 9:
                        Append(text, ref length, (char)('0' + nibble));
                        break;
                    case 0xA:
                        Append(text, ref length, '.');
                        break;
                    case 0xB:
                        Append(text, ref length, 'E');
                        break;
                    case 0xC:
                        Append(text, ref length, 'E');
                        Append(text, ref length, '-');
                        break;
                    case 0xE:
                        Append(text, ref length, '-');
                        break;
                    case 0xF:
                        done = true;
                        break;
                }
            }
        }

        // Some fonts end a number at the exponent marker. Read the exponent as zero.
        if (length > 0 && text[length - 1] is 'E' or '-')
        {
            Append(text, ref length, '0');
        }

        return double.TryParse(text.Slice(0, length), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : 0;
    }
}
