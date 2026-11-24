// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using PinataLabs.Typography.Unicode.Resources;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Maps a character to the two parts of its canonical decomposition, and two parts back to the character.
/// Hangul syllables are not in the tables. The Hangul shaper composes and decomposes them by arithmetic.
/// </summary>
internal static class CanonicalMapping
{
    // No character below U+00C0 has a canonical decomposition, and none composes with a second part below U+0300.
    private const uint FirstDecomposed = 0xC0;

    private const uint FirstSecondPart = 0x300;

    /// <summary>
    /// Gets the canonical decomposition of the character, one step deep.
    /// </summary>
    /// <param name="codePoint">The character.</param>
    /// <param name="first">The first part.</param>
    /// <param name="second">The second part, or 0 when the decomposition is a single character.</param>
    /// <returns><see langword="true"/> if the character has a canonical decomposition.</returns>
    public static bool TryDecompose(uint codePoint, out uint first, out uint second)
    {
        if (codePoint >= FirstDecomposed)
        {
            int index = CanonicalMappingData.Decomposed.BinarySearch(codePoint);
            if (index >= 0)
            {
                ReadOnlySpan<uint> parts = CanonicalMappingData.DecomposedParts;
                first = parts[index * 2];
                second = parts[(index * 2) + 1];
                return true;
            }
        }

        first = 0;
        second = 0;
        return false;
    }

    /// <summary>
    /// Gets the character that the two parts compose to.
    /// </summary>
    /// <param name="first">The first part, which is a starter.</param>
    /// <param name="second">The second part.</param>
    /// <param name="composed">The composed character.</param>
    /// <param name="excluded">
    /// Whether CompositionExclusions.txt lists the composed character. Unicode normalization does not compose such
    /// a pair, but a shaper can.
    /// </param>
    /// <returns><see langword="true"/> if a character decomposes to the two parts.</returns>
    public static bool TryCompose(uint first, uint second, out uint composed, out bool excluded)
    {
        if (second >= FirstSecondPart)
        {
            int index = CanonicalMappingData.ComposablePairs.BinarySearch(((ulong)first << 32) | second);
            if (index >= 0)
            {
                uint value = CanonicalMappingData.Composed[index];
                composed = value & ~CanonicalMappingData.ExcludedFromComposition;
                excluded = (value & CanonicalMappingData.ExcludedFromComposition) != 0;
                return true;
            }
        }

        composed = 0;
        excluded = false;
        return false;
    }
}
