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
        // markCount = 2; the first record has a NULL anchor offset, the second points to an AnchorFormat1 table.
        BigEndianBinaryWriter writer = new();
        writer.WriteUInt16(2);
        writer.WriteUInt16(0);
        writer.WriteOffset16(0);
        writer.WriteUInt16(1);
        writer.WriteOffset16(10);
        writer.WriteUInt16(1); // anchorFormat
        writer.WriteUInt16(100);
        writer.WriteUInt16(200);

        MarkArrayTable table = new(writer.GetReader(), 0);

        await Assert.That(table.MarkRecords.Length).IsEqualTo(2);
        await Assert.That(table.MarkRecords[0].MarkAnchorTable).IsNull();
        await Assert.That(table.MarkRecords[1].MarkAnchorTable).IsNotNull();
    }
}
