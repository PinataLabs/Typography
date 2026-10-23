// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;
using UnicodeTrieGenerator.StateAutomation;

namespace PinataLabs.Typography.Tests.Unicode;

public class DfaTests
{
    [Test]
    public async Task CanCompileWithSingleLiteral()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = a;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 0 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 0, EndIndex = 0 },
            new StateMatch() { StartIndex = 1, EndIndex = 1 },
            new StateMatch() { StartIndex = 3, EndIndex = 3 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithConcatenation()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = a b;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 1, 0, 1, 0 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 1, EndIndex = 2 },
            new StateMatch() { StartIndex = 4, EndIndex = 5 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithAlternation()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = (a b) | (b a);");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 1, 0, 1, 0 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 1, EndIndex = 2 },
            new StateMatch() { StartIndex = 3, EndIndex = 4 },
            new StateMatch() { StartIndex = 5, EndIndex = 6 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithRepeat()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = (a b)+;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 0, 1, 1, 0, 1 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 1, EndIndex = 4 },
            new StateMatch() { StartIndex = 6, EndIndex = 7 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithOptionalRepeat()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = b a (a b)*;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 0, 0, 1, 0, 1, 1, 1, 0 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 2, EndIndex = 7 },
            new StateMatch() { StartIndex = 9, EndIndex = 10 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithExactRepetition()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = a{3} b;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 3, EndIndex = 6 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithMinimumRepetition()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = a{3,} b;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 3, EndIndex = 6 },
            new StateMatch() { StartIndex = 7, EndIndex = 11 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithMaximumRepetition()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = a{,3} b;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 0, EndIndex = 2 },
            new StateMatch() { StartIndex = 3, EndIndex = 6 },
            new StateMatch() { StartIndex = 10, EndIndex = 11 },
            new StateMatch() { StartIndex = 12, EndIndex = 12 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithMinimumAndMaximumRepetition()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = a{3,5} b;");
        StateMatch[] matches = stateMachine.Match(new[] { 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 3, EndIndex = 6 },
            new StateMatch() { StartIndex = 7, EndIndex = 11 }
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }

    [Test]
    public async Task CanCompileWithTags()
    {
        StateMachine stateMachine = Compile.Build("a = 0; b = 1; Main = x:(b a) | y:(a b);");

        int[] input = { 1, 1, 1, 0, 0, 1, 0, 1, 1, 1, 0 };
        StateMatch[] matches = stateMachine.Match(new[] { 1, 1, 1, 0, 0, 1, 0, 1, 1, 1, 0 }).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 2, EndIndex = 3, Tags = new string[] { "x" } },
            new StateMatch() { StartIndex = 4, EndIndex = 5, Tags = new string[] { "y" } },
            new StateMatch() { StartIndex = 6, EndIndex = 7, Tags = new string[] { "y" } },
            new StateMatch() { StartIndex = 9, EndIndex = 10, Tags = new string[] { "x" } },
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);

        List<(string Tag, int Start, int End, ArraySlice<int> Slice)> applied = new();
        Dictionary<string, Action<int, int, ArraySlice<int>>> actions = new()
        {
            { "x", (start, end, slice) => applied.Add(("x", start, end, slice)) },
            { "y", (start, end, slice) => applied.Add(("y", start, end, slice)) }
        };

        stateMachine.Apply(input, actions);

        await Assert.That(applied.Count).IsEqualTo(4);

        List<(string Tag, int Start, int End, ArraySlice<int> Slice)> expectedApply = new()
        {
            ("x", 2, 3, new int[] { 1, 0 }),
            ("y", 4, 5, new int[] { 0, 1 }),
            ("y", 6, 7, new int[] { 0, 1 }),
            ("x", 9, 10, new int[] { 1, 0 }),
        };

        for (int i = 0; i < expectedApply.Count; i++)
        {
            (string Tag, int Start, int End, ArraySlice<int> Slice) e = expectedApply[i];
            (string Tag, int Start, int End, ArraySlice<int> Slice) a = applied[i];

            await Assert.That(a.Tag).IsEqualTo(e.Tag);
            await Assert.That(a.Start).IsEqualTo(e.Start);
            await Assert.That(a.End).IsEqualTo(e.End);
            await Assert.That(a.Slice.ToArray()).IsEquivalentTo(e.Slice.ToArray(), EqualityComparer<int>.Default, CollectionOrdering.Matching);
        }
    }

    [Test]
    public async Task CanCompileWithExternalSymbols()
    {
        var externalSymbols = new Dictionary<string, int>() { { "a", 0 }, { "b", 1 } };
        StateMachine stateMachine = Compile.Build("Main = a b;", externalSymbols);
        int[] input = { 0, 0, 1, 1, 0, 1, 0 };
        StateMatch[] matches = stateMachine.Match(input).ToArray();

        var expected = new StateMatch[]
        {
            new StateMatch() { StartIndex = 1, EndIndex = 2 },
            new StateMatch() { StartIndex = 4, EndIndex = 5 },
        };

        await Assert.That(matches).IsEquivalentTo(expected, EqualityComparer<StateMatch>.Default, CollectionOrdering.Matching);
    }
}
