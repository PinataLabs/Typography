// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests;

public class SystemFontCollectionTests
{
    private static readonly SystemFontCollection SysFontCollection = new();

    [Test]
    public async Task SystemFonts_IsPopulated()
    {
        await Assert.That(SystemFonts.Collection.Families.Any()).IsTrue();
        await Assert.That(SystemFonts.Families).IsEquivalentTo(SystemFonts.Collection.Families, CollectionOrdering.Matching);
    }

    [Test]
    public async Task SystemFonts_CanGetFont()
    {
        FontFamily family = SystemFonts.Families.First();

        await Assert.That(family == default).IsFalse();
        await Assert.That(SystemFonts.Get(family.Name)).IsEqualTo(family);

        SystemFonts.TryGet(family.Name, out FontFamily family2);
        await Assert.That(family2).IsEqualTo(family);
    }

    [Test]
    public async Task SystemFonts_CanGetFont_ByCulture()
    {
        FontFamily family = SystemFonts.Families.First();

        await Assert.That(family == default).IsFalse();
        await Assert.That(SystemFonts.Get(family.Name, family.Culture)).IsEqualTo(family);

        SystemFonts.TryGet(family.Name, family.Culture, out FontFamily family2);

        await Assert.That(family2).IsEqualTo(family);
        await Assert.That(SystemFonts.GetByCulture(family.Culture)).Contains(family);
    }

    [Test]
    public async Task SystemFonts_CanCreateFont()
    {
        FontFamily family = SystemFonts.Families.First();
        Font font = SystemFonts.CreateFont(family.Name, 12F);

        await Assert.That(font).IsNotNull();

        font = SystemFonts.CreateFont(family.Name, 12F, FontStyle.Regular);
        await Assert.That(font).IsNotNull();
    }

    [Test]
    public async Task SystemFonts_CanCreateFont_WithCulture()
    {
        FontFamily family = SystemFonts.Families.First();
        Font font = SystemFonts.CreateFont(family.Name, family.Culture, 12F);

        await Assert.That(font).IsNotNull();

        font = SystemFonts.CreateFont(family.Name, family.Culture, 12F, FontStyle.Regular);
        await Assert.That(font).IsNotNull();
    }

    [Test]
    public async Task CanEnumerateSystemFontMetrics()
    {
        IEnumerator<FontMetrics> enumerator = ((IReadOnlyFontMetricsCollection)SysFontCollection).GetEnumerator();

        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
            await Assert.That(enumerator.Current).IsNotNull();
        }

        await Assert.That(count > 0).IsTrue();
    }

    [Test]
    public async Task CanEnumerateNonGenericSystemFontMetrics()
    {
        System.Collections.IEnumerator enumerator = ((IReadOnlyFontMetricsCollection)SysFontCollection).GetEnumerator();

        int count = 0;
        while (enumerator.MoveNext())
        {
            count++;
            await Assert.That(enumerator.Current).IsNotNull();
        }

        await Assert.That(count > 0).IsTrue();
    }

    [Test]
    public async Task CanGetAllMetricsByCulture()
    {
        var collection = (IReadOnlyFontMetricsCollection)SysFontCollection;
        FontFamily family = SysFontCollection.Families.First();
        IEnumerable<FontMetrics> metrics = collection.GetAllMetrics(family.Name, family.Culture);

        await Assert.That(metrics.Any()).IsTrue();

        foreach (FontMetrics item in metrics)
        {
            await Assert.That(family.TryGetMetrics(item.Description.Style, out FontMetrics? familyMetrics)).IsTrue();
            await Assert.That(collection.TryGetMetrics(
                family.Name,
                family.Culture,
                item.Description.Style,
                out FontMetrics? fontMetrics)).IsTrue();

            await Assert.That(fontMetrics).IsEqualTo(familyMetrics);
        }
    }

    [Test]
    public async Task CanGetAllStylesByCulture()
    {
        FontFamily family = SysFontCollection.Families.First();
        IEnumerable<FontStyle> styles = ((IReadOnlyFontMetricsCollection)SysFontCollection).GetAllStyles(family.Name, family.Culture);

        await Assert.That(styles.Any()).IsTrue();
        await Assert.That(styles).IsEquivalentTo(family.GetAvailableStyles(), CollectionOrdering.Matching);
    }

    [Test]
    [AppContextSwitch("Switch.PinataLabs.Typography.DoNotUseNativeSystemFontsEnumeration", true)]
    [NotInParallel]
    public async Task SystemFonts_FontFamilyNotFound_ThrowsWithSearchDirectories()
    {
        static void Action() => new SystemFontCollection().Get("AFontThatDoesNotExist");

        FontFamilyNotFoundException? exception = await Assert.That(Action).ThrowsExactly<FontFamilyNotFoundException>();
        await Assert.That(exception!.FontFamily).IsEqualTo("AFontThatDoesNotExist");
        await Assert.That(exception.Message).Contains("AFontThatDoesNotExist");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            await Assert.That(exception.Message).Contains(@"Windows\Fonts", StringComparison.OrdinalIgnoreCase);
            await Assert.That(exception.SearchDirectories).Contains(e => e.IndexOf(@"Windows\Fonts", StringComparison.OrdinalIgnoreCase) != -1);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            await Assert.That(exception.Message).Contains(@"/share/fonts/", StringComparison.OrdinalIgnoreCase);
            await Assert.That(exception.SearchDirectories).Contains(e => e.IndexOf("share/fonts", StringComparison.OrdinalIgnoreCase) != -1);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await Assert.That(exception.Message).Contains(@"/Library/Fonts/", StringComparison.OrdinalIgnoreCase);
            await Assert.That(exception.SearchDirectories).Contains(e => e.IndexOf("Library/Fonts", StringComparison.OrdinalIgnoreCase) != -1);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("Android")))
        {
            await Assert.That(exception.Message).Contains(@"/system/fonts/", StringComparison.OrdinalIgnoreCase);
            await Assert.That(exception.SearchDirectories).Contains(e => e.IndexOf("/system/fonts/", StringComparison.OrdinalIgnoreCase) != -1);
        }
    }

    [Test]
    public async Task SystemFonts_FontFamilyNotFound_ThrowsWithoutSearchDirectories()
    {
        static void Action() => new SystemFontCollection().Get("AFontThatDoesNotExist");

        FontFamilyNotFoundException? exception = await Assert.That(Action).ThrowsExactly<FontFamilyNotFoundException>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await Assert.That(exception!.Message).DoesNotContain("/Library/Fonts/");
            await Assert.That(exception.SearchDirectories).IsEmpty();
        }
    }

    [Test]
    [AppContextSwitch("Switch.SixLabors.Fonts.DoNotUseNativeSystemFontsEnumeration", true)]
    [NotInParallel]
    public async Task SystemFonts_LegacySwitchName_IsIgnored()
    {
        // The pre-rebrand switch name is not honoured: native enumeration stays in use on macOS.
        static void Action() => new SystemFontCollection().Get("AFontThatDoesNotExist");

        FontFamilyNotFoundException? exception = await Assert.That(Action).ThrowsExactly<FontFamilyNotFoundException>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            await Assert.That(exception!.Message).DoesNotContain("/Library/Fonts/");
            await Assert.That(exception.SearchDirectories).IsEmpty();
        }
    }
}
