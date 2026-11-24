// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Woff;

namespace PinataLabs.Typography.Tests.Tables.Woff;

public class Woff2GlyphReconstructionTests
{
    [Test]
    public async Task AContourWithoutPointsIsDroppedAndTheOthersKept()
    {
        byte[] transformed = TransformedGlyf(contourPointCounts: [3, 0, 4]);

        Woff2Utils.ReconstructGlyphTables(transformed, out byte[] glyf, out byte[] loca);

        await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4))).IsGreaterThan(0U);
        await Assert.That(BinaryPrimitives.ReadInt16BigEndian(glyf)).IsEqualTo((short)2);
        await Assert.That(BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(10))).IsEqualTo((ushort)2);
        await Assert.That(BinaryPrimitives.ReadUInt16BigEndian(glyf.AsSpan(12))).IsEqualTo((ushort)6);
    }

    [Test]
    public async Task AGlyphWhoseContoursHaveNoPointsIsEmpty()
    {
        byte[] transformed = TransformedGlyf(contourPointCounts: [0, 0]);

        Woff2Utils.ReconstructGlyphTables(transformed, out byte[] glyf, out byte[] loca);

        await Assert.That(glyf.Length).IsEqualTo(0);
        await Assert.That(BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(4))).IsEqualTo(0U);
    }

    // One simple glyph. Each point uses triplet flag 0, a one-byte y delta, and the glyph has no instructions.
    private static byte[] TransformedGlyf(byte[] contourPointCounts)
    {
        int pointCount = 0;
        foreach (byte count in contourPointCounts)
        {
            pointCount += count;
        }

        byte[] contourStream = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(contourStream, (short)contourPointCounts.Length);
        byte[] flagStream = new byte[pointCount];
        byte[] glyphStream = new byte[pointCount + 1];
        for (int i = 0; i < pointCount; i++)
        {
            glyphStream[i] = 1;
        }

        byte[] boundsStream = new byte[4];
        byte[][] streams = [contourStream, contourPointCounts, flagStream, glyphStream, [], boundsStream, []];

        List<byte> data = [];
        byte[] header = new byte[8 + (4 * streams.Length)];
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), 1);
        for (int i = 0; i < streams.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(8 + (4 * i)), (uint)streams[i].Length);
        }

        data.AddRange(header);
        foreach (byte[] stream in streams)
        {
            data.AddRange(stream);
        }

        return [.. data];
    }
}
