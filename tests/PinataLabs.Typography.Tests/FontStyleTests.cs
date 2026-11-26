// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class FontStyleTests
{
    [Test]
    public async Task DefaultIsRegular()
    {
        await Assert.That(default(FontStyle)).IsEqualTo(FontStyle.Regular);
        await Assert.That(default(FontStyle).Weight).IsEqualTo(FontWeight.Normal);
        await Assert.That(default(FontStyle).Width).IsEqualTo(FontWidth.Normal);
        await Assert.That(default(FontStyle).Slope).IsEqualTo(FontSlope.Normal);
    }

    [Test]
    public async Task NamedStylesHaveTheirParts()
    {
        await Assert.That(FontStyle.Regular).IsEqualTo(new FontStyle(new FontWeight(400), new FontWidth(100), FontSlope.Normal));
        await Assert.That(FontStyle.Bold).IsEqualTo(new FontStyle(new FontWeight(700), new FontWidth(100), FontSlope.Normal));
        await Assert.That(FontStyle.Italic).IsEqualTo(new FontStyle(new FontWeight(400), new FontWidth(100), FontSlope.Italic));
        await Assert.That(FontStyle.BoldItalic).IsEqualTo(new FontStyle(new FontWeight(700), new FontWidth(100), FontSlope.Italic));
    }

    [Test]
    public async Task ConstructorKeepsTheParts()
    {
        FontStyle style = new(new FontWeight(650), FontWidth.Condensed, FontSlope.Oblique(8F));

        await Assert.That(style.Weight).IsEqualTo(new FontWeight(650));
        await Assert.That(style.Width).IsEqualTo(FontWidth.Condensed);
        await Assert.That(style.Slope).IsEqualTo(FontSlope.Oblique(8F));
    }

    [Test]
    public async Task WithMethodsChangeOnePart()
    {
        FontStyle style = new(FontWeight.Light, FontWidth.Expanded, FontSlope.Italic);

        await Assert.That(style.WithWeight(FontWeight.Bold)).IsEqualTo(new FontStyle(FontWeight.Bold, FontWidth.Expanded, FontSlope.Italic));
        await Assert.That(style.WithWidth(FontWidth.Condensed)).IsEqualTo(new FontStyle(FontWeight.Light, FontWidth.Condensed, FontSlope.Italic));
        await Assert.That(style.WithSlope(FontSlope.Normal)).IsEqualTo(new FontStyle(FontWeight.Light, FontWidth.Expanded, FontSlope.Normal));
        await Assert.That(FontStyle.Regular.WithWeight(FontWeight.Bold)).IsEqualTo(FontStyle.Bold);
        await Assert.That(FontStyle.Bold.WithSlope(FontSlope.Italic)).IsEqualTo(FontStyle.BoldItalic);
    }

    [Test]
    public async Task EqualityComparesEveryPart()
    {
        FontStyle style = new(FontWeight.Light, FontWidth.Expanded, FontSlope.Italic);

        await Assert.That(style == new FontStyle(FontWeight.Light, FontWidth.Expanded, FontSlope.Italic)).IsTrue();
        await Assert.That(style.GetHashCode()).IsEqualTo(new FontStyle(FontWeight.Light, FontWidth.Expanded, FontSlope.Italic).GetHashCode());
        await Assert.That(style.Equals((object)new FontStyle(FontWeight.Light, FontWidth.Expanded, FontSlope.Italic))).IsTrue();
        await Assert.That(style != style.WithWeight(FontWeight.Medium)).IsTrue();
        await Assert.That(style != style.WithWidth(FontWidth.Normal)).IsTrue();
        await Assert.That(style != style.WithSlope(FontSlope.Oblique(14F))).IsTrue();
        await Assert.That(style.Equals(null)).IsFalse();
    }

    [Test]
    public async Task ToStringListsTheParts()
        => await Assert.That(FontStyle.BoldItalic.ToString()).IsEqualTo("700 100% italic");
}
