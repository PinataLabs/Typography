// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Unicode;

public class LineBreakEnumeratorTests
{
    [Test]
    public async Task BasicLatinTest()
    {
        var lineBreaker = new LineBreakEnumerator("Hello World\r\nThis is a test.".AsSpan());
        var breaks = new List<LineBreak>();
        while (lineBreaker.MoveNext())
        {
            breaks.Add(lineBreaker.Current);
        }

        await Assert.That(breaks.Count).IsEqualTo(6);

        await Assert.That(breaks[0].PositionWrap).IsEqualTo(6);
        await Assert.That(breaks[0].Required).IsFalse();

        await Assert.That(breaks[1].PositionWrap).IsEqualTo(13);
        await Assert.That(breaks[1].Required).IsTrue();

        await Assert.That(breaks[2].PositionWrap).IsEqualTo(18);
        await Assert.That(breaks[2].Required).IsFalse();

        await Assert.That(breaks[3].PositionWrap).IsEqualTo(21);
        await Assert.That(breaks[3].Required).IsFalse();

        await Assert.That(breaks[4].PositionWrap).IsEqualTo(23);
        await Assert.That(breaks[4].Required).IsFalse();

        await Assert.That(breaks[5].PositionWrap).IsEqualTo(28);
        await Assert.That(breaks[5].Required).IsFalse();
    }

    // LB30b: [\p{Extended_Pictographic}&\p{Cn}] × EM, for every such code point.
    [Test]
    public async Task UnassignedPictographDoesNotBreakBeforeEmojiModifier()
    {
        const string modifier = "🏻";
        List<int> breaking = [];
        int count = 0;
        for (int value = 0; value <= 0x10FFFF; value++)
        {
            if (value is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            CodePoint codePoint = new(value);
            if (CodePoint.GetGeneralCategory(codePoint) != System.Globalization.UnicodeCategory.OtherNotAssigned
                || CodePoint.GetGraphemeClusterClass(codePoint) != GraphemeClusterClass.ExtendedPictographic)
            {
                continue;
            }

            count++;
            string text = char.ConvertFromUtf32(value) + modifier;
            LineBreakEnumerator enumerator = new(text.AsSpan());
            enumerator.MoveNext();
            if (enumerator.Current.PositionWrap != 2)
            {
                breaking.Add(value);
            }
        }

        await Assert.That(count).IsGreaterThan(0);
        await Assert.That(breaking).IsEmpty();
    }

    [Test]
    public async Task ForwardTextWithOuterWhitespace()
    {
        string text = " Apples Pears Bananas   ";
        var breaks = new List<LineBreak>();

        foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan()))
        {
            breaks.Add(lineBreak);
        }

        await Assert.That(breaks[0].PositionWrap).IsEqualTo(1);
        await Assert.That(breaks[0].PositionMeasure).IsEqualTo(0);
        await Assert.That(breaks[1].PositionWrap).IsEqualTo(8);
        await Assert.That(breaks[1].PositionMeasure).IsEqualTo(7);
        await Assert.That(breaks[2].PositionWrap).IsEqualTo(14);
        await Assert.That(breaks[2].PositionMeasure).IsEqualTo(13);
        await Assert.That(breaks[3].PositionWrap).IsEqualTo(24);
        await Assert.That(breaks[3].PositionMeasure).IsEqualTo(21);
    }

    [Test]
    public async Task ForwardTest()
    {
        string text = "Apples Pears Bananas";
        var breaks = new List<LineBreak>();

        foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan()))
        {
            breaks.Add(lineBreak);
        }

        await Assert.That(breaks[0].PositionWrap).IsEqualTo(7);
        await Assert.That(breaks[0].PositionMeasure).IsEqualTo(6);
        await Assert.That(breaks[1].PositionWrap).IsEqualTo(13);
        await Assert.That(breaks[1].PositionMeasure).IsEqualTo(12);
        await Assert.That(breaks[2].PositionWrap).IsEqualTo(20);
        await Assert.That(breaks[2].PositionMeasure).IsEqualTo(20);
    }

    [Test]
    [Arguments("Super, Smash Bros (1999)", new[] { 7, 13, 18, 24 })]
    [Arguments("Super Smash Bros (1999)", new[] { 6, 12, 17, 23 })]
    [Arguments("Super, Smash Bros (9999)", new[] { 7, 13, 18, 24 })]
    [Arguments("(9999)", new[] { 6 })]
    [Arguments("a, 99", new[] { 3, 5 })]
    public async Task NumbersAfterEarlierPunctuationAreNotSplit(string text, int[] expected)
    {
        var breaks = new List<int>();
        foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan()))
        {
            breaks.Add(lineBreak.PositionWrap);
        }

        await Assert.That(breaks.ToArray()).IsEquivalentTo(expected);
    }

    // Contains 19,346 tests at Unicode 18.0.0
    // https://www.unicode.org/Public/UCD/latest/ucd/auxiliary/LineBreakTest.html
    [Test]
    public async Task LineBreakTestSuitePasses()
    {
        List<LineBreakTestCase> cases = LineBreakTestCase.Read();
        await Assert.That(cases.Count).IsGreaterThan(19000);

        List<string> failures = [];
        foreach (LineBreakTestCase test in cases)
        {
            List<int> found = [];
            foreach (LineBreak lineBreak in new LineBreakEnumerator(test.Text.AsSpan()))
            {
                found.Add(lineBreak.PositionWrap);
            }

            if (!found.SequenceEqual(test.BreakPoints))
            {
                failures.Add($"line {test.LineNumber}: expected {string.Join(" ", test.BreakPoints)}, found {string.Join(" ", found)}");
            }
        }

        await Assert.That(failures.Take(10)).IsEmpty();
    }
}
