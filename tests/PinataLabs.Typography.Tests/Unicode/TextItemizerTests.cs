// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests.Unicode;

/// <summary>
/// Cutting mixed text into runs of one script and one direction, in the order they are drawn. The cases are those of
/// the PdfPinata itemizer that this replaces, with the runs that it gives.
/// </summary>
public class TextItemizerTests
{
    private const string Arabic = "\U00000639\U00000631\U00000628\U0000064A";
    private const string Hebrew = "\U000005D0\U000005D1";
    private const string Joined = "\U00000639\U0000200D\U00000631";
    private const string ByteOrderMark = "\U0000FEFF";
    private const string Be = "\U00000431";

    [Test]
    public async Task ARunIsBothOneDirectionAndOneScript()
        => await AssertRuns(
            "Hi " + Arabic,
            BidiParagraphDirection.Auto,
            new ItemizedRun(0, 3, ScriptClass.Latin, 0),
            new ItemizedRun(3, 4, ScriptClass.Arabic, 1));

    [Test]
    public async Task TheRunsComeBackInTheOrderTheyAreDrawn()
    {
        // The Latin was written last and is drawn leftmost, in a right to left paragraph.
        await AssertRuns(
            Arabic + " Hi",
            BidiParagraphDirection.RightToLeft,
            new ItemizedRun(5, 2, ScriptClass.Latin, 2),
            new ItemizedRun(0, 5, ScriptClass.Arabic, 1));
    }

    [Test]
    public async Task TwoRightToLeftScriptsSideBySideAreTwoRunsDrawnRightToLeft()
        => await AssertRuns(
            Hebrew + Arabic,
            BidiParagraphDirection.RightToLeft,
            new ItemizedRun(2, 4, ScriptClass.Arabic, 1),
            new ItemizedRun(0, 2, ScriptClass.Hebrew, 1));

    [Test]
    public async Task ASpaceInsideARightToLeftRunDoesNotCutIt()
    {
        // Asked of the paragraph, the space goes with the Latin before it, and the bidi algorithm then draws it inside the Arabic.
        await AssertRuns(
            "one " + Arabic,
            BidiParagraphDirection.RightToLeft,
            new ItemizedRun(3, 5, ScriptClass.Arabic, 1),
            new ItemizedRun(0, 3, ScriptClass.Latin, 2));
    }

    [Test]
    public async Task LatinHebrewAndArabicAreCutAndOrdered()
    {
        const string text = "abc " + Hebrew + " " + "\U00000639\U00000631" + " def";

        await AssertRuns(
            text,
            BidiParagraphDirection.Auto,
            new ItemizedRun(0, 4, ScriptClass.Latin, 0),
            new ItemizedRun(7, 2, ScriptClass.Arabic, 1),
            new ItemizedRun(4, 3, ScriptClass.Hebrew, 1),
            new ItemizedRun(9, 4, ScriptClass.Latin, 0));
    }

    [Test]
    public async Task AJoiningControlStaysInsideTheRunItSitsIn()
        => await AssertRuns(Joined, BidiParagraphDirection.RightToLeft, new ItemizedRun(0, 3, ScriptClass.Arabic, 1));

    [Test]
    public async Task AnyOtherRemovedCharacterEndsTheRunAndIsInNoRun()
        => await AssertRuns(
            "ab" + ByteOrderMark + "cd",
            BidiParagraphDirection.Auto,
            new ItemizedRun(0, 2, ScriptClass.Latin, 0),
            new ItemizedRun(3, 2, ScriptClass.Latin, 0));

    [Test]
    public async Task EveryCharacterIsInExactlyOneRun()
    {
        const string mixed = "Hi " + Arabic + " 12 " + Hebrew + "!";

        ItemizedRun[] runs = TextItemizer.Itemize(mixed, BidiParagraphDirection.Auto);

        await Assert.That(runs.SelectMany(run => Enumerable.Range(run.Start, run.Length)).OrderBy(index => index))
            .IsEquivalentTo(Enumerable.Range(0, mixed.Length), CollectionOrdering.Matching);
    }

    [Test]
    public async Task APairedBracketTakesTheScriptOfItsOpeningBracketInsideOneBidiRun()
    {
        // The brackets are left to right text round a Hebrew word and an Arabic word in a right to left paragraph.
        const string text = "\U000005D0 (\U00000639\U00000631) \U000005D1";

        await AssertRuns(
            text,
            BidiParagraphDirection.RightToLeft,
            new ItemizedRun(5, 3, ScriptClass.Hebrew, 1),
            new ItemizedRun(3, 2, ScriptClass.Arabic, 1),
            new ItemizedRun(0, 3, ScriptClass.Hebrew, 1));
    }

    [Test]
    public async Task ALeftToRightParagraphOfPlainTextIsOneRun()
        => await AssertRuns("The quick brown fox.", BidiParagraphDirection.Auto, new ItemizedRun(0, 20, ScriptClass.Latin, 0));

    [Test]
    public async Task NothingIsNoRuns()
        => await Assert.That(TextItemizer.Itemize(string.Empty, BidiParagraphDirection.Auto)).IsEmpty();

    [Test]
    public async Task ALineOfAParagraphHasRuleL1AppliedToIt()
    {
        // Two Arabic words with a space between them, in a left to right paragraph.
        const string text = "\U00000627\U00000628 \U0000062A\U0000062B";
        BidiParagraph paragraph = BidiParagraph.Resolve(text, BidiParagraphDirection.LeftToRight);

        await Assert.That(TextItemizer.Itemize(paragraph, 0, text.Length))
            .IsEquivalentTo([new ItemizedRun(0, 5, ScriptClass.Arabic, 1)], CollectionOrdering.Matching);
        await Assert.That(TextItemizer.Itemize(paragraph, 0, 3))
            .IsEquivalentTo([new ItemizedRun(0, 2, ScriptClass.Arabic, 1), new ItemizedRun(2, 1, ScriptClass.Common, 0)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ANullParagraphThrows()
        => await Assert.That(() => TextItemizer.Itemize(null!, 0, 0)).Throws<ArgumentNullException>();

    [Test]
    public async Task ALineOutsideTheTextThrows()
    {
        BidiParagraph paragraph = BidiParagraph.Resolve("abc", BidiParagraphDirection.Auto);

        await Assert.That(() => TextItemizer.Itemize(paragraph, 2, 2)).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task ACharacterWithNoScriptInsideAWordDoesNotCutIt()
        => await AssertRuns("ab\uE000cd", BidiParagraphDirection.Auto, new ItemizedRun(0, 5, ScriptClass.Latin, 0));

    [Test]
    public async Task AJoiningControlBetweenTwoLevelsIsShapedWithTheLetterItShapes()
        => await AssertRuns(
            "a \u200D\u0628",
            BidiParagraphDirection.LeftToRight,
            new ItemizedRun(0, 2, ScriptClass.Latin, 0),
            new ItemizedRun(2, 2, ScriptClass.Arabic, 1));

    [Test]
    public async Task ALineThatSplitsASurrogatePairThrows()
    {
        BidiParagraph paragraph = BidiParagraph.Resolve("a\U00010800b", BidiParagraphDirection.Auto);

        await Assert.That(() => TextItemizer.Itemize(paragraph, 2, 2)).Throws<ArgumentOutOfRangeException>();
    }

    private static async Task AssertRuns(string text, BidiParagraphDirection direction, params ItemizedRun[] expected)
        => await Assert.That(TextItemizer.Itemize(text, direction)).IsEquivalentTo(expected, CollectionOrdering.Matching);
}
