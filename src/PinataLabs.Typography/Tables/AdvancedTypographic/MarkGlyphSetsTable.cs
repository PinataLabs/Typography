// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace PinataLabs.Typography.Tables.AdvancedTypographic;

/// <summary>
/// The mark glyph sets of the GDEF table. A lookup with the UseMarkFilteringSet flag skips every mark that is not in
/// its set.
/// <see href="https://learn.microsoft.com/en-us/typography/opentype/spec/gdef#mark-glyph-sets-table"/>
/// </summary>
internal sealed class MarkGlyphSetsTable
{
    private readonly CoverageTable[] coverageTables;

    private MarkGlyphSetsTable(CoverageTable[] coverageTables) => this.coverageTables = coverageTables;

    public static MarkGlyphSetsTable Load(BigEndianBinaryReader reader, long offset)
    {
        // +-----------------+----------------------------------+-------------------------------------------------+
        // | Type            | Name                             | Description                                     |
        // +=================+==================================+=================================================+
        // | uint16          | format                           | Format identifier == 1                          |
        // +-----------------+----------------------------------+-------------------------------------------------+
        // | uint16          | markGlyphSetCount                | Number of mark glyph sets defined               |
        // +-----------------+----------------------------------+-------------------------------------------------+
        // | Offset32        | coverageOffsets[markGlyphSetCount] | Array of offsets to mark glyph set coverage   |
        // |                 |                                  | tables, from the start of the MarkGlyphSets table. |
        // +-----------------+----------------------------------+-------------------------------------------------+
        reader.Seek(offset, SeekOrigin.Begin);
        reader.ReadUInt16();
        ushort markGlyphSetCount = reader.ReadUInt16();
        uint[] coverageOffsets = new uint[markGlyphSetCount];
        for (int i = 0; i < coverageOffsets.Length; i++)
        {
            coverageOffsets[i] = reader.ReadOffset32();
        }

        CoverageTable[] coverageTables = new CoverageTable[markGlyphSetCount];
        for (int i = 0; i < coverageTables.Length; i++)
        {
            coverageTables[i] = CoverageTable.Load(reader, offset + coverageOffsets[i]);
        }

        return new MarkGlyphSetsTable(coverageTables);
    }

    /// <summary>
    /// Gets a value indicating whether a mark glyph set has a glyph. As in HarfBuzz, a set that the table does not
    /// have holds no glyph.
    /// </summary>
    /// <param name="markGlyphSet">The zero-based index of the mark glyph set.</param>
    /// <param name="glyphId">The glyph ID.</param>
    /// <returns><see langword="true"/> if the set has the glyph.</returns>
    public bool Covers(ushort markGlyphSet, ushort glyphId)
        => markGlyphSet < this.coverageTables.Length && this.coverageTables[markGlyphSet].CoverageIndexOf(glyphId) >= 0;
}
