// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.FontPrograms.Type1;
using PinataLabs.Typography.FontSubsetting;

namespace PinataLabs.Typography;

/// <summary>
/// Cuts a font program down to the glyphs that a document uses, and leaves every glyph at the glyph ID that it had.
/// </summary>
/// <remarks>
/// <para>
/// A glyph that is kept keeps its ID and its outline. A glyph that is dropped stays in the font and has no outline:
/// a CFF glyph is a lone <c>endchar</c>, and a TrueType glyph has no data. The glyph count, the glyph IDs, and so
/// the <c>/CIDToGIDMap /Identity</c> of a PDF font that embeds the subset, stay as they were.
/// </para>
/// <para>
/// The subset keeps more than the glyphs that the caller names. It adds the glyphs that they draw:
/// the base and the accent of a CFF glyph that ends with <c>seac</c>, and the components of a composite TrueType glyph,
/// as far down as the components go. A CFF glyph also needs its subroutines, so a subroutine that a kept glyph calls, directly or
/// from another subroutine, keeps its body, global or local, in the Font DICT of the glyph. A subroutine that no kept glyph calls
/// keeps its place in its INDEX and has a lone <c>return</c> for a body, so the biases and the numbers of the
/// calls do not change.
/// </para>
/// <para>
/// A CFF subset keeps the charset, the encoding, the FDSelect and the strings as they were, so glyph names and CIDs stay the same.
/// It lays the table out again, so a part of the table that the Top DICT does not reach is not in the subset.
/// </para>
/// <para>
/// The formats are an sfnt file with TrueType outlines in a 'glyf' table, an OpenType file with a 'CFF ' table, and a bare CFF font.
/// A subset of an sfnt file is an sfnt file: it has the tables of <see cref="FontSubsetOptions.Tables"/>, the checksum of each table,
/// and the 'head' checksum adjustment. The data that this class reads can be untrusted, such as a font that a PDF file embeds.
/// For malformed data it throws <see cref="InvalidFontFileException"/>, or a type that derives from it, and no other type.
/// </para>
/// </remarks>
public static class FontSubsetter
{
    private const uint Cff = 0x43464620; // 'CFF '
    private const uint Glyf = 0x676C7966; // 'glyf'
    private const uint Head = 0x68656164; // 'head'
    private const uint Loca = 0x6C6F6361; // 'loca'
    private const uint Maxp = 0x6D617870; // 'maxp'

    /// <summary>
    /// Subsets a font program.
    /// </summary>
    /// <remarks>
    /// The work for each kept glyph is bounded as drawing the glyph is: a CFF charstring runs at most 1,048,576 operators and calls
    /// subroutines 10 deep. The closure never recurses, so a cycle of composite glyphs ends. The font data is not changed.
    /// A font that is not a CFF font or a TrueType font, such as WOFF, a font collection, a Type 1 font or a CFF2 font, is not supported.
    /// </remarks>
    /// <param name="font">The font file. See <see cref="FontProgram.Load(ReadOnlyMemory{byte})"/> for the formats.</param>
    /// <param name="glyphIds">
    /// The IDs of the glyphs to keep. Glyph 0, the .notdef glyph, is always kept. The glyphs that the kept glyphs need are kept too.
    /// </param>
    /// <param name="options">The options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The subset font, in the format of <paramref name="font"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="glyphIds"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A glyph ID is not the ID of a glyph of the font.</exception>
    /// <exception cref="ArgumentException"><paramref name="options"/> has a table tag that is not 4 printable ASCII characters.</exception>
    /// <exception cref="InvalidFontFileException">
    /// The data is malformed or is not in a supported format, or the data of a kept glyph is malformed.
    /// </exception>
    /// <exception cref="FontProgramTooLargeException">
    /// The subsetter would retain more than <see cref="FontProgramOptions.MaxRetainedBytes"/> of <see cref="FontSubsetOptions.ProgramOptions"/>.
    /// This type derives from <see cref="InvalidFontFileException"/>.
    /// </exception>
    public static byte[] Subset(ReadOnlyMemory<byte> font, IReadOnlyCollection<ushort> glyphIds, FontSubsetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(glyphIds);
        SortedSet<uint> tags = SelectTags(options);
        bool tablesNamed = options?.Tables is not null;

        LoadBudget budget = new(font.Length, options?.ProgramOptions?.MaxRetainedBytes);
        ReadOnlySpan<byte> span = font.Span;
        if (Type1Program.IsType1(span))
        {
            throw new InvalidFontFileException("A Type 1 font program is not supported.");
        }

        if (CffProgram.IsBareCff(span))
        {
            return Build(CffSubset.Create(font, glyphIds, ref budget), ref budget);
        }

        if (CffProgram.IsBareCff2(span))
        {
            throw new InvalidFontFileException("A CFF2 font program is not supported.");
        }

        SfntDirectory directory = SfntDirectory.Read(font);
        switch (directory.Version)
        {
            case SfntDirectory.OpenTypeCffVersion:
                return SubsetOpenTypeCff(directory, glyphIds, tags, tablesNamed, ref budget);
            case SfntDirectory.TrueTypeVersion or SfntDirectory.AppleTrueTypeVersion:
                return SubsetTrueType(directory, glyphIds, tags, tablesNamed, ref budget);
            case 0x74746366:
                throw new InvalidFontFileException("A font collection is not a supported font program.");
            case 0x774F4646 or 0x774F4632:
                throw new InvalidFontFileException("A WOFF or WOFF2 file is not a supported font program.");
            default:
                throw new InvalidFontFileException("The data is not a supported font program.");
        }
    }

    private static byte[] SubsetOpenTypeCff(SfntDirectory directory, IReadOnlyCollection<ushort> glyphIds, SortedSet<uint> tags, bool tablesNamed, ref LoadBudget budget)
    {
        if (!directory.TryGetTable(Cff, out ReadOnlyMemory<byte> cff))
        {
            throw new InvalidFontFileException(directory.TryGetTable(SfntDirectory.Cff2, out _)
                ? "A font program with a 'CFF2' table is not supported."
                : "The font has no 'CFF ' table.");
        }

        // The default tables hold the outlines of both formats, and a font keeps those of its own. A caller that names tables gets what it names.
        if (!tablesNamed)
        {
            tags.ExceptWith([Glyf, Loca]);
        }

        tags.UnionWith([Cff, Head, Maxp]);
        SfntTableSource[] tables = CopyTables(directory, tags);
        Replace(tables, Cff, CffSubset.Create(cff, glyphIds, ref budget));
        return Build(directory.Version, tables, ref budget);
    }

    private static byte[] SubsetTrueType(SfntDirectory directory, IReadOnlyCollection<ushort> glyphIds, SortedSet<uint> tags, bool tablesNamed, ref LoadBudget budget)
    {
        TrueTypeSubset glyphs = TrueTypeSubset.Create(directory, glyphIds, ref budget);
        if (!tablesNamed)
        {
            tags.Remove(Cff);
        }

        tags.UnionWith([Glyf, Head, Loca, Maxp]);
        SfntTableSource[] tables = CopyTables(directory, tags);
        Replace(tables, Glyf, glyphs.Glyf);
        Replace(tables, Loca, glyphs.Loca);
        return Build(directory.Version, tables, ref budget);
    }

    private static byte[] Build(CffSubset cff, ref LoadBudget budget)
    {
        byte[] output = budget.Allocate<byte>(cff.Length);
        cff.Write(output);
        return output;
    }

    private static byte[] Build(uint version, SfntTableSource[] tables, ref LoadBudget budget)
    {
        byte[] output = budget.Allocate<byte>(SfntWriter.GetLength(tables));
        SfntWriter.Write(version, tables, output);
        return output;
    }

    // The tags of the tables to keep, in order. A tag is checked here, before any work, because a bad tag is the caller's mistake.
    private static SortedSet<uint> SelectTags(FontSubsetOptions? options)
    {
        SortedSet<uint> tags = [];
        foreach (string tag in options?.Tables ?? FontSubsetOptions.DefaultTables)
        {
            if (!IsTag(tag))
            {
                throw new ArgumentException($"The table tag '{tag}' is not 4 printable ASCII characters.", nameof(options));
            }

            tags.Add((uint)((tag[0] << 24) | (tag[1] << 16) | (tag[2] << 8) | tag[3]));
        }

        return tags;
    }

    private static bool IsTag(string? tag)
    {
        if (tag is null || tag.Length != 4)
        {
            return false;
        }

        foreach (char c in tag)
        {
            if (c is < ' ' or > '~')
            {
                return false;
            }
        }

        return true;
    }

    // The tables of the font that are in the set of tags, each as the bytes of the font.
    private static SfntTableSource[] CopyTables(SfntDirectory directory, SortedSet<uint> tags)
    {
        List<SfntTableSource> tables = [];
        foreach (uint tag in tags)
        {
            if (directory.TryGetTable(tag, out ReadOnlyMemory<byte> table))
            {
                tables.Add(new SfntTableSource(tag, table));
            }
        }

        return [.. tables];
    }

    private static void Replace(SfntTableSource[] tables, uint tag, ISubsetTable table)
    {
        int index = Array.FindIndex(tables, source => source.Tag == tag);
        tables[index] = new SfntTableSource(tag, table);
    }
}
