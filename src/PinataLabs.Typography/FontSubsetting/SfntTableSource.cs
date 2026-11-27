// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.FontSubsetting;

/// <summary>
/// A table of the file that <see cref="SfntWriter"/> writes: bytes that it copies, or a table that is built as the file is written.
/// </summary>
internal readonly struct SfntTableSource
{
    private readonly ReadOnlyMemory<byte> bytes;
    private readonly ISubsetTable? table;

    public SfntTableSource(uint tag, ReadOnlyMemory<byte> bytes)
    {
        this.Tag = tag;
        this.bytes = bytes;
        this.table = null;
    }

    public SfntTableSource(uint tag, ISubsetTable table)
    {
        this.Tag = tag;
        this.bytes = default;
        this.table = table;
    }

    public uint Tag { get; }

    public int Length => this.table?.Length ?? this.bytes.Length;

    public void Write(Span<byte> destination)
    {
        if (this.table is not null)
        {
            this.table.Write(destination);
        }
        else
        {
            this.bytes.Span.CopyTo(destination);
        }
    }
}
