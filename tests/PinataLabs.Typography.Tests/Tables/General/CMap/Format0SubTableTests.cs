// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General.CMap;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.WellKnownIds;
using TUnit.Assertions.Enums;

namespace PinataLabs.Typography.Tests.Tables.General.CMap
{
    public class Format0SubTableTests
    {
        [Test]
        public async Task LoadFormat0()
        {
            var writer = new BigEndianBinaryWriter();

            // int subtableCount = 1;
            writer.WriteCMapSubTable(
                new Format0SubTable(
                    0,
                    PlatformIDs.Windows,
                    2,
                    new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

            BigEndianBinaryReader reader = writer.GetReader();
            ushort format = reader.ReadUInt16(); // read format before we pass along as that's what the cmap table does
            await Assert.That(format).IsEqualTo(0);

            Format0SubTable table = Format0SubTable.Load(
                new[] { new EncodingRecord(PlatformIDs.Windows, 2, 0) },
                reader).Single();

            await Assert.That(table.Language).IsEqualTo(0);
            await Assert.That(table.Platform).IsEqualTo(PlatformIDs.Windows);
            await Assert.That(table.Encoding).IsEqualTo(2);
            await Assert.That(table.GlyphIds).IsEquivalentTo(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, CollectionOrdering.Matching);
        }

        [Test]
        public async Task GetCharacter()
        {
            var format = new Format0SubTable(0, PlatformIDs.Windows, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            bool found = format.TryGetGlyphId(new CodePoint(4), out ushort id);

            await Assert.That(found).IsTrue();
            await Assert.That(id).IsEqualTo(5);
        }

        [Test]
        public async Task GetCharacter_missing()
        {
            var format = new Format0SubTable(0, PlatformIDs.Windows, 2, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

            bool found = format.TryGetGlyphId(new CodePoint(99), out ushort id);

            await Assert.That(found).IsFalse();
            await Assert.That(id).IsEqualTo(0);
        }
    }
}
