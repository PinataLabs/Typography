// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;

namespace PinataLabs.Typography.Fuzz;

/// <summary>
/// Makes damaged copies of font files: stacked bit flips, boundary values in 8, 16 and 32 bits, small
/// arithmetic, chunks deleted, copied or inserted, cuts, and splices with other inputs.
/// The same seed gives the same sequence, so a failure in the test suite reproduces.
/// </summary>
internal sealed class Mutator
{
    private static readonly byte[] Bytes = [0, 1, 0x7F, 0x80, 0xFF, 0x20, 0x0C, 0x0E, 0x1C, 0x1D, 0x1E];

    private static readonly ushort[] Words = [0, 1, 2, 0x7F, 0x80, 0xFF, 0x100, 0x7FFF, 0x8000, 0xFFFE, 0xFFFF];

    private static readonly uint[] DoubleWords = [0, 1, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 0x10000, 0xFFFF];

    private readonly Random random;

    public Mutator(int seed) => this.random = new Random(seed);

    /// <summary>
    /// Makes one damaged copy.
    /// </summary>
    /// <param name="input">The input to damage.</param>
    /// <param name="donors">Other inputs to splice from. Can be empty.</param>
    /// <param name="maxLength">The longest result.</param>
    /// <returns>The damaged copy.</returns>
    public byte[] Mutate(ReadOnlySpan<byte> input, IReadOnlyList<byte[]> donors, int maxLength)
    {
        List<byte> data = [.. input];
        int operations = 1 << this.random.Next(0, 5);
        for (int i = 0; i < operations; i++)
        {
            this.MutateOnce(data, donors, maxLength);
        }

        if (data.Count > maxLength)
        {
            data.RemoveRange(maxLength, data.Count - maxLength);
        }

        return [.. data];
    }

    private void MutateOnce(List<byte> data, IReadOnlyList<byte[]> donors, int maxLength)
    {
        if (data.Count == 0)
        {
            data.Add((byte)this.random.Next(256));
            return;
        }

        int position = this.random.Next(data.Count);
        switch (this.random.Next(12))
        {
            case 0:
                data[position] ^= (byte)(1 << this.random.Next(8));
                break;
            case 1:
                data[position] = Bytes[this.random.Next(Bytes.Length)];
                break;
            case 2:
                data[position] = (byte)this.random.Next(256);
                break;
            case 3:
                data[position] = (byte)(data[position] + this.random.Next(-16, 17));
                break;
            case 4:
                this.WriteWord(data, position, Words[this.random.Next(Words.Length)]);
                break;
            case 5:
                this.WriteWord(data, position, (ushort)(ReadWord(data, position) + this.random.Next(-64, 65)));
                break;
            case 6:
                uint value = DoubleWords[this.random.Next(DoubleWords.Length)];
                this.WriteWord(data, position, (ushort)(value >> 16));
                this.WriteWord(data, position + 2, (ushort)value);
                break;
            case 7:
                data.RemoveRange(position, Math.Min(data.Count - position, this.ChunkLength()));
                break;
            case 8:
                int source = this.random.Next(data.Count);
                byte[] chunk = [.. data.GetRange(source, Math.Min(data.Count - source, this.ChunkLength()))];
                if (this.random.Next(2) == 0 && data.Count + chunk.Length <= maxLength)
                {
                    data.InsertRange(position, chunk);
                }
                else
                {
                    for (int i = 0; i < chunk.Length && position + i < data.Count; i++)
                    {
                        data[position + i] = chunk[i];
                    }
                }

                break;
            case 9:
                byte[] inserted = new byte[this.ChunkLength()];
                this.random.NextBytes(inserted);
                if (data.Count + inserted.Length <= maxLength)
                {
                    data.InsertRange(position, inserted);
                }

                break;
            case 10:
                data.RemoveRange(position, data.Count - position);
                break;
            default:
                if (donors.Count > 0)
                {
                    byte[] donor = donors[this.random.Next(donors.Count)];
                    int from = this.random.Next(donor.Length);
                    int length = Math.Min(donor.Length - from, this.ChunkLength());
                    for (int i = 0; i < length && position + i < data.Count; i++)
                    {
                        data[position + i] = donor[from + i];
                    }
                }

                break;
        }
    }

    // Mostly short chunks, sometimes long ones.
    private int ChunkLength() => 1 + this.random.Next(1 << this.random.Next(1, 11));

    private static ushort ReadWord(List<byte> data, int position)
        => position + 1 < data.Count ? (ushort)((data[position] << 8) | data[position + 1]) : data[position];

    private void WriteWord(List<byte> data, int position, ushort value)
    {
        bool bigEndian = this.random.Next(4) != 0;
        byte first = bigEndian ? (byte)(value >> 8) : (byte)value;
        byte second = bigEndian ? (byte)value : (byte)(value >> 8);
        if (position < data.Count)
        {
            data[position] = first;
        }

        if (position + 1 < data.Count)
        {
            data[position + 1] = second;
        }
    }
}
