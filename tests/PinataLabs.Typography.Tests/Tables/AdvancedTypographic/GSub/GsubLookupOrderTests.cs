// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

    private static List<ushort> GlyphIds(string text, LayoutMode mode)
    {
        FontFamily family = new FontCollection().Add(TestFonts.GsubLookupOrderFile);
        TextOptions options = new(family.CreateFont(30)) { LayoutMode = mode };
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
}
