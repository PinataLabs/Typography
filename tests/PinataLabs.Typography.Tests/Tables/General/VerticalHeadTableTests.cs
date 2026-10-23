// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class VerticalHeadTableTests
    {
        [Test]
        public async Task LoadVerticalHeadTable()
        {
            var writer = new BigEndianBinaryWriter();

            writer.WriteVerticalHeadTable(new VerticalHeadTable(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11));

            var tbl = VerticalHeadTable.Load(writer.GetReader());

            await Assert.That(tbl.Ascender).IsEqualTo(1);
            await Assert.That(tbl.Descender).IsEqualTo(2);
            await Assert.That(tbl.LineGap).IsEqualTo(3);
            await Assert.That(tbl.AdvanceHeightMax).IsEqualTo(4);
            await Assert.That(tbl.MinTopSideBearing).IsEqualTo(5);
            await Assert.That(tbl.MinBottomSideBearing).IsEqualTo(6);
            await Assert.That(tbl.YMaxExtent).IsEqualTo(7);
            await Assert.That(tbl.CaretSlopeRise).IsEqualTo(8);
            await Assert.That(tbl.CaretSlopeRun).IsEqualTo(9);
            await Assert.That(tbl.CaretOffset).IsEqualTo(10);
            await Assert.That(tbl.NumberOfVMetrics).IsEqualTo(11);
        }

        [Test]
        public async Task ShouldReturnNullWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using MemoryStream stream = writer.GetStream();
            await Assert.That(VerticalHeadTable.Load(new FontReader(stream))).IsNull();
        }
    }
}
