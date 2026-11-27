// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.Unicode;

/// <summary>
/// A list of glyph names and their text, as the generator writes it: the names sorted by ordinal and joined in one
/// string, and the text of each name joined in another, each with the offsets where its entries start.
/// </summary>
internal sealed class GlyphNameTable
{
    private readonly string names;
    private readonly ushort[] nameOffsets;
    private readonly string values;
    private readonly ushort[] valueOffsets;

    public GlyphNameTable(string names, ushort[] nameOffsets, string values, ushort[] valueOffsets)
    {
        this.names = names;
        this.nameOffsets = nameOffsets;
        this.values = values;
        this.valueOffsets = valueOffsets;
    }

    public int Count => this.nameOffsets.Length - 1;

    public string GetName(int index) => this.names.Substring(this.nameOffsets[index], this.nameOffsets[index + 1] - this.nameOffsets[index]);

    public string GetValue(int index) => this.values.Substring(this.valueOffsets[index], this.valueOffsets[index + 1] - this.valueOffsets[index]);

    public bool TryGet(ReadOnlySpan<char> glyphName, out string text)
    {
        int low = 0;
        int high = this.Count - 1;
        while (low <= high)
        {
            int middle = (low + high) >> 1;
            ReadOnlySpan<char> candidate = this.names.AsSpan(this.nameOffsets[middle], this.nameOffsets[middle + 1] - this.nameOffsets[middle]);
            int order = candidate.SequenceCompareTo(glyphName);
            if (order == 0)
            {
                text = this.GetValue(middle);
                return true;
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        text = string.Empty;
        return false;
    }
}
