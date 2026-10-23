// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class TrueTypeCollectionTests
{
    [Test]
    public async Task AddViaPathReturnsDescription()
    {
        var suit = new FontCollection();
        IEnumerable<FontFamily> collectionFromPath = suit.AddCollection(TestFonts.SimpleTrueTypeCollection, out IEnumerable<FontDescription> descriptions);

        await Assert.That(descriptions.Count()).IsEqualTo(2);
        FontFamily openSans = await Assert.That(collectionFromPath).HasSingleItem(x => x.Name == "Open Sans");
        FontFamily abFont = await Assert.That(collectionFromPath).HasSingleItem(x => x.Name == "SixLaborsSampleAB");

        await Assert.That(descriptions.Count()).IsEqualTo(2);
        FontDescription openSansDescription = await Assert.That(descriptions).HasSingleItem(x => x.FontNameInvariantCulture == "Open Sans");
        FontDescription abFontDescription = await Assert.That(descriptions).HasSingleItem(x => x.FontNameInvariantCulture == "SixLaborsSampleAB regular");
    }

    [Test]
    public async Task AddViaPathAddFontFileInstances()
    {
        var sut = new FontCollection();
        IEnumerable<FontFamily> collectionFromPath = sut.AddCollection(TestFonts.SimpleTrueTypeCollection, out IEnumerable<FontDescription> descriptions);

        IEnumerable<FontMetrics> allInstances = sut.Families.SelectMany(x => ((IReadOnlyFontMetricsCollection)sut).GetAllMetrics(x.Name, CultureInfo.InvariantCulture));

        foreach (FontMetrics i in allInstances)
        {
            await Assert.That(i).IsTypeOf<FileFontMetrics>();
        }
    }

    [Test]
    public async Task AddViaStreamReturnsDescription()
    {
        var suit = new FontCollection();
        IEnumerable<FontFamily> collectionFromPath = suit.AddCollection(TestFonts.SSimpleTrueTypeCollectionData(), out IEnumerable<FontDescription> descriptions);

        await Assert.That(collectionFromPath.Count()).IsEqualTo(2);
        FontFamily openSans = await Assert.That(collectionFromPath).HasSingleItem(x => x.Name == "Open Sans");
        FontFamily abFont = await Assert.That(collectionFromPath).HasSingleItem(x => x.Name == "SixLaborsSampleAB");

        await Assert.That(descriptions.Count()).IsEqualTo(2);
        FontDescription openSansDescription = await Assert.That(descriptions).HasSingleItem(x => x.FontNameInvariantCulture == "Open Sans");
        FontDescription abFontDescription = await Assert.That(descriptions).HasSingleItem(x => x.FontNameInvariantCulture == "SixLaborsSampleAB regular");
    }
}
