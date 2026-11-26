// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Fuzz;
using PinataLabs.Typography.Tables.Cff;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Tests for the variation store of a CFF2 table, which gives <c>blend</c> the number of regions of each
/// ItemVariationData.
/// </summary>
public class Cff2VariationStoreTests
{
    // In Source Sans 3 glyph 9 ("H") has blend operators and glyph 1 has none. Of the 2478 glyphs, only 9 draw without the store.
    private const int GlyphWithBlend = 9;
    private const int GlyphWithoutBlend = 1;

    [Test]
    public async Task TheStoreOfSourceSans3HasTheRegionsAndSubtablesOfTheFont()
    {
        byte[] table = ReadCff2Table();
        Cff2TopDict.TryGetVariationStoreOffset(table, out int offset);

        bool found = ItemVariationStore.TryReadCff2(table, offset, out ItemVariationStore store);
        bool first = store.TryGetDataCounts(0, out int firstItems, out int firstRegions);
        bool second = store.TryGetDataCounts(1, out int secondItems, out int secondRegions);

        // fontTools: three regions on one axis, and two subtables of 2 and 1 regions with no rows.
        await Assert.That(found).IsTrue();
        await Assert.That((store.AxisCount, store.RegionCount, store.DataCount)).IsEqualTo((1, 3, 2));
        await Assert.That(first && second).IsTrue();
        await Assert.That((firstItems, firstRegions, secondItems, secondRegions)).IsEqualTo((0, 2, 0, 1));
    }

    [Test]
    public async Task GlyphsWithBlendOperatorsDrawWhenTheStoreIsIntact()
    {
        CffGlyphData[] glyphs = Cff2Parser.Load(ReadCff2Table());

        await Assert.That(Draws(glyphs[GlyphWithBlend])).IsTrue();
        await Assert.That(Draws(glyphs[GlyphWithoutBlend])).IsTrue();
    }

    [Test]
    public async Task AMalformedStoreIsAbsentAndOnlyGlyphsWithBlendOperatorsFail()
    {
        byte[] table = ReadCff2Table();
        Cff2TopDict.TryGetVariationStoreOffset(table, out int offset);

        // The format of the store is the first field after the length.
        table[offset + 3] = 2;
        CffGlyphData[] glyphs = Cff2Parser.Load(table);

        await Assert.That(glyphs.Length).IsGreaterThan(GlyphWithoutBlend);
        await Assert.That(Draws(glyphs[GlyphWithBlend])).IsFalse();
        await Assert.That(Draws(glyphs[GlyphWithoutBlend])).IsTrue();
    }

    private static byte[] ReadCff2Table()
        => SfntDirectory.Read(File.ReadAllBytes(TestFonts.SourceSans3VariableCff2File))
            .GetTable(SfntDirectory.Cff2, "CFF2")
            .ToArray();

    private static bool Draws(CffGlyphData glyph)
    {
        NullGlyphOutlineSink sink = default;
        return glyph.TryWriteOutline(ref sink);
    }
}
