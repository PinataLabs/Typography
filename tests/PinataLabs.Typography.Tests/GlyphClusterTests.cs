// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The cluster of a glyph is the UTF-16 index of the first character it stands for. As in HarfBuzz at its default cluster
/// level, a mark, a joiner, an emoji modifier or a tag character shares the cluster of the character before it, and a
/// ligature or a reordering gives the glyphs it joins the lowest cluster among them.
/// </summary>
public class GlyphClusterTests
{
    [Test]
    public async Task MarkSharesTheClusterOfItsBase()
        => await Assert.That(Clusters("a\u0301b")).IsEquivalentTo(new[] { 0, 0, 2 });

    [Test]
    public async Task ClusterIsAUtf16Index()
        => await Assert.That(Clusters("a\U0001F600b")).IsEquivalentTo(new[] { 0, 1, 3 });

    [Test]
    public async Task ZeroWidthJoinerSharesTheClusterOfTheCharacterBeforeIt()
        => await Assert.That(Clusters("a\u200Db\u200Cc")).IsEquivalentTo(new[] { 0, 0, 2, 3, 4 });

    [Test]
    public async Task HalfwidthKatakanaSoundMarkShareTheClusterOfTheCharacterBeforeThem()
        => await Assert.That(Clusters("ｶﾞﾊﾟa")).IsEquivalentTo(new[] { 0, 0, 2, 2, 4 });

    [Test]
    public async Task SpacingMarkThatUnicodeKeepsForCanonicalEquivalenceSharesTheClusterOfTheCharacterBeforeIt()
        => await Assert.That(Clusters("一াa")).IsEquivalentTo(new[] { 0, 0, 2 });

    [Test]
    public async Task EmojiModifierAndTagShareTheClusterOfTheirBase()
        => await Assert.That(Clusters("\U0001F44D\U0001F3FDa\U0001F3F4\U000E0067")).IsEquivalentTo(new[] { 0, 0, 4, 5, 5 });

    [Test]
    public async Task PictographAfterAJoinerSharesTheClusterOfTheCharacterBeforeTheJoiner()
        => await Assert.That(Clusters("a\u200D\U0001F468b")).IsEquivalentTo(new[] { 0, 0, 0, 4 });

    [Test]
    public async Task SecondRegionalIndicatorOfEachPairSharesTheClusterOfTheFirst()
        => await Assert.That(Clusters("\U0001F1E9\U0001F1EA\U0001F1EB\U0001F1F7\U0001F1E9")).IsEquivalentTo(new[] { 0, 0, 4, 4, 8 });

    [Test]
    public async Task MarkAfterTheStartOfTheRunTakesItsOwnIndex()
        => await Assert.That(Clusters("\u0301a")).IsEquivalentTo(new[] { 0, 1 });

    [Test]
    public async Task LigatureTakesTheLowestClusterOfItsComponentsAndTheGlyphsBetweenThem()
    {
        GlyphSubstitutionCollection collection = Collection("fa\u0301i!");

        // A ligature of the glyphs at 0 and 3, over the mark that sits between them.
        collection.Replace(0, new[] { 3 }, 100, 1);

        await Assert.That(Clusters(collection)).IsEquivalentTo(new[] { 0, 0, 0, 4 });
    }

    [Test]
    public async Task MergeExtendsToTheGlyphsThatShareTheClusterOfItsEnds()
    {
        // The glyphs of b and its mark share a cluster, so merging a with b brings the mark with it.
        GlyphSubstitutionCollection collection = Collection("ab\u0301c");

        collection.MergeClusters(0, 2);

        await Assert.That(Clusters(collection)).IsEquivalentTo(new[] { 0, 0, 0, 3 });
    }

    [Test]
    public async Task MovingAGlyphMergesTheClustersItMovesOver()
    {
        GlyphSubstitutionCollection collection = Collection("abcd");

        collection.MoveGlyph(2, 0);

        await Assert.That(Clusters(collection)).IsEquivalentTo(new[] { 0, 0, 0, 3 });
        await Assert.That(collection.Count).IsEqualTo(4);
    }

    // In vertical mixed text U+5B57 and U+4E00 are upright, a is sideways, and each sideways mark takes the orientation
    // of its grapheme cluster. A reordering that moves U+4E00 before U+5B57, as a shaper moves a pre-base sign, leaves
    // the mark of U+4E00 after the mark of U+5B57, where it keeps the orientation of U+4E00. So the reordered glyphs stay
    // one upright run.
    [Test]
    public async Task AReorderingJoinsTheGraphemeClustersItMovesOver()
    {
        GlyphSubstitutionCollection collection = Collection("\u5B57\u0301\u4E00\u0302a", LayoutMode.VerticalMixedLeftRight);
        collection.ResolveRunOrientations();

        collection.MoveGlyph(2, 0);

        await Assert.That(RunLengths(collection)).IsEquivalentTo(new[] { 4, 1 });
    }

    private static int[] Clusters(string text) => Clusters(Collection(text));

    private static int[] Clusters(GlyphSubstitutionCollection collection)
        => Enumerable.Range(0, collection.Count).Select(i => collection[i].Cluster).ToArray();

    private static int[] RunLengths(GlyphSubstitutionCollection collection)
    {
        List<int> lengths = [];
        ShapingRunEnumerator runs = new(collection);
        while (runs.MoveNext())
        {
            lengths.Add(runs.Count);
        }

        return [.. lengths];
    }

    private static GlyphSubstitutionCollection Collection(string text, LayoutMode layoutMode = LayoutMode.HorizontalTopBottom)
    {
        Font font = FakeFont.CreateFont("a");
        GlyphSubstitutionCollection collection = new(new TextOptions(font) { LayoutMode = layoutMode });
        TextRun run = new() { Start = 0, End = text.Length, Font = font };
        int offset = 0;
        int index = 0;
        int grapheme = 0;
        SpanGraphemeEnumerator graphemes = new(text);
        while (graphemes.MoveNext())
        {
            SpanCodePointEnumerator codePoints = new(graphemes.Current);
            while (codePoints.MoveNext())
            {
                collection.AddGlyph((ushort)(offset + 1), codePoints.Current, TextDirection.LeftToRight, run, offset, index, grapheme);
                index += codePoints.Current.Utf16SequenceLength;
                offset++;
            }

            grapheme++;
        }

        return collection;
    }
}
