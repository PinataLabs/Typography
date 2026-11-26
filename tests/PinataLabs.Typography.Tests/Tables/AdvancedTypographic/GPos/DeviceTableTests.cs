// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.GPos;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

/// <summary>
/// A value record or an anchor table points to a device table. The library keeps the address of a <c>VariationIndex</c>
/// table, and ignores a <c>Device</c> table, which holds deltas for sizes of a font that does not vary.
/// </summary>
public class DeviceTableTests
{
    private const ushort VariationIndexFormat = 0x8000;
    private const ushort DeviceFormat = 2;

    [Test]
    public async Task AValueRecordKeepsTheVariationIndexOfEachDevice()
    {
        // The record is at 0 and its four devices follow it, each with the outer and inner index of a VariationIndex.
        BigEndianBinaryWriter writer = new();
        writer.WriteInt16(-40);
        writer.WriteOffset16(10);
        writer.WriteOffset16(16);
        writer.WriteOffset16(22);
        writer.WriteOffset16(28);
        WriteDevice(writer, 1, 2, VariationIndexFormat);
        WriteDevice(writer, 3, 4, VariationIndexFormat);
        WriteDevice(writer, 5, 6, VariationIndexFormat);
        WriteDevice(writer, 7, 8, VariationIndexFormat);
        BigEndianBinaryReader reader = writer.GetReader();

        ValueRecord record = new(reader, ValueFormat.XAdvance | ValueFormat.XPlacementDevice | ValueFormat.YPlacementDevice | ValueFormat.XAdvanceDevice | ValueFormat.YAdvanceDevice, 0);

        await Assert.That(record.XAdvance).IsEqualTo((short)-40);
        await Assert.That((record.XPlacementVariation.Outer, record.XPlacementVariation.Inner)).IsEqualTo((1, 2));
        await Assert.That((record.YPlacementVariation.Outer, record.YPlacementVariation.Inner)).IsEqualTo((3, 4));
        await Assert.That((record.XAdvanceVariation.Outer, record.XAdvanceVariation.Inner)).IsEqualTo((5, 6));
        await Assert.That((record.YAdvanceVariation.Outer, record.YAdvanceVariation.Inner)).IsEqualTo((7, 8));

        // The reader stays after the record, so that the next record of an array starts there.
        await Assert.That(reader.BaseStream.Position).IsEqualTo(10L);
    }

    [Test]
    public async Task TheOffsetsOfAValueRecordCountFromItsParentTable()
    {
        // The parent table starts at 4, and its device is 6 bytes after that.
        BigEndianBinaryWriter writer = new();
        writer.WriteUInt32(0);
        writer.WriteOffset16(6);
        writer.WriteUInt16(0);
        writer.WriteUInt16(0);
        WriteDevice(writer, 9, 10, VariationIndexFormat);
        BigEndianBinaryReader reader = writer.GetReader();
        reader.Seek(4, SeekOrigin.Begin);

        ValueRecord record = new(reader, ValueFormat.XAdvanceDevice, 4);

        await Assert.That((record.XAdvanceVariation.Outer, record.XAdvanceVariation.Inner)).IsEqualTo((9, 10));
    }

    [Test]
    public async Task ADeviceTableOfAFontThatDoesNotVaryHasNoVariationIndex()
    {
        BigEndianBinaryWriter writer = new();
        writer.WriteOffset16(4);
        writer.WriteUInt16(0);
        WriteDevice(writer, 9, 10, DeviceFormat);
        BigEndianBinaryReader reader = writer.GetReader();

        ValueRecord record = new(reader, ValueFormat.XAdvanceDevice, 0);

        await Assert.That(record.XAdvanceVariation.IsNone).IsTrue();
    }

    [Test]
    public async Task ANullOrTruncatedDeviceOffsetHasNoVariationIndex()
    {
        // The first device is NULL. The second one starts 2 bytes before the end of the stream.
        BigEndianBinaryWriter writer = new();
        writer.WriteOffset16(0);
        writer.WriteOffset16(6);
        writer.WriteUInt16(1);
        writer.WriteUInt16(2);
        BigEndianBinaryReader reader = writer.GetReader();

        ValueRecord record = new(reader, ValueFormat.XPlacementDevice | ValueFormat.YPlacementDevice, 0);

        await Assert.That(record.XPlacementVariation.IsNone).IsTrue();
        await Assert.That(record.YPlacementVariation.IsNone).IsTrue();
    }

    [Test]
    [Arguments(10L, true)]
    [Arguments(9L, false)]
    [Arguments(4L, false)]
    [Arguments(-1L, true)]
    public async Task ADeviceTableMustFitInTheGposTableAndNotOnlyInTheFont(long tableLength, bool hasIndex)
    {
        // The record is 2 bytes and its device starts at 4 and ends at 10. The stream goes on after the table, as a font
        // goes on with the next table, and a table of -1 bytes runs to the end of the stream.
        BigEndianBinaryWriter writer = new();
        writer.WriteOffset16(4);
        writer.WriteUInt16(0);
        WriteDevice(writer, 9, 10, VariationIndexFormat);
        writer.WriteUInt32(0);
        BigEndianBinaryReader reader = new(writer.GetStream(), true, tableLength);

        ValueRecord record = new(reader, ValueFormat.XAdvanceDevice, 0);

        await Assert.That(record.XAdvanceVariation.IsNone).IsNotEqualTo(hasIndex);
    }

    [Test]
    public async Task AnAnchorKeepsTheDeltaOfEachCoordinate()
    {
        // Teko moves at least one of its first rows of the GDEF store at wght 700. A row that does not move would pass anyway.
        Font font = new FontCollection().Add(TestFonts.TekoVariableFile).CreateFont(12, FontStyle.Bold);
        VariationIndex moved = FindMovedIndex(font.FontMetrics);
        float delta = font.FontMetrics.GetDeviceDelta(moved);

        // The anchor is at 0, with the horizontal device at 10 and the vertical one at 16, and its vertical coordinate is 200.
        BigEndianBinaryWriter writer = new();
        writer.WriteUInt16(3);
        writer.WriteInt16(100);
        writer.WriteInt16(200);
        writer.WriteOffset16(10);
        writer.WriteOffset16(0);
        WriteDevice(writer, (ushort)moved.Outer, (ushort)moved.Inner, VariationIndexFormat);
        AnchorTable horizontal = AnchorTable.Load(writer.GetReader(), 0);

        BigEndianBinaryWriter vertical = new();
        vertical.WriteUInt16(3);
        vertical.WriteInt16(100);
        vertical.WriteInt16(200);
        vertical.WriteOffset16(0);
        vertical.WriteOffset16(10);
        WriteDevice(vertical, (ushort)moved.Outer, (ushort)moved.Inner, VariationIndexFormat);
        AnchorTable verticalAnchor = AnchorTable.Load(vertical.GetReader(), 0);

        AnchorXY x = horizontal.GetAnchor(font.FontMetrics, null!, null!);
        AnchorXY y = verticalAnchor.GetAnchor(font.FontMetrics, null!, null!);

        await Assert.That(delta).IsNotEqualTo(0F);
        await Assert.That((x.XCoordinate, x.YCoordinate)).IsEqualTo((100 + delta, 200F));
        await Assert.That((y.XCoordinate, y.YCoordinate)).IsEqualTo((100F, 200 + delta));
    }

    [Test]
    public async Task TheFaceAndTheDefaultInstanceHaveNoDelta()
    {
        FontFamily family = new FontCollection().Add(TestFonts.TekoVariableFile);
        Font bold = family.CreateFont(12, FontStyle.Bold);
        VariationIndex moved = FindMovedIndex(bold.FontMetrics);

        // Teko has its default instance at wght 300, which is the face.
        Font face = family.CreateFont(12, FontStyle.Regular, new FontVariation(Tag.Parse("wght"), 300F));

        await Assert.That(face.FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(face.FontMetrics.GetDeviceDelta(moved)).IsEqualTo(0F);
    }

    private static VariationIndex FindMovedIndex(FontMetrics instance)
    {
        for (ushort inner = 0; inner < 64; inner++)
        {
            VariationIndex index = new(0, inner);
            if (instance.GetDeviceDelta(index) != 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException("The instance has no delta in the first rows.");
    }

    private static void WriteDevice(BigEndianBinaryWriter writer, ushort first, ushort second, ushort deltaFormat)
    {
        writer.WriteUInt16(first);
        writer.WriteUInt16(second);
        writer.WriteUInt16(deltaFormat);
    }
}
