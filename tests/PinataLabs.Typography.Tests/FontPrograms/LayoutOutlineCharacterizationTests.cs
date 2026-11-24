// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// Pins the outline that the layout path draws for every glyph of a font, so a change to the
/// outline code that alters any glyph fails here. The baselines are in Fonts/LayoutOutlines.
/// To write them again after an intended change, run the tests with UPDATE_LAYOUT_OUTLINES=1.
/// </summary>
public class LayoutOutlineCharacterizationTests
{
    [Test]
    [Arguments("SourceSansPro-Regular.otf")]
    [Arguments("FDArrayTest257.otf")]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("FontPrograms/cff-type2.otf")]
    [Arguments("FontPrograms/cff-seac.otf")]
    public async Task EveryGlyphMatchesTheRecordedBaseline(string fontFile)
    {
        string[] actual = LayoutOutlineRecorder.Describe(TestFonts.GetPath(fontFile));
        string baselineName = Path.GetFileNameWithoutExtension(fontFile) + ".txt";

        if (Environment.GetEnvironmentVariable("UPDATE_LAYOUT_OUTLINES") == "1")
        {
            File.WriteAllText(Path.Combine(SourceDirectory(), "..", "Fonts", "LayoutOutlines", baselineName), string.Join('\n', actual) + '\n');
        }

        string[] expected = File.ReadAllLines(TestFonts.GetPath("LayoutOutlines/" + baselineName));

        List<string> differences = [];
        for (int i = 0; i < Math.Max(expected.Length, actual.Length) && differences.Count < 10; i++)
        {
            string? expectedLine = i < expected.Length ? expected[i] : null;
            string? actualLine = i < actual.Length ? actual[i] : null;
            if (expectedLine != actualLine)
            {
                differences.Add($"expected '{expectedLine}' but was '{actualLine}'");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    /// <summary>
    /// Compares the outline that the layout path draws for every glyph of a TrueType font with the outline
    /// that <see cref="FontProgram"/> writes. The two share one decoder, so the outlines are the same.
    /// The recorder keeps a coordinate as text, so the comparison allows a thousandth of a font unit.
    /// </summary>
    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("Roboto-Regular.ttf")]
    [Arguments("DejaVuSans.ttf")]
    [Arguments("TimesNewRoman.ttf")]
    public async Task EveryTrueTypeGlyphMatchesFontProgram(string fontFile)
    {
        string path = TestFonts.GetPath(fontFile);
        FontProgram program = FontProgram.Load(File.ReadAllBytes(path));
        Font font = LayoutOutlineRecorder.LoadAtUnitScale(path);
        List<string> differences = [];

        for (int i = 0; i < program.GlyphCount && differences.Count < 10; i++)
        {
            GlyphOutline expected = new();
            if (!program.TryWriteOutline((ushort)i, ref expected))
            {
                differences.Add($"glyph {i}: no outline");
                continue;
            }

            string calls = LayoutOutlineRecorder.Render(font, (ushort)i);
            string? difference = GlyphOutline.Compare(
                expected,
                GlyphOutline.Identity,
                GlyphOutline.Parse(LayoutOutlineRecorder.ToGlyphSpaceOutline(calls)),
                GlyphOutline.Identity,
                tolerance: 1e-3);

            if (difference is not null)
            {
                differences.Add($"glyph {i}: {difference}");
            }
        }

        await Assert.That(program.GlyphCount).IsGreaterThan(0);
        await Assert.That(differences).IsEmpty();
    }

    /// <summary>
    /// Compares the outline that the layout path draws for every glyph of a synthetic font with fontTools, in ems.
    /// Layout draws in units of the 'head' table, so a CID-keyed glyph must carry the matrix of its Font DICT.
    /// </summary>
    [Test]
    [Arguments("cff-type2")]
    [Arguments("cff-seac")]
    [Arguments("cff-cid-top-identity")]
    [Arguments("cff-cid-upem2048")]
    public async Task EveryGlyphOfASyntheticFontMatchesFontTools(string fixtureName)
    {
        FontProgramFixture fixture = FontProgramFixture.Load(fixtureName);
        Font font = LayoutOutlineRecorder.LoadAtUnitScale(fixture.FontPath);
        double scale = 1.0 / font.FontMetrics.UnitsPerEm;
        List<string> differences = [];

        for (int i = 0; i < fixture.Glyphs.Length; i++)
        {
            string calls = LayoutOutlineRecorder.Render(font, (ushort)i);
            string? difference = GlyphOutline.Compare(
                GlyphOutline.Parse(fixture.Glyphs[i].Outline),
                fixture.Glyphs[i].Matrix ?? fixture.FontMatrix,
                GlyphOutline.Parse(LayoutOutlineRecorder.ToGlyphSpaceOutline(calls)),
                [scale, 0, 0, scale, 0, 0]);

            if (difference is not null)
            {
                differences.Add($"glyph {i}: {difference}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    /// <summary>
    /// A seac whose base or accent glyph is missing draws nothing in layout, as it did before seac
    /// was supported, rather than throwing for the whole text.
    /// </summary>
    [Test]
    public async Task SeacWithAMissingComponentDrawsNothing()
    {
        Font font = LayoutOutlineRecorder.LoadAtUnitScale(TestFonts.GetPath("FontPrograms/cff-seac-missing.otf"));

        await Assert.That(LayoutOutlineRecorder.Render(font, 2)).IsEqualTo(string.Empty);
        await Assert.That(LayoutOutlineRecorder.Render(font, 3)).IsEqualTo(string.Empty);
        await Assert.That(LayoutOutlineRecorder.Render(font, 1)).IsNotEqualTo(string.Empty);
    }

    private static string SourceDirectory([CallerFilePath] string path = "")
        => Path.GetDirectoryName(path)!;
}
