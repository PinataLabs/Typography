// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using PinataLabs.Typography.Tables.General.Colr;
using Xunit;
using static PinataLabs.Typography.Tests.WriterExtensions;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class ColrTableTests
    {
        [Fact]
        public void ShouldReturnNullWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                Assert.Null(ColrTable.Load(new FontReader(stream)));
            }
        }

        [Fact]
        public void ShouldReturnTableValues()
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
                Assert.Equal(2, layers.Length);
            }
        }
    }
}
