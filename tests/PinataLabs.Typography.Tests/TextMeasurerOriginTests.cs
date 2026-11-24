// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class TextMeasurerOriginTests
{
    private static readonly Vector2[] Origins = [Vector2.Zero, new(5000, 5000), new(100000, 100000)];

    private static TextOptions Options(LayoutMode mode, float lineSpacing, Vector2 origin)
        => new(new FontCollection().Add(TestFonts.CentralBaselineTestFile).CreateFont(100))
        {
            LayoutMode = mode,
            LineSpacing = lineSpacing,
            Origin = origin,
        };

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom, 1F)]
    [Arguments(LayoutMode.HorizontalTopBottom, 1.5F)]
    [Arguments(LayoutMode.VerticalLeftRight, 1F)]
    [Arguments(LayoutMode.VerticalLeftRight, 1.5F)]
    [Arguments(LayoutMode.VerticalMixedRightLeft, 1.5F)]
    public async Task MeasureAdvanceAndSizeDoNotDependOnOrigin(LayoutMode mode, float lineSpacing)
    {
        const string text = "あいうABCえお\nAB";
        FontRectangle advance = TextMeasurer.MeasureAdvance(text, Options(mode, lineSpacing, Vector2.Zero));
        FontRectangle size = TextMeasurer.MeasureSize(text, Options(mode, lineSpacing, Vector2.Zero));

        foreach (Vector2 origin in Origins)
        {
            await Assert.That(TextMeasurer.MeasureAdvance(text, Options(mode, lineSpacing, origin))).IsEqualTo(advance);
            await Assert.That(TextMeasurer.MeasureSize(text, Options(mode, lineSpacing, origin))).IsEqualTo(size);
        }
    }

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom, 1.5F)]
    [Arguments(LayoutMode.VerticalLeftRight, 1.5F)]
    public async Task MeasureBoundsIsTheZeroOriginBoundsMovedToTheOrigin(LayoutMode mode, float lineSpacing)
    {
        // At an origin of 100,000 a float position is only exact to 1/128px, so the extent is what stays exact.
        const string text = "あいうABCえお\nAB";
        FontRectangle bounds = TextMeasurer.MeasureBounds(text, Options(mode, lineSpacing, Vector2.Zero));

        foreach (Vector2 origin in Origins)
        {
            FontRectangle moved = TextMeasurer.MeasureBounds(text, Options(mode, lineSpacing, origin));

            await Assert.That(moved.Size).IsEqualTo(bounds.Size);
            await Assert.That(moved.Location).IsEqualTo(bounds.Location + origin);
        }
    }

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom, 1.5F)]
    [Arguments(LayoutMode.VerticalLeftRight, 1.5F)]
    public async Task CharacterMeasuresDoNotDependOnOrigin(LayoutMode mode, float lineSpacing)
    {
        const string text = "あいうABCえお\nAB";
        TextOptions zero = Options(mode, lineSpacing, Vector2.Zero);
        TextMeasurer.TryMeasureCharacterAdvances(text, zero, out ReadOnlySpan<GlyphBounds> advanceSpan);
        TextMeasurer.TryMeasureCharacterSizes(text, zero, out ReadOnlySpan<GlyphBounds> sizeSpan);
        TextMeasurer.TryMeasureCharacterBounds(text, zero, out ReadOnlySpan<GlyphBounds> boundsSpan);
        GlyphBounds[] advances = advanceSpan.ToArray();
        GlyphBounds[] sizes = sizeSpan.ToArray();
        GlyphBounds[] bounds = boundsSpan.ToArray();

        foreach (Vector2 origin in Origins)
        {
            TextOptions options = Options(mode, lineSpacing, origin);
            TextMeasurer.TryMeasureCharacterAdvances(text, options, out ReadOnlySpan<GlyphBounds> movedAdvanceSpan);
            TextMeasurer.TryMeasureCharacterSizes(text, options, out ReadOnlySpan<GlyphBounds> movedSizeSpan);
            TextMeasurer.TryMeasureCharacterBounds(text, options, out ReadOnlySpan<GlyphBounds> movedBoundsSpan);
            GlyphBounds[] movedAdvances = movedAdvanceSpan.ToArray();
            GlyphBounds[] movedSizes = movedSizeSpan.ToArray();
            GlyphBounds[] movedBounds = movedBoundsSpan.ToArray();

            for (int i = 0; i < bounds.Length; i++)
            {
                await Assert.That(movedAdvances[i].Bounds).IsEqualTo(advances[i].Bounds);
                await Assert.That(movedSizes[i].Bounds).IsEqualTo(sizes[i].Bounds);
                await Assert.That(movedBounds[i].Bounds.Size).IsEqualTo(bounds[i].Bounds.Size);
                await Assert.That(movedBounds[i].Bounds.Location).IsEqualTo(bounds[i].Bounds.Location + origin);
            }
        }
    }
}
