// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// Cuts a paragraph into the runs that a shaper takes one at a time: each in one script and one direction, in the
/// order they are drawn.
/// </summary>
/// <remarks>
/// <para>
/// The bidi algorithm says which way each stretch of text runs, and script itemization says which script it is in. A
/// shaper needs both, so the runs are those of the bidi algorithm cut at each change of script.
/// </para>
/// <para>
/// A script is found inside each bidi run and not across the paragraph. A space takes the script of the text before
/// it, but before it in the run the space is drawn in. In a Latin word, a space and an Arabic word, the space goes
/// with the Latin when the paragraph is read as a whole, and the bidi algorithm then draws it inside the Arabic.
/// </para>
/// <para>
/// A bidi run that holds several scripts is one direction, so its pieces are drawn in that direction: when it runs
/// right to left, the piece written first is the rightmost.
/// </para>
/// </remarks>
public static class TextItemizer
{
    /// <summary>
    /// Gets the runs of a paragraph in visual order, leftmost first, with the paragraph as one line.
    /// </summary>
    /// <param name="text">The paragraph.</param>
    /// <param name="direction">The direction of the paragraph, or <see cref="BidiParagraphDirection.Auto"/> to read it from the text.</param>
    /// <returns>The runs, which cover all of the text.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a defined value.</exception>
    public static ItemizedRun[] Itemize(ReadOnlySpan<char> text, BidiParagraphDirection direction)
        => Itemize(BidiParagraph.Resolve(text, direction), 0, text.Length);

    /// <summary>
    /// Gets the runs of one line of a resolved paragraph in visual order, leftmost first.
    /// </summary>
    /// <remarks>
    /// The runs are those of <see cref="BidiParagraph.GetVisualRuns"/> for the line, so rule L1 applies to the line
    /// and the characters that rule X9 removes are in no run, except U+200C and U+200D.
    /// </remarks>
    /// <param name="paragraph">The paragraph.</param>
    /// <param name="lineStart">The index of the first code unit of the line.</param>
    /// <param name="lineLength">The number of code units in the line.</param>
    /// <returns>The runs of the line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="paragraph"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The line is not inside the text.</exception>
    public static ItemizedRun[] Itemize(BidiParagraph paragraph, int lineStart, int lineLength)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        BidiLevelRun[] levelRuns = paragraph.GetVisualRuns(lineStart, lineLength);
        var runs = new List<ItemizedRun>(levelRuns.Length);
        foreach (BidiLevelRun levelRun in levelRuns)
        {
            int first = runs.Count;
            foreach (ScriptRun scriptRun in ScriptItemizer.Itemize(paragraph.Text, levelRun.Start, levelRun.Length))
            {
                runs.Add(new ItemizedRun(scriptRun.Start, scriptRun.Length, scriptRun.Script, levelRun.Level));
            }

            if (levelRun.IsRightToLeft)
            {
                runs.Reverse(first, runs.Count - first);
            }
        }

        return [.. runs];
    }
}
