// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// Font matching over faces that cover ranges, as a variable font does. A static face covers one point.
/// A face that covers a range matches every value in the range exactly, and otherwise the search
/// measures the distance to the nearest end of the range.
/// </summary>
public class FontStyleMatcherTests
{
    [Test]
    public async Task NoFacesGiveNoMatch()
        => await Assert.That(FontStyleMatcher.Match([], FontStyle.Regular)).IsEqualTo(-1);

    [Test]
    public async Task OneFaceAlwaysMatches()
    {
        FontStyleRange[] faces = [Weights(900F, 900F)];

        await Assert.That(FontStyleMatcher.Match(faces, FontStyle.Regular)).IsEqualTo(0);
        await Assert.That(FontStyleMatcher.Match(faces, FontStyle.BoldItalic.WithWidth(FontWidth.UltraCondensed))).IsEqualTo(0);
    }

    [Test]
    public async Task ARangeMatchesEveryWeightInItExactly()
    {
        // A static regular face, then a face that covers 100 to 900.
        FontStyleRange[] faces = [Weights(400F, 400F), Weights(100F, 900F)];

        await Assert.That(FontStyleMatcher.Match(faces, WeightStyle(650F))).IsEqualTo(1);
        await Assert.That(FontStyleMatcher.Match(faces, WeightStyle(150F))).IsEqualTo(1);

        // Both cover 400, so the first of them wins.
        await Assert.That(FontStyleMatcher.Match(faces, WeightStyle(400F))).IsEqualTo(0);
    }

    [Test]
    [Arguments(750F, 1)]
    [Arguments(800F, 1)]
    [Arguments(701F, 1)]
    [Arguments(700F, 0)]
    [Arguments(300F, 0)]
    [Arguments(200F, 2)]
    [Arguments(100F, 2)]
    public async Task ARangeIsMeasuredToItsNearestEnd(float weight, int expected)
    {
        // The face at index 0 covers 300 to 700, index 1 covers 900, and index 2 covers 100.
        FontStyleRange[] faces = [Weights(300F, 700F), Weights(900F, 900F), Weights(100F, 100F)];

        await Assert.That(FontStyleMatcher.Match(faces, WeightStyle(weight))).IsEqualTo(expected);
    }

    [Test]
    public async Task WeightAbove500PrefersTheNearestHeavierEnd()
    {
        // 600 is above the range 300 to 500 and below the range 700 to 800: the heavier face is tried first.
        FontStyleRange[] faces = [Weights(300F, 500F), Weights(700F, 800F)];

        await Assert.That(FontStyleMatcher.Match(faces, WeightStyle(600F))).IsEqualTo(1);
    }

    [Test]
    public async Task WeightBelow400PrefersTheNearestLighterEnd()
    {
        FontStyleRange[] faces = [Weights(450F, 500F), Weights(100F, 200F)];

        await Assert.That(FontStyleMatcher.Match(faces, WeightStyle(300F))).IsEqualTo(1);
    }

    [Test]
    public async Task WeightFrom400To500PrefersTheRangeAboveUpTo500ThenBelowThenAbove500()
    {
        // 450 is not in any range: the range from 460 is above it and not above 500, so it comes first.
        await Assert.That(FontStyleMatcher.Match([Weights(300F, 400F), Weights(460F, 600F)], WeightStyle(450F))).IsEqualTo(1);

        // A range that starts above 500 comes after the lighter range.
        await Assert.That(FontStyleMatcher.Match([Weights(520F, 600F), Weights(300F, 400F)], WeightStyle(450F))).IsEqualTo(1);

        // With nothing lighter, the range above 500 is chosen.
        await Assert.That(FontStyleMatcher.Match([Weights(520F, 600F), Weights(900F, 900F)], WeightStyle(450F))).IsEqualTo(0);
    }

    [Test]
    public async Task WidthRangeContainsTheRequestedWidth()
    {
        // The face at index 0 covers 50% to 90%, index 1 covers 75% to 125%.
        FontStyleRange[] faces = [Widths(50F, 90F), Widths(75F, 125F)];

        await Assert.That(FontStyleMatcher.Match(faces, WidthStyle(100F))).IsEqualTo(1);
        await Assert.That(FontStyleMatcher.Match(faces, WidthStyle(60F))).IsEqualTo(0);
        await Assert.That(FontStyleMatcher.Match(faces, WidthStyle(80F))).IsEqualTo(0);
    }

    [Test]
    public async Task WidthOutsideEveryRangeGoesToTheNearestEndInTheCssDirection()
    {
        FontStyleRange[] faces = [Widths(50F, 60F), Widths(130F, 150F)];

        // A request of 100% or less looks at narrower widths first.
        await Assert.That(FontStyleMatcher.Match(faces, WidthStyle(100F))).IsEqualTo(0);

        // A wider request looks at wider widths first.
        await Assert.That(FontStyleMatcher.Match(faces, WidthStyle(101F))).IsEqualTo(1);
    }

    [Test]
    public async Task WidthDecidesBeforeWeight()
    {
        // The first face has the weight, and the second has the width.
        FontStyleRange[] faces = [Range(700F, 700F, 100F, 100F), Range(400F, 400F, 75F, 75F)];

        await Assert.That(FontStyleMatcher.Match(faces, FontStyle.Bold.WithWidth(FontWidth.Condensed))).IsEqualTo(1);
    }

    [Test]
    public async Task ObliqueRangeMatchesAnAngleInsideIt()
    {
        // An upright face, then a face that has the upright glyphs and the angles from 0 to 15 degrees, as a slnt axis gives.
        FontStyleRange[] faces = [FontStyleRange.FromStyle(FontStyle.Regular), Slopes(hasNormal: true, hasItalic: false, 0F, 15F)];

        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Oblique(12F)))).IsEqualTo(1);
        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Oblique(0F)))).IsEqualTo(0);
        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Normal))).IsEqualTo(0);
    }

    [Test]
    public async Task ItalicRequestPrefersAnObliqueRangeToAnUprightFace()
    {
        FontStyleRange[] faces = [FontStyleRange.FromStyle(FontStyle.Regular), Slopes(hasNormal: true, hasItalic: false, 0F, 15F)];

        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Italic))).IsEqualTo(1);
    }

    [Test]
    public async Task FaceWithAnItalicAxisHasNormalAndItalicGlyphs()
    {
        // An upright face, then a face whose ital axis covers both.
        FontStyleRange[] faces = [FontStyleRange.FromStyle(FontStyle.Regular.WithWeight(FontWeight.Bold)), Slopes(hasNormal: true, hasItalic: true, 0F, 0F)];

        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Italic))).IsEqualTo(1);

        // Slope is matched before weight, so the face that has the slope wins over the face that has the weight.
        await Assert.That(FontStyleMatcher.Match(faces, FontStyle.BoldItalic)).IsEqualTo(1);

        // Both have upright glyphs, and the first of them has the weight.
        await Assert.That(FontStyleMatcher.Match(faces, FontStyle.Bold)).IsEqualTo(0);
    }

    [Test]
    public async Task ObliqueRangeIsMeasuredToItsNearestAngle()
    {
        // A face with the angles 2 to 6 degrees and a face with 30 to 40 degrees.
        FontStyleRange[] faces = [Slopes(hasNormal: false, hasItalic: false, 2F, 6F), Slopes(hasNormal: false, hasItalic: false, 30F, 40F)];

        // An angle of 14 degrees looks at the steeper angles first, as CSS does from 11 degrees.
        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Oblique(14F)))).IsEqualTo(1);

        // An angle of 8 degrees looks at the shallower angles first, but only the angles from 0 to the request.
        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Oblique(8F)))).IsEqualTo(0);
        await Assert.That(FontStyleMatcher.Match(faces, SlopeStyle(FontSlope.Oblique(35F)))).IsEqualTo(1);
    }

    private static FontStyle WeightStyle(float weight) => FontStyle.Regular.WithWeight(new FontWeight(weight));

    private static FontStyle WidthStyle(float percent) => FontStyle.Regular.WithWidth(new FontWidth(percent));

    private static FontStyle SlopeStyle(FontSlope slope) => FontStyle.Regular.WithSlope(slope);

    private static FontStyleRange Weights(float min, float max) => Range(min, max, 100F, 100F);

    private static FontStyleRange Widths(float min, float max) => Range(400F, 400F, min, max);

    private static FontStyleRange Range(float minWeight, float maxWeight, float minWidth, float maxWidth)
        => new(
            new FontWeight(minWeight),
            new FontWeight(maxWeight),
            new FontWidth(minWidth),
            new FontWidth(maxWidth),
            hasNormal: true,
            hasItalic: false,
            hasOblique: false,
            minObliqueAngle: 0F,
            maxObliqueAngle: 0F);

    private static FontStyleRange Slopes(bool hasNormal, bool hasItalic, float minAngle, float maxAngle)
        => new(
            FontWeight.Normal,
            FontWeight.Normal,
            FontWidth.Normal,
            FontWidth.Normal,
            hasNormal,
            hasItalic,
            hasOblique: maxAngle > 0F,
            minAngle,
            maxAngle);
}
