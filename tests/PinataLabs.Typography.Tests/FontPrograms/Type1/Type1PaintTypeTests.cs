// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms.Type1;

public class Type1PaintTypeTests
{
    [Test]
    public async Task AStrokedFontReportsItsPaintTypeAndStrokeWidth()
    {
        FontProgram font = Load("/PaintType 2 def /StrokeWidth 40 def\n");

        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Stroked);
        await Assert.That(font.StrokeWidth).IsEqualTo(40F);
    }

    [Test]
    public async Task AStrokeWidthKeepsItsFraction()
    {
        FontProgram font = Load("/PaintType 2 def /StrokeWidth 12.5 def\n");

        await Assert.That(font.StrokeWidth).IsEqualTo(12.5F);
    }

    [Test]
    public async Task AFilledFontReportsFilledAndNoStrokeWidth()
    {
        FontProgram font = Load("/PaintType 0 def\n");

        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Filled);
        await Assert.That(font.StrokeWidth).IsEqualTo(0F);
    }

    [Test]
    public async Task AFontWithoutPaintTypeIsFilled()
    {
        FontProgram font = Load("/FontName /Other def\n");

        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Filled);
        await Assert.That(font.StrokeWidth).IsEqualTo(0F);
    }

    [Test]
    [Arguments("/PaintType 1 def")]
    [Arguments("/PaintType 3 def")]
    [Arguments("/PaintType -2 def")]
    [Arguments("/PaintType (two) def")]
    [Arguments("/PaintType 2.5 def")]
    public async Task APaintTypeOtherThanTwoIsFilled(string entry)
    {
        FontProgram font = Load(entry + " /StrokeWidth 30 def\n");

        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Filled);
        await Assert.That(font.StrokeWidth).IsEqualTo(30F);
    }

    [Test]
    [Arguments("/StrokeWidth -5 def")]
    [Arguments("/StrokeWidth 1e39 def")]
    [Arguments("/StrokeWidth (wide) def")]
    [Arguments("/StrokeWidth [3] def")]
    public async Task AnUnusableStrokeWidthIsZero(string entry)
    {
        FontProgram font = Load("/PaintType 2 def " + entry + "\n");

        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Stroked);
        await Assert.That(font.StrokeWidth).IsEqualTo(0F);
    }

    private static FontProgram Load(string entries)
    {
        Type1FontBuilder builder = new Type1FontBuilder()
            .AddGlyph(".notdef", "0 500 hsbw endchar")
            .AddGlyph("A", "10 500 hsbw 100 vlineto 100 hlineto closepath endchar");
        builder.ClearTextBody = entries + "/FontMatrix [0.001 0 0 0.001 0 0] readonly def\n";
        return FontProgram.Load(builder.BuildPfb());
    }
}
