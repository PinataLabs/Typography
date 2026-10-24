// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// Tests for <see cref="TextOptions.TabWidth"/> with a font that has no glyph for U+0009 (#39).
/// </summary>
public class TabWidthTests
{
    // Open Sans does not map U+0009, so a tab resolves to the .notdef glyph.
    private static readonly Font OpenSans = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(24);

    private static float Advance(string text, float tabWidth, LayoutMode mode = LayoutMode.HorizontalTopBottom)
    {
        FontRectangle r = TextMeasurer.MeasureAdvance(text, new TextOptions(OpenSans) { TabWidth = tabWidth, LayoutMode = mode });
        return mode == LayoutMode.HorizontalTopBottom ? r.Width : r.Height;
    }

    [Test]
    public async Task WidthIncreasesWithTabWidth()
    {
        float w0 = Advance("a\tb", 0);
        float w4 = Advance("a\tb", 4);
        float w8 = Advance("a\tb", 8);

        await Assert.That(w4).IsGreaterThan(w0);
        await Assert.That(w8).IsGreaterThan(w4);
    }

    [Test]
    public async Task TabAdvanceIsTabWidthTimesSpaceAdvance()
    {
        // A tab at TabWidth 4 occupies the same as four spaces.
        await Assert.That(Advance("a	b", 4)).IsEqualTo(Advance("a    b", -1)).Within(0.05F);
    }

    [Test]
    public async Task ZeroTabWidthGivesTabNoAdvance()
    {
        await Assert.That(Advance("a\tb", 0)).IsEqualTo(Advance("ab", -1)).Within(0.05F);
    }

    [Test]
    public async Task DefaultTabWidthKeepsNotdefAdvance()
    {
        // Regression guard: -1 still uses the font's glyph advance (the .notdef box here).
        await Assert.That(Advance("a\tb", -1)).IsEqualTo(43F).Within(0.5F);
    }

    [Test]
    public async Task VerticalLayoutUsesTabWidthAsAdvanceHeight()
    {
        await Assert.That(Advance("a	b", 4, LayoutMode.VerticalLeftRight))
            .IsEqualTo(Advance("a    b", -1, LayoutMode.VerticalLeftRight)).Within(0.05F);
    }

    [Test]
    public async Task WrappedTextWithTabsUsesNewAdvances()
    {
        // "aaa<tab>bbb" fits within the wrap length of "aaabbb" at TabWidth 0, but not at TabWidth 8.
        float limit = Advance("aaabbb", -1) + 1;
        string text = "aaa\tbbb";

        int narrow = TextMeasurer.CountLines(text, new TextOptions(OpenSans) { TabWidth = 0, WrappingLength = limit });
        int wide = TextMeasurer.CountLines(text, new TextOptions(OpenSans) { TabWidth = 8, WrappingLength = limit });

        await Assert.That(narrow).IsEqualTo(1);
        await Assert.That(wide).IsGreaterThan(1);
    }

    [Test]
    public async Task TabRendersNoOutline()
    {
        var renderer = new GlyphRenderer();
        TextRenderer.RenderTextTo(renderer, "\t", new TextOptions(OpenSans) { TabWidth = 4 });

        await Assert.That(renderer.ControlPoints.Count).IsEqualTo(0);
    }
}
