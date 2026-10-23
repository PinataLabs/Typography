// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Issues
{
    public class Issues_23
    {
        [Test]
        public async Task BleadingFonts()
        {
            // wendy one returns wrong points for 'o'
            Font font = new FontCollection().Add(TestFonts.WendyOneFile).CreateFont(12);

            var r = new GlyphRenderer();

            new TextRenderer(r).RenderText("o", new TextOptions(new Font(font, 30)));

            await Assert.That(r.ControlPoints).DoesNotContain(System.Numerics.Vector2.Zero);
        }
    }
}
