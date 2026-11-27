// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests.Unicode;

/// <summary>
/// Script itemization on its own, which has no conformance suite. The cases of the first group are those of the
/// PdfPinata itemizer that this replaces, and give the same runs.
/// </summary>
public class ScriptItemizerTests
{
    private const string Arabic = "\U00000639\U00000631\U00000628\U0000064A";
    private const string Hebrew = "\U000005D0\U000005D1";
    private const string Be = "\U00000431";

    [Test]
    public async Task TextOfOneScriptIsOneRun()
        => await AssertRuns("Hello", new ScriptRun(0, 5, ScriptClass.Latin));

    [Test]
    public async Task AChangeOfScriptStartsANewRun()
        => await AssertRuns("Hi" + Arabic, new ScriptRun(0, 2, ScriptClass.Latin), new ScriptRun(2, 4, ScriptClass.Arabic));

    [Test]
    public async Task PunctuationAndSpacesGoWithTheTextBeforeThem()
        => await AssertRuns("Hi. " + Arabic, new ScriptRun(0, 4, ScriptClass.Latin), new ScriptRun(4, 4, ScriptClass.Arabic));

    [Test]
    public async Task LeadingPunctuationBelongsToTheScriptThatFollowsIt()
        => await AssertRuns("(Hello)", new ScriptRun(0, 7, ScriptClass.Latin));

    [Test]
    public async Task ACombiningMarkTakesTheScriptOfTheLetterItSitsOn()
        => await AssertRuns("e\U00000301", new ScriptRun(0, 2, ScriptClass.Latin));

    [Test]
    public async Task TextWithNoLettersIsOneCommonRun()
        => await AssertRuns("12 + 34!", new ScriptRun(0, 8, ScriptClass.Common));

    [Test]
    public async Task NothingIsNoRuns()
        => await AssertRuns(string.Empty);

    [Test]
    public async Task ASurrogatePairIsOneCharacter()
        => await AssertRuns("A\U0001D400B", new ScriptRun(0, 4, ScriptClass.Latin));

    [Test]
    public async Task AWindowIsItemizedOnItsOwnAndIndexedIntoTheWholeText()
    {
        const string both = Hebrew + Arabic;

        await AssertRuns(both, Hebrew.Length, Arabic.Length, new ScriptRun(2, 4, ScriptClass.Arabic));
    }

    [Test]
    public async Task AWindowSweepsPunctuationIntoItselfAndNotIntoWhatSurroundsIt()
    {
        const string text = "one " + Arabic;

        await AssertRuns(text, 3, 1 + Arabic.Length, new ScriptRun(3, 5, ScriptClass.Arabic));
    }

    [Test]
    public async Task AWindowThatEndsInASurrogatePairIsNotReadPastItsEnd()
    {
        const string text = "A\U0001D400B";

        await AssertRuns(text, 0, 2, new ScriptRun(0, 2, ScriptClass.Latin));
    }

    [Test]
    public async Task AClosingBracketTakesTheScriptOfItsOpeningBracket()
        => await AssertRuns(
            "a (" + Be + ") c",
            new ScriptRun(0, 3, ScriptClass.Latin),
            new ScriptRun(3, 1, ScriptClass.Cyrillic),
            new ScriptRun(4, 3, ScriptClass.Latin));

    [Test]
    public async Task NestedBracketsTakeTheScriptOfTheirOwnOpeningBracket()
        => await AssertRuns(
            "a ([" + Be + "]) c",
            new ScriptRun(0, 4, ScriptClass.Latin),
            new ScriptRun(4, 1, ScriptClass.Cyrillic),
            new ScriptRun(5, 4, ScriptClass.Latin));

    [Test]
    public async Task BracketsBeforeTheFirstLetterTakeItsScript()
        => await AssertRuns("(" + Be + ")", new ScriptRun(0, 3, ScriptClass.Cyrillic));

    [Test]
    public async Task AClosingBracketWithNoOpeningBracketTakesTheScriptBeforeIt()
        => await AssertRuns("a" + Be + ") b", new ScriptRun(0, 1, ScriptClass.Latin), new ScriptRun(1, 3, ScriptClass.Cyrillic), new ScriptRun(4, 1, ScriptClass.Latin));

    [Test]
    public async Task AClosingBracketOfAnotherKindDoesNotCloseTheOpeningBracket()
        => await AssertRuns("a (" + Be + "] c", new ScriptRun(0, 3, ScriptClass.Latin), new ScriptRun(3, 3, ScriptClass.Cyrillic), new ScriptRun(6, 1, ScriptClass.Latin));

    [Test]
    public async Task BracketsThatAreCanonicallyEquivalentPair()
    {
        // U+2329 and U+232A are canonically equivalent to U+3008 and U+3009.
        await AssertRuns(
            "a \U00002329" + Be + "\U00003009",
            new ScriptRun(0, 3, ScriptClass.Latin),
            new ScriptRun(3, 1, ScriptClass.Cyrillic),
            new ScriptRun(4, 1, ScriptClass.Latin));
    }

    private static Task AssertRuns(string text, params ScriptRun[] expected)
        => AssertRuns(text, 0, text.Length, expected);

    private static async Task AssertRuns(string text, int start, int length, params ScriptRun[] expected)
    {
        ScriptRun[] actual = ScriptItemizer.Itemize(text, start, length).ToArray();

        await Assert.That(actual).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }
}
