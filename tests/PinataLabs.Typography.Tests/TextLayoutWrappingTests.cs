// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class TextLayoutWrappingTests
{
    private const float FontSize = 20;

    private static Font CreateFont()
        => new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(FontSize);

    /// <summary>
    /// Lays out the text and returns the text of each rendered line. Blank lines are not represented.
    /// </summary>
    private static List<string> Layout(string text, TextOptions options)
    {
        IReadOnlyList<GlyphLayout> glyphs = TextLayout.GenerateLayout(text, options);
        List<string> lines = [];
        StringBuilder? sb = null;
        foreach (GlyphLayout g in glyphs)
        {
            if (g.IsStartOfLine || sb is null)
            {
                if (sb is not null)
                {
                    lines.Add(sb.ToString());
                }

                sb = new();
            }

            sb.Append(g.CodePoint.ToString());
        }

        if (sb is not null)
        {
            lines.Add(sb.ToString());
        }

        return lines;
    }

    // Counts lines including blank ones, using the height of the laid out text.
    private static int LineCount(string text, TextOptions options)
        => (int)Math.Round(TextMeasurer.MeasureAdvance(text, options).Height / FontSize);

    [Test]
    [Arguments("a")]
    [Arguments("v")]
    [Arguments("i")]
    [Arguments("v\r\ni")]
    [Arguments("i\r\nv")]
    public async Task BreakAllWithWrappingLengthSmallerThanGlyphDoesNotThrow(string text)
    {
        TextOptions options = new(CreateFont()) { WrappingLength = 1, WordBreaking = WordBreaking.BreakAll };

        List<string> lines = Layout(text, options);

        await Assert.That(string.Concat(lines)).IsEqualTo(text.Replace("\r\n", string.Empty));
    }

    [Test]
    public async Task LeadingWhitespaceFollowedByLongWordDoesNotThrow()
    {
        string text = new string(' ', 20) + "NEWS_CATEGORY=EWF&NEWS_HASH=4b298ff9277ef9fdf515356be95ea3caf57cd36&OFFSET=0&SEARCH_VALUE=CA88105E1088&ID_NEWS";
        TextOptions options = new(CreateFont()) { WrappingLength = 300 };

        List<string> lines = Layout(text, options);

        await Assert.That(lines.Count).IsGreaterThan(0);
        await Assert.That(lines.Any(l => l.Contains("ID_NEWS"))).IsTrue();
    }

    [Test]
    public async Task BreakWordSplitsLongWordAfterWhitespaceOnlyLine()
    {
        const string word = "abcdefghijklmnopqrstuvwxyz";
        TextOptions options = new(CreateFont()) { WrappingLength = 100, WordBreaking = WordBreaking.BreakWord };

        List<string> lines = Layout(word + " \r\n " + word, options);

        await Assert.That(lines.Count).IsGreaterThan(2);
        await Assert.That(string.Concat(lines).Replace(" ", string.Empty)).IsEqualTo(word + word);
        await Assert.That(lines.All(l => l.Length > 0)).IsTrue();
    }

    [Test]
    public async Task BreakWordDoesNotSplitWordsThatFit()
    {
        TextOptions options = new(CreateFont()) { WrappingLength = 150, WordBreaking = WordBreaking.BreakWord };

        List<string> lines = Layout("abcdef ghijk lmnopq rstuvwxyz", options);

        await Assert.That(lines.Select(l => l.Trim())).IsEquivalentTo(new[] { "abcdef ghijk", "lmnopq", "rstuvwxyz" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task ExplicitNewlinesAfterLineThatExactlyFitsAreHonoured(int newlines)
    {
        Font font = CreateFont();
        const string first = "- Lorem ipsullll";

        // Make the first line exactly as wide as the wrapping length.
        float exact = TextMeasurer.MeasureAdvance(first, new TextOptions(font)).Width;
        TextOptions options = new(font) { WrappingLength = exact };

        int single = LineCount(first, options);
        int actual = LineCount(first + new string('\n', newlines) + "dolor", options);

        await Assert.That(single).IsEqualTo(1);
        await Assert.That(actual).IsEqualTo(1 + newlines + 1 - 1);
    }

    [Test]
    public async Task ExplicitNewlineNeverAddsLinesAtAnyWrappingLength()
    {
        Font font = CreateFont();
        for (int w = 100; w <= 300; w++)
        {
            TextOptions options = new(font) { WrappingLength = w };
            int one = LineCount("What connects the following:", options);
            int lf = LineCount("What connects the following:\nx", options);
            int crlf = LineCount("What connects the following:\r\nx", options);
            int blank1 = LineCount("- Lorem ipsullll", options);
            int blank2 = LineCount("- Lorem ipsullll\n\ndolor", options);
            int blank3 = LineCount("- Lorem ipsullll\n\n\ndolor", options);

            await Assert.That(lf).IsEqualTo(one + 1);
            await Assert.That(crlf).IsEqualTo(one + 1);
            await Assert.That(blank2).IsEqualTo(blank1 + 2);
            await Assert.That(blank3).IsEqualTo(blank1 + 3);
        }
    }

    [Test]
    public async Task WrappingIsIndependentOfNewlineStyle()
    {
        Font font = CreateFont();
        string[] names = ["- Bill Clinton", "- Richard Nixon", "- Lyndon B. Johnson", "- John F. Kennedy"];
        string[] texts =
        [
            string.Join("\n", names[0], names[1], names[2], names[3]),
            string.Join("\n", names[0], names[3], names[1], names[2]),
        ];

        foreach (string text in texts)
        {
            for (int w = 60; w <= 400; w += 3)
            {
                TextOptions options = new(font) { WrappingLength = w };
                List<string> lf = Layout(text, options);
                List<string> crlf = Layout(text.Replace("\n", "\r\n"), options);

                await Assert.That(crlf.Select(l => l.Trim('\r', '\n'))).IsEquivalentTo(lf.Select(l => l.Trim('\r', '\n')), TUnit.Assertions.Enums.CollectionOrdering.Matching);
                await Assert.That(new string(string.Concat(lf).Where(c => !char.IsWhiteSpace(c)).ToArray())).IsEqualTo(new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray()));
            }
        }
    }

    [Test]
    public async Task WrappedLinesDoNotKeepTrailingWhitespace()
    {
        Font font = CreateFont();
        const string text = "This text should break     into two lines of text ok";
        for (int w = 60; w <= 400; w += 7)
        {
            List<string> lines = Layout(text, new TextOptions(font) { WrappingLength = w });
            foreach (string line in lines.Take(lines.Count - 1))
            {
                await Assert.That(line.EndsWith(' ')).IsFalse();
            }
        }
    }

    [Test]
    public async Task SolidusDoesNotBreakWordWhenEarlierSpaceIsAvailable()
    {
        Font font = CreateFont();
        TextOptions options = new(font) { WrappingLength = 150 };

        List<string> lines = Layout("aaaaa bbbbb/ccccc ddddd", options);

        await Assert.That(lines.Select(l => l.Trim())).IsEquivalentTo(new[] { "aaaaa", "bbbbb/ccccc", "ddddd" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task SolidusBreaksLongWordWhenNoEarlierOpportunity()
    {
        Font font = CreateFont();
        TextOptions options = new(font) { WrappingLength = 80 };

        List<string> lines = Layout("bbbbb/ccccc", options);

        await Assert.That(lines.Select(l => l.Trim())).IsEquivalentTo(new[] { "bbbbb/", "ccccc" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
