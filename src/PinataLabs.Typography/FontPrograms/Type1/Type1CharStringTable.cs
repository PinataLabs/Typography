// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.FontPrograms.Type1;

/// <summary>
/// Where each charstring of a set (the glyphs, or the subroutines) lies in the decrypted private part.
/// </summary>
internal sealed class Type1CharStringTable
{
    private const int Missing = -1;

    private readonly int[] offsets;
    private readonly int[] lengths;

    public Type1CharStringTable(int count)
    {
        this.offsets = new int[count];
        this.lengths = new int[count];
        this.lengths.AsSpan().Fill(Missing);
    }

    public int Count => this.offsets.Length;

    public long RetainedByteCount => 2L * sizeof(int) * this.offsets.Length;

    public void Set(int index, int offset, int length)
    {
        this.offsets[index] = offset;
        this.lengths[index] = length;
    }

    /// <summary>
    /// Decrypts every charstring in place and drops its <c>lenIV</c> leading bytes.
    /// </summary>
    /// <param name="data">The decrypted private part.</param>
    /// <param name="lenIV">The <c>/lenIV</c> value. A negative value means the charstrings are not encrypted.</param>
    public void Decrypt(byte[] data, int lenIV)
    {
        if (lenIV < 0)
        {
            return;
        }

        for (int i = 0; i < this.lengths.Length; i++)
        {
            int length = this.lengths[i];
            if (length < lenIV)
            {
                this.lengths[i] = Missing;
                continue;
            }

            Type1Eexec.DecryptCharString(data.AsSpan(this.offsets[i], length));
            this.offsets[i] += lenIV;
            this.lengths[i] = length - lenIV;
        }
    }

    public bool TryGet(byte[] data, int index, out ReadOnlySpan<byte> charString)
    {
        if ((uint)index >= (uint)this.lengths.Length || this.lengths[index] < 0)
        {
            charString = default;
            return false;
        }

        charString = data.AsSpan(this.offsets[index], this.lengths[index]);
        return true;
    }
}
