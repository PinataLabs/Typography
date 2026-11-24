// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// The PaintType and StrokeWidth of the Top DICT. The fixtures come from tests/tools/generate_font_program_fixtures.py,
/// and <see cref="FontProgramParityTests"/> compares every fixture with the value that fontTools reads.
/// </summary>
public class CffPaintTypeTests
{
    [Test]
    [Arguments("cff-paint-stroked.otf", 40F)]
    [Arguments("cff-paint-stroked-real.otf", 12.5F)]
    [Arguments("bare-cff-paint-stroked.cff", 40F)]
    public async Task AStrokedFontReportsItsPaintTypeAndStrokeWidth(string file, float strokeWidth)
    {
        FontProgram font = Load(file);

        await Assert.That(font.Format).IsEqualTo(FontProgramFormat.Cff);
        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Stroked);
        await Assert.That(font.StrokeWidth).IsEqualTo(strokeWidth);
    }

    [Test]
    public async Task PaintTypeOneIsFilledAndKeepsItsStrokeWidth()
    {
        FontProgram font = Load("cff-paint-outline.otf");

        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Filled);
        await Assert.That(font.StrokeWidth).IsEqualTo(30F);
    }

    [Test]
    [Arguments("FontPrograms/bare-SourceSansPro-Regular.cff")]
    [Arguments("FontPrograms/cff-cid-fdselect0.otf")]
    [Arguments("OpenSans-Regular.ttf")]
    public async Task AFilledFontReportsFilledAndNoStrokeWidth(string file)
    {
        FontProgram font = FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath(file)));

        await Assert.That(font.PaintType).IsEqualTo(GlyphPaintType.Filled);
        await Assert.That(font.StrokeWidth).IsEqualTo(0F);
    }

    private static FontProgram Load(string file)
        => FontProgram.Load(File.ReadAllBytes(TestFonts.GetPath("FontPrograms/" + file)));
}
