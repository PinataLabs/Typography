// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables.TrueType.Glyphs;

namespace PinataLabs.Typography.Tables.Woff;

// Source code is based on https://github.com/LayoutFarm/Typography
// see https://github.com/LayoutFarm/Typography/blob/master/Typography.OpenFont/WebFont/Woff2Reader.cs
// TODO: There's still some cleanup required here to bring the code up to a maintainable standard.
internal static class Woff2Utils
{
    // We don't reuse the const tag headers from our table types for clarity.
    private static readonly string[] KnownTableTags =
    [
        "cmap", "head", "hhea", "hmtx", "maxp", "name", "OS/2", "post", "cvt ",
        "fpgm", "glyf", "loca", "prep", "CFF ", "VORG", "EBDT", "EBLC", "gasp",
        "hdmx", "kern", "LTSH", "PCLT", "VDMX", "vhea", "vmtx", "BASE", "GDEF",
        "GPOS", "GSUB", "EBSC", "JSTF", "MATH", "CBDT", "CBLC", "COLR", "CPAL",
        "SVG ", "sbix", "acnt", "avar", "bdat", "bloc", "bsln", "cvar", "fdsc",
        "feat", "fmtx", "fvar", "gvar", "hsty", "just", "lcar", "mort", "morx",
        "opbd", "prop", "trak", "Zapf", "Silf", "Glat", "Gloc", "Feat", "Sill"
    ];

    private const byte OneMoreByteCode1 = 255;
    private const byte OneMoreByteCode2 = 254;
    private const byte WordCode = 253;
    private const byte LowestUCode = 253;

    public static ReadOnlyDictionary<string, TableHeader> ReadWoff2Headers(BigEndianBinaryReader reader, int tableCount)
    {
        uint expectedTableStartAt = 0;
        var headers = new Dictionary<string, TableHeader>(tableCount);
        for (int i = 0; i < tableCount; i++)
        {
            Woff2TableHeader woffTableHeader = Read(reader, expectedTableStartAt, out uint nextExpectedTableStartAt);
            expectedTableStartAt = nextExpectedTableStartAt;
            headers.Add(woffTableHeader.Tag, woffTableHeader);
        }

        return new ReadOnlyDictionary<string, TableHeader>(headers);
    }

    public static Woff2TableHeader Read(BigEndianBinaryReader reader, uint expectedTableStartAt, out uint nextExpectedTableStartAt)
    {
        // Leave the first byte open to store flagByte
        const uint woff2FlagsTransform = 1 << 8;
        byte flagsByte = reader.ReadByte();
        int knownTable = flagsByte & 0x3F;
        string tableName = knownTable == 0x3F ? reader.ReadTag() : KnownTableTags[knownTable];

        uint flags = 0;
        byte xformVersion = (byte)((flagsByte >> 6) & 0x03);

        // 0 means xform for glyph/loca, non-0 for others
        if (tableName is "glyf" or "loca")
        {
            if (xformVersion == 0)
            {
                flags |= woff2FlagsTransform;
            }
        }
        else if (xformVersion != 0)
        {
            flags |= woff2FlagsTransform;
        }

        flags |= xformVersion;

        if (!ReadUIntBase128(reader, out uint tableOrigLength))
        {
            throw new FontException("Error parsing woff2 table header");
        }

        uint tableTransformLength = tableOrigLength;
        if ((flags & woff2FlagsTransform) != 0)
        {
            if (!ReadUIntBase128(reader, out tableTransformLength))
            {
                throw new FontException("Error parsing woff2 table header");
            }

            if (tableName == "loca" && tableTransformLength > 0)
            {
                throw new FontException("Error parsing woff2 table header");
            }
        }

        nextExpectedTableStartAt = expectedTableStartAt + tableTransformLength;
        if (nextExpectedTableStartAt < expectedTableStartAt)
        {
            throw new FontException("Error parsing woff2 table header");
        }

        return new Woff2TableHeader(tableName, 0, expectedTableStartAt, tableTransformLength);
    }

    /// <summary>
    /// Builds the 'glyf' and 'loca' tables of a font from the transformed 'glyf' table of a WOFF2 file.
    /// </summary>
    /// <param name="data">The transformed 'glyf' table.</param>
    /// <param name="glyf">The 'glyf' table.</param>
    /// <param name="loca">The 'loca' table, with 32-bit offsets.</param>
    /// <exception cref="InvalidFontFileException">The transformed table is malformed.</exception>
    public static void ReconstructGlyphTables(ReadOnlySpan<byte> data, out byte[] glyf, out byte[] loca)
    {
        // The table is a header and seven streams. A glyph takes its values from each stream in turn.
        // Fixed     | version               | 0
        // UInt16    | numGlyphs             | Number of glyphs
        // UInt16    | indexFormat           | Offset format of the original 'loca' table
        // UInt32    | nContourStreamSize    | Int16 number of contours of each glyph
        // UInt32    | nPointsStreamSize     | 255UInt16 number of points of each contour
        // UInt32    | flagStreamSize        | UInt8 flag of each point
        // UInt32    | glyphStreamSize       | Coordinates of each point, and the instruction length of each glyph
        // UInt32    | compositeStreamSize   | Component records of each composite glyph
        // UInt32    | bboxStreamSize        | A bit for each glyph, then the Int16 bounding boxes of the glyphs whose bit is set
        // UInt32    | instructionStreamSize | UInt8 instructions of each glyph
        FontDataReader header = new(data);
        header.ReadUInt32();
        int glyphCount = header.ReadUInt16();
        header.ReadUInt16();

        Span<uint> sizes = stackalloc uint[7];
        for (int i = 0; i < sizes.Length; i++)
        {
            sizes[i] = header.ReadUInt32();
        }

        int position = header.Position;
        FontDataReader contourCounts = new(TakeStream(data, ref position, sizes[0]));
        FontDataReader pointCounts = new(TakeStream(data, ref position, sizes[1]));
        FontDataReader flags = new(TakeStream(data, ref position, sizes[2]));
        ReadOnlySpan<byte> glyphStream = TakeStream(data, ref position, sizes[3]);
        ReadOnlySpan<byte> compositeStream = TakeStream(data, ref position, sizes[4]);
        ReadOnlySpan<byte> boundsStream = TakeStream(data, ref position, sizes[5]);
        ReadOnlySpan<byte> instructionStream = TakeStream(data, ref position, sizes[6]);
        FontDataReader glyphReader = new(glyphStream);
        FontDataReader composites = new(compositeStream);
        FontDataReader instructions = new(instructionStream);

        // The bit array is padded to a multiple of four bytes.
        int bitmapLength = ((glyphCount + 31) >> 5) << 2;
        if (bitmapLength > boundsStream.Length)
        {
            throw Malformed();
        }

        FontDataReader explicitBounds = new(boundsStream, bitmapLength);

        ArrayBufferWriter<byte> output = new(data.Length);
        loca = new byte[(glyphCount + 1) * 4];
        ushort[] endPoints = [];
        SimplePoint[] points = [];
        for (int glyphId = 0; glyphId < glyphCount; glyphId++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(glyphId * 4), (uint)output.WrittenCount);
            int contourCount = contourCounts.ReadInt16();
            bool hasExplicitBounds = (boundsStream[glyphId >> 3] & (0x80 >> (glyphId & 7))) != 0;

            if (contourCount == 0)
            {
                // An empty glyph has no bounding box. Skip one that the file gives it.
                if (hasExplicitBounds && !explicitBounds.TrySkip(8))
                {
                    throw Malformed();
                }

                continue;
            }

            WriteInt16(output, (short)contourCount);
            if (contourCount < 0)
            {
                if (!hasExplicitBounds)
                {
                    throw new InvalidFontFileException("A composite glyph of the WOFF2 font has no bounding box.");
                }

                output.Write(Take(ref explicitBounds, boundsStream, 8));
                if (CopyComponents(ref composites, compositeStream, output))
                {
                    int length = Read255UInt16(ref glyphReader);
                    WriteUInt16(output, (ushort)length);
                    output.Write(Take(ref instructions, instructionStream, length));
                }

                continue;
            }

            if (endPoints.Length < contourCount)
            {
                endPoints = new ushort[contourCount];
            }

            int pointCount = 0;
            for (int i = 0; i < contourCount; i++)
            {
                pointCount += Read255UInt16(ref pointCounts);
                if (pointCount is 0 or > ushort.MaxValue)
                {
                    throw Malformed();
                }

                endPoints[i] = (ushort)(pointCount - 1);
            }

            if (points.Length < pointCount)
            {
                points = new SimplePoint[pointCount];
            }

            ReadPoints(ref flags, ref glyphReader, glyphStream, points.AsSpan(0, pointCount), out Bounds bounds);
            if (hasExplicitBounds)
            {
                output.Write(Take(ref explicitBounds, boundsStream, 8));
            }
            else
            {
                WriteInt16(output, (short)bounds.Min.X);
                WriteInt16(output, (short)bounds.Min.Y);
                WriteInt16(output, (short)bounds.Max.X);
                WriteInt16(output, (short)bounds.Max.Y);
            }

            for (int i = 0; i < contourCount; i++)
            {
                WriteUInt16(output, endPoints[i]);
            }

            int instructionLength = Read255UInt16(ref glyphReader);
            WriteUInt16(output, (ushort)instructionLength);
            output.Write(Take(ref instructions, instructionStream, instructionLength));
            WritePoints(output, points.AsSpan(0, pointCount));
        }

        BinaryPrimitives.WriteUInt32BigEndian(loca.AsSpan(glyphCount * 4), (uint)output.WrittenCount);
        glyf = output.WrittenSpan.ToArray();
    }

    private static ReadOnlySpan<byte> TakeStream(ReadOnlySpan<byte> data, ref int position, uint length)
    {
        if (length > (uint)(data.Length - position))
        {
            throw Malformed();
        }

        ReadOnlySpan<byte> stream = data.Slice(position, (int)length);
        position += (int)length;
        return stream;
    }

    private static ReadOnlySpan<byte> Take(ref FontDataReader reader, ReadOnlySpan<byte> stream, int count)
    {
        int start = reader.Position;
        if (!reader.TrySkip(count))
        {
            throw Malformed();
        }

        return stream.Slice(start, count);
    }

    private static InvalidFontFileException Malformed()
        => new("The transformed 'glyf' table of the WOFF2 font is malformed.");

    private static void WriteInt16(ArrayBufferWriter<byte> output, short value)
        => WriteUInt16(output, (ushort)value);

    private static void WriteUInt16(ArrayBufferWriter<byte> output, ushort value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(output.GetSpan(2), value);
        output.Advance(2);
    }

    /// <summary>
    /// Copies the component records of one composite glyph. The records have the form of the 'glyf' table.
    /// </summary>
    /// <returns><see langword="true"/> if the glyph has instructions.</returns>
    private static bool CopyComponents(ref FontDataReader composites, ReadOnlySpan<byte> compositeStream, ArrayBufferWriter<byte> output)
    {
        bool hasInstructions = false;
        CompositeGlyphFlags flags;
        do
        {
            int start = composites.Position;
            flags = (CompositeGlyphFlags)composites.ReadUInt16();
            hasInstructions |= (flags & CompositeGlyphFlags.WeHaveInstructions) != 0;

            // The glyph index, then the two arguments, then the scale or the matrix.
            int size = 2 + ((flags & CompositeGlyphFlags.Args1And2AreWords) != 0 ? 4 : 2);
            if ((flags & CompositeGlyphFlags.WeHaveAScale) != 0)
            {
                size += 2;
            }
            else if ((flags & CompositeGlyphFlags.WeHaveXAndYScale) != 0)
            {
                size += 4;
            }
            else if ((flags & CompositeGlyphFlags.WeHaveATwoByTwo) != 0)
            {
                size += 8;
            }

            if (!composites.TrySkip(size))
            {
                throw Malformed();
            }

            output.Write(compositeStream.Slice(start, composites.Position - start));
        }
        while ((flags & CompositeGlyphFlags.MoreComponents) != 0);

        return hasInstructions;
    }

    /// <summary>
    /// Reads the points of one simple glyph. The flag of a point selects how its coordinates are packed.
    /// </summary>
    private static void ReadPoints(ref FontDataReader flags, ref FontDataReader glyphReader, ReadOnlySpan<byte> glyphStream, Span<SimplePoint> points, out Bounds bounds)
    {
        int x = 0;
        int y = 0;
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int i = 0; i < points.Length; i++)
        {
            byte flag = flags.ReadByte();
            TripleEncodingRecord enc = TripleEncodingTable.EncTable[flag & 0x7F];

            // The byte count includes the flag.
            ReadOnlySpan<byte> packed = Take(ref glyphReader, glyphStream, enc.ByteCount - 1);
            int dx;
            int dy;
            switch (enc.XBits)
            {
                case 0:
                    dx = 0;
                    dy = enc.Ty(packed[0]);
                    break;
                case 4:
                    dx = enc.Tx(packed[0] >> 4);
                    dy = enc.Ty(packed[0] & 0xF);
                    break;
                case 8:
                    dx = enc.Tx(packed[0]);
                    dy = enc.YBits == 8 ? enc.Ty(packed[1]) : 0;
                    break;
                case 12:
                    dx = enc.Tx((packed[0] << 4) | (packed[1] >> 4));
                    dy = enc.Ty(((packed[1] & 0xF) << 8) | packed[2]);
                    break;
                default:
                    dx = enc.Tx((packed[0] << 8) | packed[1]);
                    dy = enc.Ty((packed[2] << 8) | packed[3]);
                    break;
            }

            // The 'glyf' table holds a coordinate and a difference of coordinates in 16 bits.
            x += dx;
            y += dy;
            if (dx is < short.MinValue or > short.MaxValue || dy is < short.MinValue or > short.MaxValue
                || x is < short.MinValue or > short.MaxValue || y is < short.MinValue or > short.MaxValue)
            {
                throw Malformed();
            }

            // The high bit of the flag is clear for a point on the curve.
            points[i] = new SimplePoint((short)dx, (short)dy, flag >> 7 == 0);
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        bounds = new Bounds(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Writes the flags, the x coordinates and the y coordinates of a simple glyph in the form of the 'glyf' table.
    /// </summary>
    private static void WritePoints(ArrayBufferWriter<byte> output, ReadOnlySpan<SimplePoint> points)
    {
        const int onCurve = 0x01;
        const int xShort = 0x02;
        const int yShort = 0x04;
        const int xSameOrPositive = 0x10;
        const int ySameOrPositive = 0x20;

        Span<byte> flags = output.GetSpan(points.Length).Slice(0, points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            SimplePoint point = points[i];
            flags[i] = (byte)((point.OnCurve ? onCurve : 0)
                | CoordinateFlags(point.DeltaX, xShort, xSameOrPositive)
                | CoordinateFlags(point.DeltaY, yShort, ySameOrPositive));
        }

        output.Advance(points.Length);
        foreach (SimplePoint point in points)
        {
            WriteCoordinate(output, point.DeltaX);
        }

        foreach (SimplePoint point in points)
        {
            WriteCoordinate(output, point.DeltaY);
        }
    }

    // A difference is absent when it is zero, one byte with its sign in the flag when it is short, else two bytes.
    private static int CoordinateFlags(short delta, int shortFlag, int sameOrPositiveFlag)
        => delta switch
        {
            0 => sameOrPositiveFlag,
            > 0 and < 256 => shortFlag | sameOrPositiveFlag,
            < 0 and > -256 => shortFlag,
            _ => 0,
        };

    private static void WriteCoordinate(ArrayBufferWriter<byte> output, short delta)
    {
        if (delta == 0)
        {
            return;
        }

        if (delta is > -256 and < 256)
        {
            output.GetSpan(1)[0] = (byte)Math.Abs(delta);
            output.Advance(1);
            return;
        }

        WriteInt16(output, delta);
    }

    /// <summary>
    /// Reads the UIntBase128 Data Type.
    /// </summary>
    /// <param name="reader">The binary reader using big endian encoding.</param>
    /// <param name="result">The result as uint.</param>
    /// <returns>true, if succeeded.</returns>
    private static bool ReadUIntBase128(BigEndianBinaryReader reader, out uint result)
    {
        // UIntBase128 is a different variable length encoding of unsigned integers,
        // suitable for values up to 2^(32) - 1.
        // A UIntBase128 encoded number is a sequence of bytes for which the most significant bit
        // is set for all but the last byte,
        // and clear for the last byte.
        //
        // The number itself is base 128 encoded in the lower 7 bits of each byte.
        // Thus, a decoding procedure for a UIntBase128 is:
        // start with value = 0.
        // Consume a byte, setting value = old value times 128 + (byte bitwise - and 127).
        // Repeat last step until the most significant bit of byte is false.
        //
        // UIntBase128 encoding format allows a possibility of sub-optimal encoding,
        // where e.g.the same numerical value can be represented with variable number of bytes(utilizing leading 'zeros').
        // For example, the value 63 could be encoded as either one byte 0x3F or two(or more) bytes: [0x80, 0x3f].
        // An encoder must not allow this to happen and must produce shortest possible encoding.
        // A decoder MUST reject the font file if it encounters a UintBase128 - encoded value with leading zeros(a value that starts with the byte 0x80),
        // if UintBase128 - encoded sequence is longer than 5 bytes,
        // or if a UintBase128 - encoded value exceeds 232 - 1.
        uint accum = 0;
        result = 0;
        for (int i = 0; i < 5; i++)
        {
            byte data_byte = reader.ReadByte();

            // No leading 0's
            if (i == 0 && data_byte == 0x80)
            {
                return false;
            }

            // If any of top 7 bits are set then << 7 would overflow.
            if ((accum & 0xFE000000) != 0)
            {
                return false;
            }

            accum = (accum << 7) | (uint)(data_byte & 0x7F);

            // Spin until most significant bit of data byte is false.
            if ((data_byte & 0x80) == 0)
            {
                result = accum;
                return true;
            }
        }

        // UIntBase128 sequence exceeds 5 bytes.
        return false;
    }

    /// <summary>
    /// Reads a 255UInt16: an unsigned 16-bit integer in one to three bytes, where a first byte
    /// from 253 says how to read the bytes that follow.
    /// </summary>
    private static int Read255UInt16(ref FontDataReader reader)
    {
        byte code = reader.ReadByte();
        return code switch
        {
            WordCode => reader.ReadUInt16(),
            OneMoreByteCode1 => reader.ReadByte() + LowestUCode,
            OneMoreByteCode2 => reader.ReadByte() + (LowestUCode * 2),
            _ => code,
        };
    }

    private readonly struct SimplePoint
    {
        public SimplePoint(short deltaX, short deltaY, bool onCurve)
        {
            this.DeltaX = deltaX;
            this.DeltaY = deltaY;
            this.OnCurve = onCurve;
        }

        public short DeltaX { get; }

        public short DeltaY { get; }

        public bool OnCurve { get; }
    }
}
