// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class CffTableValidationTests
{
    [Test]
    public async Task FontDeclaringCffOutlinesWithoutCffTableIsRejected()
    {
        byte[] data = File.ReadAllBytes(TestFonts.FDArrayTest257File);

        // Rename the 'CFF ' directory entry so the font still declares CFF outlines but has no CFF table.
        ushort numTables = (ushort)((data[4] << 8) | data[5]);
        bool renamed = false;
        for (int i = 0; i < numTables; i++)
        {
            int entry = 12 + (i * 16);
            if (Encoding.ASCII.GetString(data, entry, 4) == "CFF ")
            {
                data[entry] = (byte)'X';
                renamed = true;
            }
        }

        await Assert.That(renamed).IsTrue();

        await Assert.That(() =>
        {
            FontCollection collection = new();
            collection.Add(new MemoryStream(data));
        }).Throws<InvalidFontFileException>();
    }

    [Test]
    public async Task ACidFontIgnoresTheBadPrivateOffsetOfItsTopDict()
    {
        byte[] data = FontWithBadTopPrivate.Create();

        Font font = new FontCollection().Add(new MemoryStream(data)).CreateFont(8);
        ColorGlyphRenderer renderer = new();

        TextRenderer.RenderTextTo(renderer, "A", new TextOptions(font));

        await Assert.That(renderer.GlyphKeys).HasSingleItem();
    }
}
