// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tests.Fakes;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.WellKnownIds;

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
    public async Task VariableFontHasTheAdvanceOfHmtxAtItsDefaultInstance()
    {
        var sut = new FontCollection();
        FontFamily family = sut.Add(TestFonts.TekoVariableFile);

        // fontTools reads the advance of A at the default instance, wght 300, from hmtx: 343 of 1000 units.
        // A regular request is the instance wght 400, so the test asks for the default.
        Font font = family.CreateFont(12, default, [.. family.Faces[0].Variations]);

        await Assert.That(font.FontMetrics.UnitsPerEm).IsEqualTo((ushort)1000);
        await Assert.That(font.TryGetGlyphs(new CodePoint('A'), out IReadOnlyList<Glyph>? glyphs)).IsTrue();
        await Assert.That(glyphs![0].GlyphMetrics.AdvanceWidth).IsEqualTo(343);
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

    [Test]
    public async Task FacesWithDifferentLocalizedNamesStayInOneFamily()
    {
        // The Light face has the typographic name only in English. The Regular face has its family name in Japanese.
        // Each face gives a different name for ja-JP, and they must still be one family.
        var ja = new CultureInfo("ja-JP");
        FakeFontInstance light = FakeFontInstance.CreateWithNames(
            Name(KnownNameIds.FontFamilyName, "Foo Light", 0x0409),
            Name(KnownNameIds.TypographicFamilyName, "Foo", 0x0409));
        FakeFontInstance regular = FakeFontInstance.CreateWithNames(
            Name(KnownNameIds.FontFamilyName, "Foo", 0x0409),
            Name(KnownNameIds.FontFamilyName, "フー", 0x0411));
        var collection = new FontCollection();
        ((IFontMetricsCollection)collection).AddMetrics(light);
        ((IFontMetricsCollection)collection).AddMetrics(regular);

        FontFamily[] families = collection.GetByCulture(ja).ToArray();

        await Assert.That(families.Length).IsEqualTo(1);
        await Assert.That(families[0].Faces.Count).IsEqualTo(2);

        // Either name of the family finds both faces, and the family is the same one.
        FontFamily byJapanese = collection.Get("フー", ja);
        FontFamily byEnglish = collection.Get("Foo", ja);
        await Assert.That(byJapanese.Faces.Count).IsEqualTo(2);
        await Assert.That(byEnglish.Faces.Count).IsEqualTo(2);
        await Assert.That(byJapanese).IsEqualTo(byEnglish);
        await Assert.That(byJapanese).IsEqualTo(families[0]);
    }

    [Test]
    public async Task FamiliesWithNamesThatDifferOnlyInCaseAreOneFamilyInTheCulture()
    {
        var ja = new CultureInfo("ja-JP");
        var collection = new FontCollection();
        var metrics = (IFontMetricsCollection)collection;
        metrics.AddMetrics(FakeFontInstance.CreateWithNames(
            Name(KnownNameIds.FontFamilyName, "Alpha", 0x0409),
            Name(KnownNameIds.FontFamilyName, "Same", 0x0411)));
        metrics.AddMetrics(FakeFontInstance.CreateWithNames(
            Name(KnownNameIds.FontFamilyName, "Beta", 0x0409),
            Name(KnownNameIds.FontFamilyName, "SAME", 0x0411)));

        FontFamily[] families = collection.GetByCulture(ja).ToArray();

        await Assert.That(families.Length).IsEqualTo(1);
        await Assert.That(collection.GetByCulture(CultureInfo.InvariantCulture).Count()).IsEqualTo(2);
    }

    [Test]
    public async Task FacesKeepTheOrderInWhichTheyWereAdded()
    {
        var collection = new FontCollection();
        var metrics = (IFontMetricsCollection)collection;
        FakeFontInstance[] faces = [.. Enumerable.Range(0, 50).Select(i => FakeFontInstance.CreateWithNames(Name(KnownNameIds.FontFamilyName, "Family", 0x0409)))];
        foreach (FakeFontInstance face in Enumerable.Reverse(faces))
        {
            metrics.AddMetrics(face);
        }

        // Adding a face again changes neither the order nor the count.
        metrics.AddMetrics(faces[10]);

        await Assert.That(collection.Get("Family").Faces.Cast<FakeFontInstance>().SequenceEqual(Enumerable.Reverse(faces))).IsTrue();
    }

    [Test]
    public async Task ReadingWhileAddingFacesDoesNotThrow()
    {
        // MatchFace reads the collection on every Font, so a thread that creates fonts
        // can overlap another thread that adds faces.
        const int Count = 300;
        FakeFontInstance[] faces = [.. Enumerable.Range(0, Count).Select(i => FakeFontInstance.CreateWithNames(Name(KnownNameIds.FontFamilyName, "Family " + (i % 7), 0x0409)))];
        FontCollection current = new();
        using CancellationTokenSource stop = new();
        List<Exception> errors = [];

        Task[] readers = [.. Enumerable.Range(0, 3).Select(reader => Task.Run(() =>
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    FontCollection collection = Volatile.Read(ref current);
                    var metrics = (IFontMetricsCollection)collection;
                    _ = metrics.GetAllMetrics("Family 0", CultureInfo.InvariantCulture);
                    _ = collection.Families.Count();
                    _ = collection.TryGet("Family 3", out _);
                    using IEnumerator<FontMetrics> e = metrics.GetEnumerator();
                    while (e.MoveNext())
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                lock (errors)
                {
                    errors.Add(ex);
                }
            }
        }))];

        // Without the lock a reader fails in the first rounds. The time limit is what a pass costs.
        DateTime end = DateTime.UtcNow.AddSeconds(2);
        while (DateTime.UtcNow < end)
        {
            var next = new FontCollection();
            var metrics = (IFontMetricsCollection)next;
            Volatile.Write(ref current, next);
            foreach (FakeFontInstance face in faces)
            {
                metrics.AddMetrics(face);
            }

            lock (errors)
            {
                if (errors.Count > 0)
                {
                    break;
                }
            }
        }

        await stop.CancelAsync();
        await Task.WhenAll(readers);

        await Assert.That(errors).IsEmpty();
    }

    private static NameRecord Name(KnownNameIds id, string value, ushort languageId)
        => new(PlatformIDs.Windows, languageId, id, value);
}
