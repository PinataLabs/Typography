// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// The style range of a variable face covers the range of its wght and wdth axes, and the slopes that its ital and
/// slnt axes reach. A part without an axis comes from the OS/2 table, as for a static face.
/// </summary>
public class FontStyleRangeVariationTests
{
    [Test]
    public async Task WeightRangeIsTheWghtAxis()
    {
        FontStyleRange teko = Range(TestFonts.TekoVariableFile);
        FontStyleRange sourceSans = Range(TestFonts.SourceSans3VariableCff2File);

        await Assert.That(teko.MinWeight.Value).IsEqualTo(300F);
        await Assert.That(teko.MaxWeight.Value).IsEqualTo(700F);
        await Assert.That(sourceSans.MinWeight.Value).IsEqualTo(200F);
        await Assert.That(sourceSans.MaxWeight.Value).IsEqualTo(900F);
    }

    [Test]
    public async Task WidthAndSlopeWithoutAnAxisComeFromOs2()
    {
        FontStyleRange teko = Range(TestFonts.TekoVariableFile);

        await Assert.That(teko.MinWidth.Percent).IsEqualTo(100F);
        await Assert.That(teko.MaxWidth.Percent).IsEqualTo(100F);
        await Assert.That(teko.HasNormal).IsTrue();
        await Assert.That(teko.HasItalic).IsFalse();
        await Assert.That(teko.HasOblique).IsFalse();
    }

    [Test]
    public async Task AFontWithWghtWdthAndItalAxesCoversTheirRanges()
    {
        FontStyleRange range = Range(TestFonts.VariationAxesFile);

        await Assert.That(range.MinWeight.Value).IsEqualTo(100F);
        await Assert.That(range.MaxWeight.Value).IsEqualTo(900F);
        await Assert.That(range.MinWidth.Percent).IsEqualTo(75F);
        await Assert.That(range.MaxWidth.Percent).IsEqualTo(125F);
        await Assert.That(range.HasNormal).IsTrue();
        await Assert.That(range.HasItalic).IsTrue();
        await Assert.That(range.HasOblique).IsFalse();
    }

    [Test]
    public async Task ASlntAxisCoversTheNegatedAnglesAsObliqueSlopes()
    {
        // The axis runs from -12 to 0 degrees counter-clockwise, which is 0 to 12 degrees of CSS oblique.
        FontStyleRange range = Range(TestFonts.VariationSlantFile);

        await Assert.That(range.HasNormal).IsTrue();
        await Assert.That(range.HasItalic).IsFalse();
        await Assert.That(range.HasOblique).IsTrue();
        await Assert.That(range.MinObliqueAngle).IsEqualTo(0F);
        await Assert.That(range.MaxObliqueAngle).IsEqualTo(12F);
        await Assert.That(range.Contains(FontStyle.Regular.WithSlope(FontSlope.Oblique(8F)))).IsTrue();
        await Assert.That(range.Contains(FontStyle.Regular.WithSlope(FontSlope.Oblique(14F)))).IsFalse();
        await Assert.That(range.Contains(FontStyle.Regular.WithSlope(FontSlope.Oblique(-4F)))).IsFalse();
        await Assert.That(range.Contains(FontStyle.Italic)).IsFalse();
    }

    [Test]
    public async Task ARangeContainsTheStylesBetweenItsAxisLimits()
    {
        FontStyleRange teko = Range(TestFonts.TekoVariableFile);

        await Assert.That(teko.Contains(FontStyle.Regular)).IsTrue();
        await Assert.That(teko.Contains(FontStyle.Bold)).IsTrue();
        await Assert.That(teko.Contains(FontStyle.Regular.WithWeight(new FontWeight(250F)))).IsFalse();
        await Assert.That(teko.Contains(FontStyle.Regular.WithWeight(FontWeight.Black))).IsFalse();
        await Assert.That(teko.Contains(FontStyle.Italic)).IsFalse();
    }

    [Test]
    [Arguments(100)]
    [Arguments(300)]
    [Arguments(650)]
    [Arguments(900)]
    public async Task AFamilyOfOneVariableFaceMatchesEveryWeight(int weight)
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        FontMetrics face = family.MatchFace(FontStyle.Regular.WithWeight(new FontWeight(weight)));

        await Assert.That(face).IsSameReferenceAs(family.Faces[0]);
    }

    private static FontStyleRange Range(string path)
        => new FontCollection().Add(path).Faces[0].Description.StyleRange;
}
