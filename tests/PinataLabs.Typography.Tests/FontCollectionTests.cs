// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
        await Assert.That(description.Style).IsEqualTo(FontStyle.Regular);
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
        await Assert.That(description.Style).IsEqualTo(FontStyle.Regular);
    }

    [Test]
    public async Task NotFoundThrowsCorrectException()
    {
        const string invalid = "qwerty";
        FontFamilyNotFoundException ex = await Assert.That(() => new FontCollection().Get(invalid)).ThrowsExactly<FontFamilyNotFoundException>();

        await Assert.That(ex.FontFamily).IsEqualTo(invalid);
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
