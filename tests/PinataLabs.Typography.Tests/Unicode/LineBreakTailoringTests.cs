// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Unicode;

public class LineBreakTailoringTests
{
    public const LineBreakTailoring Both = LineBreakTailoring.SouthEastAsianAsIdeographs | LineBreakTailoring.HangulAsAlphabetic;

    [Test]
    [Arguments("\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35", LineBreakTailoring.None, new[] { 6 })]
    [Arguments("\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35", LineBreakTailoring.SouthEastAsianAsIdeographs, new[] { 1, 3, 4, 6 })]
    [Arguments("\u0E2A\u0E27\u0E31\u0E2A\u0E14\u0E35", LineBreakTailoring.HangulAsAlphabetic, new[] { 6 })]
    [Arguments("\u0E81\u0E82", LineBreakTailoring.SouthEastAsianAsIdeographs, new[] { 1, 2 })]
    [Arguments("\u1000\u1001", LineBreakTailoring.SouthEastAsianAsIdeographs, new[] { 1, 2 })]
    [Arguments("\u1780\u1781", LineBreakTailoring.SouthEastAsianAsIdeographs, new[] { 1, 2 })]
    [Arguments("\uD55C\uAD6D\uC5B4 \uD14D\uC2A4\uD2B8", LineBreakTailoring.None, new[] { 1, 2, 4, 5, 6, 7 })]
    [Arguments("\uD55C\uAD6D\uC5B4 \uD14D\uC2A4\uD2B8", LineBreakTailoring.HangulAsAlphabetic, new[] { 4, 7 })]
    [Arguments("\uD55C\uAD6D\uC5B4 \uD14D\uC2A4\uD2B8", LineBreakTailoring.SouthEastAsianAsIdeographs, new[] { 1, 2, 4, 5, 6, 7 })]
    [Arguments("\u1112\u1161\u11AB\u1100\u116E\u11A8", LineBreakTailoring.None, new[] { 3, 6 })]
    [Arguments("\u1112\u1161\u11AB\u1100\u116E\u11A8", LineBreakTailoring.HangulAsAlphabetic, new[] { 6 })]
    [Arguments("\u6F22\u5B57", LineBreakTailoring.HangulAsAlphabetic, new[] { 1, 2 })]
    [Arguments("\u0E2A\u0E27 \uD55C\uAD6D", Both, new[] { 1, 3, 5 })]
    public async Task ATailoringChangesWhereTheTextBreaks(string text, LineBreakTailoring tailoring, int[] expected)
    {
        List<int> breaks = [];
        foreach (LineBreak lineBreak in new LineBreakEnumerator(text.AsSpan(), tailoring))
        {
            breaks.Add(lineBreak.PositionWrap);
        }

        await Assert.That(breaks.ToArray()).IsEquivalentTo(expected);
    }

    // The tailored breaks are the algorithm's, not a different one: mandatory breaks stay mandatory.
    [Test]
    public async Task ATailoredTextKeepsItsMandatoryBreaks()
    {
        List<(int, bool)> breaks = [];
        foreach (LineBreak lineBreak in new LineBreakEnumerator("\u0E2A\u0E27\n\u0E2A".AsSpan(), Both))
        {
            breaks.Add((lineBreak.PositionWrap, lineBreak.Required));
        }

        await Assert.That(breaks.ToArray()).IsEquivalentTo(new[] { (1, false), (3, true), (4, false) });
    }

    [Test]
    public async Task NoTailoringIsTheDefault()
    {
        const string text = "\u0E2A\u0E27 \uD55C\uAD6D\uC5B4 A";
        await Assert.That(Breaks(text, LineBreakTailoring.None)).IsEqualTo(Breaks(text, default));
        await Assert.That(Breaks(text, LineBreakTailoring.None)).IsNotEqualTo(Breaks(text, Both));
    }

    // A tailoring names classes. Every code point outside them breaks as it does with no tailoring, beside
    // characters of every other class, and so does a combination of the two tailorings outside both.
    [Test]
    public async Task ATailoringChangesNoCodePointOutsideItsClasses()
    {
        string[] neighbours = ["A", "\u4E00", "1", ")", "(", "\"", " "];
        List<string> differing = [];
        Span<char> text = stackalloc char[8];
        for (int value = 0; value <= 0x10FFFF; value++)
        {
            if (value is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            LineBreakClass cls = CodePoint.GetLineBreakClass(new CodePoint(value));
            bool southEastAsian = cls == LineBreakClass.SA;
            bool hangul = cls is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3;
            foreach (string neighbour in neighbours)
            {
                for (int side = 0; side < 2; side++)
                {
                    int length = Compose(value, neighbour, side == 0, text);
                    ReadOnlySpan<char> sample = text[..length];
                    ulong none = Breaks(sample, LineBreakTailoring.None);
                    if ((!southEastAsian && Breaks(sample, LineBreakTailoring.SouthEastAsianAsIdeographs) != none)
                        || (!hangul && Breaks(sample, LineBreakTailoring.HangulAsAlphabetic) != none)
                        || (!southEastAsian && !hangul && Breaks(sample, Both) != none))
                    {
                        differing.Add($"U+{value:X4} {cls} beside {neighbour}");
                    }
                }
            }
        }

        await Assert.That(differing).IsEmpty();
    }

    // An SA letter breaks as an ideograph does, whatever it is beside. The ideograph is one that is not East Asian
    // wide, as the letters are not, because the tailoring changes the class and leaves the width alone.
    [Test]
    public async Task ASouthEastAsianLetterBreaksAsAnIdeographDoes()
    {
        int ideograph = FirstOf(LineBreakClass.ID, eastAsian: false);
        List<int> neighbours = ExcludingTailoredClasses(hangul: false);
        HashSet<Property> deep = [];
        List<string> differing = [];
        int checkedLetters = 0;
        foreach (int letter in CodePointsOf(LineBreakClass.SA))
        {
            if (IsMark(letter))
            {
                continue;
            }

            checkedLetters++;
            CompareWith(letter, ideograph, LineBreakTailoring.SouthEastAsianAsIdeographs, neighbours, deep.Add(Properties(letter)), differing);
        }

        await Assert.That(checkedLetters).IsGreaterThan(100);
        await Assert.That(differing).IsEmpty();
    }

    // The marks of the class stay marks of the letter before them, as LB1 has it.
    [Test]
    public async Task ASouthEastAsianMarkStaysACombiningMark()
    {
        int mark = FirstOf(LineBreakClass.CM, eastAsian: false);
        List<int> neighbours = ExcludingTailoredClasses(hangul: false);
        HashSet<Property> deep = [];
        List<string> differing = [];
        int checkedMarks = 0;
        foreach (int value in CodePointsOf(LineBreakClass.SA))
        {
            if (!IsMark(value))
            {
                continue;
            }

            checkedMarks++;
            CompareWith(value, mark, LineBreakTailoring.SouthEastAsianAsIdeographs, neighbours, deep.Add(Properties(value)), differing);
        }

        await Assert.That(checkedMarks).IsGreaterThan(100);
        await Assert.That(differing).IsEmpty();
    }

    // Hangul syllables and jamo break as a letter does. Syllables of one class break alike, so a sample of the
    // 11,172 of them stands for the lot; the previous test has already tried every code point beside a few.
    [Test]
    public async Task AHangulCodePointBreaksAsALetterDoes()
    {
        int letter = FirstOf(LineBreakClass.AL, eastAsian: true);
        List<int> neighbours = ExcludingTailoredClasses(hangul: true);
        HashSet<Property> deep = [];
        List<string> differing = [];
        int checkedCodePoints = 0;
        int syllable = 0;
        foreach (LineBreakClass cls in new[] { LineBreakClass.JL, LineBreakClass.JV, LineBreakClass.JT, LineBreakClass.H2, LineBreakClass.H3 })
        {
            foreach (int value in CodePointsOf(cls))
            {
                if (cls is LineBreakClass.H2 or LineBreakClass.H3 && syllable++ % 97 != 0)
                {
                    continue;
                }

                checkedCodePoints++;
                CompareWith(value, letter, LineBreakTailoring.HangulAsAlphabetic, neighbours, deep.Add(Properties(value)), differing);
            }
        }

        await Assert.That(checkedCodePoints).IsGreaterThan(300);
        await Assert.That(differing).IsEmpty();
    }

    // Breaks of a text as a bit for each position, and another 32 up for those that are required.
    private static ulong Breaks(ReadOnlySpan<char> text, LineBreakTailoring tailoring)
    {
        ulong breaks = 0;
        var enumerator = new LineBreakEnumerator(text, tailoring);
        while (enumerator.MoveNext())
        {
            breaks |= 1UL << (enumerator.Current.PositionWrap + (enumerator.Current.Required ? 32 : 0));
        }

        return breaks;
    }

    private static ulong Breaks(string text, LineBreakTailoring tailoring) => Breaks(text.AsSpan(), tailoring);

    private static int Compose(int value, string neighbour, bool before, Span<char> text)
    {
        int length = 0;
        if (!before)
        {
            neighbour.AsSpan().CopyTo(text);
            length = neighbour.Length;
        }

        length += Write(value, text[length..]);
        if (before)
        {
            neighbour.AsSpan().CopyTo(text[length..]);
            length += neighbour.Length;
        }

        return length;
    }

    private static int Write(int value, Span<char> text)
    {
        if (value < 0x10000)
        {
            text[0] = (char)value;
            return 1;
        }

        value -= 0x10000;
        text[0] = (char)(0xD800 + (value >> 10));
        text[1] = (char)(0xDC00 + (value & 0x3FF));
        return 2;
    }

    private static IEnumerable<int> CodePointsOf(LineBreakClass cls)
    {
        for (int value = 0; value <= 0x10FFFF; value++)
        {
            if (value is not (>= 0xD800 and <= 0xDFFF) && CodePoint.GetLineBreakClass(new CodePoint(value)) == cls)
            {
                yield return value;
            }
        }
    }

    private static bool IsMark(int value)
        => CodePoint.GetGeneralCategory(new CodePoint(value)) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    private static bool IsEastAsian(int value)
        => TrieValueLayout.IsEastAsianWidth(UnicodeData.GetLineBreakValue((uint)value));

    private static int FirstOf(LineBreakClass cls, bool eastAsian)
    {
        foreach (int value in CodePointsOf(cls))
        {
            if (IsEastAsian(value) == eastAsian && CodePoint.GetGeneralCategory(new CodePoint(value)) != UnicodeCategory.OtherNotAssigned)
            {
                return value;
            }
        }

        throw new InvalidOperationException($"No assigned {cls} that is {(eastAsian ? string.Empty : "not ")}East Asian.");
    }

    // One code point for each class, East Asian width and general category: what the rules tell code points apart by.
    private static List<int> ExcludingTailoredClasses(bool hangul)
    {
        HashSet<Property> seen = [];
        List<int> neighbours = [];
        for (int value = 0; value <= 0x10FFFF; value++)
        {
            if (value is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            LineBreakClass cls = CodePoint.GetLineBreakClass(new CodePoint(value));
            bool tailored = hangul
                ? cls is LineBreakClass.JL or LineBreakClass.JV or LineBreakClass.JT or LineBreakClass.H2 or LineBreakClass.H3
                : cls == LineBreakClass.SA;
            if (!tailored && seen.Add(Properties(value)))
            {
                neighbours.Add(value);
            }
        }

        return neighbours;
    }

    // The code point, tailored, breaks as the model does untailored, alone and beside every neighbour. When deep, it is
    // also between a neighbour and one of each class, which reaches the rules that read two characters on. That is
    // for the first code point of each set of properties, since the rules tell code points apart by nothing else.
    private static void CompareWith(int value, int model, LineBreakTailoring tailoring, List<int> neighbours, bool deep, List<string> differing)
    {
        List<int> wholeClasses = deep ? OnePerClass(neighbours) : [];
        Span<char> actual = stackalloc char[8];
        Span<char> expected = stackalloc char[8];
        CheckOne(actual[..Build(actual, -1, value, -1)], expected[..Build(expected, -1, model, -1)], tailoring, value, "alone", differing);
        foreach (int first in neighbours)
        {
            CheckOne(actual[..Build(actual, -1, value, first)], expected[..Build(expected, -1, model, first)], tailoring, value, $"before U+{first:X4}", differing);
            CheckOne(actual[..Build(actual, first, value, -1)], expected[..Build(expected, first, model, -1)], tailoring, value, $"after U+{first:X4}", differing);
            foreach (int second in wholeClasses)
            {
                CheckOne(actual[..Build(actual, first, value, second)], expected[..Build(expected, first, model, second)], tailoring, value, $"between U+{first:X4} and U+{second:X4}", differing);
            }
        }
    }

    // The code points that are not -1, in order.
    private static int Build(Span<char> text, int first, int middle, int last)
    {
        int length = 0;
        foreach (int value in new[] { first, middle, last })
        {
            if (value >= 0)
            {
                length += Write(value, text[length..]);
            }
        }

        return length;
    }

    private static Property Properties(int value)
        => new(CodePoint.GetLineBreakClass(new CodePoint(value)), IsEastAsian(value), CodePoint.GetGeneralCategory(new CodePoint(value)));

    private static List<int> OnePerClass(List<int> neighbours)
    {
        HashSet<LineBreakClass> seen = [];
        return neighbours.FindAll(value => seen.Add(CodePoint.GetLineBreakClass(new CodePoint(value))));
    }

    private static void CheckOne(ReadOnlySpan<char> actual, ReadOnlySpan<char> expected, LineBreakTailoring tailoring, int value, string where, List<string> differing)
    {
        if (Breaks(actual, tailoring) != Breaks(expected, LineBreakTailoring.None) && differing.Count < 20)
        {
            differing.Add($"U+{value:X4} {where}");
        }
    }

    // What the rules tell one code point from another by.
    private readonly record struct Property(LineBreakClass Class, bool EastAsian, UnicodeCategory Category);
}
