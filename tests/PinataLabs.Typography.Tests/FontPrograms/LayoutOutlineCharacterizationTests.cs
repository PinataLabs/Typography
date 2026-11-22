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
    public async Task EveryGlyphMatchesTheRecordedBaseline(string fontFile)
    {
        string[] actual = LayoutOutlineRecorder.Describe(TestFonts.GetPath(fontFile));
        string baselineName = Path.ChangeExtension(fontFile, ".txt");

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

    private static string SourceDirectory([CallerFilePath] string path = "")
        => Path.GetDirectoryName(path)!;
}
