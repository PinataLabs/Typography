// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// Font matching as CSS Fonts Level 4 section 5.2 "Matching font styles" gives it. Each case builds a family
/// from the files of Matching Sans that it needs. A search order is the faces in the order in which the search
/// reaches them: the best match, then the best match when that face is gone, and so on.
/// </summary>
public class FontMatchingTests
{
    private static readonly string[] Weights = MatchingSans.Weights;

    private static readonly string[] Widths = ["ExtraCondensed", "Condensed", "Regular", "Expanded", "ExtraExpanded"];

    private static readonly string[] Slopes = ["Regular", "Italic", "Oblique", "SteepOblique"];

    [Test]
    public async Task FamilyHasMoreThanFourStaticWeights()
    {
        FontFamily family = MatchingSans.Family(Weights);

        await Assert.That(family.Faces.Count).IsEqualTo(9);
        await Assert.That(family.Faces.Select(x => x.Description.StyleRange.MinWeight.Value).OrderBy(x => x))
            .IsEquivalentTo([100F, 200F, 300F, 400F, 500F, 600F, 700F, 800F, 900F]);
    }

    [Test]
    [Arguments(100)]
    [Arguments(200)]
    [Arguments(300)]
    [Arguments(400)]
    [Arguments(500)]
    [Arguments(600)]
    [Arguments(700)]
    [Arguments(800)]
    [Arguments(900)]
    public async Task EachWeightOfTheFamilyMatchesItself(int weight)
    {
        FontFamily family = MatchingSans.Family(Weights);

        FontMetrics face = family.MatchFace(FontStyle.Regular.WithWeight(new FontWeight(weight)));

        await Assert.That(face.Description.StyleRange.MinWeight.Value).IsEqualTo(weight);
        await Assert.That(face.Description.StyleRange.MaxWeight.Value).IsEqualTo(weight);
    }

    // A weight from 400 to 500 tries the weights from the request up to 500, then the lighter weights,
    // then the weights above 500.
    [Test]
    [Arguments(400, new[] { "Regular", "Medium", "Light", "ExtraLight", "Thin", "SemiBold", "Bold", "ExtraBold", "Black" })]
    [Arguments(450, new[] { "Medium", "Regular", "Light", "ExtraLight", "Thin", "SemiBold", "Bold", "ExtraBold", "Black" })]
    [Arguments(500, new[] { "Medium", "Regular", "Light", "ExtraLight", "Thin", "SemiBold", "Bold", "ExtraBold", "Black" })]
    public async Task WeightsFrom400To500SearchUpTo500ThenDownThenUp(int weight, string[] expected)
    {
        string[] order = MatchingSans.SearchOrder(Weights, FontStyle.Regular.WithWeight(new FontWeight(weight)));

        await Assert.That(order).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    // A weight below 400 tries the lighter weights first, then the heavier weights.
    [Test]
    [Arguments(300, new[] { "Light", "ExtraLight", "Thin", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" })]
    [Arguments(350, new[] { "Light", "ExtraLight", "Thin", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" })]
    [Arguments(100, new[] { "Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" })]
    [Arguments(1, new[] { "Thin", "ExtraLight", "Light", "Regular", "Medium", "SemiBold", "Bold", "ExtraBold", "Black" })]
    public async Task WeightsBelow400SearchDownThenUp(int weight, string[] expected)
    {
        string[] order = MatchingSans.SearchOrder(Weights, FontStyle.Regular.WithWeight(new FontWeight(weight)));

        await Assert.That(order).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    // A weight above 500 tries the heavier weights first, then the lighter weights.
    [Test]
    [Arguments(600, new[] { "SemiBold", "Bold", "ExtraBold", "Black", "Medium", "Regular", "Light", "ExtraLight", "Thin" })]
    [Arguments(650, new[] { "Bold", "ExtraBold", "Black", "SemiBold", "Medium", "Regular", "Light", "ExtraLight", "Thin" })]
    [Arguments(900, new[] { "Black", "ExtraBold", "Bold", "SemiBold", "Medium", "Regular", "Light", "ExtraLight", "Thin" })]
    [Arguments(1000, new[] { "Black", "ExtraBold", "Bold", "SemiBold", "Medium", "Regular", "Light", "ExtraLight", "Thin" })]
    public async Task WeightsAbove500SearchUpThenDown(int weight, string[] expected)
    {
        string[] order = MatchingSans.SearchOrder(Weights, FontStyle.Regular.WithWeight(new FontWeight(weight)));

        await Assert.That(order).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    [Test]
    [Arguments(450, new[] { "Regular", "SemiBold" }, "Regular")]
    [Arguments(450, new[] { "Light", "SemiBold" }, "Light")]
    [Arguments(450, new[] { "SemiBold", "Bold" }, "SemiBold")]
    [Arguments(400, new[] { "Light", "Medium" }, "Medium")]
    [Arguments(400, new[] { "Thin", "Light", "Black" }, "Light")]
    [Arguments(500, new[] { "Regular", "SemiBold" }, "Regular")]
    [Arguments(500, new[] { "Light", "Black" }, "Light")]
    [Arguments(600, new[] { "Light", "Regular", "Bold", "Black" }, "Bold")]
    [Arguments(600, new[] { "Light", "Regular", "Medium" }, "Medium")]
    [Arguments(250, new[] { "Thin", "Regular", "Bold" }, "Thin")]
    [Arguments(250, new[] { "Regular", "Bold" }, "Regular")]
    [Arguments(700, new[] { "Regular" }, "Regular")]
    public async Task WeightWithMissingFaces(int weight, string[] faces, string expected)
    {
        FontFamily family = MatchingSans.Family(faces);

        FontMetrics face = family.MatchFace(FontStyle.Regular.WithWeight(new FontWeight(weight)));

        await Assert.That(MatchingSans.FaceName(face)).IsEqualTo(expected);
    }

    // A width of 100% or less tries the narrower widths first, then the wider widths.
    // A wider request tries the wider widths first, then the narrower widths.
    [Test]
    [Arguments(100F, new[] { "Regular", "Condensed", "ExtraCondensed", "Expanded", "ExtraExpanded" })]
    [Arguments(90F, new[] { "Condensed", "ExtraCondensed", "Regular", "Expanded", "ExtraExpanded" })]
    [Arguments(70F, new[] { "ExtraCondensed", "Condensed", "Regular", "Expanded", "ExtraExpanded" })]
    [Arguments(50F, new[] { "ExtraCondensed", "Condensed", "Regular", "Expanded", "ExtraExpanded" })]
    [Arguments(110F, new[] { "Expanded", "ExtraExpanded", "Regular", "Condensed", "ExtraCondensed" })]
    [Arguments(125F, new[] { "Expanded", "ExtraExpanded", "Regular", "Condensed", "ExtraCondensed" })]
    [Arguments(130F, new[] { "ExtraExpanded", "Expanded", "Regular", "Condensed", "ExtraCondensed" })]
    [Arguments(200F, new[] { "ExtraExpanded", "Expanded", "Regular", "Condensed", "ExtraCondensed" })]
    public async Task WidthSearchOrder(float percent, string[] expected)
    {
        string[] order = MatchingSans.SearchOrder(Widths, FontStyle.Regular.WithWidth(new FontWidth(percent)));

        await Assert.That(order).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    // An italic request tries italic faces, then oblique faces from 11 degrees up, then oblique faces below 11 degrees,
    // then upright faces.
    [Test]
    public async Task ItalicSlopeSearchOrder()
    {
        string[] order = MatchingSans.SearchOrder(Slopes, FontStyle.Italic);

        await Assert.That(order).IsEquivalentTo(["Italic", "SteepOblique", "Oblique", "Regular"], CollectionOrdering.Matching);
    }

    // An oblique request of 11 degrees or more tries the steeper faces first, then the shallower, then italic, then upright.
    [Test]
    [Arguments(14F, new[] { "SteepOblique", "Oblique", "Italic", "Regular" })]
    [Arguments(25F, new[] { "SteepOblique", "Oblique", "Italic", "Regular" })]
    [Arguments(11F, new[] { "SteepOblique", "Oblique", "Italic", "Regular" })]
    [Arguments(20F, new[] { "SteepOblique", "Oblique", "Italic", "Regular" })]
    public async Task ObliqueSlopeSearchOrderFrom11Degrees(float degrees, string[] expected)
    {
        string[] order = MatchingSans.SearchOrder(Slopes, FontStyle.Regular.WithSlope(FontSlope.Oblique(degrees)));

        await Assert.That(order).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    // An oblique request below 11 degrees tries the shallower faces first, then the steeper, then italic, then upright.
    [Test]
    [Arguments(10F, new[] { "Oblique", "SteepOblique", "Italic", "Regular" })]
    [Arguments(5F, new[] { "Oblique", "SteepOblique", "Italic", "Regular" })]
    [Arguments(0F, new[] { "Regular", "Oblique", "SteepOblique", "Italic" })]
    public async Task ObliqueSlopeSearchOrderBelow11Degrees(float degrees, string[] expected)
    {
        string[] order = MatchingSans.SearchOrder(Slopes, FontStyle.Regular.WithSlope(FontSlope.Oblique(degrees)));

        await Assert.That(order).IsEquivalentTo(expected, CollectionOrdering.Matching);
    }

    // A normal request tries upright faces, then oblique faces from the shallowest, then italic faces.
    [Test]
    public async Task NormalSlopeSearchOrder()
    {
        string[] order = MatchingSans.SearchOrder(Slopes, FontStyle.Regular);

        await Assert.That(order).IsEquivalentTo(["Regular", "Oblique", "SteepOblique", "Italic"], CollectionOrdering.Matching);
    }

    // A request that leans to the left follows the steps of its mirror image: the faces that lean to the right
    // count as leaning the other way, so italic comes first, then upright, then the faces that lean right.
    [Test]
    public async Task BackwardsObliqueSlopeSearchOrder()
    {
        string[] order = MatchingSans.SearchOrder(Slopes, FontStyle.Regular.WithSlope(FontSlope.Oblique(-10F)));

        await Assert.That(order).IsEquivalentTo(["Italic", "Regular", "Oblique", "SteepOblique"], CollectionOrdering.Matching);
    }

    [Test]
    public async Task WidthIsMatchedBeforeSlope()
    {
        // The italic face has the requested width. The upright face has the width of the condensed request.
        FontFamily family = MatchingSans.Family("Italic", "Condensed");

        FontMetrics face = family.MatchFace(FontStyle.Italic.WithWidth(FontWidth.Condensed));

        await Assert.That(MatchingSans.FaceName(face)).IsEqualTo("Condensed");
    }

    [Test]
    public async Task SlopeIsMatchedBeforeWeight()
    {
        // A bold italic request takes the italic face of weight 400 over the upright face of weight 700.
        FontFamily family = MatchingSans.Family("Bold", "Italic");

        FontMetrics face = family.MatchFace(FontStyle.BoldItalic);

        await Assert.That(MatchingSans.FaceName(face)).IsEqualTo("Italic");
    }

    [Test]
    public async Task WidthIsMatchedBeforeWeight()
    {
        FontFamily family = MatchingSans.Family("Bold", "Condensed");

        FontMetrics face = family.MatchFace(FontStyle.Bold.WithWidth(FontWidth.Condensed));

        await Assert.That(MatchingSans.FaceName(face)).IsEqualTo("Condensed");
    }

    [Test]
    public async Task WeightChoosesAmongTheFacesOfTheMatchedSlope()
    {
        FontFamily family = MatchingSans.Family("Light", "Regular", "Bold", "LightItalic", "Italic", "BoldItalic");

        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.BoldItalic))).IsEqualTo("BoldItalic");
        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Italic))).IsEqualTo("Italic");
        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Italic.WithWeight(FontWeight.Light)))).IsEqualTo("LightItalic");
        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Bold))).IsEqualTo("Bold");
        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Regular))).IsEqualTo("Regular");
    }

    [Test]
    public async Task EveryFaceOfTheFamilyMatchesItsOwnStyle()
    {
        FontFamily family = MatchingSans.Family(MatchingSans.AllFaces);

        foreach (string name in MatchingSans.AllFaces)
        {
            FontMetrics face = family.MatchFace(MatchingSans.StyleOf(name));

            await Assert.That(MatchingSans.FaceName(face)).IsEqualTo(name);
        }
    }

    [Test]
    public async Task FamilyAlwaysGivesAFace()
    {
        // The family has one bold face. A request for another style falls back to it.
        FontFamily family = MatchingSans.Family("Bold");

        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Regular))).IsEqualTo("Bold");
        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Italic))).IsEqualTo("Bold");
        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Regular.WithWidth(FontWidth.UltraCondensed)))).IsEqualTo("Bold");
        await Assert.That(MatchingSans.FaceName(family.MatchFace(FontStyle.Regular.WithSlope(FontSlope.Oblique(-30F))))).IsEqualTo("Bold");
    }

    [Test]
    public async Task OfFacesThatTieTheFirstAddedWins()
    {
        string copy = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ttf");
        File.Copy(TestFonts.MatchingSansFile("Regular"), copy);
        try
        {
            FontCollection collection = new();
            collection.Add(copy);
            collection.Add(TestFonts.MatchingSansFile("Regular"));
            FontFamily family = collection.Get("Matching Sans");

            FontMetrics face = family.MatchFace(FontStyle.Regular);

            await Assert.That(family.Faces.Count).IsEqualTo(2);
            await Assert.That(((FileFontMetrics)face).Path).IsEqualTo(copy);
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task FacesAreInTheOrderInWhichTheyWereAdded()
    {
        string[] added = ["Bold", "Thin", "Italic", "Regular"];

        FontFamily family = MatchingSans.Family(added);

        await Assert.That(family.Faces.Select(MatchingSans.FaceName)).IsEquivalentTo(added, CollectionOrdering.Matching);
    }

    [Test]
    public async Task FontUsesTheMatchedFace()
    {
        FontFamily family = MatchingSans.Family(MatchingSans.AllFaces);

        await Assert.That(MatchingSans.FaceName(family.CreateFont(12).FontMetrics)).IsEqualTo("Regular");
        await Assert.That(MatchingSans.FaceName(family.CreateFont(12, FontStyle.Bold).FontMetrics)).IsEqualTo("Bold");
        await Assert.That(MatchingSans.FaceName(family.CreateFont(12, FontStyle.BoldItalic).FontMetrics)).IsEqualTo("BoldItalic");
        await Assert.That(MatchingSans.FaceName(family.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.Light)).FontMetrics)).IsEqualTo("Light");
        await Assert.That(MatchingSans.FaceName(family.CreateFont(12, FontStyle.Regular.WithWidth(FontWidth.Condensed)).FontMetrics)).IsEqualTo("Condensed");
        await Assert.That(MatchingSans.FaceName(family.CreateFont(12).WithStyle(FontStyle.Italic).FontMetrics)).IsEqualTo("Italic");
    }

    [Test]
    public async Task FontNeverFailsToFindAFace()
    {
        FontFamily family = MatchingSans.Family("Light");

        Font font = family.CreateFont(12, FontStyle.BoldItalic);

        await Assert.That(font.Style).IsEqualTo(FontStyle.BoldItalic);
        await Assert.That(MatchingSans.FaceName(font.FontMetrics)).IsEqualTo("Light");
    }

    [Test]
    public async Task FamilyWithRegularAndBoldRendersAsBefore()
    {
        // Glyph A is 630 units wide in the regular face and 660 in the bold face, at 1000 units to the em.
        FontFamily family = MatchingSans.Family("Regular", "Bold");

        float regular = Advance(family.CreateFont(1000));
        float bold = Advance(family.CreateFont(1000, FontStyle.Bold));
        float italic = Advance(family.CreateFont(1000, FontStyle.Italic));
        float boldItalic = Advance(family.CreateFont(1000, FontStyle.BoldItalic));

        await Assert.That(regular).IsEqualTo(630F).Within(0.01F);
        await Assert.That(bold).IsEqualTo(660F).Within(0.01F);

        // Italic falls back to the upright face of the weight, as it did before.
        await Assert.That(italic).IsEqualTo(630F).Within(0.01F);
        await Assert.That(boldItalic).IsEqualTo(660F).Within(0.01F);
    }

    [Test]
    public async Task EachFaceDrawsItsOwnGlyphs()
    {
        FontFamily family = MatchingSans.Family(MatchingSans.AllFaces);

        // The generator gives the face with index i an advance of 600 + 10 * i for glyph A.
        for (int i = 0; i < MatchingSans.AllFaces.Length; i++)
        {
            string name = MatchingSans.AllFaces[i];
            float advance = Advance(family.CreateFont(1000, MatchingSans.StyleOf(name)));

            await Assert.That(advance).IsEqualTo(600F + (10 * i)).Within(0.01F);
        }
    }

    [Test]
    public async Task FallbackFontsKeepTheStyleOfTheFont()
    {
        // The Khmer font has no glyph A, so the fallback family draws it. It has the bold face that the style asks for.
        FontFamily khmer = new FontCollection().Add(TestFonts.NotoSansKhmerRegular);
        FontFamily fallback = MatchingSans.Family("Regular", "Bold");

        TextOptions options = new(khmer.CreateFont(1000, FontStyle.Bold))
        {
            FallbackFontFamilies = [fallback],
        };

        await Assert.That(TextMeasurer.MeasureAdvance("A", options).Width).IsEqualTo(660F).Within(0.01F);
    }

    private static float Advance(Font font)
        => TextMeasurer.MeasureAdvance("A", new TextOptions(font)).Width;
}
