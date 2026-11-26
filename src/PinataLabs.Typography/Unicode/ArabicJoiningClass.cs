// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Represents the Unicode Arabic Joining value of a given <see cref="CodePoint"/>.
/// <see href="https://www.unicode.org/versions/Unicode14.0.0/ch09.pdf"/>
/// </summary>
public readonly struct ArabicJoiningClass
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArabicJoiningClass"/> struct.
    /// </summary>
    /// <param name="codePoint">The codepoint.</param>
    public ArabicJoiningClass(CodePoint codePoint)
    {
        UnicodeCategory category = CodePoint.GetGeneralCategory(codePoint);
        uint value = UnicodeData.GetJoiningClass((uint)codePoint.Value);
        this.JoiningType = GetJoiningType(codePoint, value, category);
        this.JoiningGroup = (ArabicJoiningGroup)((value >> 16) & 0xFF);
    }

    /// <summary>
    /// Gets the Unicode joining type.
    /// </summary>
    public ArabicJoiningType JoiningType { get; }

    /// <summary>
    /// Gets the Unicode joining group.
    /// </summary>
    public ArabicJoiningGroup JoiningGroup { get; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ArabicJoiningType GetJoiningType(CodePoint codePoint, uint value, UnicodeCategory category)
    {
        var type = (ArabicJoiningType)(value & 0xFF);

        // All others not explicitly listed have joining type U
        if (type == ArabicJoiningType.NonJoining)
        {
            // The trie gives U both to the characters that ArabicShaping.txt lists as U and to those it does not list.
            // These are the characters of General Category Mn, Me or Cf that it lists as U, which stay U.
            if (codePoint.Value is (>= 0x0600 and <= 0x0605)
                or 0x06DD
                or 0x0890
                or 0x0891
                or 0x08E2
                or 0x180E
                or 0x200C
                or (>= 0x2066 and <= 0x2069)
                or 0x110BD
                or 0x110CD)
            {
                return type;
            }

            // Those that are not explicitly listed and that are of General Category Mn, Me, or Cf have joining type T.
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
            {
                type = ArabicJoiningType.Transparent;
            }
        }

        return type;
    }
}
