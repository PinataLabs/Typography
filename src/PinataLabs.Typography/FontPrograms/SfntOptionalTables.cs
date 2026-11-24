// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;

namespace PinataLabs.Typography.FontPrograms;

/// <summary>
/// The optional tables of an sfnt file that a font program can use, each checked for the structure that reading it needs.
/// A table that is missing, or that fails the check, is treated as absent, so it can never fail a load.
/// Each table that is not usable is named in <see cref="Missing"/> or <see cref="Malformed"/>.
/// </summary>
internal readonly struct SfntOptionalTables
{
    private const uint Name = 0x6E616D65;
    private const uint Os2 = 0x4F532F32;

    private const int NameHeaderSize = 6;
    private const int NameRecordSize = 12;
    private const int Os2MinimumSize = 68;
    private const int PostHeaderSize = 32;
    private const int CmapHeaderSize = 4;
    private const int CmapRecordSize = 8;
    private const int HheaSize = 36;
    private const int MetricSize = 4;

    private SfntOptionalTables(
        ReadOnlyMemory<byte> post,
        ReadOnlyMemory<byte> cmap,
        SfntAdvances advances,
        OptionalFontTables missing,
        OptionalFontTables malformed)
    {
        this.Post = post;
        this.Cmap = cmap;
        this.Advances = advances;
        this.Missing = missing;
        this.Malformed = malformed;
    }

    /// <summary>
    /// Gets the 'post' table, or an empty table when it is missing or malformed.
    /// </summary>
    public ReadOnlyMemory<byte> Post { get; }

    /// <summary>
    /// Gets the 'cmap' table, or an empty table when it is missing or malformed.
    /// </summary>
    public ReadOnlyMemory<byte> Cmap { get; }

    /// <summary>
    /// Gets the advance widths, empty when 'hhea' or 'hmtx' is missing or malformed.
    /// </summary>
    public SfntAdvances Advances { get; }

    /// <summary>
    /// Gets the tables that the font does not have.
    /// </summary>
    public OptionalFontTables Missing { get; }

    /// <summary>
    /// Gets the tables that the font has but that are too damaged to use.
    /// </summary>
    public OptionalFontTables Malformed { get; }

    /// <summary>
    /// Reads the optional tables. This method does not throw.
    /// </summary>
    /// <param name="directory">The table directory of the font.</param>
    /// <returns>The <see cref="SfntOptionalTables"/>.</returns>
    public static SfntOptionalTables Read(SfntDirectory directory)
    {
        OptionalFontTables missing = OptionalFontTables.None;
        OptionalFontTables malformed = OptionalFontTables.None;

        Find(directory, Name, OptionalFontTables.Name, ref missing, ref malformed, out _);
        Find(directory, Os2, OptionalFontTables.Os2, ref missing, ref malformed, out _);
        Find(directory, SfntDirectory.Post, OptionalFontTables.Post, ref missing, ref malformed, out ReadOnlyMemory<byte> post);
        Find(directory, SfntDirectory.Cmap, OptionalFontTables.Cmap, ref missing, ref malformed, out ReadOnlyMemory<byte> cmap);
        bool hasHhea = Find(directory, SfntDirectory.Hhea, OptionalFontTables.Hhea, ref missing, ref malformed, out ReadOnlyMemory<byte> hhea);
        bool hasHmtx = Find(directory, SfntDirectory.Hmtx, OptionalFontTables.Hmtx, ref missing, ref malformed, out ReadOnlyMemory<byte> hmtx);

        SfntAdvances advances = hasHhea && hasHmtx ? SfntAdvances.Create(hhea.Span, hmtx) : default;
        return new SfntOptionalTables(post, cmap, advances, missing, malformed);
    }

    // Finds a table and checks it. A table that is missing or malformed is recorded and comes back empty.
    private static bool Find(
        SfntDirectory directory,
        uint tag,
        OptionalFontTables table,
        ref OptionalFontTables missing,
        ref OptionalFontTables malformed,
        out ReadOnlyMemory<byte> contents)
    {
        if (!directory.TryGetTable(tag, out contents))
        {
            missing |= table;
            contents = default;
            return false;
        }

        if (!IsValid(table, contents.Span))
        {
            malformed |= table;
            contents = default;
            return false;
        }

        return true;
    }

    private static bool IsValid(OptionalFontTables table, ReadOnlySpan<byte> contents)
        => table switch
        {
            // The count of name records is the second field, and each record is 12 bytes.
            OptionalFontTables.Name => contents.Length >= NameHeaderSize
                && NameHeaderSize + ((long)BinaryPrimitives.ReadUInt16BigEndian(contents.Slice(2)) * NameRecordSize) <= contents.Length,
            OptionalFontTables.Os2 => contents.Length >= Os2MinimumSize,

            // A version that the OpenType specification defines, and the room for the glyph count that version 2 adds.
            OptionalFontTables.Post => contents.Length >= PostHeaderSize
                && BinaryPrimitives.ReadUInt32BigEndian(contents) switch
                {
                    0x00010000 or 0x00025000 or 0x00030000 or 0x00040000 => true,
                    0x00020000 => contents.Length >= PostHeaderSize + 2,
                    _ => false,
                },
            OptionalFontTables.Cmap => contents.Length >= CmapHeaderSize
                && BinaryPrimitives.ReadUInt16BigEndian(contents) == 0
                && CmapHeaderSize + ((long)BinaryPrimitives.ReadUInt16BigEndian(contents.Slice(2)) * CmapRecordSize) <= contents.Length,

            // numberOfHMetrics is the last field of 'hhea'. A font with no metric has no advance to give.
            OptionalFontTables.Hhea => contents.Length >= HheaSize && BinaryPrimitives.ReadUInt16BigEndian(contents.Slice(34)) != 0,
            OptionalFontTables.Hmtx => contents.Length >= MetricSize,
            _ => false,
        };
}
