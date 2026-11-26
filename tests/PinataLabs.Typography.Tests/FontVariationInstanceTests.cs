// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Unicode;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests;

/// <summary>
/// A <see cref="Font"/> of a variable face draws with a variation instance, which the style, the size and the explicit
/// variations choose. Fonts that ask for the same coordinates share one instance for as long as one of them is alive.
/// </summary>
public class FontVariationInstanceTests
{
    private static readonly Tag Weight = Tag.Parse("wght");
    private static readonly Tag Width = Tag.Parse("wdth");
    private static readonly Tag Slant = Tag.Parse("slnt");
    private static readonly Tag OpticalSize = Tag.Parse("opsz");
    private static readonly Tag Italic = Tag.Parse("ital");

    [Test]
    public async Task ARegularRequestOnTekoDrawsTheWeight400()
    {
        // The default instance of Teko is at wght 300, so a regular request is a variation instance.
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font font = family.CreateFont(12);

        await Assert.That(font.FontMetrics.Variations.Single().Axis).IsEqualTo(Weight);
        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(400F);
        await Assert.That(font.FontMetrics).IsNotSameReferenceAs(family.Faces[0]);
        await Assert.That(font.FontMetrics.Description).IsSameReferenceAs(family.Faces[0].Description);
        await Assert.That(font.FontMetrics.Face).IsSameReferenceAs(family.Faces[0]);
    }

    [Test]
    [Arguments("Variations/Teko-Variable.woff")]
    [Arguments("Variations/Teko-Variable.woff2")]
    public async Task ACompressedVariableFontHasItsAxesAndInstances(string path)
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(path));

        Font bold = family.CreateFont(12, FontStyle.Bold);

        await Assert.That(family.Faces[0].VariationAxes.Single().Tag).IsEqualTo(Weight);
        await Assert.That(family.Faces[0].NamedInstances.Count).IsEqualTo(5);
        await Assert.That(family.Faces[0].Description.StyleRange.MaxWeight.Value).IsEqualTo(700F);
        await Assert.That(bold.FontMetrics.Variations.Single().Value).IsEqualTo(700F);
        await Assert.That((int)bold.FontMetrics.Coordinates.Values[0]).IsEqualTo(16384);
        await Assert.That((int)family.CreateFont(12).FontMetrics.Coordinates.Values[0]).IsEqualTo(3452);
        await Assert.That(bold.Name).IsEqualTo("Teko Bold");
    }

    [Test]
    public async Task ABoldRequestOnTekoDrawsTheWeight700()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font font = family.CreateFont(12, FontStyle.Bold);

        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(700F);
        await Assert.That(font.Style).IsEqualTo(FontStyle.Bold);
    }

    [Test]
    public async Task ALightRequestOnTekoIsTheFaceItself()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font font = family.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.Light));

        await Assert.That(font.FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(300F);
    }

    [Test]
    public async Task ASourceSansRequestIsAnInstanceOfTheCffFace()
    {
        FontFamily family = new FontCollection().Add(TestFonts.SourceSans3VariableCff2File);

        Font regular = family.CreateFont(12);
        Font bold = family.CreateFont(12, FontStyle.Bold);

        await Assert.That(regular.FontMetrics.Variations.Single().Value).IsEqualTo(400F);
        await Assert.That(bold.FontMetrics.Variations.Single().Value).IsEqualTo(700F);
        await Assert.That(regular.FontMetrics).IsNotSameReferenceAs(bold.FontMetrics);
        await Assert.That(regular.FontMetrics.Face).IsSameReferenceAs(family.Faces[0]);
    }

    [Test]
    public async Task AnInstanceVariesTheAdvancesAndKeepsWhatNoTableVaries()
    {
        // Teko has HVAR, so the advance of a glyph changes. Its MVAR has no ascender, so the ascender does not.
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        Font light = family.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.Light));
        Font bold = family.CreateFont(12, FontStyle.Bold);

        light.FontMetrics.TryGetGlyphMetrics(new CodePoint('A'), TextAttributes.None, TextDecorations.None, LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out IReadOnlyList<GlyphMetrics>? expected);
        bold.FontMetrics.TryGetGlyphMetrics(new CodePoint('A'), TextAttributes.None, TextDecorations.None, LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out IReadOnlyList<GlyphMetrics>? actual);

        await Assert.That(bold.FontMetrics.UnitsPerEm).IsEqualTo(light.FontMetrics.UnitsPerEm);
        await Assert.That(bold.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo(light.FontMetrics.HorizontalMetrics.Ascender);
        await Assert.That(actual![0].AdvanceWidth).IsNotEqualTo(expected![0].AdvanceWidth);
        await Assert.That(actual[0].FontMetrics).IsSameReferenceAs(bold.FontMetrics);
        await Assert.That(expected[0].FontMetrics.Owner).IsSameReferenceAs(family.Faces[0]);
    }

    [Test]
    public async Task ANamedInstanceGivesTheFamilyAndTheSubfamilyAsItsName()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        await Assert.That(family.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.Light)).Name).IsEqualTo("Teko Light");
        await Assert.That(family.CreateFont(12).Name).IsEqualTo("Teko Regular");
        await Assert.That(family.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.Medium)).Name).IsEqualTo("Teko Medium");
        await Assert.That(family.CreateFont(12, FontStyle.Regular.WithWeight(FontWeight.SemiBold)).Name).IsEqualTo("Teko SemiBold");
        await Assert.That(family.CreateFont(12, FontStyle.Bold).Name).IsEqualTo("Teko Bold");
        await Assert.That(family.CreateFont(12, FontStyle.Regular, new FontVariation(Weight, 600F)).Name).IsEqualTo("Teko SemiBold");
    }

    [Test]
    public async Task AnInstanceThatTheFontDoesNotNameHasTheFullNameOfTheFace()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        await Assert.That(family.CreateFont(12, FontStyle.Regular.WithWeight(new FontWeight(450F))).Name).IsEqualTo("Teko Light");
    }

    [Test]
    public async Task AnInstanceThatTheFontNamesNeedsAllItsAxesToMatch()
    {
        // VariationAxes names the instance Bold at wght 700, wdth 100, opsz 14, ital 0 and GRAD 0.
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);

        await Assert.That(family.CreateFont(14, FontStyle.Bold).Name).IsEqualTo("Variation Axes Bold");
        await Assert.That(family.CreateFont(14, FontStyle.Bold, new FontVariation(Tag.Parse("GRAD"), 10F)).Name).IsEqualTo("Variation Axes Regular");
        await Assert.That(family.CreateFont(12, FontStyle.Bold).Name).IsEqualTo("Variation Axes Regular");
        await Assert.That(family.CreateFont(14, FontStyle.Italic).Name).IsEqualTo("Variation Axes Italic");
    }

    [Test]
    public async Task FontsWithEqualCoordinatesShareOneFontMetrics()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font first = family.CreateFont(12);
        Font second = family.CreateFont(30);
        Font explicitWeight = family.CreateFont(12, FontStyle.Bold, new FontVariation(Weight, 400F));
        Font other = family.CreateFont(12, FontStyle.Bold);

        await Assert.That(second.FontMetrics).IsSameReferenceAs(first.FontMetrics);
        await Assert.That(explicitWeight.FontMetrics).IsSameReferenceAs(first.FontMetrics);
        await Assert.That(other.FontMetrics).IsNotSameReferenceAs(first.FontMetrics);
    }

    [Test]
    public async Task ValuesThatNormaliseToTheSameCoordinatesShareTheInstance()
    {
        // The F2Dot14 coordinates are 1/16384 apart, which is 0.02 units of Teko weight.
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font first = family.CreateFont(12, FontStyle.Regular, new FontVariation(Weight, 400F));
        Font second = family.CreateFont(12, FontStyle.Regular, new FontVariation(Weight, 400.001F));

        FontMetrics firstMetrics = first.FontMetrics;
        FontMetrics secondMetrics = second.FontMetrics;

        // The instance reports the values of the request that made it.
        await Assert.That(secondMetrics).IsSameReferenceAs(firstMetrics);
        await Assert.That(secondMetrics.Variations.Single().Value).IsEqualTo(400F);
    }

    [Test]
    public async Task TwoInstancesOfOneFaceHaveDifferentGlyphCaches()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font regular = family.CreateFont(12);
        Font bold = family.CreateFont(12, FontStyle.Bold);
        regular.TryGetGlyphs(new CodePoint('A'), out IReadOnlyList<Glyph>? regularGlyphs);
        bold.TryGetGlyphs(new CodePoint('A'), out IReadOnlyList<Glyph>? boldGlyphs);

        await Assert.That(regularGlyphs![0].GlyphMetrics.FontMetrics).IsSameReferenceAs(regular.FontMetrics);
        await Assert.That(boldGlyphs![0].GlyphMetrics.FontMetrics).IsSameReferenceAs(bold.FontMetrics);
    }

    [Test]
    public async Task TheInstanceIsCollectedWhenBothFontsAreGone()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        WeakReference instance = CreateTwoFontsThatShareAnInstance(family);

        await Assert.That(await IsCollected(instance)).IsTrue();
    }

    [Test]
    public async Task TheInstanceIsAliveWhileOneFontHoldsIt()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        (Font held, WeakReference instance) = CreateTwoFontsAndDropOne(family);

        bool collectedWhileHeld = await IsCollected(instance, attempts: 3);
        FontMetrics again = family.CreateFont(20).FontMetrics;
        GC.KeepAlive(held);

        await Assert.That(collectedWhileHeld).IsFalse();
        await Assert.That(again).IsSameReferenceAs(held.FontMetrics);
    }

    [Test]
    public async Task AnInstanceThatWasCollectedIsMadeAgain()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        WeakReference first = CreateTwoFontsThatShareAnInstance(family);
        await Assert.That(await IsCollected(first)).IsTrue();

        Font font = family.CreateFont(12);

        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(400F);
    }

    [Test]
    public async Task TheTableOfTheFaceDropsTheEntriesOfCollectedInstances()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        WeakReference collected = CreateTwoFontsThatShareAnInstance(family);
        await Assert.That(await IsCollected(collected)).IsTrue();

        // The next instance that the face makes removes the entries of the collected ones.
        Font font = family.CreateFont(12, FontStyle.Bold);

        await Assert.That(CountInstances(family)).IsEqualTo(1);
        GC.KeepAlive(font);
    }

    [Test]
    public async Task ManyThreadsThatAskForTheSameCoordinatesGetOneInstance()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        FontMetrics[] results = new FontMetrics[64];
        Font[] fonts = new Font[results.Length];
        using ManualResetEventSlim start = new(false);
        Task[] tasks = Enumerable.Range(0, results.Length).Select(i => Task.Run(() =>
        {
            start.Wait();
            fonts[i] = family.CreateFont(10 + i, FontStyle.Bold);
            results[i] = fonts[i].FontMetrics;
        })).ToArray();
        start.Set();
        await Task.WhenAll(tasks);

        await Assert.That(results.Distinct().Count()).IsEqualTo(1);
        GC.KeepAlive(fonts);
    }

    [Test]
    public async Task AFontPathOfAnInstanceIsThePathOfTheFace()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        Font font = family.CreateFont(12);

        bool fontHasPath = font.TryGetPath(out string? path);
        bool familyHasPaths = family.TryGetPaths(out IEnumerable<string> paths);

        await Assert.That(font.FontMetrics).IsNotSameReferenceAs(family.Faces[0]);
        await Assert.That(fontHasPath).IsTrue();
        await Assert.That(path).IsEqualTo(TestFonts.TekoVariableFile);
        await Assert.That(familyHasPaths).IsTrue();
        await Assert.That(paths).IsEquivalentTo([TestFonts.TekoVariableFile]);
    }

    [Test]
    public async Task AFontFromAStreamHasNoPath()
    {
        FontFamily family = new FontCollection().Add(new MemoryStream(File.ReadAllBytes(TestFonts.TekoVariableFile)));
        Font font = family.CreateFont(12);

        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(400F);
        await Assert.That(font.TryGetPath(out _)).IsFalse();
    }

    [Test]
    public async Task ADifferentInstanceOfAFaceGivesADifferentGlyphRendererKey()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        GlyphRenderer regular = Render(family.CreateFont(30), "A");
        GlyphRenderer sameRegular = Render(family.CreateFont(40), "A");
        GlyphRenderer bold = Render(family.CreateFont(30, FontStyle.Bold), "A");

        await Assert.That(regular.GlyphKeys.Single().FontMetrics).IsSameReferenceAs(sameRegular.GlyphKeys.Single().FontMetrics);
        await Assert.That(regular.GlyphKeys.Single().FontMetrics).IsNotSameReferenceAs(bold.GlyphKeys.Single().FontMetrics);
        await Assert.That(regular.GlyphKeys.Single().Equals(bold.GlyphKeys.Single())).IsFalse();
    }

    [Test]
    public async Task TheGlyphRendererKeyOfTheDefaultInstanceIsTheFace()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        Font light = family.CreateFont(30, FontStyle.Regular.WithWeight(FontWeight.Light));

        GlyphRenderer renderer = Render(light, "A");

        await Assert.That(renderer.GlyphKeys.Single().FontMetrics).IsSameReferenceAs(light.FontMetrics);
        await Assert.That(renderer.GlyphKeys.Single().FontMetrics).IsSameReferenceAs(family.Faces[0]);
    }

    [Test]
    public async Task AnInstanceOfAFontWithoutCvarHintsWithTheControlValuesOfTheFace()
    {
        // The glyph program of VariationAxes rounds the top of its boxes to the pixel grid. Its default instance is at
        // opsz 14, so a font of size 14 is the face, and a font of size 12 is an instance. The font has no 'cvar' table, so both
        // hint with the same control values, and the hinted box is the same at both sizes in pixels.
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);
        Font face = family.CreateFont(14);
        Font instance = family.CreateFont(12);

        List<System.Numerics.Vector2> faceHinted = Render(face, "A", HintingMode.Standard).ControlPoints;
        List<System.Numerics.Vector2> faceUnhinted = Render(face, "A", HintingMode.None).ControlPoints;
        List<System.Numerics.Vector2> instanceHinted = Render(instance, "A", HintingMode.Standard).ControlPoints;
        List<System.Numerics.Vector2> instanceUnhinted = Render(instance, "A", HintingMode.None).ControlPoints;

        await Assert.That(face.FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(instance.FontMetrics).IsNotSameReferenceAs(family.Faces[0]);
        await Assert.That(faceHinted.SequenceEqual(faceUnhinted)).IsFalse();
        await Assert.That(instanceHinted.SequenceEqual(instanceUnhinted)).IsFalse();
    }

    [Test]
    public async Task AStaticFontIgnoresVariationsAndIsItsOwnFontMetrics()
    {
        FontFamily family = new FontCollection().Add(TestFonts.OpenSansFile);

        Font font = family.CreateFont(12, FontStyle.Regular, new FontVariation(Weight, 700F));

        await Assert.That(font.FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(font.FontMetrics.Variations).IsEmpty();
        await Assert.That(font.FontMetrics.VariationAxes).IsEmpty();
        await Assert.That(font.Variations.Single()).IsEqualTo(new FontVariation(Weight, 700F));
    }

    [Test]
    public async Task ExplicitVariationsWinOverTheStyleAndTheSize()
    {
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);

        Font font = family.CreateFont(30, FontStyle.BoldItalic, new FontVariation(Weight, 500F), new FontVariation(OpticalSize, 20F), new FontVariation(Italic, 0F));

        await Assert.That(ValuesOf(font)).IsEquivalentTo([("wght", 500F), ("wdth", 100F), ("opsz", 20F), ("ital", 0F), ("GRAD", 0F)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task TheStyleSetsWeightWidthAndItalic()
    {
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);

        Font font = family.CreateFont(30, FontStyle.BoldItalic.WithWidth(FontWidth.Condensed));

        await Assert.That(ValuesOf(font)).IsEquivalentTo([("wght", 700F), ("wdth", 75F), ("opsz", 30F), ("ital", 1F), ("GRAD", 0F)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ItalicSetsSlantOnAFontWithNoItalAxis()
    {
        // The slnt axis of VariationSlant runs from -12 to 0, so the -14 of an italic request is clamped to -12.
        FontFamily family = new FontCollection().Add(TestFonts.VariationSlantFile);

        await Assert.That(ValuesOf(family.CreateFont(12, FontStyle.Italic))).IsEquivalentTo([("wght", 400F), ("slnt", -12F)], CollectionOrdering.Matching);
        await Assert.That(ValuesOf(family.CreateFont(12, FontStyle.Regular.WithSlope(FontSlope.Oblique(6F))))).IsEquivalentTo([("wght", 400F), ("slnt", -6F)], CollectionOrdering.Matching);
        await Assert.That(ValuesOf(family.CreateFont(12))).IsEquivalentTo([("wght", 400F), ("slnt", 0F)], CollectionOrdering.Matching);
    }

    [Test]
    public async Task TheSizeInPointsSetsTheOpticalSizeWithinTheAxisRange()
    {
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);

        await Assert.That(OpticalSizeOf(family.CreateFont(8))).IsEqualTo(8F);
        await Assert.That(OpticalSizeOf(family.CreateFont(24))).IsEqualTo(24F);
        await Assert.That(OpticalSizeOf(family.CreateFont(4))).IsEqualTo(8F);
        await Assert.That(OpticalSizeOf(family.CreateFont(200))).IsEqualTo(144F);
        await Assert.That(OpticalSizeOf(family.CreateFont(24, FontStyle.Regular, new FontVariation(OpticalSize, 100F)))).IsEqualTo(100F);
    }

    [Test]
    public async Task ASizeOfTheDefaultOpticalSizeGivesTheFace()
    {
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);

        await Assert.That(family.CreateFont(14).FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(family.CreateFont(14.001F).FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(family.CreateFont(15).FontMetrics).IsNotSameReferenceAs(family.Faces[0]);
    }

    [Test]
    public async Task AFontAtAnotherSizeHasAnotherOpticalSize()
    {
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);
        Font small = family.CreateFont(9);

        Font large = small.WithSize(60);

        await Assert.That(OpticalSizeOf(small)).IsEqualTo(9F);
        await Assert.That(OpticalSizeOf(large)).IsEqualTo(60F);
        await Assert.That(large.FontMetrics).IsNotSameReferenceAs(small.FontMetrics);
    }

    [Test]
    public async Task AValueOutsideTheAxisIsClampedAndAnUnknownTagIsIgnored()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font font = family.CreateFont(12, FontStyle.Regular, new FontVariation(Weight, 5000F), new FontVariation(Tag.Parse("XXXX"), 3F));
        Font bold = family.CreateFont(12, FontStyle.Bold);

        await Assert.That(font.FontMetrics).IsSameReferenceAs(bold.FontMetrics);
        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(700F);
    }

    [Test]
    public async Task TheLastValueOfARepeatedTagWinsAndTheFontListsTheTagOnce()
    {
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);

        Font font = family.CreateFont(
            14,
            FontStyle.Regular,
            new FontVariation(Weight, 300F),
            new FontVariation(Width, 80F),
            new FontVariation(Weight, 600F));

        await Assert.That(font.Variations).IsEquivalentTo([new FontVariation(Weight, 600F), new FontVariation(Width, 80F)], CollectionOrdering.Matching);
        await Assert.That(ValuesOf(font).First()).IsEqualTo(("wght", 600F));
    }

    [Test]
    public async Task AFontThatWasAskedForNoVariationsHasNone()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        Font font = family.CreateFont(12);

        await Assert.That(font.Variations).IsEmpty();
    }

    [Test]
    public async Task WithSizeAndWithStyleKeepTheVariations()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        Font font = family.CreateFont(12, FontStyle.Regular, new FontVariation(Weight, 550F));

        await Assert.That(font.WithSize(20).Variations).IsEquivalentTo([new FontVariation(Weight, 550F)]);
        await Assert.That(font.WithStyle(FontStyle.Bold).Variations).IsEquivalentTo([new FontVariation(Weight, 550F)]);
        await Assert.That(font.WithStyle(FontStyle.Bold).FontMetrics.Variations.Single().Value).IsEqualTo(550F);
    }

    [Test]
    public async Task WithVariationsReplacesTheVariations()
    {
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);
        Font font = family.CreateFont(14, FontStyle.Regular, new FontVariation(Weight, 550F), new FontVariation(Width, 90F));

        Font other = font.WithVariations(new FontVariation(Width, 110F));

        await Assert.That(other.Variations).IsEquivalentTo([new FontVariation(Width, 110F)]);
        await Assert.That(ValuesOf(other)).IsEquivalentTo([("wght", 400F), ("wdth", 110F), ("opsz", 14F), ("ital", 0F), ("GRAD", 0F)], CollectionOrdering.Matching);
        await Assert.That(font.WithVariations().Variations).IsEmpty();
        await Assert.That(other.Size).IsEqualTo(14F);
    }

    [Test]
    public async Task AVariationsArrayConvertsToTheSpanParameter()
    {
        // A caller on a compiler without params collections passes an array. This is the call that it makes.
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        FontVariation[] variations = [new FontVariation(Weight, 650F)];

        Font font = family.CreateFont(12, FontStyle.Regular, variations);
        Font constructed = new(family, 12, FontStyle.Regular, variations);

        await Assert.That(font.FontMetrics).IsSameReferenceAs(constructed.FontMetrics);
        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(650F);

        // The font copies the values.
        variations[0] = new FontVariation(Weight, 300F);
        await Assert.That(font.Variations.Single().Value).IsEqualTo(650F);
    }

    [Test]
    public async Task ANamedInstanceIsDrawnByPassingItsVariations()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        FontNamedInstance semiBold = family.Faces[0].NamedInstances.Single(x => x.Name(System.Globalization.CultureInfo.InvariantCulture) == "SemiBold");

        Font font = family.CreateFont(12, FontStyle.Regular, semiBold.Variations.ToArray());

        await Assert.That(font.FontMetrics.Variations.Single().Value).IsEqualTo(600F);
        await Assert.That(font.Name).IsEqualTo("Teko SemiBold");
    }

    [Test]
    public async Task TheNormalisedCoordinatesOfAnInstanceAreTheF2Dot14ValuesOfTheReference()
    {
        // fontTools gives 3452 for wght 400 and 16384 for wght 700 in Teko.
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);

        await Assert.That((int)family.CreateFont(12).FontMetrics.Coordinates.Values[0]).IsEqualTo(3452);
        await Assert.That((int)family.CreateFont(12, FontStyle.Bold).FontMetrics.Coordinates.Values[0]).IsEqualTo(16384);
        await Assert.That(family.Faces[0].Coordinates.IsDefault).IsTrue();
    }

    [Test]
    public async Task AFallbackFontCarriesTheVariationsOfTheFont()
    {
        // VariationSlant has the glyph A and not B, so the layout takes B from Teko, with the variations of the font.
        FontCollection collection = new();
        FontFamily primary = collection.Add(TestFonts.VariationSlantFile);
        FontFamily fallback = collection.Add(TestFonts.TekoVariableFile);
        Font font = primary.CreateFont(30, FontStyle.Bold, new FontVariation(Weight, 650F));

        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "AB", new TextOptions(font) { FallbackFontFamilies = [fallback], Dpi = 72 });

        GlyphRendererParameters fromPrimary = renderer.GlyphKeys.Single(x => x.CodePoint == new CodePoint('A'));
        GlyphRendererParameters fromFallback = renderer.GlyphKeys.Single(x => x.CodePoint == new CodePoint('B'));

        await Assert.That(fromPrimary.FontMetrics.Face).IsSameReferenceAs(primary.Faces[0]);
        await Assert.That(fromFallback.FontMetrics.Face).IsSameReferenceAs(fallback.Faces[0]);
        await Assert.That(fromFallback.FontMetrics.Variations.Single().Value).IsEqualTo(650F);
        await Assert.That(fromFallback.PointSize).IsEqualTo(30F);
    }

    [Test]
    public async Task AFallbackFontCarriesTheStyleOfTheFont()
    {
        FontCollection collection = new();
        FontFamily primary = collection.Add(TestFonts.VariationSlantFile);
        FontFamily fallback = collection.Add(TestFonts.TekoVariableFile);

        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "AB", new TextOptions(primary.CreateFont(30, FontStyle.Bold)) { FallbackFontFamilies = [fallback], Dpi = 72 });

        await Assert.That(renderer.GlyphKeys.Single(x => x.CodePoint == new CodePoint('B')).FontMetrics.Variations.Single().Value).IsEqualTo(700F);
    }

    [Test]
    public async Task AFallbackFontIgnoresTheVariationsThatItsFaceLacks()
    {
        // Teko has no wdth axis, so the width of the font does not reach it.
        FontCollection collection = new();
        FontFamily primary = collection.Add(TestFonts.VariationAxesFile);
        FontFamily fallback = collection.Add(TestFonts.TekoVariableFile);
        Font font = primary.CreateFont(30, FontStyle.Regular, new FontVariation(Width, 80F));

        Font fallbackFont = font.WithFamily(fallback);

        await Assert.That(fallbackFont.Variations).IsEquivalentTo([new FontVariation(Width, 80F)]);
        await Assert.That(fallbackFont.FontMetrics.Variations.Single().Value).IsEqualTo(400F);
        await Assert.That(fallbackFont.Size).IsEqualTo(30F);
    }

    private static GlyphRenderer Render(Font font, string text, HintingMode hinting = HintingMode.None)
    {
        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, text, new TextOptions(font) { HintingMode = hinting, Dpi = 72 });
        return renderer;
    }

    private static (string Axis, float Value)[] ValuesOf(Font font)
        => font.FontMetrics.Variations.Select(x => (x.Axis.ToString(), x.Value)).ToArray();

    private static float OpticalSizeOf(Font font)
        => font.FontMetrics.Variations.Single(x => x.Axis == OpticalSize).Value;

    // The garbage collector needs the objects out of every live local, so the method that makes them does not inline.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateTwoFontsThatShareAnInstance(FontFamily family)
    {
        Font first = family.CreateFont(12);
        Font second = family.CreateFont(30);
        FontMetrics instance = first.FontMetrics;
        if (!ReferenceEquals(instance, second.FontMetrics))
        {
            throw new InvalidOperationException("The fonts did not share an instance.");
        }

        // Drawing text fills the glyph caches of the instance.
        TextMeasurer.MeasureAdvance("Hello", new TextOptions(first));
        return new WeakReference(instance);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (Font Held, WeakReference Instance) CreateTwoFontsAndDropOne(FontFamily family)
    {
        Font held = family.CreateFont(12);
        Font dropped = family.CreateFont(30);
        if (!ReferenceEquals(held.FontMetrics, dropped.FontMetrics))
        {
            throw new InvalidOperationException("The fonts did not share an instance.");
        }

        return (held, new WeakReference(held.FontMetrics));
    }

    private static async Task<bool> IsCollected(WeakReference reference, int attempts = 20)
    {
        for (int i = 0; i < attempts && reference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Yield();
        }

        return !reference.IsAlive;
    }

    private static int CountInstances(FontFamily family)
    {
        FieldInfo stream = typeof(FileFontMetrics).GetField("fontMetrics", BindingFlags.Instance | BindingFlags.NonPublic)!;
        StreamFontMetrics face = ((Lazy<StreamFontMetrics>)stream.GetValue(family.Faces[0])!).Value;
        FieldInfo table = typeof(StreamFontMetrics).GetField("instances", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return ((System.Collections.ICollection)table.GetValue(face)!).Count;
    }
}
