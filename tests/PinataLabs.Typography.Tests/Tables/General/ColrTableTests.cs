// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General.Colr;
using static PinataLabs.Typography.Tests.WriterExtensions;

namespace PinataLabs.Typography.Tests.Tables.General;

public class ColrTableTests
{
    [Test]
    public async Task ShouldReturnNullWhenTableCouldNotBeFound()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteTrueTypeFileHeader();

        using (System.IO.MemoryStream stream = writer.GetStream())
        {
            await Assert.That(ColrTable.Load(new FontReader(stream))).IsNull();
        }
    }

    [Test]
    public async Task ShouldReturnTableValues()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteTrueTypeFileHeader();
        writer.WriteColrTable(new[]
        {
            new ColrGlyphRecord
            {
                Glyph = 1,
                Layers =
                {
                    new ColrLayerRecord { Glyph = 10, Palette = 1 },
                    new ColrLayerRecord { Glyph = 11, Palette = 2 }
                }
            },
            new ColrGlyphRecord
            {
                Glyph = 2,
                Layers =
                {
                    new ColrLayerRecord { Glyph = 12, Palette = 1 },
                    new ColrLayerRecord { Glyph = 13, Palette = 2 }
                }
            }
        });

        using (System.IO.Stream stream = TestFonts.TwemojiMozillaData())
        {
            var reader = new FontReader(stream);
            ColrTable tbl = reader.GetTable<ColrTable>();

            System.Span<LayerRecord> layers = tbl.GetLayers(15);
            await Assert.That(layers.Length).IsEqualTo(2);
        }
    }
}
