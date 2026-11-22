// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// The kinds of PostScript token that the Type 1 parser tells apart.
/// </summary>
internal enum Type1TokenKind
{
    /// <summary>A literal name. The token excludes the leading slash.</summary>
    Name,

    /// <summary>An executable name or a number.</summary>
    Word,

    /// <summary>A string, either parenthesised or hexadecimal.</summary>
    String,

    /// <summary>The <c>{</c> that opens a procedure.</summary>
    ProcedureBegin,

    /// <summary>The <c>}</c> that closes a procedure.</summary>
    ProcedureEnd,

    /// <summary>The <c>[</c> that opens an array.</summary>
    ArrayBegin,

    /// <summary>The <c>]</c> that closes an array.</summary>
    ArrayEnd,
}

/// <summary>
/// One PostScript token, as a range of the tokenized data.
/// </summary>
internal readonly struct Type1Token
{
    public Type1Token(Type1TokenKind kind, int start, int length)
    {
        this.Kind = kind;
        this.Start = start;
        this.Length = length;
    }

    public Type1TokenKind Kind { get; }

    public int Start { get; }

    public int Length { get; }
}
