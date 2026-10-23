// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests
{
    public class TextOptionsTests
    {
        private readonly Font fakeFont;
        private readonly TextOptions newTextOptions;
        private readonly TextOptions clonedTextOptions;

        public TextOptionsTests()
        {
            this.fakeFont = FakeFont.CreateFont("ABC");
            this.newTextOptions = new TextOptions(this.fakeFont);
            this.clonedTextOptions = new TextOptions(this.newTextOptions);
        }

        [Test]
        public async Task ConstructorTest_FontOnly()
        {
            Font font = FakeFont.CreateFont("ABC");
            var options = new TextOptions(font);

            await Assert.That(options.Dpi).IsEqualTo(72);
            await Assert.That(options.FallbackFontFamilies).IsEmpty();
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(Vector2.Zero);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task ConstructorTest_FontWithSingleDpi()
        {
            Font font = FakeFont.CreateFont("ABC");
            const float dpi = 123;
            var options = new TextOptions(font) { Dpi = dpi };

            await Assert.That(options.Dpi).IsEqualTo(dpi);
            await Assert.That(options.FallbackFontFamilies).IsEmpty();
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(Vector2.Zero);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task ConstructorTest_FontWithOrigin()
        {
            Font font = FakeFont.CreateFont("ABC");
            var origin = new Vector2(123, 345);
            TextOptions options = new(font) { Origin = origin };

            await Assert.That(options.Dpi).IsEqualTo(72);
            await Assert.That(options.FallbackFontFamilies).IsEmpty();
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(origin);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task ConstructorTest_FontWithSingleDpiWithOrigin()
        {
            Font font = FakeFont.CreateFont("ABC");
            var origin = new Vector2(123, 345);
            const float dpi = 123;
            TextOptions options = new(font) { Dpi = dpi, Origin = origin };

            await Assert.That(options.Dpi).IsEqualTo(dpi);
            await Assert.That(options.FallbackFontFamilies).IsEmpty();
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(origin);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task ConstructorTest_FontOnly_WithFallbackFonts()
        {
            Font font = FakeFont.CreateFont("ABC");
            FontFamily[] fontFamilies = new[]
            {
                FakeFont.CreateFont("DEF").Family,
                FakeFont.CreateFont("GHI").Family
            };

            var options = new TextOptions(font)
            {
                FallbackFontFamilies = fontFamilies
            };

            await Assert.That(options.Dpi).IsEqualTo(72);
            await Assert.That(options.FallbackFontFamilies).IsEqualTo(fontFamilies);
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(Vector2.Zero);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task ConstructorTest_FontWithSingleDpi_WithFallbackFonts()
        {
            Font font = FakeFont.CreateFont("ABC");
            FontFamily[] fontFamilies = new[]
            {
                FakeFont.CreateFont("DEF").Family,
                FakeFont.CreateFont("GHI").Family
            };

            const float dpi = 123;
            var options = new TextOptions(font)
            {
                Dpi = dpi,
                FallbackFontFamilies = fontFamilies
            };

            await Assert.That(options.Dpi).IsEqualTo(dpi);
            await Assert.That(options.FallbackFontFamilies).IsEqualTo(fontFamilies);
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(Vector2.Zero);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task ConstructorTest_FontWithOrigin_WithFallbackFonts()
        {
            Font font = FakeFont.CreateFont("ABC");
            FontFamily[] fontFamilies = new[]
            {
                FakeFont.CreateFont("DEF").Family,
                FakeFont.CreateFont("GHI").Family
            };

            var origin = new Vector2(123, 345);
            TextOptions options = new(font)
            {
                FallbackFontFamilies = fontFamilies,
                Origin = origin
            };

            await Assert.That(options.Dpi).IsEqualTo(72);
            await Assert.That(options.FallbackFontFamilies).IsEqualTo(fontFamilies);
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(origin);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task ConstructorTest_FontWithSingleDpiWithOrigin_WithFallbackFonts()
        {
            Font font = FakeFont.CreateFont("ABC");
            FontFamily[] fontFamilies = new[]
            {
                FakeFont.CreateFont("DEF").Family,
                FakeFont.CreateFont("GHI").Family
            };

            var origin = new Vector2(123, 345);
            const float dpi = 123;
            TextOptions options = new(font)
            {
                Dpi = dpi,
                FallbackFontFamilies = fontFamilies,
                Origin = origin
            };

            await Assert.That(options.Dpi).IsEqualTo(dpi);
            await Assert.That(options.FallbackFontFamilies).IsEqualTo(fontFamilies);
            await Assert.That(options.Font).IsEqualTo(font);
            await Assert.That(options.Origin).IsEqualTo(origin);
            await VerifyPropertyDefault(options);
        }

        [Test]
        public async Task GetMissingGlyphFromMainFont()
        {
            Font font = FakeFont.CreateFontWithInstance("ABC", "ABC", out Fakes.FakeFontInstance abcFontInstance);
            FontFamily[] fontFamilies = new[]
            {
                FakeFont.CreateFontWithInstance("DEF", "DEF", out Fakes.FakeFontInstance defFontInstance).Family,
                FakeFont.CreateFontWithInstance("GHI", "GHI", out Fakes.FakeFontInstance ghiFontInstance).Family
            };

            var options = new TextOptions(font)
            {
                FallbackFontFamilies = fontFamilies,
                ColorFontSupport = ColorFontSupport.None
            };

            ReadOnlySpan<char> text = "Z".AsSpan();
            var renderer = new GlyphRenderer();
            TextRenderer.RenderTextTo(renderer, text, options);

            GlyphRendererParameters glyph = await Assert.That(renderer.GlyphKeys).HasSingleItem();
            await Assert.That(glyph.GlyphType).IsEqualTo(GlyphType.Fallback);
            await Assert.That(glyph.Font).IsEqualTo(abcFontInstance.Description.FontNameInvariantCulture.ToUpper());
        }

        [Test]
        [Arguments('A', "abcFontInstance")]
        [Arguments('F', "defFontInstance")]
        [Arguments('H', "efghiFontInstance")]
        public async Task GetGlyphFromFirstAvailableInstance(char character, string instance)
        {
            Font font = FakeFont.CreateFontWithInstance("ABC", "ABC", out Fakes.FakeFontInstance abcFontInstance);
            FontFamily[] fontFamilies = new[]
            {
                FakeFont.CreateFontWithInstance("DEF", "DEF", out Fakes.FakeFontInstance defFontInstance).Family,
                FakeFont.CreateFontWithInstance("EFGHI", "EFGHI", out Fakes.FakeFontInstance efghiFontInstance).Family
            };

            var options = new TextOptions(font)
            {
                FallbackFontFamilies = fontFamilies,
                ColorFontSupport = ColorFontSupport.None
            };

            ReadOnlySpan<char> text = new[] { character };
            var renderer = new GlyphRenderer();
            TextRenderer.RenderTextTo(renderer, text, options);
            GlyphRendererParameters glyph = await Assert.That(renderer.GlyphKeys).HasSingleItem();
            await Assert.That(glyph.GlyphType).IsEqualTo(GlyphType.Standard);
            Fakes.FakeFontInstance expectedInstance = instance switch
            {
                "abcFontInstance" => abcFontInstance,
                "defFontInstance" => defFontInstance,
                "efghiFontInstance" => efghiFontInstance,
                _ => throw new Exception("does not match")
            };

            await Assert.That(glyph.Font).IsEqualTo(expectedInstance.Description.FontNameInvariantCulture.ToUpper());
        }

        [Test]
        public async Task CloneTextOptionsIsNotNull()
        {
            await Assert.That(this.clonedTextOptions != null).IsTrue();
        }

        [Test]
        public async Task DefaultTextOptionsApplyKerning()
        {
            const KerningMode expected = KerningMode.Standard;
            await Assert.That(this.newTextOptions.KerningMode).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.KerningMode).IsEqualTo(expected);
        }

        [Test]
        public async Task DefaultTextOptionsHorizontalAlignment()
        {
            const HorizontalAlignment expected = HorizontalAlignment.Left;
            await Assert.That(this.newTextOptions.HorizontalAlignment).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.HorizontalAlignment).IsEqualTo(expected);
        }

        [Test]
        public async Task DefaultTextOptionsVerticalAlignment()
        {
            const VerticalAlignment expected = VerticalAlignment.Top;
            await Assert.That(this.newTextOptions.VerticalAlignment).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.VerticalAlignment).IsEqualTo(expected);
        }

        [Test]
        public async Task DefaultTextOptionsDpi()
        {
            const float expected = 72F;
            await Assert.That(this.newTextOptions.Dpi).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.Dpi).IsEqualTo(expected);
        }

        [Test]
        public async Task DefaultTextOptionsTabWidth()
        {
            const float expected = -1F;
            await Assert.That(this.newTextOptions.TabWidth).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.TabWidth).IsEqualTo(expected);
        }

        [Test]
        public async Task DefaultTextOptionsWrappingLength()
        {
            const float expected = -1F;
            await Assert.That(this.newTextOptions.WrappingLength).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.WrappingLength).IsEqualTo(expected);
        }

        [Test]
        public async Task DefaultTextOptionsLineSpacing()
        {
            const float expected = 1F;
            await Assert.That(this.newTextOptions.LineSpacing).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.LineSpacing).IsEqualTo(expected);
        }

        [Test]
        public async Task DefaultTextOptionsTextJustification()
        {
            const TextJustification expected = TextJustification.None;
            await Assert.That(this.newTextOptions.TextJustification).IsEqualTo(expected);
            await Assert.That(this.clonedTextOptions.TextJustification).IsEqualTo(expected);
        }

        [Test]
        public async Task NonDefaultClone()
        {
            TextOptions expected = new(this.fakeFont)
            {
                KerningMode = KerningMode.None,
                Dpi = 46F,
                HorizontalAlignment = HorizontalAlignment.Center,
                TabWidth = 3F,
                LineSpacing = -1F,
                VerticalAlignment = VerticalAlignment.Bottom,
                WrappingLength = 42F,
                FeatureTags = new List<Tag> { FeatureTags.OldstyleFigures }
            };

            TextOptions actual = new(expected);

            await Assert.That(actual.KerningMode).IsEqualTo(expected.KerningMode);
            await Assert.That(actual.Dpi).IsEqualTo(expected.Dpi);
            await Assert.That(actual.LineSpacing).IsEqualTo(expected.LineSpacing);
            await Assert.That(actual.HorizontalAlignment).IsEqualTo(expected.HorizontalAlignment);
            await Assert.That(actual.TabWidth).IsEqualTo(expected.TabWidth);
            await Assert.That(actual.VerticalAlignment).IsEqualTo(expected.VerticalAlignment);
            await Assert.That(actual.WrappingLength).IsEqualTo(expected.WrappingLength);
            await Assert.That(actual.FeatureTags).IsEquivalentTo(expected.FeatureTags, CollectionOrdering.Matching);
        }

        [Test]
        public async Task CloneIsDeep()
        {
            var expected = new TextOptions(this.fakeFont);
            TextOptions actual = new(expected)
            {
                KerningMode = KerningMode.None,
                Dpi = 46F,
                HorizontalAlignment = HorizontalAlignment.Center,
                TabWidth = 3F,
                LineSpacing = 2F,
                VerticalAlignment = VerticalAlignment.Bottom,
                TextJustification = TextJustification.InterCharacter,
                WrappingLength = 42F
            };

            await Assert.That(actual.KerningMode).IsNotEqualTo(expected.KerningMode);
            await Assert.That(actual.Dpi).IsNotEqualTo(expected.Dpi);
            await Assert.That(actual.LineSpacing).IsNotEqualTo(expected.LineSpacing);
            await Assert.That(actual.HorizontalAlignment).IsNotEqualTo(expected.HorizontalAlignment);
            await Assert.That(actual.TabWidth).IsNotEqualTo(expected.TabWidth);
            await Assert.That(actual.VerticalAlignment).IsNotEqualTo(expected.VerticalAlignment);
            await Assert.That(actual.WrappingLength).IsNotEqualTo(expected.WrappingLength);
            await Assert.That(actual.TextJustification).IsNotEqualTo(expected.TextJustification);
        }

        private static async Task VerifyPropertyDefault(TextOptions options)
        {
            await Assert.That(options.TabWidth).IsEqualTo(-1);
            await Assert.That(options.KerningMode).IsEqualTo(KerningMode.Standard);
            await Assert.That(options.WrappingLength).IsEqualTo(-1);
            await Assert.That(options.HorizontalAlignment).IsEqualTo(HorizontalAlignment.Left);
            await Assert.That(options.VerticalAlignment).IsEqualTo(VerticalAlignment.Top);
            await Assert.That(options.TextAlignment).IsEqualTo(TextAlignment.Start);
            await Assert.That(options.TextJustification).IsEqualTo(TextJustification.None);
            await Assert.That(options.TextDirection).IsEqualTo(TextDirection.Auto);
            await Assert.That(options.LayoutMode).IsEqualTo(LayoutMode.HorizontalTopBottom);
            await Assert.That(options.LineSpacing).IsEqualTo(1);
        }
    }
}
