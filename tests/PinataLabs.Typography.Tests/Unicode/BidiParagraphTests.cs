// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using TUnit.Core.Interfaces;

namespace PinataLabs.Typography.Tests.Unicode;

/// <summary>
/// The public bidi API against the Unicode conformance files, and the rules that the files do not cover:
/// joining controls and rule L1 per line.
/// </summary>
public class BidiParagraphTests
{
    private const string Alef = "\U00000627";
    private const string Beh = "\U00000628";
    private const string Zwj = "\U0000200D";
    private const string Zwnj = "\U0000200C";
    private const string SoftHyphen = "\U000000AD";

    private static readonly Dictionary<BidiCharacterType, string> ClassRepresentatives = new()
    {
        [BidiCharacterType.LeftToRight] = "a",
        [BidiCharacterType.RightToLeft] = "\U000005D0",
        [BidiCharacterType.ArabicLetter] = Alef,
        [BidiCharacterType.EuropeanNumber] = "0",
        [BidiCharacterType.EuropeanSeparator] = "+",
        [BidiCharacterType.EuropeanTerminator] = "$",
        [BidiCharacterType.ArabicNumber] = "\U00000660",
        [BidiCharacterType.CommonSeparator] = ",",
        [BidiCharacterType.NonspacingMark] = "\U00000300",
        [BidiCharacterType.BoundaryNeutral] = SoftHyphen,
        [BidiCharacterType.ParagraphSeparator] = "\U00002029",
        [BidiCharacterType.SegmentSeparator] = "\t",
        [BidiCharacterType.Whitespace] = " ",
        [BidiCharacterType.OtherNeutral] = "!",
        [BidiCharacterType.LeftToRightEmbedding] = "\U0000202A",
        [BidiCharacterType.LeftToRightOverride] = "\U0000202D",
        [BidiCharacterType.RightToLeftEmbedding] = "\U0000202B",
        [BidiCharacterType.RightToLeftOverride] = "\U0000202E",
        [BidiCharacterType.PopDirectionalFormat] = "\U0000202C",
        [BidiCharacterType.LeftToRightIsolate] = "\U00002066",
        [BidiCharacterType.RightToLeftIsolate] = "\U00002067",
        [BidiCharacterType.FirstStrongIsolate] = "\U00002068",
        [BidiCharacterType.PopDirectionalIsolate] = "\U00002069",
    };

    private ITestOutput Output => TestContext.Current!.Output;

    [Test]
    public async Task EveryClassRepresentativeHasItsClass()
    {
        foreach ((BidiCharacterType type, string text) in ClassRepresentatives)
        {
            await Assert.That(CodePoint.GetBidiClass(new CodePoint(text[0])).CharacterType).IsEqualTo(type);
        }
    }

    [Test]
    public async Task PassesBidiTest()
    {
        string[] lines = File.ReadAllLines(Path.Combine(TestEnvironment.UnicodeTestDataFullPath, "BidiTest.txt"));
        var stopwatch = Stopwatch.StartNew();

        var failures = new List<string>();
        int cases = 0;
        int[] expectedLevels = [];
        int[] expectedOrder = [];
        for (int lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            string line = lines[lineNumber - 1].Split('#')[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("@Levels:", StringComparison.Ordinal))
            {
                expectedLevels = ParseNumbers(line["@Levels:".Length..]);
                continue;
            }

            if (line.StartsWith("@Reorder:", StringComparison.Ordinal))
            {
                expectedOrder = ParseNumbers(line["@Reorder:".Length..]);
                continue;
            }

            if (line[0] == '@')
            {
                continue;
            }

            string[] parts = line.Split(';');
            var text = new StringBuilder();
            foreach (string name in parts[0].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                text.Append(ClassRepresentatives[UnicodeTypeMaps.BidiCharacterTypeMap[name]]);
            }

            int directions = Convert.ToInt32(parts[1].Trim(), 16);
            foreach ((int bit, BidiParagraphDirection direction) in new[]
            {
                (1, BidiParagraphDirection.Auto),
                (2, BidiParagraphDirection.LeftToRight),
                (4, BidiParagraphDirection.RightToLeft),
            })
            {
                if ((directions & bit) == 0)
                {
                    continue;
                }

                cases++;
                string? failure = Check(text.ToString(), direction, null, expectedLevels, expectedOrder);
                if (failure is not null && failures.Count < 10)
                {
                    failures.Add($"line {lineNumber} {direction}: {failure}");
                }
            }
        }

        stopwatch.Stop();
        this.Output.WriteLine($"BidiTest.txt: {cases} cases in {stopwatch.ElapsedMilliseconds} ms");
        await Assert.That(failures).IsEmpty();
        await Assert.That(cases).IsGreaterThan(400_000);
    }

    [Test]
    public async Task PassesBidiCharacterTest()
    {
        string[] lines = File.ReadAllLines(Path.Combine(TestEnvironment.UnicodeTestDataFullPath, "BidiCharacterTest.txt"));
        var stopwatch = Stopwatch.StartNew();

        var failures = new List<string>();
        int cases = 0;
        for (int lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
        {
            string line = lines[lineNumber - 1].Split('#')[0].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(';');
            var text = new StringBuilder();
            foreach (string codePoint in fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                text.Append(char.ConvertFromUtf32(Convert.ToInt32(codePoint, 16)));
            }

            BidiParagraphDirection direction = int.Parse(fields[1]) switch
            {
                0 => BidiParagraphDirection.LeftToRight,
                1 => BidiParagraphDirection.RightToLeft,
                _ => BidiParagraphDirection.Auto,
            };

            cases++;
            string? failure = Check(text.ToString(), direction, int.Parse(fields[2]), ParseNumbers(fields[3]), ParseNumbers(fields[4]));
            if (failure is not null && failures.Count < 10)
            {
                failures.Add($"line {lineNumber} {direction}: {failure}");
            }
        }

        stopwatch.Stop();
        this.Output.WriteLine($"BidiCharacterTest.txt: {cases} cases in {stopwatch.ElapsedMilliseconds} ms");
        await Assert.That(failures).IsEmpty();
        await Assert.That(cases).IsGreaterThan(90_000);
    }

    [Test]
    public async Task JoiningControlsStayInsideTheRunAroundThem()
    {
        foreach (string joiner in new[] { Zwj, Zwnj })
        {
            string text = Alef + joiner + Beh;
            foreach (BidiParagraphDirection direction in new[] { BidiParagraphDirection.LeftToRight, BidiParagraphDirection.RightToLeft, BidiParagraphDirection.Auto })
            {
                BidiLevelRun[] runs = BidiParagraph.Resolve(text, direction).GetVisualRuns(0, text.Length);

                await Assert.That(runs.Length).IsEqualTo(1);
                await Assert.That(runs[0].Start).IsEqualTo(0);
                await Assert.That(runs[0].Length).IsEqualTo(3);
                await Assert.That(runs[0].IsRightToLeft).IsTrue();
            }
        }
    }

    [Test]
    public async Task JoiningControlsBetweenTwoLevelsJoinTheRunBeforeThem()
    {
        const string text = Alef + Zwj + "a";

        BidiLevelRun[] runs = BidiParagraph.Resolve(text, BidiParagraphDirection.LeftToRight).GetVisualRuns(0, text.Length);

        await Assert.That(runs).IsEquivalentTo([new BidiLevelRun(0, 2, 1), new BidiLevelRun(2, 1, 0)]);
    }

    [Test]
    public async Task OtherRemovedCharactersAreInNoRunAndEndTheRunBeforeThem()
    {
        const string text = Alef + SoftHyphen + Beh;

        BidiParagraph paragraph = BidiParagraph.Resolve(text, BidiParagraphDirection.LeftToRight);

        await Assert.That(paragraph.GetVisualRuns(0, text.Length)).IsEquivalentTo([new BidiLevelRun(2, 1, 1), new BidiLevelRun(0, 1, 1)]);
    }

    [Test]
    public async Task SurrogatePairsShareALevelAndStayInOneRun()
    {
        // U+10800 CYPRIOT SYLLABLE A is right to left.
        const string text = "a \U00010800\U00010801";

        BidiParagraph paragraph = BidiParagraph.Resolve(text, BidiParagraphDirection.LeftToRight);

        await Assert.That(paragraph.Levels.ToArray()).IsEquivalentTo(new byte[] { 0, 0, 1, 1, 1, 1 });
        await Assert.That(paragraph.GetVisualRuns(0, text.Length)).IsEquivalentTo([new BidiLevelRun(0, 2, 0), new BidiLevelRun(2, 4, 1)]);
    }

    [Test]
    public async Task RuleL1ResetsTheWhitespaceAtTheEndOfEachLine()
    {
        // Two Arabic words with a space between them, in a left to right paragraph.
        const string text = "\U00000627\U00000628 \U0000062A\U0000062B";
        BidiParagraph paragraph = BidiParagraph.Resolve(text, BidiParagraphDirection.LeftToRight);

        // As one line, the space is between two right-to-left letters and so is at their level.
        await Assert.That(paragraph.GetVisualRuns(0, text.Length)).IsEquivalentTo([new BidiLevelRun(0, 5, 1)]);

        // A line that ends at the space has it reset to the paragraph level.
        await Assert.That(paragraph.GetVisualRuns(0, 3)).IsEquivalentTo([new BidiLevelRun(2, 1, 0), new BidiLevelRun(0, 2, 1)]);

        // The same space at the start of the next line is followed by letters and keeps its level.
        await Assert.That(paragraph.GetVisualRuns(2, 3)).IsEquivalentTo([new BidiLevelRun(2, 3, 1)]);
    }

    [Test]
    public async Task LevelsTreatTheParagraphAsOneLine()
    {
        const string text = "\U00000627\U00000628 ";

        BidiParagraph paragraph = BidiParagraph.Resolve(text, BidiParagraphDirection.LeftToRight);

        await Assert.That(paragraph.Levels.ToArray()).IsEquivalentTo(new byte[] { 1, 1, 0 });
    }

    [Test]
    public async Task AutoTakesTheDirectionOfTheFirstStrongCharacter()
    {
        await Assert.That(BidiParagraph.Resolve("123 \U000005D0a", BidiParagraphDirection.Auto).ParagraphLevel).IsEqualTo((byte)1);
        await Assert.That(BidiParagraph.Resolve("123 a\U000005D0", BidiParagraphDirection.Auto).ParagraphLevel).IsEqualTo((byte)0);
        await Assert.That(BidiParagraph.Resolve("123", BidiParagraphDirection.Auto).ParagraphLevel).IsEqualTo((byte)0);
    }

    [Test]
    public async Task EmptyTextHasNoLevelsAndNoRuns()
    {
        BidiParagraph paragraph = BidiParagraph.Resolve([], BidiParagraphDirection.RightToLeft);

        await Assert.That(paragraph.ParagraphLevel).IsEqualTo((byte)1);
        await Assert.That(paragraph.Levels.Length).IsEqualTo(0);
        await Assert.That(paragraph.GetVisualRuns(0, 0)).IsEmpty();
    }

    [Test]
    [Arguments(-1, 1)]
    [Arguments(0, 4)]
    [Arguments(3, 1)]
    [Arguments(4, 0)]
    [Arguments(1, -1)]
    public async Task ALineOutsideTheTextThrows(int lineStart, int lineLength)
    {
        BidiParagraph paragraph = BidiParagraph.Resolve("abc", BidiParagraphDirection.Auto);

        await Assert.That(() => paragraph.GetVisualRuns(lineStart, lineLength)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task AnUndefinedDirectionThrows()
        => await Assert.That(() => BidiParagraph.Resolve("abc", (BidiParagraphDirection)3)).Throws<ArgumentOutOfRangeException>();

    private static int[] ParseNumbers(string field)
        => field.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x == "x" ? -1 : int.Parse(x))
            .ToArray();

    /// <summary>
    /// Resolves a paragraph and compares it to the expected level of each code point (-1 for a code point that rule X9
    /// removes) and to the expected visual order of the code points that are not removed, with the paragraph as one line.
    /// </summary>
    /// <returns>A description of the first difference, or <see langword="null"/> when there is none.</returns>
    private static string? Check(
        string text,
        BidiParagraphDirection direction,
        int? expectedParagraphLevel,
        int[] expectedLevels,
        int[] expectedOrder)
    {
        BidiParagraph paragraph = BidiParagraph.Resolve(text, direction);
        if (expectedParagraphLevel is { } level && paragraph.ParagraphLevel != level)
        {
            return $"paragraph level {paragraph.ParagraphLevel}, expected {level}";
        }

        var codePointOfUnit = new int[text.Length];
        int codePoint = -1;
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsLowSurrogate(text[i]))
            {
                codePoint++;
            }

            codePointOfUnit[i] = codePoint;
        }

        if (codePoint + 1 != expectedLevels.Length)
        {
            return $"{codePoint + 1} code points, expected {expectedLevels.Length}";
        }

        ReadOnlySpan<byte> levels = paragraph.Levels;
        for (int i = 0; i < text.Length; i++)
        {
            int expected = expectedLevels[codePointOfUnit[i]];
            if (expected >= 0 && levels[i] != expected)
            {
                return $"code unit {i} level {levels[i]}, expected {expected}";
            }
        }

        var order = new List<int>(expectedOrder.Length);
        foreach (BidiLevelRun run in paragraph.GetVisualRuns(0, text.Length))
        {
            for (int k = 0; k < run.Length; k++)
            {
                int unit = run.IsRightToLeft ? run.Start + run.Length - 1 - k : run.Start + k;
                if (!char.IsLowSurrogate(text[unit]) && expectedLevels[codePointOfUnit[unit]] >= 0)
                {
                    order.Add(codePointOfUnit[unit]);
                }
            }
        }

        return order.SequenceEqual(expectedOrder)
            ? null
            : $"order {string.Join(' ', order)}, expected {string.Join(' ', expectedOrder)}";
    }
}
