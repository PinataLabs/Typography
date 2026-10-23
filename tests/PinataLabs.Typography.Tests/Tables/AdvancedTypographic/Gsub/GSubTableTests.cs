// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GSub
{
    public partial class GSubTableTests
    {
        [Test]
        [Arguments("ا", 139)]
        [Arguments("ب", 157)]
        [Arguments("پ", 162)]
        [Arguments("ت", 167)]
        [Arguments("ث", 172)]
        [Arguments("ج", 181)]
        [Arguments("چ", 185)]
        [Arguments("ح", 189)]
        [Arguments("خ", 193)]
        [Arguments("د", 197)]
        [Arguments("ز", 207)]
        [Arguments("ر", 203)]
        [Arguments("س", 215)]
        [Arguments("ش", 219)]
        [Arguments("ص", 223)]
        [Arguments("ض", 227)]
        [Arguments("ط", 231)]
        [Arguments("ع", 239)]
        [Arguments("غ", 243)]
        [Arguments("ف", 247)]
        [Arguments("ق", 264)]
        [Arguments("ک", 273)]
        [Arguments("گ", 277)]
        [Arguments("ل", 281)]
        [Arguments("م", 287)]
        [Arguments("ن", 291)]
        [Arguments("و", 316)]
        [Arguments("ه", 298)]
        [Arguments("ی", 335)]
        public async Task RenderArabicCharacters_WithIsolatedForm_Works(string testStr, int expectedGlyphIndex)
        {
            // arrange
            Font arabicFont = new FontCollection().Add(TestFonts.ArabicFontFile).CreateFont(8);
            var renderer = new ColorGlyphRenderer();

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(arabicFont));

            // assert
            GlyphRendererParameters glyphKey = await Assert.That(renderer.GlyphKeys).HasSingleItem();
            await Assert.That(glyphKey.GlyphId).IsEqualTo(expectedGlyphIndex);
        }

        // LookupType1SubTable
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#11-single-substitution-format-1
        [Test]
        public async Task SingleSubstitution_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubTestFontFile1).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "A";
            int expectedGlyphIndex = 38; // we expect A to be mapped to B.

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            GlyphRendererParameters glyphKey = await Assert.That(renderer.GlyphKeys).HasSingleItem();
            await Assert.That(glyphKey.GlyphId).IsEqualTo(expectedGlyphIndex);
        }

        [Test]
        public async Task ContextualFractions_WithFractionSlash_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.RobotoRegular).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "9⁄2";
            int[] expectedGlyphIndices = { 580, 404, 453 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font) { FeatureTags = new Tag[] { FeatureTags.Numerators, FeatureTags.Denominators } });

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        [Test]
        public async Task ContextualFractions_WithSlash_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.RobotoRegular).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "9/2";
            int[] expectedGlyphIndices = { 580, 404, 453 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font) { FeatureTags = new Tag[] { FeatureTags.Fractions } });

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType2SubTable
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#21-multiple-substitution-format-1
        [Test]
        public async Task MultipleSubstitution_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubTestFontFile1).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "C";
            int expectedGlyphIndex = 40; // we expect C to be mapped to D.

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            GlyphRendererParameters glyphKey = await Assert.That(renderer.GlyphKeys).HasSingleItem();
            await Assert.That(glyphKey.GlyphId).IsEqualTo(expectedGlyphIndex);
        }

        // LookupType3SubTable
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#lookuptype-3-alternate-substitution-subtable
        [Test]
        public async Task AlternateSubstitution_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubTestFontFile1).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "E";
            int expectedGlyphIndex = 42; // we expect E to be mapped to F.

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            GlyphRendererParameters glyphKey = await Assert.That(renderer.GlyphKeys).HasSingleItem();
            await Assert.That(glyphKey.GlyphId).IsEqualTo(expectedGlyphIndex);
        }

        // LookupType4SubTable
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#lookuptype-3-alternate-substitution-subtable
        [Test]
        public async Task LigatureSubstitution_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubTestFontFile1).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "ffi";
            int expectedGlyphIndex = 229;

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            GlyphRendererParameters glyphKey = await Assert.That(renderer.GlyphKeys).HasSingleItem();
            await Assert.That(glyphKey.GlyphId).IsEqualTo(expectedGlyphIndex);
        }

        // LookupType5SubTable, Format 1.
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#lookuptype-5-contextual-substitution-subtable
        [Test]
        public async Task ContextualSubstitution_Format1_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubLookupType5Format1).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "\u0041\u0042"; // "6566" (\u0041\u0042) -> "6576"
            int[] expectedGlyphIndices = { 3, 7 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType5SubTable, Format 2.
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#lookuptype-5-contextual-substitution-subtable
        [Test]
        public async Task ContextualSubstitution_Format2_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubLookupType5Format2).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "\u0041\u0042"; // "6566" (\u0041\u0042) -> "6576"
            int[] expectedGlyphIndices = { 3, 7 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType5SubTable, Format 3
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#lookuptype-5-contextual-substitution-subtable
        [Test]
        public async Task ContextualSubstitution_Format3_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubLookupType5Format3).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "\u0041\u0042\u0043\u0044"; // "65666768" -> "657678"
            int[] expectedGlyphIndices = { 67, 78, 80 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType6SubTable, Format 1
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#62-chained-contexts-substitution-format-1-class-based-glyph-contexts
        [Test]
        public async Task ChainedContextsSubstitution_Format1_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubLookupType6Format1).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "\u0014\u0015\u0016\u0017"; // "20212223" -> "20636423"
            int[] expectedGlyphIndices = { 22, 63, 64, 25 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType6SubTable, Format 2
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#62-chained-contexts-substitution-format-2-class-based-glyph-contexts
        [Test]
        public async Task ChainedContextsSubstitution_Format2_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubLookupType6Format2).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "\u0014\u0015\u0016\u0017"; // "20212223" -> "20216423"
            int[] expectedGlyphIndices = { 22, 23, 64, 25 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType6SubTable, Format 3
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#63-chained-contexts-substitution-format-3-coverage-based-glyph-contexts
        [Test]
        public async Task ChainedContextsSubstitution_Format3_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubTestFontFile2).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "x=y"; // This should be replaced with "x>y".
            int[] expectedGlyphIndices = { 89, 31, 90 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType6SubTable, Format 3
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#63-chained-contexts-substitution-format-3-coverage-based-glyph-contexts
        [Test]
        public async Task ChainedContextsSubstitution_Format3_WithCursiveScript_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.FormalScript).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "ba"; // Characters following b should have a special form and should be replaced.
            int[] expectedGlyphIndices = { 69, 102 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // LookupType8SubTable
        // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#lookuptype-8-reverse-chaining-contextual-single-substitution-subtable
        [Test]
        public async Task ReverseChainingContextualSingleSubstitution_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.GSubTestFontFile2).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "X89"; // X89 -> XYZ
            int[] expectedGlyphIndices = { 57, 58, 59 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font));

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        // See upstream PR #248 (pre-fork).
        [Test]
        public async Task OldStyleFiguresFeature_Works()
        {
            // arrange
            Font font = new FontCollection().Add(TestFonts.EbGaramond).CreateFont(12);
            var renderer = new ColorGlyphRenderer();
            string testStr = "123456";
            int[] expectedGlyphIndices = { 2242, 2243, 2244, 2245, 2246, 2247 };

            // act
            TextRenderer.RenderTextTo(renderer, testStr, new TextOptions(font)
            {
                FeatureTags = new List<Tag> { FeatureTags.OldstyleFigures }
            });

            // assert
            await Assert.That(renderer.GlyphKeys.Count).IsEqualTo(expectedGlyphIndices.Length);
            for (int i = 0; i < expectedGlyphIndices.Length; i++)
            {
                await Assert.That(renderer.GlyphKeys[i].GlyphId).IsEqualTo(expectedGlyphIndices[i]);
            }
        }

        [Test]
        public async Task BillionLaughsAttackDoesNotThrowException()
        {
            // Arrange
            Font font = new FontCollection().Add(TestFonts.GSubLookupType2BillionLaughs).CreateFont(12);

            // Act
            TextMeasurer.MeasureSize("lol", new TextOptions(font));
        }
    }
}
