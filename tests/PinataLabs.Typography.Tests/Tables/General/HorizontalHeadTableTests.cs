// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class HorizontalHeadTableTests
    {
        [Test]
        public async Task LoadHorizontalHeadTable()
        {
            var writer = new BigEndianBinaryWriter();

            writer.WriteHorizontalHeadTable(new HorizontalHeadTable(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11));

            var tbl = HorizontalHeadTable.Load(writer.GetReader());

            await Assert.That(tbl.Ascender).IsEqualTo(1);
            await Assert.That(tbl.Descender).IsEqualTo(2);
            await Assert.That(tbl.LineGap).IsEqualTo(3);
            await Assert.That(tbl.AdvanceWidthMax).IsEqualTo(4);
            await Assert.That(tbl.MinLeftSideBearing).IsEqualTo(5);
            await Assert.That(tbl.MinRightSideBearing).IsEqualTo(6);
            await Assert.That(tbl.XMaxExtent).IsEqualTo(7);
            await Assert.That(tbl.CaretSlopeRise).IsEqualTo(8);
            await Assert.That(tbl.CaretSlopeRun).IsEqualTo(9);
            await Assert.That(tbl.CaretOffset).IsEqualTo(10);
            await Assert.That(tbl.NumberOfHMetrics).IsEqualTo(11);
        }

        [Test]
        public async Task ShouldReturnNullWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                await Assert.That(HorizontalHeadTable.Load(new FontReader(stream))).IsNull();
            }
        }
    }
}
