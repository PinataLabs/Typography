// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Text;

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// Splits the PostScript text of a Type 1 font program into tokens. It never throws for bad data.
/// </summary>
internal ref struct Type1Tokenizer
{
    private readonly ReadOnlySpan<byte> data;

    public Type1Tokenizer(ReadOnlySpan<byte> data)
    {
        this.data = data;
        this.Position = 0;
    }

    public int Position { get; set; }

    public static bool IsWhiteSpace(byte value)
        => value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or 0;

    public static bool TryParseInt(ReadOnlySpan<byte> text, out int value)
    {
        return Utf8Parser.TryParse(text, out value, out int consumed) && consumed == text.Length;
    }

    public static bool TryParseNumber(ReadOnlySpan<byte> text, out double value)
    {
        return Utf8Parser.TryParse(text, out value, out int consumed)
            && consumed == text.Length
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }

    public readonly ReadOnlySpan<byte> GetBytes(Type1Token token) => this.data.Slice(token.Start, token.Length);

    public readonly bool Is(Type1Token token, Type1TokenKind kind, ReadOnlySpan<byte> text)
        => token.Kind == kind && this.GetBytes(token).SequenceEqual(text);

    public bool TryRead(out Type1Token token)
    {
        this.SkipWhiteSpaceAndComments();
        if (this.Position >= this.data.Length)
        {
            token = default;
            return false;
        }

        int start = this.Position;
        byte first = this.data[start];
        switch (first)
        {
            case (byte)'/':
                this.Position++;

                // An immediately evaluated name, //name, reads as the name.
                if (this.Position < this.data.Length && this.data[this.Position] == (byte)'/')
                {
                    this.Position++;
                }

                start = this.Position;
                this.SkipRegularCharacters();
                token = new Type1Token(Type1TokenKind.Name, start, this.Position - start);
                return true;

            case (byte)'(':
                this.SkipString();
                token = new Type1Token(Type1TokenKind.String, start, this.Position - start);
                return true;

            case (byte)'<':
                if (this.NextIs((byte)'<'))
                {
                    this.Position += 2;
                    token = new Type1Token(Type1TokenKind.Word, start, 2);
                    return true;
                }

                int close = this.data.Slice(start).IndexOf((byte)'>');
                this.Position = close < 0 ? this.data.Length : start + close + 1;
                token = new Type1Token(Type1TokenKind.String, start, this.Position - start);
                return true;

            case (byte)'>':
                this.Position += this.NextIs((byte)'>') ? 2 : 1;
                token = new Type1Token(Type1TokenKind.Word, start, this.Position - start);
                return true;

            case (byte)'{':
                return this.ReadDelimiter(Type1TokenKind.ProcedureBegin, out token);

            case (byte)'}':
                return this.ReadDelimiter(Type1TokenKind.ProcedureEnd, out token);

            case (byte)'[':
                return this.ReadDelimiter(Type1TokenKind.ArrayBegin, out token);

            case (byte)']':
                return this.ReadDelimiter(Type1TokenKind.ArrayEnd, out token);

            case (byte)')':
                return this.ReadDelimiter(Type1TokenKind.Word, out token);

            default:
                this.SkipRegularCharacters();
                token = new Type1Token(Type1TokenKind.Word, start, this.Position - start);
                return true;
        }
    }

    /// <summary>
    /// Reads the next token as an integer. Leaves the position unchanged when it is not one.
    /// </summary>
    /// <param name="value">The integer.</param>
    /// <returns><see langword="true"/> if the next token is an integer.</returns>
    public bool TryReadInt(out int value)
    {
        int saved = this.Position;
        if (this.TryRead(out Type1Token token)
            && token.Kind == Type1TokenKind.Word
            && TryParseInt(this.GetBytes(token), out value))
        {
            return true;
        }

        this.Position = saved;
        value = 0;
        return false;
    }

    /// <summary>
    /// Skips to the end of a procedure whose opening brace has been read.
    /// </summary>
    public void SkipProcedure()
    {
        int depth = 1;
        while (depth > 0 && this.TryRead(out Type1Token token))
        {
            if (token.Kind == Type1TokenKind.ProcedureBegin)
            {
                depth++;
            }
            else if (token.Kind == Type1TokenKind.ProcedureEnd)
            {
                depth--;
            }
        }
    }

    /// <summary>
    /// Reads the binary data that follows an <c>RD</c> style token: one separator byte, then <paramref name="length"/> bytes.
    /// </summary>
    /// <param name="length">The number of data bytes.</param>
    /// <param name="start">The position of the first data byte.</param>
    /// <returns><see langword="true"/> if the data lies inside the buffer.</returns>
    public bool TryReadBinary(int length, out int start)
    {
        start = this.Position + 1;
        if (length < 0 || start > this.data.Length || length > this.data.Length - start)
        {
            return false;
        }

        this.Position = start + length;
        return true;
    }

    private static bool IsDelimiter(byte value)
        => value is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']'
            or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

    private readonly bool NextIs(byte value)
        => this.Position + 1 < this.data.Length && this.data[this.Position + 1] == value;

    private bool ReadDelimiter(Type1TokenKind kind, out Type1Token token)
    {
        token = new Type1Token(kind, this.Position, 1);
        this.Position++;
        return true;
    }

    private void SkipWhiteSpaceAndComments()
    {
        while (this.Position < this.data.Length)
        {
            byte value = this.data[this.Position];
            if (value == (byte)'%')
            {
                while (this.Position < this.data.Length && this.data[this.Position] is not ((byte)'\r' or (byte)'\n'))
                {
                    this.Position++;
                }
            }
            else if (IsWhiteSpace(value))
            {
                this.Position++;
            }
            else
            {
                return;
            }
        }
    }

    private void SkipRegularCharacters()
    {
        while (this.Position < this.data.Length
            && !IsWhiteSpace(this.data[this.Position])
            && !IsDelimiter(this.data[this.Position]))
        {
            this.Position++;
        }
    }

    private void SkipString()
    {
        int depth = 0;
        while (this.Position < this.data.Length)
        {
            byte value = this.data[this.Position++];
            if (value == (byte)'\\')
            {
                this.Position = Math.Min(this.Position + 1, this.data.Length);
            }
            else if (value == (byte)'(')
            {
                depth++;
            }
            else if (value == (byte)')' && --depth == 0)
            {
                return;
            }
        }
    }
}
