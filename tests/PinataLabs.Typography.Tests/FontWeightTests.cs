// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class FontWeightTests
{
    [Test]
    public async Task DefaultIsNormal()
    {
        await Assert.That(default(FontWeight)).IsEqualTo(FontWeight.Normal);
        await Assert.That(default(FontWeight).Value).IsEqualTo(400F);
    }

    [Test]
    [Arguments(1F)]
    [Arguments(100F)]
    [Arguments(350.5F)]
    [Arguments(400F)]
    [Arguments(650F)]
    [Arguments(700F)]
    [Arguments(1000F)]
    public async Task ValueRoundTrips(float value)
        => await Assert.That(new FontWeight(value).Value).IsEqualTo(value);

    [Test]
    [Arguments(-5F, 1F)]
    [Arguments(0F, 1F)]
    [Arguments(1001F, 1000F)]
    [Arguments(float.PositiveInfinity, 1000F)]
    [Arguments(float.NegativeInfinity, 1F)]
    public async Task ConstructorClampsToTheCssRange(float value, float expected)
        => await Assert.That(new FontWeight(value).Value).IsEqualTo(expected);

    [Test]
    public async Task ConstructorRejectsNaN()
        => await Assert.That(() => new FontWeight(float.NaN)).ThrowsExactly<ArgumentOutOfRangeException>();

    [Test]
    public async Task KeywordsHaveTheCssValues()
    {
        await Assert.That(FontWeight.Thin.Value).IsEqualTo(100F);
        await Assert.That(FontWeight.ExtraLight.Value).IsEqualTo(200F);
        await Assert.That(FontWeight.Light.Value).IsEqualTo(300F);
        await Assert.That(FontWeight.Normal.Value).IsEqualTo(400F);
        await Assert.That(FontWeight.Medium.Value).IsEqualTo(500F);
        await Assert.That(FontWeight.SemiBold.Value).IsEqualTo(600F);
        await Assert.That(FontWeight.Bold.Value).IsEqualTo(700F);
        await Assert.That(FontWeight.ExtraBold.Value).IsEqualTo(800F);
        await Assert.That(FontWeight.Black.Value).IsEqualTo(900F);
    }

    [Test]
    public async Task EqualityAndOrdering()
    {
        await Assert.That(new FontWeight(700F)).IsEqualTo(FontWeight.Bold);
        await Assert.That(new FontWeight(700F) == FontWeight.Bold).IsTrue();
        await Assert.That(new FontWeight(650F) != FontWeight.Bold).IsTrue();
        await Assert.That(new FontWeight(700F).GetHashCode()).IsEqualTo(FontWeight.Bold.GetHashCode());
        await Assert.That(FontWeight.Light.CompareTo(FontWeight.Bold)).IsLessThan(0);
        await Assert.That(FontWeight.Bold.CompareTo(FontWeight.Light)).IsGreaterThan(0);
        await Assert.That(FontWeight.Bold.CompareTo(FontWeight.Bold)).IsEqualTo(0);
    }

    [Test]
    public async Task ToStringIsTheValue()
        => await Assert.That(new FontWeight(650.5F).ToString()).IsEqualTo("650.5");
}
