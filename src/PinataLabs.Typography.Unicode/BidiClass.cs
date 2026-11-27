// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Represents the Unicode Bidi value of a given <see cref="CodePoint"/>.
/// <see href="https://unicode.org/reports/tr9/#Table"/>
/// </summary>
public readonly struct BidiClass
{
    private readonly uint bidiValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="BidiClass"/> struct.
    /// </summary>
    /// <param name="codePoint">The codepoint.</param>
    public BidiClass(CodePoint codePoint)
        => this.bidiValue = UnicodeData.GetBidiData((uint)codePoint.Value);

    /// <summary>
    /// Gets the Unicode Bidirectional character type.
    /// </summary>
    public BidiCharacterType CharacterType
        => (BidiCharacterType)(this.bidiValue >> 24);

    /// <summary>
    /// Gets the Unicode Bidirectional paired bracket type.
    /// </summary>
    public BidiPairedBracketType PairedBracketType
        => (BidiPairedBracketType)((this.bidiValue >> 16) & 0xFF);

    /// <summary>
    /// Gets the canonical code point of the closing bracket of the pair that this paired bracket is half of.
    /// An opening bracket and its closing bracket give the same key, so the key matches them.
    /// </summary>
    /// <param name="codePoint">The code point that this instance is the bidi class of.</param>
    /// <returns>The key.</returns>
    internal int GetBracketPairKey(CodePoint codePoint)
    {
        if (this.PairedBracketType == BidiPairedBracketType.Open)
        {
            codePoint = new CodePoint(this.bidiValue & 0xFFFF);
        }

        return CodePoint.GetCanonicalType(codePoint).Value;
    }

    /// <summary>
    /// Gets the codepoint representing the bracket pairing for this instance.
    /// </summary>
    /// <param name="codePoint">
    /// When this method returns, contains the codepoint representing the bracket pairing for this instance;
    /// otherwise, the default value for the type of the <paramref name="codePoint"/> parameter.
    /// This parameter is passed uninitialized.
    /// .</param>
    /// <returns><see langword="true"/> if this instance has a bracket pairing; otherwise, <see langword="false"/></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetPairedBracket(out CodePoint codePoint)
    {
        if (this.PairedBracketType == BidiPairedBracketType.None)
        {
            codePoint = default;
            return false;
        }

        codePoint = new CodePoint(this.bidiValue & 0xFFFF);
        return true;
    }
}
