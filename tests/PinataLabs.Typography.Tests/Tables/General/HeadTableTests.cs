// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class HeadTableTests
    {
        [Test]
        public async Task LoadHead()
        {
            var writer = new BigEndianBinaryWriter();

            writer.WriteHeadTable(new HeadTable(
                HeadTable.HeadFlags.None,
                HeadTable.HeadMacStyle.Italic | HeadTable.HeadMacStyle.Bold,
                1024,
                new DateTime(2017, 02, 06, 07, 47, 00),
                new DateTime(2017, 02, 07, 07, 47, 00),
                new Bounds(0, 0, 1024, 1022),
                0,
                HeadTable.IndexLocationFormats.Offset16));

            var head = HeadTable.Load(writer.GetReader());

            await Assert.That(head.Flags).IsEqualTo(HeadTable.HeadFlags.None);
            await Assert.That(head.MacStyle).IsEqualTo(HeadTable.HeadMacStyle.Italic | HeadTable.HeadMacStyle.Bold);
            await Assert.That(head.UnitsPerEm).IsEqualTo(1024);
            await Assert.That(head.Created).IsEqualTo(new DateTime(2017, 02, 06, 07, 47, 00));
            await Assert.That(head.Modified).IsEqualTo(new DateTime(2017, 02, 07, 07, 47, 00));
            await Assert.That(head.Bounds.Min.X).IsEqualTo(0);
            await Assert.That(head.Bounds.Min.Y).IsEqualTo(0);
            await Assert.That(head.Bounds.Max.X).IsEqualTo(1024);
            await Assert.That(head.Bounds.Max.Y).IsEqualTo(1022);
            await Assert.That(head.LowestRecPPEM).IsEqualTo(0);
            await Assert.That(head.IndexLocationFormat).IsEqualTo(HeadTable.IndexLocationFormats.Offset16);
        }
    }
}
