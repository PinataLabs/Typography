// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Text;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class HintingRobustnessTests
{
    // These fonts contain hint programs that read control values and points beyond
    // the bounds of the tables they address.
    [Test]
    [Arguments("ogham", 0x1680, 0x169C)]
    [Arguments("runic", 0x16A0, 0x16EA)]
    public async Task HintingFontsWithOutOfRangeHintReferencesDoesNotThrow(string script, int first, int last)
    {
        string path = script == "ogham" ? TestFonts.NotoSansOghamFile : TestFonts.NotoSansRunicFile;

        StringBuilder sb = new();
        for (int cp = first; cp <= last; cp++)
        {
            sb.Append(char.ConvertFromUtf32(cp));
        }

        string text = sb.ToString();
        FontFamily family = new FontCollection().Add(path);

        foreach (float size in new[] { 8F, 12F, 16F, 24F, 40F })
        {
            Font font = family.CreateFont(size);
            GlyphRenderer renderer = new();
            TextOptions options = new(font) { HintingMode = HintingMode.Standard };
            TextRenderer.RenderTextTo(renderer, text, options);
            await Assert.That(renderer.ControlPoints.Count).IsGreaterThan(0);
        }
    }
}
