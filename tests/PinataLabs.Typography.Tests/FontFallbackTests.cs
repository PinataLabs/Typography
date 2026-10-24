// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class FontFallbackTests
{
    private const string Sinhala = "වේගවත් දුඹුරු හිවලා කම්මැලි බල්ලා උඩින් පනී";
    private const string Kannada = "ಕನ್ನಡಭಾಷೆಯಲ್ಲಿಕ್ಷಿಪ್ರಕಂದುನರಿ";
    private const string Khmer = "ភាសាខ្មែរស្រ្តីក្នុង";

    private static async Task<T> RunWithTimeout<T>(Func<T> action)
    {
        Task<T> task = Task.Run(action);
        Task completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (completed != task)
        {
            throw new TimeoutException("Text layout did not complete.");
        }

        return await task;
    }

    [Test]
    public async Task MeasureAdvanceWithGlyphMissingFromEveryFontDoesNotThrow()
    {
        var collection = new FontCollection();
        Font font = collection.Add(TestFonts.OpenSansFile).CreateFont(8.25F);

        FontRectangle plain = await RunWithTimeout(() => TextMeasurer.MeasureAdvance("xꪗ", new TextOptions(font)));
        await Assert.That(plain.Width).IsGreaterThan(0);

        FontFamily fallback = collection.Add(TestFonts.NotoSansSCThinFile);
        FontRectangle withFallback = await RunWithTimeout(() => TextMeasurer.MeasureAdvance(
            "xꪗ",
            new TextOptions(font) { FallbackFontFamilies = new[] { fallback } }));
        await Assert.That(withFallback.Width).IsGreaterThan(0);
    }

    [Test]
    public async Task UnsupportedComplexScriptWithFallbackCompletesAndRenders()
    {
        var collection = new FontCollection();
        Font font = collection.Add(TestFonts.OpenSansFile).CreateFont(30);
        FontFamily fallback = collection.Add(TestFonts.NotoSansSCThinFile);
        var options = new TextOptions(font) { FallbackFontFamilies = new[] { fallback } };

        FontRectangle advance = await RunWithTimeout(() => TextMeasurer.MeasureAdvance(Sinhala, options));
        await Assert.That(advance.Width).IsGreaterThan(0);

        int glyphs = await RunWithTimeout(() =>
        {
            var renderer = new GlyphRenderer();
            TextRenderer.RenderTextTo(renderer, "Quick " + Sinhala + " brown", options);
            return renderer.GlyphKeys.Count;
        });
        await Assert.That(glyphs).IsGreaterThan(0);
    }

    [Test]
    [Arguments(nameof(Kannada))]
    [Arguments(nameof(Khmer))]
    public async Task FallbackFontShapesLikeMainFont(string which)
    {
        string text = which == nameof(Kannada) ? Kannada : Khmer;
        string path = which == nameof(Kannada) ? TestFonts.NotoSansKannadaRegular : TestFonts.NotoSansKhmerRegular;

        var collection = new FontCollection();
        FontFamily script = collection.Add(path);
        FontFamily latin = collection.Add(TestFonts.OpenSansFile);

        var main = new GlyphRenderer();
        TextRenderer.RenderTextTo(main, text, new TextOptions(script.CreateFont(30)));

        var fallback = new GlyphRenderer();
        TextRenderer.RenderTextTo(fallback, text, new TextOptions(latin.CreateFont(30)) { FallbackFontFamilies = new[] { script } });

        await Assert.That(fallback.GlyphKeys.Count).IsEqualTo(main.GlyphKeys.Count);
        for (int i = 0; i < main.GlyphKeys.Count; i++)
        {
            await Assert.That(fallback.GlyphKeys[i].GlyphId).IsEqualTo(main.GlyphKeys[i].GlyphId);
        }
    }
}
