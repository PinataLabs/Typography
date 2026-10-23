// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_191
    {
        [Test]
        public async Task CanLoadMacintoshGlyphs()
        {
            Font font = new FontCollection()
                .AddCollection(TestFonts.HelveticaTTCFile)
                .First(x => x.GetAvailableStyles().Contains(FontStyle.Regular)).CreateFont(12);

            const ColorFontSupport support = ColorFontSupport.None;

            await Assert.That(font.TryGetGlyphs(new CodePoint('A'), support, out IReadOnlyList<Glyph> glyphsA)).IsTrue();
            Glyph[] a = glyphsA.ToArray();

            await Assert.That(font.TryGetGlyphs(new CodePoint('x'), support, out IReadOnlyList<Glyph> glyphsX)).IsTrue();
            Glyph[] x = glyphsX.ToArray();

            Glyph ga = await Assert.That(a).HasSingleItem();
            Glyph gx = await Assert.That(x).HasSingleItem();
            await Assert.That(gx).IsNotEqualTo(ga);

            await Assert.That(ga.GlyphMetrics.AdvanceWidth).IsEqualTo(1366);
            await Assert.That(ga.GlyphMetrics.AdvanceHeight).IsEqualTo(2048);

            await Assert.That(gx.GlyphMetrics.AdvanceWidth).IsEqualTo(1024);
            await Assert.That(gx.GlyphMetrics.AdvanceHeight).IsEqualTo(2048);
        }
    }
}
