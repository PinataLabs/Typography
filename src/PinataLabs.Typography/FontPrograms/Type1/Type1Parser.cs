// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// Reads the two parts of a Type 1 font program: the cleartext and the decrypted private part.
/// It runs no PostScript. It recognises the forms that font programs use in practice.
/// </summary>
internal static class Type1Parser
{
    private const int MaxGlyphCount = ushort.MaxValue + 1;

    public static void ParseHeader(ReadOnlySpan<byte> clearText, ref Type1Header header)
    {
        Type1Tokenizer tokenizer = new(clearText);
        bool hasFontMatrix = false;
        Span<double> numbers = stackalloc double[6];
        while (tokenizer.TryRead(out Type1Token token))
        {
            if (tokenizer.Is(token, Type1TokenKind.Word, "eexec"u8))
            {
                header.EexecEnd = tokenizer.Position;
                break;
            }

            if (token.Kind != Type1TokenKind.Name)
            {
                continue;
            }

            ReadOnlySpan<byte> key = tokenizer.GetBytes(token);
            if (key.SequenceEqual("FontMatrix"u8) && !hasFontMatrix)
            {
                if (!TryReadNumbers(ref tokenizer, numbers) || !FitSingle(numbers))
                {
                    throw new InvalidFontFileException("The Type 1 font program has a malformed FontMatrix.");
                }

                header.FontMatrix = new Matrix3x2(
                    (float)numbers[0],
                    (float)numbers[1],
                    (float)numbers[2],
                    (float)numbers[3],
                    (float)numbers[4],
                    (float)numbers[5]);
                hasFontMatrix = true;
            }
            else if (key.SequenceEqual("FontBBox"u8))
            {
                if (TryReadNumbers(ref tokenizer, numbers.Slice(0, 4)))
                {
                    header.FontBBox = new Bounds((float)numbers[0], (float)numbers[1], (float)numbers[2], (float)numbers[3]);
                }
            }
            else if (key.SequenceEqual("PaintType"u8))
            {
                if (tokenizer.TryReadInt(out int paintType))
                {
                    header.PaintType = paintType;
                }
            }
            else if (key.SequenceEqual("Encoding"u8) && header.EncodingKind == Type1EncodingKind.None)
            {
                ReadEncoding(ref tokenizer, ref header);
            }
        }
    }

    /// <summary>
    /// Reads <c>/lenIV</c>, <c>/Subrs</c> and <c>/CharStrings</c> from the decrypted private part, and decrypts
    /// the charstrings in place.
    /// </summary>
    /// <param name="data">The decrypted private part. The program takes ownership of it.</param>
    /// <param name="header">The cleartext values.</param>
    /// <param name="clearText">The cleartext that <paramref name="header"/> refers to.</param>
    /// <param name="budget">
    /// The budget that the arrays of the program are charged to. They are charged together, after every count is read,
    /// so a refusal carries the size that loads.
    /// </param>
    /// <returns>The font program.</returns>
    public static Type1Program ParsePrivate(byte[] data, in Type1Header header, ReadOnlySpan<byte> clearText, ref LoadBudget budget)
    {
        Type1Tokenizer tokenizer = new(data);
        int lenIV = 4;
        int subrsStart = -1;
        int subrCount = 0;
        int charStringsStart = -1;
        int entries = 0;
        int notdef = -1;
        while (tokenizer.TryRead(out Type1Token token))
        {
            if (token.Kind == Type1TokenKind.ProcedureBegin)
            {
                tokenizer.SkipProcedure();
                continue;
            }

            if (token.Kind != Type1TokenKind.Name)
            {
                continue;
            }

            ReadOnlySpan<byte> key = tokenizer.GetBytes(token);
            if (key.SequenceEqual("lenIV"u8))
            {
                if (tokenizer.TryReadInt(out int value))
                {
                    lenIV = value;
                }
            }
            else if (subrsStart < 0 && key.SequenceEqual("Subrs"u8) && tokenizer.TryReadInt(out int count))
            {
                // Every entry takes text, so a count above the data length is not a count.
                if (count < 0 || count > data.Length)
                {
                    throw new InvalidFontFileException("The Type 1 font program has a malformed Subrs array.");
                }

                subrCount = count;
                subrsStart = tokenizer.Position;
                ReadSubrs(ref tokenizer, subrCount, null);
            }
            else if (charStringsStart < 0 && key.SequenceEqual("CharStrings"u8) && tokenizer.TryReadInt(out _))
            {
                charStringsStart = tokenizer.Position;
                entries = CountCharStrings(ref tokenizer, out notdef);
            }
        }

        if (charStringsStart < 0)
        {
            throw new InvalidFontFileException("The Type 1 font program has no CharStrings dictionary.");
        }

        // Glyph 0 is .notdef. A font program without one gets an empty one.
        int glyphCount = notdef < 0 ? entries + 1 : entries;
        if (entries == 0 || glyphCount > MaxGlyphCount)
        {
            throw new InvalidFontFileException("The Type 1 font program has no glyphs, or too many.");
        }

        budget.Reserve(
            (2 * LoadBudget.SizeOf<int>(subrCount))
            + (3 * LoadBudget.SizeOf<int>(glyphCount))
            + LoadBudget.SizeOf<byte>(glyphCount)
            + (header.EncodingKind == Type1EncodingKind.None ? 0 : LoadBudget.SizeOf<ushort>(256)));

        Type1CharStringTable subrs = new(subrCount, ref budget);
        if (subrsStart >= 0)
        {
            tokenizer.Position = subrsStart;
            ReadSubrs(ref tokenizer, subrCount, subrs);
        }

        Type1CharStringTable glyphs = new(glyphCount, ref budget);
        int[] nameOffsets = budget.Allocate<int>(glyphCount);
        byte[] nameLengths = budget.Allocate<byte>(glyphCount);
        tokenizer.Position = charStringsStart;
        ReadCharStrings(ref tokenizer, entries, notdef, glyphs, nameOffsets, nameLengths);

        subrs.Decrypt(data, lenIV);
        glyphs.Decrypt(data, lenIV);
        return new Type1Program(data, in header, clearText, glyphs, nameOffsets, nameLengths, subrs, ref budget);
    }

    private static bool TryReadNumbers(ref Type1Tokenizer tokenizer, scoped Span<double> numbers)
    {
        if (!tokenizer.TryRead(out Type1Token open)
            || open.Kind is not (Type1TokenKind.ArrayBegin or Type1TokenKind.ProcedureBegin))
        {
            return false;
        }

        for (int i = 0; i < numbers.Length; i++)
        {
            if (!tokenizer.TryRead(out Type1Token token)
                || token.Kind != Type1TokenKind.Word
                || !Type1Tokenizer.TryParseNumber(tokenizer.GetBytes(token), out numbers[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool FitSingle(ReadOnlySpan<double> numbers)
    {
        foreach (double number in numbers)
        {
            if (!float.IsFinite((float)number))
            {
                return false;
            }
        }

        return true;
    }

    private static void ReadEncoding(ref Type1Tokenizer tokenizer, ref Type1Header header)
    {
        int saved = tokenizer.Position;
        if (!tokenizer.TryRead(out Type1Token token))
        {
            return;
        }

        if (tokenizer.Is(token, Type1TokenKind.Word, "StandardEncoding"u8))
        {
            header.EncodingKind = Type1EncodingKind.Standard;
            return;
        }

        if (token.Kind == Type1TokenKind.ArrayBegin)
        {
            header.EncodingKind = Type1EncodingKind.Custom;
            int code = 0;
            while (code < 256 && tokenizer.TryRead(out token) && token.Kind == Type1TokenKind.Name)
            {
                header.EncodingNameStarts[code] = token.Start;
                header.EncodingNameLengths[code] = token.Length;
                code++;
            }

            return;
        }

        if (token.Kind != Type1TokenKind.Word || !Type1Tokenizer.TryParseInt(tokenizer.GetBytes(token), out _))
        {
            tokenizer.Position = saved;
            return;
        }

        // "256 array", an optional loop that fills it with .notdef, then "dup code /name put" per code.
        header.EncodingKind = Type1EncodingKind.Custom;
        while (true)
        {
            saved = tokenizer.Position;
            if (!tokenizer.TryRead(out token))
            {
                return;
            }

            if (token.Kind == Type1TokenKind.ProcedureBegin)
            {
                tokenizer.SkipProcedure();
            }
            else if (tokenizer.Is(token, Type1TokenKind.Word, "def"u8))
            {
                return;
            }
            else if (tokenizer.Is(token, Type1TokenKind.Word, "eexec"u8))
            {
                tokenizer.Position = saved;
                return;
            }
            else if (tokenizer.Is(token, Type1TokenKind.Word, "dup"u8) && tokenizer.TryReadInt(out int code))
            {
                saved = tokenizer.Position;
                if (tokenizer.TryRead(out Type1Token name) && name.Kind == Type1TokenKind.Name)
                {
                    if ((uint)code < 256)
                    {
                        header.EncodingNameStarts[code] = name.Start;
                        header.EncodingNameLengths[code] = name.Length;
                    }
                }
                else
                {
                    tokenizer.Position = saved;
                }
            }
        }
    }

    // Without a table it only checks the entries and moves past them, so that the counts are known before any array exists.
    private static void ReadSubrs(ref Type1Tokenizer tokenizer, int count, Type1CharStringTable? subrs)
    {
        while (true)
        {
            int saved = tokenizer.Position;
            if (!tokenizer.TryRead(out Type1Token token))
            {
                break;
            }

            // The next key of the private dictionary ends the array, whatever the font calls its "ND".
            if (token.Kind == Type1TokenKind.Name)
            {
                tokenizer.Position = saved;
                break;
            }

            if (!tokenizer.Is(token, Type1TokenKind.Word, "dup"u8))
            {
                continue;
            }

            if (!tokenizer.TryReadInt(out int index) || !tokenizer.TryReadInt(out int length))
            {
                break;
            }

            if ((uint)index >= (uint)count || !TryReadBinary(ref tokenizer, length, out int start))
            {
                throw new InvalidFontFileException("The Type 1 font program has a malformed or truncated subroutine.");
            }

            subrs?.Set(index, start, length);
        }
    }

    // The declared dictionary size is not trusted: the arrays are sized from the entries the data holds.
    private static int CountCharStrings(ref Type1Tokenizer tokenizer, out int notdef)
    {
        int entries = 0;
        notdef = -1;
        while (TryReadCharString(ref tokenizer, out Type1Token name, out _, out _))
        {
            if (notdef < 0 && tokenizer.Is(name, Type1TokenKind.Name, ".notdef"u8))
            {
                notdef = entries;
            }

            entries++;
        }

        return entries;
    }

    private static void ReadCharStrings(
        ref Type1Tokenizer tokenizer,
        int entries,
        int notdef,
        Type1CharStringTable glyphs,
        int[] nameOffsets,
        byte[] nameLengths)
    {
        if (notdef < 0)
        {
            nameOffsets[0] = Type1Program.SynthesizedName;
        }

        for (int entry = 0; entry < entries; entry++)
        {
            TryReadCharString(ref tokenizer, out Type1Token name, out int start, out int length);
            if (name.Length > byte.MaxValue)
            {
                throw new InvalidFontFileException("The Type 1 font program has a glyph name that is too long.");
            }

            int glyphId = entry == notdef ? 0 : entry < notdef || notdef < 0 ? entry + 1 : entry;
            nameOffsets[glyphId] = name.Start;
            nameLengths[glyphId] = (byte)name.Length;
            glyphs.Set(glyphId, start, length);
        }
    }

    private static bool TryReadCharString(ref Type1Tokenizer tokenizer, out Type1Token name, out int start, out int length)
    {
        while (tokenizer.TryRead(out Type1Token token))
        {
            if (token.Kind == Type1TokenKind.ProcedureBegin)
            {
                tokenizer.SkipProcedure();
            }
            else if (tokenizer.Is(token, Type1TokenKind.Word, "end"u8))
            {
                name = default;
                start = 0;
                length = 0;
                return false;
            }
            else if (token.Kind == Type1TokenKind.Name && tokenizer.TryReadInt(out length))
            {
                if (!TryReadBinary(ref tokenizer, length, out start))
                {
                    throw new InvalidFontFileException("The Type 1 font program has a malformed or truncated charstring.");
                }

                name = token;
                return true;
            }
        }

        // Without this rule a font file cut between two charstrings would load with some of its glyphs.
        throw new InvalidFontFileException("The Type 1 font program has a truncated CharStrings dictionary.");
    }

    // "length RD data": the token that follows the length has a name the font chooses.
    private static bool TryReadBinary(ref Type1Tokenizer tokenizer, int length, out int start)
    {
        start = 0;
        return tokenizer.TryRead(out _) && tokenizer.TryReadBinary(length, out start);
    }
}
