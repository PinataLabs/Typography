// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.General.CMap;
using PinataLabs.Typography.Unicode;
using PinataLabs.Typography.WellKnownIds;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;

namespace PinataLabs.Typography.Tests.Tables.General.CMap
{
    public class Format4SubTableTests
    {
        [Test]
        public async Task LoadFormat4()
        {
            var writer = new BigEndianBinaryWriter();

            // int subtableCount = 1;
            writer.WriteCMapSubTable(
                new Format4SubTable(
                    0,
                    PlatformIDs.Windows,
                    2,
                    new[] { new Format4SubTable.Segment(0, 1, 2, 3, 4) },
                    new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 }));

            BigEndianBinaryReader reader = writer.GetReader();
            ushort format = reader.ReadUInt16(); // read format before we pass along as that's what the cmap table does
            await Assert.That(format).IsEqualTo(4);

            Format4SubTable table = Format4SubTable.Load(
                new[] { new EncodingRecord(PlatformIDs.Windows, 2, 0) },
                reader).Single();

            await Assert.That(table.Language).IsEqualTo(0);
            await Assert.That(table.Platform).IsEqualTo(PlatformIDs.Windows);
            await Assert.That(table.Encoding).IsEqualTo(2);
            await Assert.That(table.GlyphIds).IsEquivalentTo(new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 }, CollectionOrdering.Matching);

            await Assert.That(table.Segments).HasSingleItem();
            Format4SubTable.Segment seg = table.Segments[0];
            await Assert.That(seg.Index).IsEqualTo(0);
            await Assert.That(seg.End).IsEqualTo(1);
            await Assert.That(seg.Start).IsEqualTo(2);
            await Assert.That(seg.Delta).IsEqualTo(3);
            await Assert.That(seg.Offset).IsEqualTo(4);
        }

        [Test]
        [Arguments(10, 1, true)]
        [Arguments(20, 11, true)]
        [Arguments(30, 12, true)]
        [Arguments(90, 72, true)]
        [Arguments(500, 0, false)] // not in range
        public async Task GetCharacter(int src, int expected, bool expectedFound)
        {
            // segCountX2:    8
            // searchRange:   8
            // entrySelector: 4
            // rangeShift:    0
            // endCode:       20  90  480  0xffff
            // reservedPad:   0
            // startCode:     10  30  153  0xffff
            // dDelta:        -9  -18 -27  1
            // idRangeOffset: 0   0   0    0
            ushort[] glyphs = Enumerable.Range(0, expected).Select(x => (ushort)x).ToArray();

            Format4SubTable.Segment[] segments = new[]
            {
                new Format4SubTable.Segment(0, 20, 10, -9, 0),
                new Format4SubTable.Segment(1, 90, 30, -18, 0),
                new Format4SubTable.Segment(2, 480, 153, -27, 0),
            };

            var table = new Format4SubTable(
                0,
                PlatformIDs.Windows,
                0,
                segments,
                glyphs);

            // ushort id = table.GetGlyphId(src);
            bool found = table.TryGetGlyphId(new CodePoint(src), out ushort id);

            await Assert.That(id).IsEqualTo(expected);
            await Assert.That(found).IsEqualTo(expectedFound);
        }
    }
}
