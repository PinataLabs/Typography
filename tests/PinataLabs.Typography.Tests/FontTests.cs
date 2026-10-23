// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class FontTests
{
    [Test]
    public async Task FontClass_DefaultFontFamilyThrowsException()
    {
        ArgumentException? ex = await Assert.That(() => new Font(default(FontFamily), 0F)).ThrowsExactly<ArgumentException>();

        await Assert.That(ex!.ParamName).IsEqualTo("family");
    }

    [Test]
    public async Task FontClass_DefaultFontFamilyWithSizeThrowsException()
    {
        ArgumentException? ex = await Assert.That(() => new Font(default(FontFamily), 0F, FontStyle.Regular)).ThrowsExactly<ArgumentException>();

        await Assert.That(ex!.ParamName).IsEqualTo("family");
    }

    [Test]
    public async Task FontClass_NullFontThrowsException()
    {
        ArgumentNullException? ex = await Assert.That(() => new Font(null!, FontStyle.Regular)).ThrowsExactly<ArgumentNullException>();

        await Assert.That(ex!.ParamName).IsEqualTo("prototype");
    }

    [Test]
    public async Task FontClass_NullWithSizeFontThrowsException()
    {
        ArgumentNullException? ex = await Assert.That(() => new Font(null!, 0f, FontStyle.Regular)).ThrowsExactly<ArgumentNullException>();

        await Assert.That(ex!.ParamName).IsEqualTo("prototype");
    }

    [Test]
    public async Task FontClassWithPath_SetProperties()
    {
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.CarterOneFile);
        var font = new Font(family, 12);

        await Assert.That(font.Name).IsEqualTo("Carter One");
        await Assert.That(font.Size).IsEqualTo(12);
        await Assert.That(font.RequestedStyle).IsEqualTo(FontStyle.Regular);
        await Assert.That(font.IsBold).IsFalse();
        await Assert.That(font.IsItalic).IsFalse();
        await Assert.That(font.TryGetPath(out string? path)).IsTrue();
        await Assert.That(path).IsNotNull();
    }

    [Test]
    public async Task FontClassNoPath_SetProperties()
    {
        var collection = new FontCollection();
        using Stream stream = TestFonts.CarterOneFileData();
        FontFamily family = collection.Add(stream);
        var font = new Font(family, 12);

        await Assert.That(font.Name).IsEqualTo("Carter One");
        await Assert.That(font.Size).IsEqualTo(12);
        await Assert.That(font.RequestedStyle).IsEqualTo(FontStyle.Regular);
        await Assert.That(font.IsBold).IsFalse();
        await Assert.That(font.IsItalic).IsFalse();
        await Assert.That(font.TryGetPath(out string? path)).IsFalse();
        await Assert.That(path).IsNull();
    }
}
