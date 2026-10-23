// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

namespace PinataLabs.Typography.Tests;

public class Accents
{
    [Test]
    [Arguments('á')]
    [Arguments('é')]
    [Arguments('í')]
    [Arguments('ó')]
    [Arguments('ú')]
    [Arguments('ç')]
    [Arguments('ã')]
    [Arguments('õ')]
    public void MeasuringAccentedCharacterDoesNotThrow(char c)
    {
        FontFamily sans = new FontCollection().Add(TestFonts.OpenSansFile);
        var font = new Font(sans, 1f, FontStyle.Regular);

        FontRectangle size = TextMeasurer.MeasureSize(c.ToString(), new TextOptions(font));
    }

    [Test]
    [Arguments('á')]
    [Arguments('é')]
    [Arguments('í')]
    [Arguments('ó')]
    [Arguments('ú')]
    [Arguments('ç')]
    [Arguments('ã')]
    [Arguments('õ')]
    public void MeasuringWordWithAccentedCharacterDoesNotThrow(char c)
    {
        FontFamily sans = new FontCollection().Add(TestFonts.OpenSansFile);
        var font = new Font(sans, 1f, FontStyle.Regular);

        FontRectangle size = TextMeasurer.MeasureSize($"abc{c}def", new TextOptions(font));
    }
}
