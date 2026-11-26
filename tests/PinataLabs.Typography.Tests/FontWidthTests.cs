// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class FontWidthTests
{
    [Test]
    public async Task DefaultIsNormal()
    {
        await Assert.That(default(FontWidth)).IsEqualTo(FontWidth.Normal);
        await Assert.That(default(FontWidth).Percent).IsEqualTo(100F);
    }

    [Test]
    [Arguments(50F)]
    [Arguments(62.5F)]
    [Arguments(87.5F)]
    [Arguments(100F)]
    [Arguments(112.5F)]
    [Arguments(137.25F)]
    [Arguments(200F)]
    public async Task PercentRoundTrips(float percent)
        => await Assert.That(new FontWidth(percent).Percent).IsEqualTo(percent);

    [Test]
    [Arguments(-5F, 50F)]
    [Arguments(49F, 50F)]
    [Arguments(201F, 200F)]
    [Arguments(float.PositiveInfinity, 200F)]
    [Arguments(float.NegativeInfinity, 50F)]
    public async Task ConstructorClampsToTheCssRange(float percent, float expected)
        => await Assert.That(new FontWidth(percent).Percent).IsEqualTo(expected);

    [Test]
    public async Task ConstructorRejectsNaN()
        => await Assert.That(() => new FontWidth(float.NaN)).ThrowsExactly<ArgumentOutOfRangeException>();

    [Test]
    public async Task KeywordsHaveTheCssValues()
    {
        await Assert.That(FontWidth.UltraCondensed.Percent).IsEqualTo(50F);
        await Assert.That(FontWidth.ExtraCondensed.Percent).IsEqualTo(62.5F);
        await Assert.That(FontWidth.Condensed.Percent).IsEqualTo(75F);
        await Assert.That(FontWidth.SemiCondensed.Percent).IsEqualTo(87.5F);
        await Assert.That(FontWidth.Normal.Percent).IsEqualTo(100F);
        await Assert.That(FontWidth.SemiExpanded.Percent).IsEqualTo(112.5F);
        await Assert.That(FontWidth.Expanded.Percent).IsEqualTo(125F);
        await Assert.That(FontWidth.ExtraExpanded.Percent).IsEqualTo(150F);
        await Assert.That(FontWidth.UltraExpanded.Percent).IsEqualTo(200F);
    }

    [Test]
    public async Task EqualityAndOrdering()
    {
        await Assert.That(new FontWidth(75F)).IsEqualTo(FontWidth.Condensed);
        await Assert.That(new FontWidth(75F) == FontWidth.Condensed).IsTrue();
        await Assert.That(new FontWidth(80F) != FontWidth.Condensed).IsTrue();
        await Assert.That(new FontWidth(75F).GetHashCode()).IsEqualTo(FontWidth.Condensed.GetHashCode());
        await Assert.That(FontWidth.Condensed.CompareTo(FontWidth.Expanded)).IsLessThan(0);
        await Assert.That(FontWidth.Expanded.CompareTo(FontWidth.Condensed)).IsGreaterThan(0);
        await Assert.That(FontWidth.Expanded.CompareTo(FontWidth.Expanded)).IsEqualTo(0);
    }

    [Test]
    public async Task ToStringIsThePercentage()
        => await Assert.That(FontWidth.SemiCondensed.ToString()).IsEqualTo("87.5%");
}
