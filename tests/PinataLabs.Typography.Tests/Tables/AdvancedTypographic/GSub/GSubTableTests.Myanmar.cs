// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GSub;

/// <content>
/// Tests for the Myanmar shaper, which follows the Microsoft OpenType specification
/// "Developing OpenType Fonts for Myanmar Script" (script tag mym2).
/// </content>
public partial class GSubTableTests
{
    private static readonly Font MyanmarNotoSansTTF = CreateFont(TestFonts.NotoSansMyanmarRegular);

    // The expected glyph ids were produced by running HarfBuzz 'hb-shape' equivalent
    // (the uharfbuzz Python binding, script Mymr, no extra features) over
    // Fonts/NotoSansMyanmar/NotoSansMyanmar-Regular.ttf. They are reference values for the
    // order the OpenType Myanmar specification requires, not values computed by this library.
    // Glyph names (from the font's post table) are given beside each case.
    [Test]

    // U+1000 U+103C: medial RA moves before the base: medial_ra.w2, ka.
    [Arguments("ကြ", new int[] { 198, 4 })]

    // U+1000 U+1031: pre-base vowel E moves before the base: _e, ka.
    [Arguments("ကေ", new int[] { 372, 4 })]

    // U+1000 U+103B U+103C U+102D: medial_ra_bt.w2, ka, medial_ya, _i.
    [Arguments("ကျြိ", new int[] { 202, 4, 382, 369 })]

    // Kinzi U+1004 U+103A U+1039 U+1002: ga, then the kinzi form after the base.
    [Arguments("င်္ဂ", new int[] { 6, 189 })]

    // Kinzi with vowel E: _e, ka, kinzi.
    [Arguments("င်္ကေ", new int[] { 372, 4, 189 })]

    // Kinzi followed by vowel I and anusvara: ka, kinzi_i_anusvara (the font ligates after reordering).
    [Arguments("င်္ကိံ", new int[] { 4, 185 })]

    // Medial RA and vowel E: E is outermost: _e, medial_ra.w2, ka.
    [Arguments("ကြေ", new int[] { 372, 198, 4 })]

    // Full cluster: _e, medial_ra.w2, ka, _aa, asat.
    [Arguments("ကြော်", new int[] { 372, 198, 4, 368, 381 })]

    // Visarga follows the vowel: ka, _aa, visarga.
    [Arguments("ကား", new int[] { 4, 368, 379 })]

    // Anusvara stays before visarga: ka, anusvara, visarga.
    [Arguments("ကံး", new int[] { 4, 377, 379 })]

    // Asat stays before visarga: ka, asat, visarga.
    [Arguments("က်း", new int[] { 4, 381, 379 })]

    // Vowel U below, then visarga: ka, _u, visarga.
    [Arguments("ကုး", new int[] { 4, 209, 379 })]

    // A real word, "Myanmar": medial_ra, ma, na, asat, ma, _aa.
    [Arguments("မြန်မာ", new int[] { 47, 29, 24, 381, 29, 368 })]
    public async Task CanShapeMyanmarText(string input, int[] expectedGlyphIndices)
    {
        ColorGlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, input, new TextOptions(MyanmarNotoSansTTF));

        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }

    [Test]
    [Arguments("င်္")]
    [Arguments("a ြ b")]
    [Arguments("္်း‍")]
    [Arguments("ကေ င်္")]
    public async Task ShapingMalformedMyanmarTextDoesNotThrow(string input)
    {
        ColorGlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, input, new TextOptions(MyanmarNotoSansTTF));

        await Assert.That(renderer.GlyphKeys.Count).IsGreaterThan(0);
    }

    // Broken clusters get a dotted circle (U+25CC, glyph 388) as the base. Same HarfBuzz reference as above.
    [Test]

    // A lone medial RA: medial_ra, then the dotted circle base.
    [Arguments("ြ", new int[] { 47, 388 })]

    // A lone vowel E: _e, dotted circle.
    [Arguments("ေ", new int[] { 372, 388 })]

    // Asat after a pre-base vowel is not valid in a cluster: _e, ka, then a dotted circle for the stray asat.
    [Arguments("ကေ်", new int[] { 372, 4, 388, 381 })]

    // Asat after vowel I starts a broken cluster, its visarga stays with it.
    [Arguments("ကိ်း", new int[] { 4, 369, 388, 381, 379 })]
    public async Task CanShapeBrokenMyanmarClustersWithDottedCircle(string input, int[] expectedGlyphIndices)
    {
        ColorGlyphRenderer renderer = new();
        TextRenderer.RenderTextTo(renderer, input, new TextOptions(MyanmarNotoSansTTF));

        await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
        for (int i = 0; i < expectedGlyphIndices.Length; i++)
        {
            await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
        }
    }
}
