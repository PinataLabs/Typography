// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class TextMeasurerRoundingTests
{
    // CentralBaselineTest.ttf has a 1000 unit em, so at 100px a line is one 100px em high
    // and its 'A' advances exactly 60px. The layout holds these in inches, where 100 / 72 is inexact.
    private static Font CentralBaselineFont(float size)
        => new FontCollection().Add(TestFonts.CentralBaselineTestFile).CreateFont(size);

    [Test]
    [Arguments(LayoutMode.VerticalLeftRight, 1F, 100F)]
    [Arguments(LayoutMode.VerticalLeftRight, 1.2F, 120F)]
    [Arguments(LayoutMode.VerticalLeftRight, 1.5F, 150F)]
    [Arguments(LayoutMode.VerticalLeftRight, 2F, 200F)]
    [Arguments(LayoutMode.VerticalRightLeft, 1.5F, 150F)]
    [Arguments(LayoutMode.VerticalMixedLeftRight, 1.5F, 150F)]
    [Arguments(LayoutMode.VerticalMixedRightLeft, 1.5F, 150F)]
    public async Task MeasureAdvanceOfAVerticalColumnIsTheLineSpacedEm(LayoutMode mode, float lineSpacing, float expected)
    {
        TextOptions options = new(CentralBaselineFont(100)) { LayoutMode = mode, LineSpacing = lineSpacing };

        FontRectangle advance = TextMeasurer.MeasureAdvance("あいうABCえお", options);

        await Assert.That(advance.Width).IsEqualTo(expected);
    }

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom, 1F, 100F)]
    [Arguments(LayoutMode.HorizontalTopBottom, 1.2F, 120F)]
    [Arguments(LayoutMode.HorizontalTopBottom, 1.5F, 150F)]
    [Arguments(LayoutMode.HorizontalTopBottom, 2F, 200F)]
    [Arguments(LayoutMode.HorizontalBottomTop, 1.5F, 150F)]
    public async Task MeasureAdvanceOfAHorizontalLineIsTheLineSpacedEm(LayoutMode mode, float lineSpacing, float expected)
    {
        TextOptions options = new(CentralBaselineFont(100)) { LayoutMode = mode, LineSpacing = lineSpacing };

        FontRectangle advance = TextMeasurer.MeasureAdvance("A", options);

        await Assert.That(advance.Width).IsEqualTo(60F);
        await Assert.That(advance.Height).IsEqualTo(expected);
    }

    [Test]
    public async Task MeasureSizeOfAHorizontalLineIsTheInkExtent()
    {
        // Line spacing 1.2 moves the 'A' 10px down, so its ink, which ends 110px down at spacing 1, ends at 120px.
        TextOptions options = new(CentralBaselineFont(100)) { LineSpacing = 1.2F };

        FontRectangle size = TextMeasurer.MeasureSize("A", options);

        await Assert.That(size.Height).IsEqualTo(120F);
    }

    [Test]
    [Arguments(LayoutMode.HorizontalTopBottom)]
    [Arguments(LayoutMode.VerticalLeftRight)]
    [Arguments(LayoutMode.VerticalMixedRightLeft)]
    public async Task MeasureAdvanceRoundsARealFractionalExtentUp(LayoutMode mode)
    {
        // At 100.5px the line is 100.5px, so a box that holds it needs 101px.
        TextOptions options = new(CentralBaselineFont(100.5F)) { LayoutMode = mode };

        FontRectangle advance = TextMeasurer.MeasureAdvance("A", options);

        float lineExtent = mode == LayoutMode.HorizontalTopBottom ? advance.Height : advance.Width;
        await Assert.That(lineExtent).IsEqualTo(101F);
    }
}
