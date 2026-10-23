// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.General.CMap;
using PinataLabs.Typography.WellKnownIds;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Tables.General
{
    public class CMapTableTests
    {
        [Test]
        public async Task LoadFormat0()
        {
            var writer = new BigEndianBinaryWriter();

            writer.WriteCMapTable(new[]
            {
                new Format0SubTable(0, PlatformIDs.Windows, 9, new byte[] { 0, 1, 2 })
            });

            var table = CMapTable.Load(writer.GetReader());

            await Assert.That(table.Tables.Where(x => x != null)).HasSingleItem();

            Format0SubTable[] format0Tables = table.Tables.OfType<Format0SubTable>().ToArray();
            await Assert.That(format0Tables).HasSingleItem();
        }

        [Test]
        public async Task ShouldThrowExceptionWhenTableCouldNotBeFound()
        {
            var writer = new BigEndianBinaryWriter();
            writer.WriteTrueTypeFileHeader();

            using (System.IO.MemoryStream stream = writer.GetStream())
            {
                InvalidFontTableException exception = await Assert.That(() => CMapTable.Load(new FontReader(stream))).ThrowsExactly<InvalidFontTableException>();

                await Assert.That(exception.Table).IsEqualTo("cmap");
            }
        }
    }
}
