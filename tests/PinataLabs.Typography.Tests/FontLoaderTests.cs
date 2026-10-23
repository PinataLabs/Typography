// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

public class FontLoaderTests
{
    [Test]
    public async Task Issue21_LoopDetectedLoadingGlyphs()
    {
        Font font = new FontCollection().Add(TestFonts.CarterOneFileData()).CreateFont(12);

        await Assert.That(font.FontMetrics.TryGetGlyphMetrics(
            new CodePoint('\0'),
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None,
            out IReadOnlyList<GlyphMetrics> _)).IsTrue();
    }

    [Test]
    public async Task LoadFontMetadata()
    {
        var description = FontDescription.LoadDescription(TestFonts.SimpleFontFileData());

        await Assert.That(description.FontNameInvariantCulture).IsEqualTo("SixLaborsSampleAB regular");
        await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
    }

    [Test]
    public async Task LoadFontMetadataWoff()
    {
        var description = FontDescription.LoadDescription(TestFonts.SimpleFontFileWoffData());

        await Assert.That(description.FontNameInvariantCulture).IsEqualTo("SixLaborsSampleAB regular");
        await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
    }

    [Test]
    public async Task LoadFont_WithTtfFormat()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFile).CreateFont(12);

        await Assert.That(font.TryGetGlyphs(new CodePoint('A'), ColorFontSupport.None, out IReadOnlyList<Glyph> glyphs)).IsTrue();

        Glyph glyph = glyphs[0];
        GlyphRenderer r = new();
        glyph.RenderTo(r, Vector2.Zero, Vector2.Zero, GlyphLayoutMode.Horizontal, new TextOptions(font));

        await Assert.That(r.ControlPoints.Count).IsEqualTo(37);
        await Assert.That(r.GlyphKeys).HasSingleItem();
        await Assert.That(r.GlyphRects).HasSingleItem();
    }

    [Test]
    public async Task LoadFont_WithWoff1Format()
    {
        Font font = new FontCollection().Add(TestFonts.OpenSansFileWoff1).CreateFont(12);

        await Assert.That(font.TryGetGlyphs(new CodePoint('A'), ColorFontSupport.None, out IReadOnlyList<Glyph> glyphs)).IsTrue();
        Glyph glyph = glyphs[0];
        GlyphRenderer r = new();
        glyph.RenderTo(r, Vector2.Zero, Vector2.Zero, GlyphLayoutMode.Horizontal, new TextOptions(font));

        await Assert.That(r.ControlPoints.Count).IsEqualTo(37);
        await Assert.That(r.GlyphKeys).HasSingleItem();
        await Assert.That(r.GlyphRects).HasSingleItem();
    }

    [Test]
    public async Task LoadFontMetadata_WithWoff1Format()
    {
        var description = FontDescription.LoadDescription(TestFonts.OpensSansWoff1Data());

        await Assert.That(description.FontNameInvariantCulture).IsEqualTo("Open Sans Regular");
        await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
    }

    [Test]
    public async Task LoadFontMetadata_WithWoff2Format()
    {
        var description = FontDescription.LoadDescription(TestFonts.OpensSansWoff2Data());

        await Assert.That(description.FontNameInvariantCulture).IsEqualTo("Open Sans Regular");
        await Assert.That(description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");
    }

    [Test]
    public async Task LoadFont_WithWoff2Format()
    {
        Font font = new FontCollection().Add(TestFonts.OpensSansWoff2Data()).CreateFont(12);

        await Assert.That(font.TryGetGlyphs(new CodePoint('A'), ColorFontSupport.None, out IReadOnlyList<Glyph> glyphs)).IsTrue();
        Glyph glyph = glyphs[0];
        GlyphRenderer r = new();
        glyph.RenderTo(r, Vector2.Zero, Vector2.Zero, GlyphLayoutMode.Horizontal, new TextOptions(font));

        await Assert.That(r.ControlPoints.Count).IsEqualTo(37);
        await Assert.That(r.GlyphKeys).HasSingleItem();
        await Assert.That(r.GlyphRects).HasSingleItem();
    }

    [Test]
    public async Task LoadFont()
    {
        Font font = new FontCollection().Add(TestFonts.SimpleFontFileData()).CreateFont(12);

        await Assert.That(font.FontMetrics.Description.FontNameInvariantCulture).IsEqualTo("SixLaborsSampleAB regular");
        await Assert.That(font.FontMetrics.Description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");

        await Assert.That(font.TryGetGlyphs(new CodePoint('a'), ColorFontSupport.None, out IReadOnlyList<Glyph> glyphs)).IsTrue();
        Glyph glyph = glyphs[0];
        GlyphRenderer r = new();
        glyph.RenderTo(r, Vector2.Zero, Vector2.Zero, GlyphLayoutMode.Horizontal, new TextOptions(font));

        // the test font only has characters .notdef, 'a' & 'b' defined
        await Assert.That(r.ControlPoints.Distinct().Count()).IsEqualTo(6);
    }

    [Test]
    public async Task LoadFontWoff()
    {
        Font font = new FontCollection().Add(TestFonts.SimpleFontFileWoffData()).CreateFont(12);

        await Assert.That(font.FontMetrics.Description.FontNameInvariantCulture).IsEqualTo("SixLaborsSampleAB regular");
        await Assert.That(font.FontMetrics.Description.FontSubFamilyNameInvariantCulture).IsEqualTo("Regular");

        await Assert.That(font.TryGetGlyphs(new CodePoint('a'), ColorFontSupport.None, out IReadOnlyList<Glyph> glyphs)).IsTrue();
        Glyph glyph = glyphs[0];
        GlyphRenderer r = new();
        glyph.RenderTo(r, Vector2.Zero, Vector2.Zero, GlyphLayoutMode.Horizontal, new TextOptions(font));

        // the test font only has characters .notdef, 'a' & 'b' defined
        await Assert.That(r.ControlPoints.Distinct().Count()).IsEqualTo(6);
    }
}
