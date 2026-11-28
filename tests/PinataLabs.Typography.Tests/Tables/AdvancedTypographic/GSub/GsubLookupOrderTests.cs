// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.Shapers;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GSub;

/// <summary>
/// GsubLookupOrder.ttf lists its lookups as <c>vrtr</c>, <c>liga</c>, <c>calt</c>.
/// The shaper plans these features in a different order, so each expected glyph
/// is only reached when the lookups are applied in LookupList order.
/// </summary>
public class GsubLookupOrderTests
{
    // Glyph ids in GsubLookupOrder.ttf, in the glyph order of tests/tools/generate_gsub_lookup_order_font.py.
    private const ushort F = 1;
    private const ushort I = 2;
    private const ushort LigatureFI = 7;
    private const ushort RotatedLigatureFI = 8;
    private const ushort AlternateA = 9;
    private const ushort LigatureAB = 10;
    private const ushort C = 11;
    private const ushort Copyright = 13;
    private const ushort LigatureCCopyright = 14;
    private const ushort LigatureDCopyright = 15;
    private const ushort BehAfterRtlmThenCcmp = 18;

    private static List<ushort> GlyphIds(string text, LayoutMode mode, params Tag[] features)
    {
        FontFamily family = new FontCollection().Add(TestFonts.GsubLookupOrderFile);
        TextOptions options = new(family.CreateFont(30)) { LayoutMode = mode, FeatureTags = features };
        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, text, options);
        return renderer.GlyphKeys.Select(k => k.GlyphId).ToList();
    }

    [Test]
    [Arguments(LayoutMode.VerticalMixedRightLeft)]
    [Arguments(LayoutMode.VerticalMixedLeftRight)]
    public async Task RotatedLigatureUsesVrtrFormWhenVrtrLookupPrecedesLiga(LayoutMode mode)
        => await Assert.That(GlyphIds("fi", mode)).IsEquivalentTo(new[] { RotatedLigatureFI });

    [Test]
    public async Task HorizontalLigatureKeepsHorizontalForm()
        => await Assert.That(GlyphIds("fi", LayoutMode.HorizontalTopBottom)).IsEquivalentTo(new[] { LigatureFI });

    [Test]
    public async Task LigaLookupRunsBeforeLaterCaltLookup()
        => await Assert.That(GlyphIds("ab", LayoutMode.HorizontalTopBottom)).IsEquivalentTo(new[] { LigatureAB });

    [Test]
    public async Task LaterLookupStillAppliesWhereEarlierLookupDoesNotMatch()
        => await Assert.That(GlyphIds("af", LayoutMode.HorizontalTopBottom)).IsEquivalentTo(new[] { AlternateA, F });

    [Test]
    public async Task LookupIsNotAppliedToGlyphWithoutItsFeature()
        => await Assert.That(GlyphIds("if", LayoutMode.HorizontalTopBottom)).IsEquivalentTo(new[] { I, F });

    // The letter has liga and ss01, and the copyright sign ss01 alone. Both features reference the ligature lookup, so
    // it forms the ligature, as in HarfBuzz, where the mask of a lookup has the bits of each feature that references it.
    [Test]
    [Arguments("c©", LigatureCCopyright)]
    [Arguments("d©", LigatureDCopyright)]
    public async Task LookupOfTwoFeaturesFormsLigatureWhenAComponentHasOnlyOneOfThem(string text, int ligature)
    {
        Font font = new FontCollection().Add(TestFonts.GsubLookupOrderFile).CreateFont(30);
        FontMetrics fontMetrics = font.FontMetrics;
        Tag liga = Tag.Parse("liga");
        Tag ss01 = Tag.Parse("ss01");
        GlyphSubstitutionCollection collection = new(new TextOptions(font)) { FontMetrics = fontMetrics };
        TextRun run = new() { Start = 0, End = text.Length, Font = font };
        for (int i = 0; i < text.Length; i++)
        {
            CodePoint codePoint = CodePoint.DecodeFromUtf16At(text, i);
            fontMetrics.TryGetGlyphId(codePoint, out ushort glyphId);
            collection.AddGlyph(glyphId, codePoint, TextDirection.LeftToRight, run, i, i, i);
            collection.AddShapingFeature(i, new TagEntry(ss01, true));
        }

        collection.AddShapingFeature(0, new TagEntry(liga, true));
        SkippingGlyphIterator iterator = new(fontMetrics, collection, 0, default);
        int count = collection.Count;
        int operations = 0;
        fontMetrics.GetGSubTable().ApplyFeatures(fontMetrics, collection, ref iterator, [new(liga), new(ss01)], [], false, ScriptClass.Latin, 0, ref count, 64, 1024, ref operations);

        await Assert.That(Enumerable.Range(0, collection.Count).Select(i => (int)collection[i].GlyphId)).IsEquivalentTo(new[] { ligature });
    }

    // In vertical mixed layout the letter is rotated and the copyright sign upright, so they are runs of two
    // orientations, which HarfBuzz shapes in two buffers. No ligature forms across them.
    [Test]
    public async Task LigatureIsNotFormedAcrossRunsOfTwoOrientations()
        => await Assert.That(GlyphIds("c©", LayoutMode.VerticalMixedRightLeft, Tag.Parse("ss01")))
            .IsEquivalentTo(new[] { C, Copyright });

    [Test]
    public async Task LigatureIsNotFormedWhenAComponentHasNoFeatureOfTheLookup()
        => await Assert.That(GlyphIds("c©", LayoutMode.VerticalMixedRightLeft).Count).IsEqualTo(2);

    [Test]
    public async Task ArabicDirectionalLookupsRunBeforeAnEarlierCcmpLookup()
        => await Assert.That(GlyphIds("ب", LayoutMode.HorizontalTopBottom)).IsEquivalentTo(new[] { BehAfterRtlmThenCcmp });

    [Test]
    public async Task ArabicShaperLeavesContextualAlternatesToTheLayoutMode()
    {
        // The Arabic shaper orders calt and rclt in a stage of their own. That must not
        // assign them to upright glyphs in vertical layout, which only get vert.
        const string text = "\u0628";
        Font font = FakeFont.CreateFont(text);
        TextOptions options = new(font) { LayoutMode = LayoutMode.VerticalRightLeft };
        GlyphSubstitutionCollection collection = new(options);
        collection.AddGlyph(1, CodePoint.DecodeFromUtf16At(text, 0), TextDirection.RightToLeft, new TextRun { Start = 0, End = 1, Font = font }, 0, 0, 0);

        new ArabicShaper(ScriptClass.Arabic, options).Plan(collection, 0, 1);

        List<string> tags = collection[0].Features.Select(f => f.Tag.ToString()).ToList();
        await Assert.That(tags).Contains("vert");
        await Assert.That(tags).Contains("rlig");
        await Assert.That(tags).DoesNotContain("calt");
        await Assert.That(tags).DoesNotContain("rclt");
    }
}
