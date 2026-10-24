// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic.GPos;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

public class MarkAttachmentTests
{
    [Test]
    public async Task MarkWithoutAnchorForBaseClassDoesNotThrow()
    {
        // U+0910 has no anchor for the class of U+0901 in the mark-to-base lookup.
        Font font = new FontCollection().Add(TestFonts.TekoVariableFile).CreateFont(30);

        FontRectangle bounds = TextMeasurer.MeasureBounds("ऐँ", new TextOptions(font));

        await Assert.That(bounds.Width).IsGreaterThan(0);
    }

    [Test]
    public async Task MarkArrayWithNullAnchorOffsetLoads()
    {
        // markCount = 4, so reading the NULL offset as an anchor table yields the invalid anchorFormat 4.
        // Records 0 and 2 have a NULL anchor offset, record 1 and 3 point to an AnchorFormat1 table.
        BigEndianBinaryWriter writer = new();
        writer.WriteUInt16(4);
        writer.WriteUInt16(0);
        writer.WriteOffset16(0);
        writer.WriteUInt16(1);
        writer.WriteOffset16(18);
        writer.WriteUInt16(0);
        writer.WriteOffset16(0);
        writer.WriteUInt16(1);
        writer.WriteOffset16(18);
        writer.WriteUInt16(1); // anchorFormat
        writer.WriteUInt16(100);
        writer.WriteUInt16(200);

        MarkArrayTable table = new(writer.GetReader(), 0);

        await Assert.That(table.MarkRecords.Length).IsEqualTo(4);
        await Assert.That(table.MarkRecords[0].MarkAnchorTable).IsNull();
        await Assert.That(table.MarkRecords[1].MarkAnchorTable).IsNotNull();
        await Assert.That(table.MarkRecords[2].MarkAnchorTable).IsNull();
        await Assert.That(table.MarkRecords[3].MarkAnchorTable).IsNotNull();
    }
}
