// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Tests.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// The FeatureVariations reader follows offsets and counts from untrusted data. It reads a table, or it rejects it, and it
/// never throws, whatever the bytes are. The fuzz project does not cover GSUB or GPOS, so these tests change the bytes of the
/// GPOS table of FeatureVariations.ttf one at a time and cut it short at every length.
/// </summary>
public class FeatureVariationsHardeningTests
{
    private static readonly byte[] Values = [0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF];

    private static readonly short[][] Coordinates = [[], [0, 0], [16384, 0], [-16384, 16384], [short.MaxValue, short.MinValue, 7]];

    // The feature list of the table, which has the one feature that the records change: kern.
    private static readonly FeatureListTable Features = GPosTable.Load(new BigEndianBinaryReader(new MemoryStream(ReadGpos(out _)), leaveOpen: false)).FeatureList;

    [Test]
    public async Task ChangingAnyByteOfTheTableReadsOrRejectsIt()
    {
        byte[] gpos = ReadGpos(out uint offset);
        List<string> thrown = [];
        for (int i = 0; i < gpos.Length; i++)
        {
            byte original = gpos[i];
            foreach (byte value in Values)
            {
                gpos[i] = value;
                try
                {
                    Use(FeatureVariationsTable.TryRead(gpos, offset, 1));
                }
                catch (Exception e)
                {
                    thrown.Add($"byte {i} = {value}: {e.GetType().Name}");
                }
            }

            gpos[i] = original;
        }

        await Assert.That(thrown).IsEmpty();
    }

    [Test]
    public async Task CuttingTheTableShortReadsOrRejectsIt()
    {
        byte[] gpos = ReadGpos(out uint offset);
        List<string> thrown = [];
        for (int length = 0; length <= gpos.Length; length++)
        {
            try
            {
                Use(FeatureVariationsTable.TryRead(gpos.AsSpan(0, length), offset, 1));
            }
            catch (Exception e)
            {
                thrown.Add($"{length} bytes: {e.GetType().Name}");
            }
        }

        await Assert.That(thrown).IsEmpty();
    }

    [Test]
    public async Task RecordsThatShareTheirSubtablesAreRejectedAfterAFixedAmountOfWork()
    {
        // 4096 records all point to one condition set of 65535 conditions, which all point to one condition. Each record
        // is cheap to write and expensive to read, so 4096 of them are 268 million conditions, if the reader repeats them.
        const int records = 4096;
        const int conditions = ushort.MaxValue;
        int conditionSet = 8 + (records * 8);
        int condition = conditionSet + 2 + (4 * conditions);
        byte[] table = new byte[condition + 8];
        BinaryPrimitives.WriteUInt16BigEndian(table, 1);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(4), records);
        for (int i = 0; i < records; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(8 + (8 * i)), (uint)conditionSet);
        }

        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(conditionSet), conditions);
        for (int i = 0; i < conditions; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(conditionSet + 2 + (4 * i)), (uint)condition);
        }

        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(condition), 1);

        await Assert.That(FeatureVariationsTable.TryRead(table, 0, 1)).IsNull();
    }

    [Test]
    public async Task ConditionsThatPointToThemselvesAreRejected()
    {
        // A NOT condition whose offset is 0 is its own child.
        byte[] table = new byte[8 + 8 + 2 + 4 + 5];
        BinaryPrimitives.WriteUInt16BigEndian(table, 1);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(8), 16);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(16), 1);
        BinaryPrimitives.WriteUInt32BigEndian(table.AsSpan(18), 6);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(22), 5);

        await Assert.That(FeatureVariationsTable.TryRead(table, 0, 1)).IsNull();
    }

    [Test]
    public async Task AMalformedGsubTableCountsAsAbsent()
    {
        // GSUB has rvrn, which substitutes C.rvrn for C from wght 650. Its table starts beyond the end of the GSUB table.
        byte[] font = VariationFontBytes.Read("Variations/FeatureVariations.ttf");
        Span<byte> gsub = VariationFontBytes.Table(font, "GSUB");
        BinaryPrimitives.WriteUInt32BigEndian(gsub.Slice(10), (uint)gsub.Length + 64);
        FontFamily family = VariationFontBytes.Load(font);
        float size = 72 * family.CreateFont(1).FontMetrics.UnitsPerEm;
        Font heavy = family.CreateFont(size, FontStyle.Regular, new FontVariation(Tag.Parse("wght"), 900F));

        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout("C".AsSpan(), new TextOptions(heavy));

        await Assert.That(layout[0].Glyph.GlyphMetrics.GlyphId).IsEqualTo((ushort)4);
    }

    private static byte[] ReadGpos(out uint featureVariationsOffset)
    {
        byte[] gpos = VariationFontBytes.Table(VariationFontBytes.Read("Variations/FeatureVariations.ttf"), "GPOS").ToArray();
        featureVariationsOffset = BinaryPrimitives.ReadUInt32BigEndian(gpos.AsSpan(10));
        return gpos;
    }

    // Asks the table for every instance that the coordinates describe, which reads the conditions and the substitutions.
    private static void Use(FeatureVariationsTable? table)
    {
        if (table is null)
        {
            return;
        }

        foreach (short[] coordinates in Coordinates)
        {
            table.Select(Features, coordinates, default);
        }
    }
}
