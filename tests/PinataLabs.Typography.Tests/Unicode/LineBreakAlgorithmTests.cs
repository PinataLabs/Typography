// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Unicode;

public class LineBreakAlgorithmTests
{
    // Every boundary of every case of LineBreakTest.txt, asked as the seam between what comes before and after it.
    [Test]
    public async Task EveryBoundaryOfTheLineBreakTestSuiteIsAnsweredAsTheSuiteSays()
    {
        List<LineBreakTestCase> cases = LineBreakTestCase.Read();
        await Assert.That(cases.Count).IsGreaterThan(19000);

        List<string> failures = [];
        int boundaries = 0;
        foreach (LineBreakTestCase test in cases)
        {
            string text = test.Text;
            int index = 0;
            for (int boundary = 0; boundary <= test.CodePoints.Length; boundary++)
            {
                boundaries++;
                bool expected = boundary > 0 && Array.IndexOf(test.BreakPoints, boundary) >= 0;
                bool found = LineBreakAlgorithm.IsBreakAllowedBetween(text.AsSpan(0, index), text.AsSpan(index));
                if (found != expected && failures.Count < 10)
                {
                    failures.Add($"line {test.LineNumber} at {boundary}: expected {expected}");
                }

                if (boundary < test.CodePoints.Length)
                {
                    index += char.ConvertFromUtf32(test.CodePoints[boundary]).Length;
                }
            }
        }

        await Assert.That(boundaries).IsGreaterThan(cases.Count);
        await Assert.That(failures).IsEmpty();
    }

    // The query is the enumerator at one position of the joined text, for each tailoring, so the two cannot disagree.
    [Test]
    [Arguments(LineBreakTailoring.None)]
    [Arguments(LineBreakTailoring.SouthEastAsianAsIdeographs)]
    [Arguments(LineBreakTailoring.HangulAsAlphabetic)]
    [Arguments(LineBreakTailoring.SouthEastAsianAsIdeographs | LineBreakTailoring.HangulAsAlphabetic)]
    public async Task ABoundaryIsAnsweredAsTheEnumeratorAnswersItInTheJoinedText(LineBreakTailoring tailoring)
    {
        List<string> failures = [];
        foreach (LineBreakTestCase test in LineBreakTestCase.Read())
        {
            string text = test.Text;
            List<int> breaks = [];
            foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan(), tailoring))
            {
                breaks.Add(lineBreak.PositionWrap);
            }

            int index = 0;
            for (int boundary = 0; boundary <= test.CodePoints.Length; boundary++)
            {
                bool expected = boundary > 0 && breaks.Contains(boundary);
                bool found = LineBreakAlgorithm.IsBreakAllowedBetween(text.AsSpan(0, index), text.AsSpan(index), tailoring);
                if (found != expected && failures.Count < 10)
                {
                    failures.Add($"line {test.LineNumber} at {boundary}: expected {expected}");
                }

                if (boundary < test.CodePoints.Length)
                {
                    index += char.ConvertFromUtf32(test.CodePoints[boundary]).Length;
                }
            }
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    [Arguments("", "", false)] // LB2
    [Arguments("", "a", false)] // LB2
    [Arguments("a", "", true)] // LB3
    [Arguments("a", "b", false)] // LB28
    [Arguments("a", " b", false)] // LB7
    [Arguments("a ", "b", true)] // LB18
    [Arguments("a\r", "\n", false)] // LB5
    [Arguments("a\r", "b", true)] // LB4
    [Arguments("a\u200B ", "b", true)] // LB8
    [Arguments("a", "\u0301", false)] // LB9
    [Arguments(" ", "\u0301", true)] // LB10, LB18
    [Arguments("\uD83C", "\uDDFA", false)] // inside a surrogate pair
    [Arguments("$", "(5", false)] // LB25, PR x OP NU
    [Arguments("$", "(a", true)] // LB31
    [Arguments("$", "(.5", false)] // LB25, PR x OP IS NU
    [Arguments("4", ".5", false)] // LB25, NU x IS
    [Arguments("1)", "%", false)] // LB25, NU CP x PO
    [Arguments("a)", "%", true)] // LB31
    [Arguments("x -", "y", false)] // LB20a
    [Arguments("x-", "y", true)] // LB31
    [Arguments("\uD83C\uDDFA\uD83C\uDDF8", "\uD83C\uDDE9", true)] // LB30a, a flag and one indicator
    [Arguments("\uD83C\uDDFA\uD83C\uDDF8\uD83C\uDDE9", "\uD83C\uDDEA", false)] // LB30a, a flag and a half
    [Arguments("a", "(", false)] // LB30
    [Arguments("a", "（", true)] // LB31, an opening bracket that is East Asian
    [Arguments("(a", "b", false)] // LB28
    [Arguments("( ", "a", false)] // LB14
    [Arguments("\u00A0", "a", false)] // LB12
    [Arguments("a", "\u2060b", false)] // LB11
    [Arguments("\u4E00", "\u4E01", true)] // LB31
    [Arguments("\u4E00", "\u3001", false)] // LB13
    [Arguments("\u4E00", "\u30FC", false)] // LB21
    public async Task AnswersASeam(string before, string after, bool expected)
    {
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween(before.AsSpan(), after.AsSpan())).IsEqualTo(expected);
    }

    [Test]
    [Arguments("\u0E2A", "\u0E27", LineBreakTailoring.None, false)]
    [Arguments("\u0E2A", "\u0E27", LineBreakTailoring.SouthEastAsianAsIdeographs, true)]
    [Arguments("\u0E2A", "\u0E31", LineBreakTailoring.SouthEastAsianAsIdeographs, false)]
    [Arguments("\u0E2A\u0E31", "\u0E27", LineBreakTailoring.SouthEastAsianAsIdeographs, true)]
    [Arguments("\uD55C", "\uAD6D", LineBreakTailoring.None, true)]
    [Arguments("\uD55C", "\uAD6D", LineBreakTailoring.HangulAsAlphabetic, false)]
    [Arguments("\uD55C ", "\uAD6D", LineBreakTailoring.HangulAsAlphabetic, true)]
    [Arguments("\uD55C", "\u6F22", LineBreakTailoring.HangulAsAlphabetic, true)]
    [Arguments("\u6F22", "\u5B57", LineBreakTailoring.HangulAsAlphabetic, true)]
    public async Task AnswersASeamWithATailoring(string before, string after, LineBreakTailoring tailoring, bool expected)
    {
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween(before.AsSpan(), after.AsSpan(), tailoring)).IsEqualTo(expected);
    }

    // A text longer than the buffer on the stack is answered as a shorter one is.
    [Test]
    public async Task ALongTextIsAnsweredAsAShortOneIs()
    {
        string spaces = new(' ', 400);
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween(("(" + spaces).AsSpan(), "a".AsSpan())).IsFalse(); // LB14
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween(("a" + spaces).AsSpan(), "a".AsSpan())).IsTrue(); // LB18
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween("a".AsSpan(), new string('b', 400).AsSpan())).IsFalse(); // LB28
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween("a".AsSpan(), (new string('b', 400) + " c").AsSpan())).IsFalse();

        string flags = string.Concat(System.Linq.Enumerable.Repeat("\uD83C\uDDFA\uD83C\uDDF8", 100));
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween(flags.AsSpan(), "\uD83C\uDDE9".AsSpan())).IsTrue(); // LB30a, whole flags
        await Assert.That(LineBreakAlgorithm.IsBreakAllowedBetween((flags + "\uD83C\uDDE9").AsSpan(), "\uD83C\uDDEA".AsSpan())).IsFalse(); // LB30a, half a flag
    }

    // What a layout engine does: it reads 32 code units of context each side, and the answer is the one of the whole text
    // for each seam of every case of the suite, so the window loses nothing there.
    [Test]
    public async Task AWindowOf32CodeUnitsEachSideAnswersAsTheWholeTextDoes()
    {
        string[] paragraphs =
        [
            "The price is $(1,234.56) or 99% of EUR 2.000,00 -- see (a) [b] \u201Cquoted\u201D text, and x-ray: 44-1.",
            "\uD83C\uDDFA\uD83C\uDDF8\uD83C\uDDE9\uD83C\uDDEA\uD83C\uDDEB\uD83C\uDDF7 flags and \uD83D\uDC4D\uD83C\uDFFB emoji",
            "\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35\u0E04\u0E23\u0E31\u0E1A \uD55C\uAD6D\uC5B4 \u6F22\u5B57\u3001\u30FC\u3002 e\u0301 a\u200Db",
        ];

        List<string> failures = [];
        foreach (string paragraph in paragraphs)
        {
            foreach (LineBreakTailoring tailoring in new[] { LineBreakTailoring.None, LineBreakTailoring.SouthEastAsianAsIdeographs | LineBreakTailoring.HangulAsAlphabetic })
            {
                for (int index = 1; index < paragraph.Length; index++)
                {
                    bool whole = LineBreakAlgorithm.IsBreakAllowedBetween(paragraph.AsSpan(0, index), paragraph.AsSpan(index), tailoring);
                    int start = Math.Max(0, index - 32);
                    int end = Math.Min(paragraph.Length, index + 32);
                    bool window = LineBreakAlgorithm.IsBreakAllowedBetween(paragraph.AsSpan(start, index - start), paragraph.AsSpan(index, end - index), tailoring);
                    if (window != whole)
                    {
                        failures.Add($"{paragraph} at {index}");
                    }
                }
            }
        }

        await Assert.That(failures).IsEmpty();
    }
}
