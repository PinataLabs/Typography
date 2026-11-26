// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Tests.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// A GDEF table of version 1.3 holds the Item Variation Store of the GPOS <c>VariationIndex</c> device tables (issue #129).
/// A store that is missing or malformed counts as absent, so that every delta is 0.
/// </summary>
public class GlyphDefinitionVariationStoreTests
{
    // The version 1.3 header has the offset of the store after the five 16-bit offsets of the version 1.2 header and
    // markGlyphSetsDefOffset: 2 + 2 + 2 * 5 + 2 bytes in.
    private const int StoreOffsetPosition = 14;

    [Test]
    [Arguments("Teko/Teko-Variable.ttf", "Teko-Variable.ttf")]
    [Arguments("SourceSans3/SourceSans3VF-Upright.otf", "SourceSans3VF-Upright.otf")]
    public async Task TheStoreHasTheShapeThatFontToolsReads(string path, string referenceName)
    {
        using JsonDocument reference = JsonDocument.Parse(File.ReadAllText(TestFonts.ItemVariationStoreFontToolsReference));
        JsonElement expected = reference.RootElement.GetProperty("fonts").GetProperty(referenceName).GetProperty("stores").GetProperty("GDEF");

        ItemVariationStore store = ReadGdef(VariationFontBytes.Read(path)).VariationStore;

        await Assert.That((store.AxisCount, store.RegionCount, store.DataCount)).IsEqualTo((
            expected.GetProperty("axisCount").GetInt32(),
            expected.GetProperty("regionCount").GetInt32(),
            expected.GetProperty("data").GetArrayLength()));
        await Assert.That(store.RegionCount).IsGreaterThan(0);
    }

    [Test]
    public async Task ATableWithNoStoreHasNoRegions()
    {
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        BinaryPrimitives.WriteUInt32BigEndian(VariationFontBytes.Table(font, "GDEF").Slice(StoreOffsetPosition), 0);

        ItemVariationStore store = ReadGdef(font).VariationStore;

        await Assert.That(store.RegionCount).IsEqualTo(0);
        await Assert.That(store.CreateScalars([16384]).GetDelta(0, 0)).IsEqualTo(0F);
    }

    [Test]
    [Arguments(0xFFFFFFF0U)]
    [Arguments(0x7FFFFFFFU)]
    [Arguments(0x80000000U)]
    public async Task AStoreThatIsNotInTheTableCountsAsAbsent(uint offset)
    {
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        BinaryPrimitives.WriteUInt32BigEndian(VariationFontBytes.Table(font, "GDEF").Slice(StoreOffsetPosition), offset);

        ItemVariationStore store = ReadGdef(font).VariationStore;

        await Assert.That(store.RegionCount).IsEqualTo(0);
        await Assert.That(store.DataCount).IsEqualTo(0);
    }

    private static GlyphDefinitionTable ReadGdef(byte[] font)
    {
        FontReader reader = new(new MemoryStream(font));
        return reader.TryGetTable<GlyphDefinitionTable>() ?? throw new System.InvalidOperationException("The font has no GDEF table.");
    }
}
