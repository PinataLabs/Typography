// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

public class FontCollectionTests
{
    [Test]
    public async Task AddViaPathReturnsDescription()
    {
        var sut = new FontCollection();
        sut.Add(TestFonts.CarterOneFile, out FontDescription description);

        await Assert.That(description).IsNotNull();
        await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("Carter One");
        await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
        await Assert.That(description.StyleRange.Contains(FontStyle.Regular)).IsTrue();
    }

    [Test]
    public async Task AddViaPathAddFontFileInstances()
    {
        var sut = new FontCollection();
        FontFamily family = sut.Add(TestFonts.CarterOneFile, out FontDescription descriptions);

        IEnumerable<FontMetrics> allInstances = ((IReadOnlyFontMetricsCollection)sut).GetAllMetrics(family.Name, CultureInfo.InvariantCulture);

        foreach (FontMetrics i in allInstances)
        {
            await Assert.That(i).IsTypeOf<FileFontMetrics>();
        }
    }

    [Test]
    public async Task AddViaStreamReturnsDescription()
    {
        var sut = new FontCollection();
        using Stream s = TestFonts.CarterOneFileData();
        FontFamily family = sut.Add(s, out FontDescription description);
        await Assert.That(description).IsNotNull();
        await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("Carter One");
        await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
        await Assert.That(description.StyleRange.Contains(FontStyle.Regular)).IsTrue();
    }

    [Test]
    public async Task VariableFontJoinsTheTypographicFamily()
    {
        // Teko-Variable.ttf has name ID 1 "Teko Light" and name ID 16 "Teko".
        var sut = new FontCollection();
        FontFamily family = sut.Add(TestFonts.TekoVariableFile, out FontDescription description);

        await Assert.That(family.Name).IsEqualTo("Teko");
        await Assert.That(description.FontFamilyInvariantCulture).IsEqualTo("Teko");
        await Assert.That(description.LegacyFontFamily(CultureInfo.InvariantCulture)).IsEqualTo("Teko Light");
        await Assert.That(sut.Get("Teko")).IsEqualTo(family);
        await Assert.That(sut.Get("teko")).IsEqualTo(family);
        await Assert.That(sut.TryGet("Teko Light", out _)).IsFalse();
        await Assert.That(sut.Families.Select(x => x.Name)).IsEquivalentTo(["Teko"]);
    }

    [Test]
    public async Task VariableFontIsStillDrawnAtItsDefaultInstance()
    {
        var sut = new FontCollection();
        FontFamily family = sut.Add(TestFonts.TekoVariableFile);

        // fontTools reads the advance of A at the default instance, wght 300, from hmtx: 343 of 1000 units.
        Font font = family.CreateFont(12);

        await Assert.That(font.FontMetrics.UnitsPerEm).IsEqualTo((ushort)1000);
        await Assert.That(font.TryGetGlyphs(new CodePoint('A'), out IReadOnlyList<Glyph>? glyphs)).IsTrue();
        await Assert.That(glyphs![0].GlyphMetrics.AdvanceWidth).IsEqualTo((ushort)343);
    }

    [Test]
    public async Task StaticFacesOfOneTypographicFamilyShareAFamily()
    {
        // Matching Sans has 19 faces, so only the typographic family name can group them.
        // Four of them have no name ID 16 and give their name ID 1.
        var sut = new FontCollection();
        foreach (string face in MatchingSans.AllFaces)
        {
            sut.Add(TestFonts.MatchingSansFile(face));
        }

        FontFamily family = sut.Get("Matching Sans");

        await Assert.That(sut.Families.Select(x => x.Name)).IsEquivalentTo(["Matching Sans"]);
        await Assert.That(((IReadOnlyFontMetricsCollection)sut).GetAllMetrics(family.Name, family.Culture).Count).IsEqualTo(MatchingSans.AllFaces.Length);
        await Assert.That(sut.TryGet("Matching Sans Light", out _)).IsFalse();
    }

    [Test]
    public async Task LegacyFamilyNameIsStillAvailable()
    {
        var sut = new FontCollection();
        sut.Add(TestFonts.MatchingSansFile("Light"), out FontDescription light);
        sut.Add(TestFonts.MatchingSansFile("Bold"), out FontDescription bold);

        await Assert.That(light.FontFamilyInvariantCulture).IsEqualTo("Matching Sans");
        await Assert.That(light.LegacyFontFamily(CultureInfo.InvariantCulture)).IsEqualTo("Matching Sans Light");
        await Assert.That(bold.FontFamilyInvariantCulture).IsEqualTo("Matching Sans");
        await Assert.That(bold.LegacyFontFamily(CultureInfo.InvariantCulture)).IsEqualTo("Matching Sans");
    }

    [Test]
    public async Task NotFoundThrowsCorrectException()
    {
        const string invalid = "qwerty";
        FontFamilyNotFoundException? ex = await Assert.That(() => new FontCollection().Get(invalid)).ThrowsExactly<FontFamilyNotFoundException>();

        await Assert.That(ex!.FontFamily).IsEqualTo(invalid);
        await Assert.That(ex.SearchDirectories).IsEmpty();
    }

    [Test]
    public async Task CanAddSystemFonts()
    {
        var collection = new FontCollection();

        await Assert.That(collection.Families.Any()).IsFalse();

        collection.AddSystemFonts();

        await Assert.That(collection.Families.Any()).IsTrue();
        await Assert.That(SystemFonts.Families.Count()).IsEqualTo(collection.Families.Count());
    }

    [Test]
    public async Task CanAddSystemFontsWithFilter()
    {
        var collection = new FontCollection();
        collection.AddSystemFonts(_ => false);

        await Assert.That(collection.Families.Any()).IsFalse();

        collection.AddSystemFonts(_ => true);

        await Assert.That(collection.Families.Any()).IsTrue();
        await Assert.That(collection.Families.Count()).IsEqualTo(SystemFonts.Collection.Families.Count());
    }
}
