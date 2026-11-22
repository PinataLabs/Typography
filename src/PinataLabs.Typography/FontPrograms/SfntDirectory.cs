// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// The table directory of an sfnt font, read from memory. It finds a table by scanning the directory,
/// so it holds no array.
/// </summary>
internal readonly struct SfntDirectory
{
    public const uint TrueTypeVersion = 0x00010000;
    public const uint AppleTrueTypeVersion = 0x74727565; // 'true'
    public const uint OpenTypeCffVersion = 0x4F54544F; // 'OTTO'

    public const uint Cff = 0x43464620; // 'CFF '
    public const uint Cff2 = 0x43464632;
    public const uint Cmap = 0x636D6170;
    public const uint Glyf = 0x676C7966;
    public const uint Head = 0x68656164;
    public const uint Hhea = 0x68686561;
    public const uint Hmtx = 0x686D7478;
    public const uint Loca = 0x6C6F6361;
    public const uint Maxp = 0x6D617870;
    public const uint Post = 0x706F7374;

    private const int HeaderSize = 12;
    private const int RecordSize = 16;

    private readonly ReadOnlyMemory<byte> data;
    private readonly int tableCount;

    private SfntDirectory(ReadOnlyMemory<byte> data, uint version, int tableCount)
    {
        this.data = data;
        this.Version = version;
        this.tableCount = tableCount;
    }

    /// <summary>
    /// Gets the sfnt version tag.
    /// </summary>
    public uint Version { get; }

    /// <summary>
    /// Reads the directory header.
    /// </summary>
    /// <param name="data">The font file.</param>
    /// <returns>The <see cref="SfntDirectory"/>.</returns>
    /// <exception cref="InvalidFontFileException">The data is too short to hold the directory.</exception>
    public static SfntDirectory Read(ReadOnlyMemory<byte> data)
    {
        FontDataReader reader = new(data.Span);
        uint version = reader.ReadUInt32();
        int tableCount = reader.ReadUInt16();
        if (HeaderSize + ((long)tableCount * RecordSize) > data.Length)
        {
            throw new InvalidFontFileException("The table directory of the font is truncated.");
        }

        return new SfntDirectory(data, version, tableCount);
    }

    /// <summary>
    /// Finds a table.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    /// <param name="table">The bytes of the table. A table that runs past the end of the file is cut there.</param>
    /// <returns><see langword="false"/> if the font has no such table or the table starts outside the file.</returns>
    public bool TryGetTable(uint tag, out ReadOnlyMemory<byte> table)
    {
        FontDataReader reader = new(this.data.Span, HeaderSize);
        for (int i = 0; i < this.tableCount; i++)
        {
            uint recordTag = reader.ReadUInt32();
            reader.ReadUInt32(); // checksum
            uint offset = reader.ReadUInt32();
            uint length = reader.ReadUInt32();
            if (recordTag == tag)
            {
                if (offset > (uint)this.data.Length)
                {
                    break;
                }

                table = this.data.Slice((int)offset, (int)Math.Min(length, (uint)this.data.Length - offset));
                return true;
            }
        }

        table = default;
        return false;
    }

    /// <summary>
    /// Finds a table that the font must have.
    /// </summary>
    /// <param name="tag">The table tag.</param>
    /// <param name="name">The table tag as text, for the exception message.</param>
    /// <returns>The bytes of the table.</returns>
    /// <exception cref="InvalidFontFileException">The font has no such table.</exception>
    public ReadOnlyMemory<byte> GetTable(uint tag, string name)
        => this.TryGetTable(tag, out ReadOnlyMemory<byte> table)
            ? table
            : throw new InvalidFontFileException($"The font has no '{name}' table.");
}
