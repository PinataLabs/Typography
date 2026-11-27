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

    // The positions count code points, so a character outside the Basic Multilingual Plane counts as one.
    [Test]
    [Arguments("\U0001F600 a", 1, 2)]
    [Arguments("\U0001D400\U0001D400  a", 2, 4)]
    [Arguments("\U0001D400\U0001D400\r\n\U0001D400", 2, 4)]
    public async Task PositionMeasureCountsCodePointsAfterACharacterOutsideTheBasicMultilingualPlane(string text, int measure, int wrap)
    {
        LineBreak first = FirstBreak(text);
        await Assert.That(first.PositionMeasure).IsEqualTo(measure);
        await Assert.That(first.PositionWrap).IsEqualTo(wrap);
    }

    // The measure stops before the whole mandatory break, CR LF being one, and before the spaces in front of it.
    [Test]
    [Arguments("a\r\nb", 1, 3)]
    [Arguments("a \r\nb", 1, 4)]
    [Arguments("a\rb", 1, 2)]
    [Arguments("a\nb", 1, 2)]
    [Arguments("a\u0085b", 1, 2)]
    [Arguments("a\u000Bb", 1, 2)]
    [Arguments("a\u000Cb", 1, 2)]
    [Arguments("a\u2028b", 1, 2)]
    [Arguments("a\u2029b", 1, 2)]
    [Arguments("a  \u0085b", 1, 4)]
    public async Task PositionMeasureStopsBeforeAMandatoryBreak(string text, int measure, int wrap)
    {
        LineBreak first = FirstBreak(text);
        await Assert.That(first.PositionMeasure).IsEqualTo(measure);
        await Assert.That(first.PositionWrap).IsEqualTo(wrap);
        await Assert.That(first.Required).IsTrue();
    }

    // A text that ends in a mandatory break measures as one that goes on after it does (LB3).
    [Test]
    [Arguments("a\r\n", 1, 3)]
    [Arguments("a\u0085", 1, 2)]
    [Arguments("a \r", 1, 3)]
    public async Task PositionMeasureStopsBeforeAMandatoryBreakAtTheEndOfTheText(string text, int measure, int wrap)
    {
        LineBreak first = FirstBreak(text);
        await Assert.That(first.PositionMeasure).IsEqualTo(measure);
        await Assert.That(first.PositionWrap).IsEqualTo(wrap);
        await Assert.That(first.Required).IsTrue();
    }

    private static LineBreak FirstBreak(string text)
    {
        LineBreakEnumerator enumerator = new(text.AsSpan());
        enumerator.MoveNext();
        return enumerator.Current;
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
