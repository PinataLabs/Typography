// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.Variations;

public class AvarTableTests
{
    private const double F2Dot14 = 16384D;

    [Test]
    public async Task TheSegmentMapsOfTekoMapTheCoordinateOfAWeight()
    {
        // fontTools reads these maps from Teko-Variable.ttf: -1 to -1, 0 to 0, 0.25 to 0.210693359375, 0.5 to 0.4398193359375,
        // 0.75 to 0.70098876953125 and 1 to 1.
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");

        AvarTable? avar = AvarTable.Parse(VariationFontBytes.Table(font, "avar"));

        await Assert.That(avar).IsNotNull();
        await Assert.That(avar!.AxisCount).IsEqualTo(1);
        await Assert.That(avar.Map(0, -1D)).IsEqualTo(-1D);
        await Assert.That(avar.Map(0, 0D)).IsEqualTo(0D);
        await Assert.That(avar.Map(0, 0.25D)).IsEqualTo(0.210693359375D);
        await Assert.That(avar.Map(0, 0.75D)).IsEqualTo(0.70098876953125D);
        await Assert.That(avar.Map(0, 1D)).IsEqualTo(1D);
    }

    [Test]
    public async Task AValueBetweenTwoEntriesIsInterpolated()
    {
        AvarTable avar = AvarTable.Parse(Build(1, [(-1D, -1D), (-0.5D, -0.25D), (0D, 0D), (0.5D, 0.75D), (1D, 1D)]))!;

        await Assert.That(avar.Map(0, 0.25D)).IsEqualTo(0.375D);
        await Assert.That(avar.Map(0, -0.25D)).IsEqualTo(-0.125D);
        await Assert.That(avar.Map(0, 0.5D)).IsEqualTo(0.75D);
    }

    [Test]
    public async Task AMapWithNoEntryMapsEveryValue()
    {
        AvarTable none = AvarTable.Parse(Build(1, []))!;

        await Assert.That(none.Map(0, 0.3D)).IsEqualTo(0.3D);
    }

    [Test]
    public async Task AMapThatDoesNotPinTheEndsAndTheDefaultIsMalformed()
    {
        await Assert.That(AvarTable.Parse(Build(1, [(-1D, -1D), (0D, 0.1D), (1D, 1D)]))).IsNull();
        await Assert.That(AvarTable.Parse(Build(1, [(-1D, -1D), (1D, 1D)]))).IsNull();
        await Assert.That(AvarTable.Parse(Build(1, [(0D, 0D), (1D, 1D)]))).IsNull();
        await Assert.That(AvarTable.Parse(Build(1, [(-1D, -1D), (0D, 0D)]))).IsNull();
    }

    [Test]
    public async Task AnAvarThatDoesNotPinTheDefaultGivesTheFaceItselfAtTheDefaults()
    {
        // The map -1 to -1, 0 to 0.1, 1 to 1 is malformed, so the avar is absent and the default coordinates stay 0.
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        Span<byte> table = VariationFontBytes.Table(font, "avar");
        byte[] replacement = Build(1, [(-1D, -1D), (0D, 0.1D), (1D, 1D)]);
        replacement.CopyTo(table);

        FontFamily family = VariationFontBytes.Load(font);
        Font atDefault = family.CreateFont(12, FontStyle.Regular, new FontVariation(Tag.Parse("wght"), 300F));

        await Assert.That(atDefault.FontMetrics).IsSameReferenceAs(family.Faces[0]);
    }

    [Test]
    public async Task WhenTwoEntriesStartAtTheSameValueTheLastOneCounts()
    {
        AvarTable avar = AvarTable.Parse(Build(1, [(-1D, -1D), (0D, 0D), (0.5D, 0.5D), (0.5D, 0.75D), (1D, 1D)]))!;

        await Assert.That(avar.Map(0, 0.5D)).IsEqualTo(0.75D);
        await Assert.That(avar.Map(0, 0.25D)).IsEqualTo(0.375D);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public async Task Version1AndVersion2ReadTheSameSegmentMaps(int majorVersion)
    {
        // A version 2 table has two more offsets after the maps, which the reader does not use.
        byte[] bytes = Build(majorVersion, [(-1D, -1D), (0D, 0D), (0.5D, 0.75D), (1D, 1D)]);
        byte[] withOffsets = new byte[bytes.Length + 8];
        bytes.CopyTo(withOffsets, 0);
        BinaryPrimitives.WriteUInt32BigEndian(withOffsets.AsSpan(bytes.Length), 0xFFFFFFFF);

        AvarTable? avar = AvarTable.Parse(withOffsets);

        await Assert.That(avar).IsNotNull();
        await Assert.That(avar!.Map(0, 0.25D)).IsEqualTo(0.375D);
    }

    [Test]
    [Arguments(0)]
    [Arguments(3)]
    public async Task AnUnknownMajorVersionIsMalformed(int majorVersion)
    {
        await Assert.That(AvarTable.Parse(Build(majorVersion, [(-1D, -1D), (0D, 0D), (1D, 1D)]))).IsNull();
    }

    [Test]
    public async Task ATableThatEndsInsideAMapIsMalformed()
    {
        byte[] bytes = Build(1, [(-1D, -1D), (0D, 0D), (1D, 1D)]);

        for (int length = 0; length < bytes.Length; length++)
        {
            await Assert.That(AvarTable.Parse(bytes.AsSpan(0, length))).IsNull();
        }

        await Assert.That(AvarTable.Parse(bytes)).IsNotNull();
    }

    [Test]
    public async Task AMapThatRunsBackwardsIsMalformed()
    {
        // The to coordinates of a map must not decrease, and neither must the from coordinates.
        await Assert.That(AvarTable.Parse(Build(1, [(-1D, -1D), (0D, 0.5D), (0.5D, 0.25D), (1D, 1D)]))).IsNull();
        await Assert.That(AvarTable.Parse(Build(1, [(-1D, -1D), (0.5D, 0.5D), (0D, 0D), (1D, 1D)]))).IsNull();
    }

    [Test]
    public async Task AnAvarThatHasADifferentNumberOfAxesThanFvarIsNotUsed()
    {
        // Teko has the axis 300, 300, 700 and the segment maps of avar make wght 400 give 3452. Without avar it gives 4096.
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        BinaryPrimitives.WriteUInt16BigEndian(VariationFontBytes.Table(font, "avar").Slice(6), 0);

        FvarTable fvar = VariationFontBytes.Load(font).Faces[0].Description.Fvar!;

        await Assert.That((int)fvar.Normalize([400D]).Values[0]).IsEqualTo(4096);
    }

    [Test]
    public async Task AMalformedAvarIsTreatedAsAbsent()
    {
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        BinaryPrimitives.WriteUInt16BigEndian(VariationFontBytes.Table(font, "avar").Slice(8), 0xFFFF);

        FontMetrics face = VariationFontBytes.Load(font).Faces[0];

        await Assert.That(face.VariationAxes.Count).IsEqualTo(1);
        await Assert.That((int)face.Description.Fvar!.Normalize([400D]).Values[0]).IsEqualTo(4096);
    }

    [Test]
    public async Task TheAvarOfAVersion2FontGivesTheCoordinatesOfVersion1()
    {
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        Span<byte> table = VariationFontBytes.Table(font, "avar");
        BinaryPrimitives.WriteUInt16BigEndian(table, 2);

        FvarTable fvar = VariationFontBytes.Load(font).Faces[0].Description.Fvar!;

        await Assert.That((int)fvar.Normalize([400D]).Values[0]).IsEqualTo(3452);
    }

    // The bytes of an avar table with one axis. Each value is an F2Dot14 number.
    private static byte[] Build(int majorVersion, (double From, double To)[] map)
    {
        byte[] bytes = new byte[8 + 2 + (4 * map.Length)];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)majorVersion);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(8), (ushort)map.Length);
        for (int i = 0; i < map.Length; i++)
        {
            BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(10 + (4 * i)), (short)(map[i].From * F2Dot14));
            BinaryPrimitives.WriteInt16BigEndian(bytes.AsSpan(12 + (4 * i)), (short)(map[i].To * F2Dot14));
        }

        return bytes;
    }
}
