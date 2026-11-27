// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Questions about where a line may break that a <see cref="LineBreakEnumerator"/> over one string cannot answer.
/// </summary>
public static class LineBreakAlgorithm
{
    // The text of a question that fits on the stack, in UTF-16 code units. The window of 32 code units each side that
    // the remarks of IsBreakAllowedBetween suggest is a quarter of it.
    private const int StackLimit = 256;

    /// <summary>
    /// Gets a value indicating whether a line may be broken where <paramref name="before"/> ends and
    /// <paramref name="after"/> begins, in the text that the two make one after the other.
    /// </summary>
    /// <param name="before">The text before the boundary.</param>
    /// <param name="after">The text after the boundary.</param>
    /// <param name="tailoring">The changes to make to the line break classes. The default is none, the algorithm as published.</param>
    /// <returns>
    /// <see langword="true"/> when a line may be broken at the boundary, or must be; <see langword="false"/> when it may not.
    /// </returns>
    /// <remarks>
    /// <para>
    /// The answer is the one a <see cref="LineBreakEnumerator"/> gives at that position of <paramref name="before"/>
    /// followed by <paramref name="after"/>, so every rule that reaches across the boundary applies: LB9 and LB10 for
    /// a combining mark at the start of <paramref name="after"/>, LB20a for a hyphen before it, LB25 for a number that
    /// goes on, LB30 for a bracket, LB30a for a flag, and the rest. It is the same code, not a second copy of the
    /// rules. The two spans are copied into one buffer, which is on the stack for a short text and rented otherwise.
    /// </para>
    /// <para>
    /// The two spans are all the text there is. Where <paramref name="before"/> starts is the start of the text
    /// (LB2), so an empty <paramref name="before"/> never gives a break, and where <paramref name="after"/> ends is
    /// the end (LB3), so an empty <paramref name="after"/> after text always does. A boundary inside a surrogate pair
    /// is never a break.
    /// </para>
    /// <para>
    /// Pass as much of the text beside the boundary as can change the answer. The rules read through rows of
    /// characters: spaces (LB14 to LB18), a number with its separators (LB25), combining marks (LB9) and regional
    /// indicators (LB30a). A row that is longer than the text passed looks as if it began or ended there, so a window
    /// of 32 code units each side is enough, except where a row of more than that runs across the boundary. Passing
    /// the whole paragraph is correct and costs time in proportion to its length.
    /// </para>
    /// </remarks>
    public static bool IsBreakAllowedBetween(
        ReadOnlySpan<char> before,
        ReadOnlySpan<char> after,
        LineBreakTailoring tailoring = LineBreakTailoring.None)
    {
        // LB2 and LB3.
        if (before.IsEmpty)
        {
            return false;
        }

        if (after.IsEmpty)
        {
            return true;
        }

        if (char.IsHighSurrogate(before[^1]) && char.IsLowSurrogate(after[0]))
        {
            return false;
        }

        int length = before.Length + after.Length;
        char[]? rented = null;
        Span<char> text = length <= StackLimit
            ? stackalloc char[length]
            : (rented = ArrayPool<char>.Shared.Rent(length)).AsSpan(0, length);
        try
        {
            before.CopyTo(text);
            after.CopyTo(text[before.Length..]);

            // The enumerator reports each opportunity by the number of code points before it.
            int boundary = CodePoint.GetCodePointCount(before);
            var enumerator = new LineBreakEnumerator(text, tailoring);
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.PositionWrap >= boundary)
                {
                    return enumerator.Current.PositionWrap == boundary;
                }
            }

            return false;
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
