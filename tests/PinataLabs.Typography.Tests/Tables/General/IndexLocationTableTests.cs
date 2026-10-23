// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.TrueType;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class IndexLocationTableTests
    {
        [Test]
        public async Task ShouldThrowExceptionWhenHeadTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                MissingFontTableException exception = await Assert.That(
                        () => IndexLocationTable.Load(new FontReader(stream))).ThrowsExactly<MissingFontTableException>();

                await Assert.That(exception.Table).IsEqualTo("head");
            }
        }

        [Test]
        public async Task ShouldThrowExceptionWhenMaximumProfileTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader(new TableHeader("head", 0, 0, 0));

            writer.WriteHeadTable(new HeadTable(
                HeadTable.HeadFlags.None,
                HeadTable.HeadMacStyle.Italic | HeadTable.HeadMacStyle.Bold,
                1024,
                new DateTime(2017, 02, 06, 07, 47, 00),
                new DateTime(2017, 02, 07, 07, 47, 00),
                new Bounds(0, 0, 1024, 1022),
                0,
                HeadTable.IndexLocationFormats.Offset16));

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                InvalidFontTableException exception = await Assert.That(
                    () => IndexLocationTable.Load(new FontReader(stream))).ThrowsExactly<InvalidFontTableException>();

                await Assert.That(exception.Table).IsEqualTo("maxp");
            }
        }

        [Test]
        public async Task ShouldReturnNullWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader(new TableHeader("head", 0, 0, 0), new TableHeader("maxp", 0, 0, 0));

            writer.WriteHeadTable(new HeadTable(
                HeadTable.HeadFlags.None,
                HeadTable.HeadMacStyle.Italic | HeadTable.HeadMacStyle.Bold,
                1024,
                new DateTime(2017, 02, 06, 07, 47, 00),
                new DateTime(2017, 02, 07, 07, 47, 00),
                new Bounds(0, 0, 1024, 1022),
                0,
                HeadTable.IndexLocationFormats.Offset16));

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                await Assert.That(IndexLocationTable.Load(new FontReader(stream))).IsNull();
            }
        }
    }
}
