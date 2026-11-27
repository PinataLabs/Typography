// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
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

    // The font of the text decides which glyphs the text has and in what order. A fallback font draws each glyph that the
    // font lacks with its own glyphs for the same character, at the place where the shaper of the font put that glyph.
    // In each row the two fonts shape the text differently. The font alone gives .notdef (0) for each character that it
    // lacks, and the fallback font alone gives its own glyphs. Both are the glyph IDs that HarfBuzz 15.0.0 gives. With
    // the fallback font, P is a glyph of the font and F a glyph of the fallback font.
    [Test]

    // Hangul: the font puts a dotted circle after the tone mark that it lacks, and the fallback font puts another one.
    [Arguments("TextShaperHangulNotdef.ttf", "TextShaperHangul.ttf", "1100 302E", "2 0 8", "2 10 8", "2P 10F 8P")]

    // The font takes the syllable apart and moves the tone mark before it. The fallback font keeps the syllable, so the
    // tone mark that follows its trailing consonant gets a dotted circle.
    [Arguments("TextShaperHangulNotdef.ttf", "TextShaperHangul.ttf", "AC00 11C3 302E", "0 2 3 5", "10 2 3 5", "10F 2P 3P 5P")]

    // Devanagari ka, virama, ra, i: the font has no pref, so ra is the base and the pre-base matra i goes before it. The
    // fallback font makes a pre-base form of virama and ra, so ka is the base and i goes first.
    [Arguments("SyllablesInvalid.ttf", "IndicReordering.ttf", "0915 094D 0930 093F", "0 19 0 6", "6 13 1", "1F 19P 6F 6P")]

    // Devanagari ra, virama, ka, i: the fallback font makes a reph of ra and virama and moves it after the base.
    [Arguments("SyllablesInvalid.ttf", "NotoSansDevanagari-Regular.ttf", "0930 094D 0915 093F", "6 19 0 0", "585 25 606", "6P 19P 585F 25F")]
    [Arguments("SyllablesInvalid.ttf", "NotoSansDevanagari-Regular.ttf", "0930 094D 0915", "6 19 0", "25 181", "6P 19P 25F")]

    // A broken cluster: each font gives the sign its own dotted circle as a base, before or after the pre-base matra.
    [Arguments("IndicReordering.ttf", "SyllablesInvalid.ttf", "0946", "12 0", "91 12", "12P 12F")]
    [Arguments("SyllablesInvalid.ttf", "IndicReordering.ttf", "093F", "0 91", "6 12", "6F 91P")]

    // The dotted circle that breaks the vowel sequence a, short e is .notdef in a font without U+25CC.
    [Arguments("SyllablesNoCircle.ttf", "SyllablesInvalid.ttf", "0905 0946", "1 0 2", "1 91 12", "1P 91F 2P")]
    public async Task FallbackFontDrawsEachGlyphThatTheFontLacksOnceWhereTheFontPutsIt(
        string font,
        string fallback,
        string codePoints,
        string fontAlone,
        string fallbackAlone,
        string expected)
    {
        string text = string.Concat(codePoints.Split(' ').Select(c => char.ConvertFromUtf32(Convert.ToInt32(c, 16))));

        await Assert.That(Glyphs(text, font)).IsEqualTo(fontAlone);
        await Assert.That(Glyphs(text, fallback)).IsEqualTo(fallbackAlone);
        await Assert.That(Glyphs(text, font, fallback)).IsEqualTo(expected);
    }

    // Gives the glyph ID of each glyph of the text, and, with a fallback font, the font that draws it.
    private static string Glyphs(string text, string font, string? fallback = null)
    {
        FontCollection collection = new();
        FontFamily family = collection.Add(TestFonts.GetPath(font));
        TextOptions options = new(family.CreateFont(12));
        if (fallback is not null)
        {
            options.FallbackFontFamilies = [collection.Add(TestFonts.GetPath(fallback))];
        }

        return string.Join(' ', TextLayout.GenerateLayout(text.AsSpan(), options).Select(g =>
        {
            GlyphMetrics metrics = g.Glyph.GlyphMetrics;
            string drawnBy = fallback is null ? string.Empty
                : metrics.FontMetrics.Description.FontFamilyInvariantCulture == family.Name ? "P" : "F";
            return metrics.GlyphId + drawnBy;
        }));
    }
}
