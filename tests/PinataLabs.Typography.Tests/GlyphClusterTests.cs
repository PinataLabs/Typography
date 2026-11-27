// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Threading.Tasks;
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

    private static int[] Clusters(string text) => Clusters(Collection(text));

    private static int[] Clusters(GlyphSubstitutionCollection collection)
        => Enumerable.Range(0, collection.Count).Select(i => collection[i].Cluster).ToArray();

    private static GlyphSubstitutionCollection Collection(string text)
    {
        Font font = FakeFont.CreateFont("a");
        GlyphSubstitutionCollection collection = new(new TextOptions(font));
        TextRun run = new() { Start = 0, End = text.Length, Font = font };
        int offset = 0;
        int index = 0;
        while (index < text.Length)
        {
            CodePoint codePoint = CodePoint.DecodeFromUtf16At(text, index, out int consumed);
            collection.AddGlyph((ushort)(offset + 1), codePoint, TextDirection.LeftToRight, run, offset, index);
            index += consumed;
            offset++;
        }

        return collection;
    }
}
