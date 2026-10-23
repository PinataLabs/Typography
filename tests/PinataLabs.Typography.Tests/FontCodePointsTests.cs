// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests
{
    public class FontCodePointsTests
    {
        [Test]
        public async Task TtfTest()
        {
            var collection = new FontCollection();
            FontFamily family = collection.Add(TestFonts.SimpleFontFile);
            Font font = family.CreateFont(12);

            IReadOnlyList<CodePoint> codePoints = font.FontMetrics.GetAvailableCodePoints();
            IEnumerable<int> codePointValues = codePoints.Select(x => x.Value);

            // Compare with https://everythingfonts.com/ttfdump
            await Assert.That(codePoints.Count).IsEqualTo(257);

            // Compare with https://fontdrop.info/
            await Assert.That(codePointValues).Contains(0x0000);
            await Assert.That(codePointValues).Contains(0x000D);
            await Assert.That(codePointValues).Contains(0x0020);
            await Assert.That(codePointValues).Contains(0x0041);
            await Assert.That(codePointValues).Contains(0x0042);
            await Assert.That(codePointValues).Contains(0x0061);
            await Assert.That(codePointValues).Contains(0x0062);
            await Assert.That(codePointValues).Contains(0xFFFF);

            HashSet<int> glyphIds = new();
            foreach (CodePoint codePoint in codePoints)
            {
                await Assert.That(font.TryGetGlyphs(codePoint, out IReadOnlyList<Glyph> glyphs)).IsTrue();
                foreach (Glyph glyph in glyphs)
                {
                    glyphIds.Add(glyph.GlyphMetrics.GlyphId);
                }
            }

            // Compare with https://fontdrop.info/
            await Assert.That(glyphIds.Count).IsEqualTo(8);
        }

        [Test]
        public async Task WoffTest()
        {
            var collection = new FontCollection();
            FontFamily family = collection.Add(TestFonts.SimpleFontFileWoff);
            Font font = family.CreateFont(12);

            IReadOnlyList<CodePoint> codePoints = font.FontMetrics.GetAvailableCodePoints();
            IEnumerable<int> codePointValues = codePoints.Select(x => x.Value);

            // Compare with https://everythingfonts.com/ttfdump
            await Assert.That(codePoints.Count).IsEqualTo(257);

            // Compare with https://fontdrop.info/
            await Assert.That(codePointValues).Contains(0x0000);
            await Assert.That(codePointValues).Contains(0x000D);
            await Assert.That(codePointValues).Contains(0x0020);
            await Assert.That(codePointValues).Contains(0x0041);
            await Assert.That(codePointValues).Contains(0x0042);
            await Assert.That(codePointValues).Contains(0x0061);
            await Assert.That(codePointValues).Contains(0x0062);
            await Assert.That(codePointValues).Contains(0xFFFF);

            HashSet<int> glyphIds = new();
            foreach (CodePoint codePoint in codePoints)
            {
                await Assert.That(font.TryGetGlyphs(codePoint, out IReadOnlyList<Glyph> glyphs)).IsTrue();
                foreach (Glyph glyph in glyphs)
                {
                    glyphIds.Add(glyph.GlyphMetrics.GlyphId);
                }
            }

            // Compare with https://fontdrop.info/
            await Assert.That(glyphIds.Count).IsEqualTo(8);
        }
    }
}
