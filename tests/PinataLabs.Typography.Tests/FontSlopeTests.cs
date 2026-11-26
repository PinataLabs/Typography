// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class FontSlopeTests
{
    [Test]
    public async Task DefaultIsNormal()
    {
        await Assert.That(default(FontSlope)).IsEqualTo(FontSlope.Normal);
        await Assert.That(default(FontSlope).Kind).IsEqualTo(FontSlopeKind.Normal);
        await Assert.That(default(FontSlope).ObliqueAngle).IsEqualTo(0F);
    }

    [Test]
    public async Task ItalicHasNoAngle()
    {
        await Assert.That(FontSlope.Italic.Kind).IsEqualTo(FontSlopeKind.Italic);
        await Assert.That(FontSlope.Italic.ObliqueAngle).IsEqualTo(0F);
    }

    [Test]
    public async Task ObliqueDefaultsToFourteenDegrees()
    {
        await Assert.That(FontSlope.Oblique().Kind).IsEqualTo(FontSlopeKind.Oblique);
        await Assert.That(FontSlope.Oblique().ObliqueAngle).IsEqualTo(14F);
    }

    [Test]
    [Arguments(-90F, -90F)]
    [Arguments(-11.5F, -11.5F)]
    [Arguments(0F, 0F)]
    [Arguments(20F, 20F)]
    [Arguments(90F, 90F)]
    [Arguments(120F, 90F)]
    [Arguments(-120F, -90F)]
    [Arguments(float.PositiveInfinity, 90F)]
    public async Task ObliqueClampsToTheCssRange(float degrees, float expected)
        => await Assert.That(FontSlope.Oblique(degrees).ObliqueAngle).IsEqualTo(expected);

    [Test]
    public async Task ObliqueRejectsNaN()
        => await Assert.That(() => FontSlope.Oblique(float.NaN)).ThrowsExactly<ArgumentOutOfRangeException>();

    [Test]
    public async Task EqualityComparesKindAndAngle()
    {
        await Assert.That(FontSlope.Oblique(10F)).IsEqualTo(FontSlope.Oblique(10F));
        await Assert.That(FontSlope.Oblique(10F) == FontSlope.Oblique(10F)).IsTrue();
        await Assert.That(FontSlope.Oblique(10F).GetHashCode()).IsEqualTo(FontSlope.Oblique(10F).GetHashCode());
        await Assert.That(FontSlope.Oblique(10F) != FontSlope.Oblique(12F)).IsTrue();
        await Assert.That(FontSlope.Italic != FontSlope.Normal).IsTrue();
        await Assert.That(FontSlope.Italic != FontSlope.Oblique(0F)).IsTrue();
    }

    [Test]
    public async Task ToStringIsTheCssValue()
    {
        await Assert.That(FontSlope.Normal.ToString()).IsEqualTo("normal");
        await Assert.That(FontSlope.Italic.ToString()).IsEqualTo("italic");
        await Assert.That(FontSlope.Oblique(-12.5F).ToString()).IsEqualTo("oblique -12.5deg");
    }
}
