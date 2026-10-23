// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.Fakes;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_47
    {
        [Test]
        [Arguments("hello world hello world hello world hello world")]
        public async Task LeftAlignedTextNewLineShouldNotStartWithWhiteSpace(string text)
        {
            Font font = CreateFont("\t x");

            var r = new GlyphRenderer();

            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(new Font(font, 30))
            {
                WrappingLength = 350,
                HorizontalAlignment = HorizontalAlignment.Left
            });

            float lineYPos = layout[0].PenLocation.Y;
            foreach (GlyphLayout glyph in layout)
            {
                if (lineYPos != glyph.PenLocation.Y)
                {
                    await Assert.That(glyph.IsWhiteSpace()).IsFalse();
                    lineYPos = glyph.PenLocation.Y;
                }
            }
        }

        [Test]
        [Arguments("hello world hello world hello world hello world", HorizontalAlignment.Left)]
        [Arguments("hello world hello world hello world hello world", HorizontalAlignment.Right)]
        [Arguments("hello world hello world hello world hello world", HorizontalAlignment.Center)]
        [Arguments("hello   world   hello   world   hello   hello   world", HorizontalAlignment.Left)]
        public async Task NewWrappedLinesShouldNotStartOrEndWithWhiteSpace(string text, HorizontalAlignment horizontalAlignment)
        {
            Font font = CreateFont("\t x");

            var r = new GlyphRenderer();

            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(new Font(font, 30))
            {
                WrappingLength = 350,
                HorizontalAlignment = horizontalAlignment
            });

            float lineYPos = layout[0].PenLocation.Y;
            for (int i = 0; i < layout.Count; i++)
            {
                GlyphLayout glyph = layout[i];
                if (lineYPos != glyph.PenLocation.Y)
                {
                    await Assert.That(glyph.IsWhiteSpace()).IsFalse();
                    await Assert.That(layout[i - 1].IsWhiteSpace()).IsFalse();
                    lineYPos = glyph.PenLocation.Y;
                }
            }
        }

        [Test]
        public async Task WhiteSpaceAtStartOfTextShouldNotBeTrimmed()
        {
            Font font = CreateFont("\t x");
            string text = "   hello world hello world hello world";

            var r = new GlyphRenderer();

            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(new Font(font, 30))
            {
                WrappingLength = 350
            });

            await Assert.That(layout[0].IsWhiteSpace()).IsTrue();
            await Assert.That(layout[1].IsWhiteSpace()).IsTrue();
            await Assert.That(layout[2].IsWhiteSpace()).IsTrue();
        }

        [Test]
        public async Task WhiteSpaceAtTheEndOfTextShouldBeTrimmed()
        {
            Font font = CreateFont("\t x");
            string text = "hello world hello world hello world   ";

            var r = new GlyphRenderer();

            IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(new Font(font, 30))
            {
                WrappingLength = 350
            });

            await Assert.That(layout[layout.Count - 1].IsWhiteSpace()).IsFalse();
            await Assert.That(layout[layout.Count - 2].IsWhiteSpace()).IsFalse();
            await Assert.That(layout[layout.Count - 3].IsWhiteSpace()).IsFalse();
        }

        public static Font CreateFont(string text)
        {
            var fc = (IFontMetricsCollection)new FontCollection();
            Font d = fc.AddMetrics(new FakeFontInstance(text), CultureInfo.InvariantCulture).CreateFont(12);
            return new Font(d, 1);
        }
    }
}
