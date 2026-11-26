// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General;

namespace PinataLabs.Typography.Tests;

public class FontStyleRangeTests
{
    private const OS2Table.FontStyleSelection None = 0;

    [Test]
    [Arguments(1, 1F)]
    [Arguments(100, 100F)]
    [Arguments(400, 400F)]
    [Arguments(700, 700F)]
    [Arguments(900, 900F)]
    [Arguments(1000, 1000F)]
    public async Task WeightIsTheWeightClass(int weightClass, float expected)
    {
        FontStyleRange range = FontStyleRange.FromTables(Os2((ushort)weightClass, 5, None), null, 0F);

        await Assert.That(range.MinWeight.Value).IsEqualTo(expected);
        await Assert.That(range.MaxWeight.Value).IsEqualTo(expected);
    }

    [Test]
    [Arguments(true, 700F)]
    [Arguments(false, 400F)]
    public async Task WeightClassZeroFallsBackToTheBoldBit(bool bold, float expected)
    {
        OS2Table.FontStyleSelection selection = bold ? OS2Table.FontStyleSelection.BOLD : OS2Table.FontStyleSelection.REGULAR;

        FontStyleRange range = FontStyleRange.FromTables(Os2(0, 5, selection), null, 0F);

        await Assert.That(range.MinWeight.Value).IsEqualTo(expected);
        await Assert.That(range.MaxWeight.Value).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, 100F)]
    [Arguments(1, 50F)]
    [Arguments(2, 62.5F)]
    [Arguments(3, 75F)]
    [Arguments(4, 87.5F)]
    [Arguments(5, 100F)]
    [Arguments(6, 112.5F)]
    [Arguments(7, 125F)]
    [Arguments(8, 150F)]
    [Arguments(9, 200F)]
    [Arguments(10, 100F)]
    [Arguments(65535, 100F)]
    public async Task WidthIsTheWidthClassAsAPercentage(int widthClass, float expected)
    {
        FontStyleRange range = FontStyleRange.FromTables(Os2(400, (ushort)widthClass, None), null, 0F);

        await Assert.That(range.MinWidth.Percent).IsEqualTo(expected);
        await Assert.That(range.MaxWidth.Percent).IsEqualTo(expected);
    }

    [Test]
    public async Task ItalicBitGivesAnItalicFace()
    {
        FontStyleRange range = FontStyleRange.FromTables(Os2(400, 5, OS2Table.FontStyleSelection.ITALIC), null, -12F);

        await Assert.That(range.HasItalic).IsTrue();
        await Assert.That(range.HasNormal).IsFalse();
        await Assert.That(range.HasOblique).IsFalse();
        await Assert.That(range.Contains(FontStyle.Italic)).IsTrue();
        await Assert.That(range.Contains(FontStyle.Regular)).IsFalse();
    }

    [Test]
    [Arguments(-10F, 10F)]
    [Arguments(-20.5F, 20.5F)]
    [Arguments(8F, -8F)]
    [Arguments(0F, 14F)]
    public async Task ObliqueBitGivesTheNegatedItalicAngle(float italicAngle, float expectedAngle)
    {
        FontStyleRange range = FontStyleRange.FromTables(Os2(400, 5, OS2Table.FontStyleSelection.OBLIQUE), null, italicAngle);

        await Assert.That(range.HasOblique).IsTrue();
        await Assert.That(range.HasItalic).IsFalse();
        await Assert.That(range.HasNormal).IsFalse();
        await Assert.That(range.MinObliqueAngle).IsEqualTo(expectedAngle);
        await Assert.That(range.MaxObliqueAngle).IsEqualTo(expectedAngle);
        await Assert.That(range.Contains(FontStyle.Regular.WithSlope(FontSlope.Oblique(expectedAngle)))).IsTrue();
    }

    [Test]
    public async Task ItalicBitWinsOverTheObliqueBit()
    {
        FontStyleRange range = FontStyleRange.FromTables(
            Os2(400, 5, OS2Table.FontStyleSelection.ITALIC | OS2Table.FontStyleSelection.OBLIQUE),
            null,
            -12F);

        await Assert.That(range.HasItalic).IsTrue();
        await Assert.That(range.HasOblique).IsFalse();
    }

    [Test]
    public async Task ItalicAngleAloneDoesNotMakeAFaceSlanted()
    {
        FontStyleRange range = FontStyleRange.FromTables(Os2(400, 5, OS2Table.FontStyleSelection.REGULAR), null, -12F);

        await Assert.That(range.HasNormal).IsTrue();
        await Assert.That(range.HasItalic).IsFalse();
        await Assert.That(range.HasOblique).IsFalse();
    }

    [Test]
    [Arguments(false, false, 400F, FontSlopeKind.Normal)]
    [Arguments(true, false, 700F, FontSlopeKind.Normal)]
    [Arguments(false, true, 400F, FontSlopeKind.Italic)]
    [Arguments(true, true, 700F, FontSlopeKind.Italic)]
    public async Task WithoutOS2TheMacStyleGivesTheStyle(bool bold, bool italic, float expectedWeight, FontSlopeKind expectedSlope)
    {
        HeadTable.HeadMacStyle macStyle = (bold ? HeadTable.HeadMacStyle.Bold : HeadTable.HeadMacStyle.None)
            | (italic ? HeadTable.HeadMacStyle.Italic : HeadTable.HeadMacStyle.None);
        HeadTable head = new(HeadTable.HeadFlags.None, macStyle, 1000, DateTime.UnixEpoch, DateTime.UnixEpoch, new Bounds(0, 0, 1, 1), 8, HeadTable.IndexLocationFormats.Offset16);

        FontStyleRange range = FontStyleRange.FromTables(null, head, -12F);

        await Assert.That(range.MinWeight.Value).IsEqualTo(expectedWeight);
        await Assert.That(range.MaxWeight.Value).IsEqualTo(expectedWeight);
        await Assert.That(range.MinWidth).IsEqualTo(FontWidth.Normal);
        await Assert.That(range.HasItalic).IsEqualTo(expectedSlope == FontSlopeKind.Italic);
        await Assert.That(range.HasNormal).IsEqualTo(expectedSlope == FontSlopeKind.Normal);
    }

    [Test]
    public async Task WithoutOS2AndHeadTheFaceIsRegular()
    {
        FontStyleRange range = FontStyleRange.FromTables(null, null, 0F);

        await Assert.That(range.Contains(FontStyle.Regular)).IsTrue();
        await Assert.That(range.MinWeight).IsEqualTo(range.MaxWeight);
    }

    [Test]
    public async Task OS2WinsOverTheMacStyle()
    {
        HeadTable head = new(HeadTable.HeadFlags.None, HeadTable.HeadMacStyle.Bold | HeadTable.HeadMacStyle.Italic, 1000, DateTime.UnixEpoch, DateTime.UnixEpoch, new Bounds(0, 0, 1, 1), 8, HeadTable.IndexLocationFormats.Offset16);

        FontStyleRange range = FontStyleRange.FromTables(Os2(400, 5, OS2Table.FontStyleSelection.REGULAR), head, 0F);

        await Assert.That(range.Contains(FontStyle.Regular)).IsTrue();
    }

    [Test]
    public async Task ContainsChecksEveryPart()
    {
        // Weight 300 to 700, width 75% to 125%, upright, and oblique from 0 to 15 degrees.
        FontStyleRange range = new(
            new FontWeight(300F),
            new FontWeight(700F),
            FontWidth.Condensed,
            FontWidth.Expanded,
            hasNormal: true,
            hasItalic: false,
            hasOblique: true,
            minObliqueAngle: 0F,
            maxObliqueAngle: 15F);

        await Assert.That(range.Contains(FontStyle.Regular)).IsTrue();
        await Assert.That(range.Contains(FontStyle.Bold)).IsTrue();
        await Assert.That(range.Contains(FontStyle.Regular.WithWeight(new FontWeight(299F)))).IsFalse();
        await Assert.That(range.Contains(FontStyle.Regular.WithWeight(new FontWeight(701F)))).IsFalse();
        await Assert.That(range.Contains(FontStyle.Regular.WithWidth(new FontWidth(74F)))).IsFalse();
        await Assert.That(range.Contains(FontStyle.Regular.WithWidth(new FontWidth(125F)))).IsTrue();
        await Assert.That(range.Contains(FontStyle.Regular.WithSlope(FontSlope.Oblique(15F)))).IsTrue();
        await Assert.That(range.Contains(FontStyle.Regular.WithSlope(FontSlope.Oblique(15.5F)))).IsFalse();
        await Assert.That(range.Contains(FontStyle.Regular.WithSlope(FontSlope.Oblique(-1F)))).IsFalse();
        await Assert.That(range.Contains(FontStyle.Italic)).IsFalse();
    }

    [Test]
    public async Task ASingleStyleRangeCoversOnlyThatStyle()
    {
        FontStyle style = new(new FontWeight(600F), FontWidth.Condensed, FontSlope.Oblique(10F));
        FontStyleRange range = FontStyleRange.FromStyle(style);

        await Assert.That(range.Contains(style)).IsTrue();
        await Assert.That(range.Contains(style.WithWeight(new FontWeight(601F)))).IsFalse();
        await Assert.That(range.Contains(style.WithWidth(FontWidth.Normal))).IsFalse();
        await Assert.That(range.Contains(style.WithSlope(FontSlope.Oblique(11F)))).IsFalse();
        await Assert.That(range.Contains(style.WithSlope(FontSlope.Italic))).IsFalse();
        await Assert.That(range.Contains(style.WithSlope(FontSlope.Normal))).IsFalse();
    }

    [Test]
    public async Task AnObliqueAngleOfZeroIsUpright()
    {
        FontStyleRange range = FontStyleRange.FromStyle(FontStyle.Regular.WithSlope(FontSlope.Oblique(0F)));

        await Assert.That(range.HasNormal).IsTrue();
        await Assert.That(range.HasOblique).IsFalse();
        await Assert.That(range.Contains(FontStyle.Regular)).IsTrue();
    }

    [Test]
    [Arguments("Thin")]
    [Arguments("ExtraLight")]
    [Arguments("Light")]
    [Arguments("Regular")]
    [Arguments("Medium")]
    [Arguments("SemiBold")]
    [Arguments("Bold")]
    [Arguments("ExtraBold")]
    [Arguments("Black")]
    [Arguments("LightItalic")]
    [Arguments("Italic")]
    [Arguments("BoldItalic")]
    [Arguments("Oblique")]
    [Arguments("SteepOblique")]
    [Arguments("ExtraCondensed")]
    [Arguments("Condensed")]
    [Arguments("CondensedBold")]
    [Arguments("Expanded")]
    [Arguments("ExtraExpanded")]
    public async Task FontFileGivesTheRangeOfItsFace(string face)
    {
        FontStyle expected = MatchingSans.StyleOf(face);

        FontStyleRange range = FontDescription.LoadDescription(TestFonts.MatchingSansFile(face)).StyleRange;

        await Assert.That(range.MinWeight).IsEqualTo(expected.Weight);
        await Assert.That(range.MaxWeight).IsEqualTo(expected.Weight);
        await Assert.That(range.MinWidth).IsEqualTo(expected.Width);
        await Assert.That(range.MaxWidth).IsEqualTo(expected.Width);
        await Assert.That(range.HasItalic).IsEqualTo(expected.Slope.Kind == FontSlopeKind.Italic);
        await Assert.That(range.HasOblique).IsEqualTo(expected.Slope.Kind == FontSlopeKind.Oblique);
        await Assert.That(range.HasNormal).IsEqualTo(expected.Slope.Kind == FontSlopeKind.Normal);
        await Assert.That(range.Contains(expected)).IsTrue();
        if (expected.Slope.Kind == FontSlopeKind.Oblique)
        {
            await Assert.That(range.MinObliqueAngle).IsEqualTo(expected.Slope.ObliqueAngle);
            await Assert.That(range.MaxObliqueAngle).IsEqualTo(expected.Slope.ObliqueAngle);
        }
    }

    [Test]
    public async Task EveryGeneratedFontIsAKnownFace()
    {
        // A face that the generator writes and MatchingSans.AllFaces lacks fails here.
        string folder = Path.GetDirectoryName(TestFonts.MatchingSansFile("Regular"))!;
        string[] files = Directory.GetFiles(folder, "MatchingSans-*.ttf");

        await Assert.That(files.Length).IsEqualTo(MatchingSans.AllFaces.Length);
    }

    private static OS2Table Os2(ushort weightClass, ushort widthClass, OS2Table.FontStyleSelection selection)
        => new(1, weightClass, widthClass, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, [], 1, 1, 1, 1, string.Empty, selection, 1, 1, 20, 10, 20, 1, 1);
}
