// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class GlyphRendererParametersTests
{
    [Test]
    public async Task FontsWithTheSameNameFromDifferentFilesAreUnequal()
    {
        string copy = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".ttf");
        File.Copy(TestFonts.MatchingSansFile("Regular"), copy);
        try
        {
            Font first = new FontCollection().Add(TestFonts.MatchingSansFile("Regular")).CreateFont(12);
            Font second = new FontCollection().Add(copy).CreateFont(12);

            GlyphRendererParameters firstKey = SingleGlyph(first);
            GlyphRendererParameters secondKey = SingleGlyph(second);

            await Assert.That(second.Name).IsEqualTo(first.Name);
            await Assert.That(secondKey.GlyphId).IsEqualTo(firstKey.GlyphId);
            await Assert.That(firstKey == secondKey).IsFalse();
            await Assert.That(firstKey != secondKey).IsTrue();
            await Assert.That(firstKey.Equals(secondKey)).IsFalse();
            await Assert.That(firstKey.Equals((object)secondKey)).IsFalse();
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Test]
    public async Task FontsWithTheSameNameFromDifferentStreamsAreUnequal()
    {
        using Stream firstStream = TestFonts.CarterOneFileData();
        using Stream secondStream = TestFonts.CarterOneFileData();
        Font first = new FontCollection().Add(firstStream).CreateFont(12);
        Font second = new FontCollection().Add(secondStream).CreateFont(12);

        await Assert.That(second.Name).IsEqualTo(first.Name);
        await Assert.That(SingleGlyph(first)).IsNotEqualTo(SingleGlyph(second));
    }

    [Test]
    public async Task OneFontGivesEqualParameters()
    {
        Font font = new FontCollection().Add(TestFonts.MatchingSansFile("Regular")).CreateFont(12);

        GlyphRendererParameters first = SingleGlyph(font);
        GlyphRendererParameters second = SingleGlyph(font);

        await Assert.That(first == second).IsTrue();
        await Assert.That(first.Equals((object)second)).IsTrue();
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
    }

    [Test]
    public async Task FacesOfOneFamilyGiveDifferentKeys()
    {
        FontFamily family = MatchingSans.Family("Regular", "Bold");

        // Two Font objects for one face draw glyphs with equal keys.
        await Assert.That(SingleGlyph(family.CreateFont(12))).IsEqualTo(SingleGlyph(family.CreateFont(12)));

        // The same glyph at the same size in two faces has different keys.
        await Assert.That(SingleGlyph(family.CreateFont(12))).IsNotEqualTo(SingleGlyph(family.CreateFont(12, FontStyle.Bold)));
    }

    [Test]
    public async Task FontMetricsOwnTheGlyph()
    {
        using Stream stream = TestFonts.CarterOneFileData();
        Font font = new FontCollection().Add(stream).CreateFont(12);

        await Assert.That(SingleGlyph(font).FontMetrics).IsSameReferenceAs(font.FontMetrics);
    }

    [Test]
    public async Task FileFontMetricsOwnTheGlyphOfAFontFromAPath()
    {
        FontFamily family = new FontCollection().Add(TestFonts.MatchingSansFile("Regular"));
        Font font = family.CreateFont(12);

        // The face that Font.FontMetrics and FontFamily.Faces give is a FileFontMetrics, and the glyph must name that face.
        await Assert.That(font.FontMetrics).IsTypeOf<FileFontMetrics>();
        await Assert.That(SingleGlyph(font).FontMetrics).IsSameReferenceAs(font.FontMetrics);
        await Assert.That(SingleGlyph(font).FontMetrics).IsSameReferenceAs(family.Faces[0]);
    }

    private static GlyphRendererParameters SingleGlyph(Font font)
    {
        GlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, "A", new TextOptions(font));
        return renderer.GlyphKeys[0];
    }
}
