// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace PinataLabs.Typography.Tables.Cff;

/// <summary>
/// Parses a Compact Font Format version 2 (CFF2) table as described in the OpenType specification:
/// https://learn.microsoft.com/typography/opentype/spec/cff2
/// Only what is required to render the default instance is read. The Item Variation Store is parsed far enough
/// to know the number of variation regions per ItemVariationData, so <c>blend</c> operators can be skipped
/// correctly (the default values are kept and the deltas discarded).
/// </summary>
internal static class Cff2Parser
{
    // Top DICT operators.
    private const int OpVstore = 24;
    private const int OpCharStrings = 17;
    private const int OpFdArray = (12 << 8) | 36;
    private const int OpFdSelect = (12 << 8) | 37;

    // Font DICT operator.
    private const int OpPrivate = 18;

    // Private DICT operators.
    private const int OpSubrs = 19;
    private const int OpVsIndex = 22;

    public static CffGlyphData[] Load(byte[] data)
    {
        Cursor header = new(data, 0);
        byte major = header.ReadByte();
        header.ReadByte(); // minor
        int headerSize = header.ReadByte();
        int topDictLength = header.ReadUInt16();

        if (major != 2)
        {
            throw new NotSupportedException($"CFF2 major version {major} is not supported.");
        }

        List<DictEntry> topDict = ReadDict(data, headerSize, topDictLength);
        byte[][] globalSubrs = ReadIndex(data, headerSize + topDictLength, out _);

        int charStringsOffset = GetOffset(topDict, OpCharStrings);
        int fdArrayOffset = GetOffset(topDict, OpFdArray);
        if (charStringsOffset <= 0 || fdArrayOffset <= 0)
        {
            throw new InvalidFontFileException("CFF2 table is missing a required CharStrings or FDArray entry.");
        }

        int fdSelectOffset = GetOffset(topDict, OpFdSelect);
        int vstoreOffset = GetOffset(topDict, OpVstore);

        byte[][] charStrings = ReadIndex(data, charStringsOffset, out _);
        int[] regionCounts = vstoreOffset > 0 ? ReadRegionCounts(data, vstoreOffset) : [];
        FontDictData[] fonts = ReadFontDicts(data, fdArrayOffset);
        ushort[] fdSelect = fdSelectOffset > 0
            ? ReadFdSelect(data, fdSelectOffset, charStrings.Length)
            : new ushort[charStrings.Length];

        CffGlyphData[] glyphs = new CffGlyphData[charStrings.Length];
        for (int i = 0; i < glyphs.Length; i++)
        {
            int fd = fdSelect[i];
            if (fd >= fonts.Length)
            {
                throw new InvalidFontFileException($"CFF2 FDSelect refers to missing Font DICT {fd}.");
            }

            FontDictData font = fonts[fd];
            glyphs[i] = new CffGlyphData((ushort)i, globalSubrs, font.LocalSubrs, 0, charStrings[i], regionCounts, font.VsIndex);
        }

        return glyphs;
    }

    private static int[] ReadRegionCounts(byte[] data, int vstoreOffset)
    {
        // uint16 length, then the ItemVariationStore:
        // uint16 format, Offset32 variationRegionListOffset, uint16 itemVariationDataCount, Offset32 itemVariationDataOffsets[].
        Cursor c = new(data, vstoreOffset);
        c.ReadUInt16(); // length
        int storeStart = c.Position;
        c.ReadUInt16(); // format
        c.ReadUInt32(); // variationRegionListOffset
        int count = c.ReadUInt16();
        int[] counts = new int[count];
        for (int i = 0; i < count; i++)
        {
            int dataOffset = checked((int)c.ReadUInt32());
            if (dataOffset == 0)
            {
                continue;
            }

            // ItemVariationData: uint16 itemCount, uint16 wordDeltaCount, uint16 regionIndexCount, ...
            Cursor ivd = new(data, storeStart + dataOffset);
            ivd.ReadUInt16();
            ivd.ReadUInt16();
            counts[i] = ivd.ReadUInt16();
        }

        return counts;
    }

    private static FontDictData[] ReadFontDicts(byte[] data, int fdArrayOffset)
    {
        byte[][] dicts = ReadIndex(data, fdArrayOffset, out _);
        FontDictData[] fonts = new FontDictData[dicts.Length];
        for (int i = 0; i < dicts.Length; i++)
        {
            List<DictEntry> fontDict = ReadDict(dicts[i], 0, dicts[i].Length);
            byte[][] localSubrs = [];
            int vsIndex = 0;

            foreach (DictEntry entry in fontDict)
            {
                if (entry.Operator == OpPrivate && entry.Operands.Count == 2)
                {
                    int size = (int)entry.Operands[0];
                    int offset = (int)entry.Operands[1];
                    if (size < 0 || offset < 0 || (long)offset + size > data.Length)
                    {
                        throw new InvalidFontFileException("CFF2 Private DICT is out of range.");
                    }

                    foreach (DictEntry priv in ReadDict(data, offset, size))
                    {
                        if (priv.Operator == OpSubrs && priv.Operands.Count == 1)
                        {
                            // Relative to the start of the Private DICT.
                            localSubrs = ReadIndex(data, offset + (int)priv.Operands[0], out _);
                        }
                        else if (priv.Operator == OpVsIndex && priv.Operands.Count == 1)
                        {
                            vsIndex = (int)priv.Operands[0];
                        }
                    }
                }
            }

            fonts[i] = new FontDictData(localSubrs, vsIndex);
        }

        return fonts;
    }

    private static ushort[] ReadFdSelect(byte[] data, int offset, int glyphCount)
    {
        Cursor c = new(data, offset);
        ushort[] result = new ushort[glyphCount];
        byte format = c.ReadByte();
        switch (format)
        {
            case 0:
                for (int i = 0; i < glyphCount; i++)
                {
                    result[i] = c.ReadByte();
                }

                break;

            case 3:
            {
                int nRanges = c.ReadUInt16();
                int first = c.ReadUInt16();
                for (int r = 0; r < nRanges; r++)
                {
                    ushort fd = c.ReadByte();
                    int next = c.ReadUInt16(); // next range's first, or the sentinel
                    Fill(result, first, next, fd);
                    first = next;
                }

                break;
            }

            case 4:
            {
                long nRanges = c.ReadUInt32();
                long first = c.ReadUInt32();
                for (long r = 0; r < nRanges; r++)
                {
                    ushort fd = c.ReadUInt16();
                    long next = c.ReadUInt32();
                    Fill(result, first, next, fd);
                    first = next;
                }

                break;
            }

            default:
                throw new NotSupportedException($"CFF2 FDSelect format {format} is not supported.");
        }

        return result;
    }

    private static void Fill(ushort[] target, long start, long end, ushort value)
    {
        if (end > target.Length)
        {
            end = target.Length;
        }

        for (long i = start; i < end; i++)
        {
            target[i] = value;
        }
    }

    private static int GetOffset(List<DictEntry> dict, int op)
    {
        foreach (DictEntry entry in dict)
        {
            if (entry.Operator == op && entry.Operands.Count > 0)
            {
                return (int)entry.Operands[0];
            }
        }

        return 0;
    }

    /// <summary>
    /// Reads a CFF2 INDEX (32-bit count) at the given offset.
    /// </summary>
    private static byte[][] ReadIndex(byte[] data, int offset, out int endOffset)
    {
        Cursor c = new(data, offset);
        uint count = c.ReadUInt32();
        if (count == 0)
        {
            endOffset = c.Position;
            return [];
        }

        int offSize = c.ReadByte();
        if (offSize is < 1 or > 4)
        {
            throw new InvalidFontFileException("Invalid CFF2 INDEX offset size.");
        }

        if (count > data.Length)
        {
            throw new InvalidFontFileException("Invalid CFF2 INDEX count.");
        }

        int n = (int)count;
        long[] offsets = new long[n + 1];
        for (int i = 0; i <= n; i++)
        {
            long value = 0;
            for (int b = 0; b < offSize; b++)
            {
                value = (value << 8) | c.ReadByte();
            }

            offsets[i] = value;
        }

        // Offsets are relative to the byte preceding the object data.
        long dataStart = c.Position - 1;
        byte[][] objects = new byte[n][];
        for (int i = 0; i < n; i++)
        {
            long start = dataStart + offsets[i];
            long end = dataStart + offsets[i + 1];
            if (offsets[i] < 1 || end < start || end > data.Length)
            {
                throw new InvalidFontFileException("Invalid CFF2 INDEX offsets.");
            }

            objects[i] = data.AsSpan((int)start, (int)(end - start)).ToArray();
        }

        endOffset = (int)(dataStart + offsets[n]);
        return objects;
    }

    private static List<DictEntry> ReadDict(byte[] data, int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > data.Length)
        {
            throw new InvalidFontFileException("CFF2 DICT is out of range.");
        }

        List<DictEntry> entries = [];
        List<double> operands = [];
        Cursor c = new(data, offset);
        int end = offset + length;
        while (c.Position < end)
        {
            byte b0 = c.ReadByte();
            if (b0 <= 24)
            {
                // CFF2 adds vsindex (22), blend (23) and vstore (24) to the CFF1 operator range 0-21.
                int op = b0 == 12 ? (12 << 8) | c.ReadByte() : b0;
                entries.Add(new DictEntry(op, operands));
                operands = [];
            }
            else if (b0 == 28)
            {
                operands.Add((short)c.ReadUInt16());
            }
            else if (b0 == 29)
            {
                operands.Add((int)c.ReadUInt32());
            }
            else if (b0 == 30)
            {
                // Real number: nibbles until 0xF. The value is not needed for default instance rendering.
                bool done = false;
                while (!done)
                {
                    byte b = c.ReadByte();
                    done = (b >> 4) == 0xF || (b & 0xF) == 0xF;
                }

                operands.Add(0);
            }
            else if (b0 is >= 32 and <= 246)
            {
                operands.Add(b0 - 139);
            }
            else if (b0 is >= 247 and <= 250)
            {
                operands.Add(((b0 - 247) * 256) + c.ReadByte() + 108);
            }
            else if (b0 is >= 251 and <= 254)
            {
                operands.Add((-(b0 - 251) * 256) - c.ReadByte() - 108);
            }
            else
            {
                throw new InvalidFontFileException($"Invalid CFF2 DICT byte {b0}.");
            }
        }

        return entries;
    }

    private readonly record struct DictEntry(int Operator, List<double> Operands);

    private readonly record struct FontDictData(byte[][] LocalSubrs, int VsIndex);

    private struct Cursor
    {
        private readonly byte[] data;

        public Cursor(byte[] data, int position)
        {
            if ((uint)position > (uint)data.Length)
            {
                throw new InvalidFontFileException("CFF2 offset is out of range.");
            }

            this.data = data;
            this.Position = position;
        }

        public int Position { get; private set; }

        public byte ReadByte()
        {
            if ((uint)this.Position >= (uint)this.data.Length)
            {
                throw new InvalidFontFileException("Unexpected end of CFF2 table.");
            }

            return this.data[this.Position++];
        }

        public ushort ReadUInt16() => (ushort)((this.ReadByte() << 8) | this.ReadByte());

        public uint ReadUInt32() => ((uint)this.ReadUInt16() << 16) | this.ReadUInt16();
    }
}
