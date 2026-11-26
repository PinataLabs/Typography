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
    public async Task FontClassWithPath_SetProperties()
    {
        var collection = new FontCollection();
        FontFamily family = collection.Add(TestFonts.CarterOneFile);
        var font = new Font(family, 12);

        await Assert.That(font.Name).IsEqualTo("Carter One");
        await Assert.That(font.Size).IsEqualTo(12);
        await Assert.That(font.Style).IsEqualTo(FontStyle.Regular);
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
        await Assert.That(font.Style).IsEqualTo(FontStyle.Regular);
        await Assert.That(font.TryGetPath(out string? path)).IsFalse();
        await Assert.That(path).IsNull();
    }

    [Test]
    public async Task StyleIsTheRequestedStyle()
    {
        FontFamily family = new FontCollection().Add(TestFonts.CarterOneFile);

        // The family has only a regular face, which draws every request.
        Font font = family.CreateFont(12, FontStyle.BoldItalic);

        await Assert.That(font.Style).IsEqualTo(FontStyle.BoldItalic);
        await Assert.That(new Font(family, 12, FontStyle.Bold).Style).IsEqualTo(FontStyle.Bold);
    }

    [Test]
    public async Task WithSizeKeepsTheFamilyAndStyle()
    {
        FontFamily family = new FontCollection().Add(TestFonts.CarterOneFile);
        Font font = family.CreateFont(12, FontStyle.Italic);

        Font resized = font.WithSize(30);

        await Assert.That(resized).IsNotSameReferenceAs(font);
        await Assert.That(resized.Size).IsEqualTo(30);
        await Assert.That(resized.Family).IsEqualTo(font.Family);
        await Assert.That(resized.Style).IsEqualTo(FontStyle.Italic);
        await Assert.That(font.Size).IsEqualTo(12);
    }

    [Test]
    public async Task WithStyleKeepsTheFamilyAndSize()
    {
        FontFamily family = new FontCollection().Add(TestFonts.CarterOneFile);
        Font font = family.CreateFont(12);

        Font bold = font.WithStyle(FontStyle.Bold);

        await Assert.That(bold).IsNotSameReferenceAs(font);
        await Assert.That(bold.Style).IsEqualTo(FontStyle.Bold);
        await Assert.That(bold.Family).IsEqualTo(font.Family);
        await Assert.That(bold.Size).IsEqualTo(12);
        await Assert.That(font.Style).IsEqualTo(FontStyle.Regular);
    }

    [Test]
    public async Task CreateFontDefaultStyleIsRegular()
    {
        FontFamily family = new FontCollection().Add(TestFonts.CarterOneFile);

        await Assert.That(family.CreateFont(12).Style).IsEqualTo(FontStyle.Regular);
        await Assert.That(family.CreateFont(12, default).Style).IsEqualTo(FontStyle.Regular);
    }
}
