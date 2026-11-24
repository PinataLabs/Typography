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
    private const ushort LigatureCCopyright = 14;
    private const ushort LigatureDCopyright = 15;

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

    // In vertical mixed layout the rotated letter has liga and ss01, and the upright copyright sign
    // has ss01 but not liga. Both features reference the ligature lookup.
    [Test]
    public async Task LookupOfTwoFeaturesFormsLigatureWhenAComponentHasOnlyOneOfThem()
        => await Assert.That(GlyphIds("c©", LayoutMode.VerticalMixedRightLeft, Tag.Parse("ss01")))
            .IsEquivalentTo(new[] { LigatureCCopyright });

    [Test]
    public async Task NestedLookupOfTwoFeaturesFormsLigatureWhenAComponentHasOnlyOneOfThem()
        => await Assert.That(GlyphIds("d©", LayoutMode.VerticalMixedRightLeft, Tag.Parse("ss01")))
            .IsEquivalentTo(new[] { LigatureDCopyright });

    [Test]
    public async Task LigatureIsNotFormedWhenAComponentHasNoFeatureOfTheLookup()
        => await Assert.That(GlyphIds("c©", LayoutMode.VerticalMixedRightLeft).Count).IsEqualTo(2);

    [Test]
    public async Task ArabicShaperLeavesContextualAlternatesToTheLayoutMode()
    {
        // The Arabic shaper orders calt and rclt in a stage of their own. That must not
        // assign them to upright glyphs in vertical layout, which only get vert.
        const string text = "\u0628";
        Font font = FakeFont.CreateFont(text);
        TextOptions options = new(font) { LayoutMode = LayoutMode.VerticalRightLeft };
        GlyphSubstitutionCollection collection = new(options);
        collection.AddGlyph(1, CodePoint.DecodeFromUtf16At(text, 0), TextDirection.RightToLeft, new TextRun { Start = 0, End = 1, Font = font }, 0);

        new ArabicShaper(ScriptClass.Arabic, options).Plan(collection, 0, 1);

        List<string> tags = collection[0].Features.Select(f => f.Tag.ToString()).ToList();
        await Assert.That(tags).Contains("vert");
        await Assert.That(tags).Contains("rlig");
        await Assert.That(tags).DoesNotContain("calt");
        await Assert.That(tags).DoesNotContain("rclt");
    }
}
