// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// Finds and decrypts the eexec section of a Type 1 font program, and decrypts charstrings.
/// </summary>
internal static class Type1Eexec
{
    private const ushort EexecKey = 55665;
    private const ushort CharStringKey = 4330;
    private const ushort C1 = 52845;
    private const ushort C2 = 22719;

    // The eexec section starts with this many random bytes, which carry no data.
    private const int EexecPrefixLength = 4;
    private const int TrailerZeroCount = 512;

    private const byte PfbMarker = 0x80;
    private const byte PfbAscii = 1;
    private const byte PfbBinary = 2;
    private const int PfbSegmentHeaderLength = 6;

    public static bool IsPfb(ReadOnlySpan<byte> data)
        => data.Length >= PfbSegmentHeaderLength && data[0] == PfbMarker && data[1] == PfbAscii;

    /// <summary>
    /// Gets the cleartext that precedes the binary segments of a PFB file.
    /// </summary>
    /// <param name="pfb">The PFB file.</param>
    /// <param name="binaryPosition">The position of the first segment that is not ASCII.</param>
    /// <returns>The cleartext. It is a copy only when the file splits it over several segments.</returns>
    public static ReadOnlySpan<byte> GetPfbClearText(ReadOnlySpan<byte> pfb, out int binaryPosition)
    {
        int position = 0;
        int total = 0;
        int count = 0;
        ReadOnlySpan<byte> first = default;
        while (TryReadPfbSegment(pfb, PfbAscii, ref position, out ReadOnlySpan<byte> segment))
        {
            if (count++ == 0)
            {
                first = segment;
            }

            total += segment.Length;
        }

        binaryPosition = position;
        if (count <= 1)
        {
            return first;
        }

        byte[] joined = new byte[total];
        position = 0;
        int written = 0;
        while (TryReadPfbSegment(pfb, PfbAscii, ref position, out ReadOnlySpan<byte> segment))
        {
            segment.CopyTo(joined.AsSpan(written));
            written += segment.Length;
        }

        return joined;
    }

    /// <summary>
    /// Decrypts the run of binary segments of a PFB file.
    /// </summary>
    /// <param name="pfb">The PFB file.</param>
    /// <param name="binaryPosition">The position of the first binary segment.</param>
    /// <param name="budget">The budget that the decrypted private part is charged to.</param>
    /// <returns>The decrypted private part, or <see langword="null"/> when the file has no binary segment.</returns>
    public static byte[]? DecryptPfbBinary(ReadOnlySpan<byte> pfb, int binaryPosition, ref LoadBudget budget)
    {
        int position = binaryPosition;
        int total = 0;
        while (TryReadPfbSegment(pfb, PfbBinary, ref position, out ReadOnlySpan<byte> segment))
        {
            total += segment.Length;
        }

        if (total == 0)
        {
            return null;
        }

        byte[] plain = AllocatePlain(total, ref budget);
        ushort r = EexecKey;
        int written = -EexecPrefixLength;
        position = binaryPosition;
        while (TryReadPfbSegment(pfb, PfbBinary, ref position, out ReadOnlySpan<byte> segment))
        {
            foreach (byte cipher in segment)
            {
                Decrypt(cipher, plain, ref r, ref written);
            }
        }

        return plain;
    }

    /// <summary>
    /// Decrypts an eexec section that is not in PFB binary segments.
    /// </summary>
    /// <param name="section">The data that follows the <c>eexec</c> operator, to the end of the file.</param>
    /// <param name="budget">The budget that the decrypted private part is charged to.</param>
    /// <returns>The decrypted private part.</returns>
    public static byte[] DecryptSection(ReadOnlySpan<byte> section, ref LoadBudget budget)
    {
        int start = 0;
        while (start < section.Length && Type1Tokenizer.IsWhiteSpace(section[start]))
        {
            start++;
        }

        section = section.Slice(start);
        section = section.Slice(0, FindTrailer(section));

        ushort r = EexecKey;
        int written = -EexecPrefixLength;
        if (!IsHex(section))
        {
            byte[] binary = AllocatePlain(section.Length, ref budget);
            foreach (byte cipher in section)
            {
                Decrypt(cipher, binary, ref r, ref written);
            }

            return binary;
        }

        byte[] plain = AllocatePlain(CountHexDigits(section) / 2, ref budget);
        int high = -1;
        foreach (byte value in section)
        {
            int digit = HexValue(value);
            if (digit < 0)
            {
                if (Type1Tokenizer.IsWhiteSpace(value))
                {
                    continue;
                }

                break;
            }

            if (high < 0)
            {
                high = digit;
            }
            else
            {
                Decrypt((byte)((high << 4) | digit), plain, ref r, ref written);
                high = -1;
            }
        }

        return plain;
    }

    /// <summary>
    /// Decrypts a charstring in place. The first <c>lenIV</c> bytes of the result carry no data.
    /// </summary>
    /// <param name="data">The encrypted charstring.</param>
    public static void DecryptCharString(Span<byte> data)
    {
        ushort r = CharStringKey;
        for (int i = 0; i < data.Length; i++)
        {
            byte cipher = data[i];
            data[i] = (byte)(cipher ^ (r >> 8));
            r = (ushort)(((cipher + r) * C1) + C2);
        }
    }

    private static void Decrypt(byte cipher, byte[] plain, ref ushort r, ref int written)
    {
        if (written >= 0)
        {
            plain[written] = (byte)(cipher ^ (r >> 8));
        }

        written++;
        r = (ushort)(((cipher + r) * C1) + C2);
    }

    private static byte[] AllocatePlain(int cipherLength, ref LoadBudget budget)
    {
        if (cipherLength <= EexecPrefixLength)
        {
            throw new InvalidFontFileException("The Type 1 font program has an empty eexec section.");
        }

        return budget.Allocate<byte>(cipherLength - EexecPrefixLength);
    }

    private static bool TryReadPfbSegment(ReadOnlySpan<byte> pfb, byte kind, scoped ref int position, out ReadOnlySpan<byte> segment)
    {
        segment = default;
        if (position > pfb.Length - PfbSegmentHeaderLength || pfb[position] != PfbMarker || pfb[position + 1] != kind)
        {
            return false;
        }

        uint length = (uint)(pfb[position + 2] | (pfb[position + 3] << 8) | (pfb[position + 4] << 16) | (pfb[position + 5] << 24));
        int start = position + PfbSegmentHeaderLength;

        // A truncated file ends inside its last segment.
        int available = (int)Math.Min(length, (uint)(pfb.Length - start));
        segment = pfb.Slice(start, available);
        position = start + available;
        return true;
    }

    /// <summary>
    /// Finds where the encrypted data ends: before the trailer of up to 512 zeros and <c>cleartomark</c>.
    /// </summary>
    private static int FindTrailer(ReadOnlySpan<byte> section)
    {
        int end = section.LastIndexOf("cleartomark"u8);
        if (end < 0)
        {
            return section.Length;
        }

        int zeros = 0;
        while (end > 0 && zeros < TrailerZeroCount)
        {
            byte value = section[end - 1];
            if (value == (byte)'0')
            {
                zeros++;
            }
            else if (!Type1Tokenizer.IsWhiteSpace(value))
            {
                break;
            }

            end--;
        }

        return end;
    }

    // The Type 1 format guarantees that binary eexec data has a byte that is not a hexadecimal digit among its first four.
    private static bool IsHex(ReadOnlySpan<byte> section)
    {
        if (section.Length < EexecPrefixLength)
        {
            return false;
        }

        for (int i = 0; i < EexecPrefixLength; i++)
        {
            if (HexValue(section[i]) < 0)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountHexDigits(ReadOnlySpan<byte> section)
    {
        int count = 0;
        foreach (byte value in section)
        {
            if (HexValue(value) >= 0)
            {
                count++;
            }
            else if (!Type1Tokenizer.IsWhiteSpace(value))
            {
                break;
            }
        }

        return count;
    }

    private static int HexValue(byte value)
        => value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - '0',
            >= (byte)'a' and <= (byte)'f' => value - 'a' + 10,
            >= (byte)'A' and <= (byte)'F' => value - 'A' + 10,
            _ => -1,
        };
}
