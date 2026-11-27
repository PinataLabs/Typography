// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// A paragraph of text that the Unicode Bidirectional Algorithm (UAX #9) has resolved: the embedding level of each
/// code unit, and the runs of a line in the order they are drawn.
/// <see href="https://unicode.org/reports/tr9/"/>
/// </summary>
/// <remarks>
/// <para>
/// A paragraph holds a copy of the text, so the text passed to <see cref="Resolve"/> need not outlive the call.
/// All indices are UTF-16 code unit indices into that text. The two code units of a surrogate pair share a level.
/// </para>
/// <para>
/// Rule X9 removes explicit embedding and override controls and every character of class BN from the
/// ordering. They still have a level, which is that of the code unit before them (the paragraph level at the start
/// of the text), so that they do not split a run. U+200C ZERO WIDTH NON-JOINER and U+200D ZERO WIDTH JOINER are
/// the exception to the removal that a shaper needs: they decide how the letters on either side join, so they stay
/// inside the run around them. Every other removed character is in no run, and ends the run before it.
/// </para>
/// </remarks>
public sealed class BidiParagraph
{
    private readonly string text;
    private readonly BidiCharacterType[] types;
    private readonly sbyte[] resolvedLevels;
    private readonly byte[] levels;

    private BidiParagraph(string text, BidiCharacterType[] types, sbyte[] resolvedLevels, byte[] levels, byte paragraphLevel)
    {
        this.text = text;
        this.types = types;
        this.resolvedLevels = resolvedLevels;
        this.levels = levels;
        this.ParagraphLevel = paragraphLevel;
    }

    /// <summary>
    /// Gets the embedding level of the paragraph: even when it runs left to right, odd when it runs right to left.
    /// This is the level the caller asked for, or the level that rules P2 and P3 read from the text.
    /// </summary>
    public byte ParagraphLevel { get; }

    /// <summary>
    /// Gets the embedding level of each UTF-16 code unit of the text, in logical order, with rule L1 applied to the
    /// whole paragraph as one line. Use <see cref="GetVisualRuns"/> for a line that is part of the paragraph.
    /// </summary>
    public ReadOnlySpan<byte> Levels => this.levels;

    /// <summary>
    /// Resolves a paragraph of text.
    /// </summary>
    /// <param name="text">The paragraph, which is all of one paragraph for the algorithm, whatever separators it holds.</param>
    /// <param name="direction">The direction of the paragraph, or <see cref="BidiParagraphDirection.Auto"/> to read it from the text.</param>
    /// <returns>The resolved paragraph.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a defined value.</exception>
    public static BidiParagraph Resolve(ReadOnlySpan<char> text, BidiParagraphDirection direction)
    {
        sbyte requestedLevel = direction switch
        {
            BidiParagraphDirection.LeftToRight => 0,
            BidiParagraphDirection.RightToLeft => 1,
            BidiParagraphDirection.Auto => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };

        var data = new BidiData();
        data.Init(text, requestedLevel);

        BidiAlgorithm bidi = BidiAlgorithm.Instance.Value!;
        bidi.Process(data, resetWhitespace: false);

        var types = new BidiCharacterType[text.Length];
        var resolvedLevels = new sbyte[text.Length];
        int codePointIndex = 0;
        int unit = 0;
        var enumerator = new SpanCodePointEnumerator(text);
        while (enumerator.MoveNext())
        {
            int width = enumerator.Current.Utf16SequenceLength;
            for (int i = 0; i < width; i++)
            {
                types[unit + i] = data.Types[codePointIndex];
                resolvedLevels[unit + i] = bidi.ResolvedLevels[codePointIndex];
            }

            codePointIndex++;
            unit += width;
        }

        sbyte paragraphLevel = (sbyte)bidi.ResolvedParagraphEmbeddingLevel;
        if (text.Length == 0)
        {
            paragraphLevel = (sbyte)(requestedLevel == 1 ? 1 : 0);
        }

        var paragraphLevels = (sbyte[])resolvedLevels.Clone();
        BidiAlgorithm.ResetWhitespaceLevels(types, paragraphLevels, paragraphLevel);

        var levels = new byte[text.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            levels[i] = (byte)paragraphLevels[i];
        }

        return new BidiParagraph(text.ToString(), types, resolvedLevels, levels, (byte)paragraphLevel);
    }

    /// <summary>
    /// Gets the runs of one line of the paragraph in visual order, leftmost first.
    /// </summary>
    /// <remarks>
    /// Rule L1 is applied to the line, so the whitespace at its end, and before each separator in it, takes the paragraph
    /// level. Rule L2 then orders the runs. The characters inside a run are in logical order, and the caller
    /// draws them reversed when <see cref="BidiLevelRun.IsRightToLeft"/> is set.
    /// </remarks>
    /// <param name="lineStart">The index of the first code unit of the line.</param>
    /// <param name="lineLength">The number of code units in the line.</param>
    /// <returns>The runs of the line.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The line is not inside the text.</exception>
    public BidiLevelRun[] GetVisualRuns(int lineStart, int lineLength)
    {
        if (lineStart < 0 || lineStart > this.text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(lineStart));
        }

        if (lineLength < 0 || lineLength > this.text.Length - lineStart)
        {
            throw new ArgumentOutOfRangeException(nameof(lineLength));
        }

        if (lineLength == 0)
        {
            return [];
        }

        var lineLevels = new sbyte[lineLength];
        Array.Copy(this.resolvedLevels, lineStart, lineLevels, 0, lineLength);
        BidiAlgorithm.ResetWhitespaceLevels(
            new ReadOnlyArraySlice<BidiCharacterType>(this.types, lineStart, lineLength),
            lineLevels,
            (sbyte)this.ParagraphLevel);

        List<BidiLevelRun> runs = this.GetLogicalRuns(lineStart, lineLevels);
        OrderVisually(runs);
        return [.. runs];
    }

    private List<BidiLevelRun> GetLogicalRuns(int lineStart, sbyte[] lineLevels)
    {
        var runs = new List<BidiLevelRun>();
        int runStart = -1;
        int runEnd = 0;
        sbyte runLevel = 0;
        int pendingJoiner = -1;

        for (int i = 0; i < lineLevels.Length; i++)
        {
            int index = lineStart + i;
            if (BidiAlgorithm.IsRemovedByX9(this.types[index]))
            {
                if (IsJoiningControl(this.text[index]))
                {
                    if (runStart >= 0)
                    {
                        runEnd = lineStart + i + 1;
                    }
                    else if (pendingJoiner < 0)
                    {
                        pendingJoiner = index;
                    }
                }
                else
                {
                    EndRun(runs, ref runStart, runEnd, runLevel);
                    pendingJoiner = -1;
                }

                continue;
            }

            if (runStart >= 0 && lineLevels[i] == runLevel)
            {
                runEnd = index + 1;
                continue;
            }

            EndRun(runs, ref runStart, runEnd, runLevel);
            runStart = pendingJoiner >= 0 ? pendingJoiner : index;
            pendingJoiner = -1;
            runEnd = index + 1;
            runLevel = lineLevels[i];
        }

        EndRun(runs, ref runStart, runEnd, runLevel);
        return runs;
    }

    private static void EndRun(List<BidiLevelRun> runs, ref int runStart, int runEnd, sbyte runLevel)
    {
        if (runStart >= 0)
        {
            runs.Add(new BidiLevelRun(runStart, runEnd - runStart, (byte)runLevel));
            runStart = -1;
        }
    }

    private static bool IsJoiningControl(char c) => c is '‌' or '‍';

    /// <summary>
    /// Reorders the runs of a line into visual order. Implements rule L2.
    /// </summary>
    private static void OrderVisually(List<BidiLevelRun> runs)
    {
        int highest = 0;
        int lowestOdd = int.MaxValue;
        foreach (BidiLevelRun run in runs)
        {
            highest = Math.Max(highest, run.Level);
            if (run.IsRightToLeft)
            {
                lowestOdd = Math.Min(lowestOdd, run.Level);
            }
        }

        for (int level = highest; level >= lowestOdd; level--)
        {
            for (int i = 0; i < runs.Count; i++)
            {
                if (runs[i].Level < level)
                {
                    continue;
                }

                int end = i;
                while (end + 1 < runs.Count && runs[end + 1].Level >= level)
                {
                    end++;
                }

                runs.Reverse(i, end - i + 1);
                i = end;
            }
        }
    }
}
