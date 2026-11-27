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
/// Rule P1 splits text into paragraphs after each paragraph separator: LF, CR, U+0085, U+2029 and U+001C to U+001E,
/// with CR LF as one separator. The separator is the last code unit of its paragraph. <see cref="Resolve"/> resolves
/// text that is one paragraph, and <see cref="ResolveAll"/> resolves each paragraph of a text. Each paragraph is
/// resolved on its own, with its own level when the direction is <see cref="BidiParagraphDirection.Auto"/>.
/// </para>
/// <para>
/// A paragraph holds a copy of the text, which the paragraphs of one text share, so the text passed to
/// <see cref="Resolve"/> or <see cref="ResolveAll"/> need not outlive the call. All indices are UTF-16 code unit indices
/// into that text: <see cref="Start"/>, the line that <see cref="GetVisualRuns"/> takes, and the runs it gives. The two
/// code units of a surrogate pair share a level.
/// </para>
/// <para>
/// Rule X9 removes explicit embedding and override controls and every character of class BN from the ordering. Each
/// still has a level, which is that of the code unit before it after rule L1, or the paragraph level at the start of
/// the paragraph. <c>TextLayout</c> of PinataLabs.Typography applies rule L1 to each line as <see cref="GetVisualRuns"/>
/// does, and draws a removed character at the start of a line in the run of the character after it, where
/// <see cref="GetVisualRuns"/> puts a joiner.
/// </para>
/// <para>
/// U+200C ZERO WIDTH NON-JOINER and U+200D ZERO WIDTH JOINER are kept in a run, because they decide how the letters
/// on either side join and a shaper has to see them next to the letter they shape. Every other removed character is
/// in no run, and ends the run before it. A joiner is in the run of the character before it, with these exceptions.
/// Where the level changes at the joiner, and the character after it joins to the character before it (the Arabic
/// joining type of that character is right joining, dual joining or join causing), the joiner is in the run after it,
/// so that <c>a ZWJ ب</c> gives the Arabic run its joiner. A joiner at the start of a line, or after a removed
/// character that is not a joiner, is in the run of the character after it, and a joiner that no character follows
/// before the end of the line or the next removed character is a run of its own. In every case the run has the level
/// of the characters in it, which for a joiner moved to the run after it is not the entry of that joiner in
/// <see cref="Levels"/>. A run never overlaps another.
/// </para>
/// </remarks>
public sealed class BidiParagraph
{
    private readonly string text;
    private readonly BidiCharacterType[] types;
    private readonly sbyte[] resolvedLevels;
    private readonly byte[] levels;

    private BidiParagraph(string text, BidiCharacterType[] types, sbyte[] resolvedLevels, byte[] levels, int start, int length, byte paragraphLevel)
    {
        this.text = text;
        this.types = types;
        this.resolvedLevels = resolvedLevels;
        this.levels = levels;
        this.Start = start;
        this.Length = length;
        this.ParagraphLevel = paragraphLevel;
    }

    /// <summary>
    /// Gets the index of the first code unit of the paragraph in the text. It is 0 for <see cref="Resolve"/>.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the number of code units of the paragraph, its paragraph separator included.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the embedding level of the paragraph: even when it runs left to right, odd when it runs right to left.
    /// This is the level the caller asked for, or the level that rules P2 and P3 read from the paragraph.
    /// </summary>
    public byte ParagraphLevel { get; }

    /// <summary>
    /// Gets the embedding level of each UTF-16 code unit of the paragraph, from <see cref="Start"/>, in logical order,
    /// with rule L1 applied to the whole paragraph as one line. Use <see cref="GetVisualRuns"/> for a line that is part of
    /// the paragraph.
    /// </summary>
    public ReadOnlySpan<byte> Levels => this.levels.AsSpan(this.Start, this.Length);

    internal ReadOnlySpan<char> Text => this.text;

    /// <summary>
    /// Resolves a paragraph of text.
    /// </summary>
    /// <param name="text">The paragraph. A paragraph separator can only be its last code unit, or CR LF its last two.</param>
    /// <param name="direction">The direction of the paragraph, or <see cref="BidiParagraphDirection.Auto"/> to read it from the text.</param>
    /// <returns>The resolved paragraph.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a defined value.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="text"/> holds more than one paragraph. <see cref="ResolveAll"/> resolves each of them.
    /// </exception>
    public static BidiParagraph Resolve(ReadOnlySpan<char> text, BidiParagraphDirection direction)
    {
        BidiParagraph[] paragraphs = ResolveAll(text, direction);
        if (paragraphs.Length > 1)
        {
            throw new ArgumentException(
                $"The text holds {paragraphs.Length} paragraphs: a paragraph separator ends the first before code unit {paragraphs[1].Start}. ResolveAll resolves each of them.",
                nameof(text));
        }

        return paragraphs[0];
    }

    /// <summary>
    /// Resolves each paragraph of a text.
    /// </summary>
    /// <param name="text">The text, which rule P1 splits after each paragraph separator.</param>
    /// <param name="direction">
    /// The direction of every paragraph, or <see cref="BidiParagraphDirection.Auto"/> for each to read its own from its
    /// text.
    /// </param>
    /// <returns>
    /// The paragraphs in the order of the text. They cover the text, and empty text is one empty paragraph. Text that
    /// ends with a paragraph separator has no empty paragraph after it.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="direction"/> is not a defined value.</exception>
    public static BidiParagraph[] ResolveAll(ReadOnlySpan<char> text, BidiParagraphDirection direction)
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

        string copy = text.ToString();
        var types = new BidiCharacterType[text.Length];
        var resolvedLevels = new sbyte[text.Length];
        var paragraphLevels = new sbyte[text.Length];
        var levels = new byte[text.Length];
        var paragraphs = new BidiParagraph[bidi.Paragraphs.Count];
        int codePointIndex = 0;
        int unit = 0;
        var enumerator = new SpanCodePointEnumerator(text);
        for (int p = 0; p < paragraphs.Length; p++)
        {
            BidiAlgorithm.Paragraph paragraph = bidi.Paragraphs[p];
            int start = unit;
            for (; codePointIndex < paragraph.End && enumerator.MoveNext(); codePointIndex++)
            {
                int width = enumerator.Current.Utf16SequenceLength;
                for (int i = 0; i < width; i++)
                {
                    types[unit + i] = data.Types[codePointIndex];
                    resolvedLevels[unit + i] = bidi.ResolvedLevels[codePointIndex];
                }

                unit += width;
            }

            // Levels has rule L1 applied to the paragraph as one line.
            Array.Copy(resolvedLevels, start, paragraphLevels, start, unit - start);
            BidiAlgorithm.ResetWhitespaceLevels(
                new ReadOnlyArraySlice<BidiCharacterType>(types, start, unit - start),
                new ArraySlice<sbyte>(paragraphLevels, start, unit - start),
                paragraph.Level);

            for (int i = start; i < unit; i++)
            {
                levels[i] = (byte)paragraphLevels[i];
            }

            paragraphs[p] = new BidiParagraph(copy, types, resolvedLevels, levels, start, unit - start, (byte)paragraph.Level);
        }

        return paragraphs;
    }

    /// <summary>
    /// Gets the runs of one line of the paragraph in visual order, leftmost first.
    /// </summary>
    /// <remarks>
    /// Rule L1 is applied to the line, so the whitespace at its end, and before each separator in it, takes the paragraph
    /// level. Rule L2 then orders the runs. The characters inside a run are in logical order, and the caller
    /// draws them reversed when <see cref="BidiLevelRun.IsRightToLeft"/> is set.
    /// </remarks>
    /// <param name="lineStart">The index of the first code unit of the line, in the text.</param>
    /// <param name="lineLength">The number of code units in the line.</param>
    /// <returns>The runs of the line.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The line is not inside the paragraph, or starts or ends between the two code units of a surrogate pair.</exception>
    public BidiLevelRun[] GetVisualRuns(int lineStart, int lineLength)
    {
        int end = this.Start + this.Length;
        if (lineStart < this.Start || lineStart > end || this.SplitsSurrogatePair(lineStart))
        {
            throw new ArgumentOutOfRangeException(nameof(lineStart));
        }

        if (lineLength < 0 || lineLength > end - lineStart || this.SplitsSurrogatePair(lineStart + lineLength))
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

    private bool SplitsSurrogatePair(int index)
        => index > 0
        && index < this.text.Length
        && char.IsHighSurrogate(this.text[index - 1])
        && char.IsLowSurrogate(this.text[index]);

    private List<BidiLevelRun> GetLogicalRuns(int lineStart, sbyte[] lineLevels)
    {
        var runs = new List<BidiLevelRun>();

        // The open run is [runStart, runEnd), and runEnd includes the joiners after its last character.
        int runStart = -1;
        int runEnd = 0;
        sbyte runLevel = 0;

        // The joiners after the last character that is not removed, or since the last removed character that is not
        // a joiner when no run is open.
        int joinersStart = -1;
        int joinersEnd = 0;
        sbyte joinersLevel = 0;

        for (int i = 0; i < lineLevels.Length; i++)
        {
            int index = lineStart + i;
            if (BidiAlgorithm.IsRemovedByX9(this.types[index]))
            {
                if (IsJoiningControl(this.text[index]))
                {
                    if (joinersStart < 0)
                    {
                        joinersStart = index;
                        joinersLevel = lineLevels[i];
                    }

                    joinersEnd = index + 1;
                    if (runStart >= 0)
                    {
                        runEnd = joinersEnd;
                    }
                }
                else
                {
                    EndRunsAtRemovedCharacter();
                }

                continue;
            }

            if (runStart >= 0 && lineLevels[i] == runLevel)
            {
                runEnd = index + 1;
                joinersStart = -1;
                continue;
            }

            int newStart = index;
            if (runStart >= 0)
            {
                // The level changes here. The joiners before this character go with it when it joins to them.
                if (joinersStart >= 0 && this.JoinsToPrevious(index))
                {
                    runEnd = joinersStart;
                    newStart = joinersStart;
                }

                runs.Add(new BidiLevelRun(runStart, runEnd - runStart, (byte)runLevel));
            }
            else if (joinersStart >= 0)
            {
                newStart = joinersStart;
            }

            joinersStart = -1;
            runStart = newStart;
            runEnd = index + 1;
            runLevel = lineLevels[i];
        }

        EndRunsAtRemovedCharacter();
        return runs;

        void EndRunsAtRemovedCharacter()
        {
            if (runStart >= 0)
            {
                runs.Add(new BidiLevelRun(runStart, runEnd - runStart, (byte)runLevel));
                runStart = -1;
            }
            else if (joinersStart >= 0)
            {
                runs.Add(new BidiLevelRun(joinersStart, joinersEnd - joinersStart, (byte)joinersLevel));
            }

            joinersStart = -1;
        }
    }

    private bool JoinsToPrevious(int index)
    {
        CodePoint codePoint = CodePoint.DecodeFromUtf16At(this.text, index, out _);
        return CodePoint.GetArabicJoiningClass(codePoint).JoiningType
            is ArabicJoiningType.RightJoining or ArabicJoiningType.DualJoining or ArabicJoiningType.JoinCausing;
    }

    private static bool IsJoiningControl(char c) => c is '\u200C' or '\u200D';

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
