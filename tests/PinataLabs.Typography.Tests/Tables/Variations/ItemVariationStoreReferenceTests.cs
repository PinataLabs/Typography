// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Fuzz;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// The Item Variation Stores of the two variable test fonts give the scalars and the deltas that fontTools gives, at the
/// axis minimum, at named instances, at the axis maximum and between them. The reference holds the F2Dot14 coordinates
/// that fontTools normalises each location to, with avar. The reader takes them as they are, because normalisation is
/// not part of it. tests/tools/generate_item_variation_store_reference.py writes the reference.
/// </summary>
public class ItemVariationStoreReferenceTests
{
    // The reference rounds a scalar to 8 digits and a delta to 6. The reader sums in single precision, which is
    // 1e-5 off fontTools at worst for these fonts, where the largest delta is a few hundred font units.
    private const float ScalarTolerance = 1e-6F;
    private const float DeltaTolerance = 1e-4F;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference FontTools = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.ItemVariationStoreFontToolsReference),
        Options)!;

    public static IEnumerable<object[]> Stores
        => FontTools.Fonts.SelectMany(font => font.Value.Stores.Keys, (font, table) => new object[] { font.Key, table });

    public static IEnumerable<object[]> StoresWithAdvanceWidthMaps
        => FontTools.Fonts
            .SelectMany(font => font.Value.Stores.Where(store => store.Value.AdvanceWidthMap is not null), (font, store) => new object[] { font.Key, store.Key });

    [Test]
    [MethodDataSource(nameof(Stores))]
    public async Task ScalarsAndDeltasMatchFontTools(string fontName, string table)
    {
        FontReference font = FontTools.Fonts[fontName];
        StoreReference expected = font.Stores[table];
        ItemVariationStore store = ReadStore(fontName, table);
        List<string> differences = [];

        if ((store.AxisCount, store.RegionCount, store.DataCount) != (expected.AxisCount, expected.RegionCount, expected.Data.Length))
        {
            differences.Add($"{(store.AxisCount, store.RegionCount, store.DataCount)}, expected {(expected.AxisCount, expected.RegionCount, expected.Data.Length)}");
        }

        for (int outer = 0; outer < expected.Data.Length; outer++)
        {
            if (!store.TryGetDataCounts(outer, out int items, out int regions)
                || (items, regions) != (expected.Data[outer].ItemCount, expected.Data[outer].RegionIndexes.Length))
            {
                differences.Add($"subtable {outer} has {items} rows and {regions} regions, expected {expected.Data[outer].ItemCount} and {expected.Data[outer].RegionIndexes.Length}");
            }
        }

        for (int i = 0; i < font.Locations.Length; i++)
        {
            Location location = font.Locations[i];
            ItemVariationScalars scalars = store.CreateScalars(location.Coordinates);
            CompareScalars(location.Name, scalars, expected, expected.Scalars[i], differences);
            CompareDeltas(location.Name, scalars, expected, expected.Deltas[i], differences);
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(StoresWithAdvanceWidthMaps))]
    public async Task AdvanceWidthMapAndDeltasMatchFontTools(string fontName, string table)
    {
        FontReference font = FontTools.Fonts[fontName];
        StoreReference expected = font.Stores[table];
        byte[] hvar = ReadTable(fontName, table);
        ItemVariationStore store = ReadStore(fontName, table);

        // HVAR: major and minor version, the offset of the store, then the offset of the advance width map.
        bool read = DeltaSetIndexMap.TryRead(hvar, (int)BinaryPrimitives.ReadUInt32BigEndian(hvar.AsSpan(8)), out DeltaSetIndexMap map);
        List<string> differences = [];

        for (int glyph = 0; glyph < expected.AdvanceWidthMap!.Length; glyph++)
        {
            if (!map.TryGet(glyph, out int outer, out int inner) || ((outer << 16) | inner) != expected.AdvanceWidthMap[glyph])
            {
                differences.Add($"glyph {glyph} maps to ({outer}, {inner}), expected VarIdx {expected.AdvanceWidthMap[glyph]}");
                continue;
            }

            for (int i = 0; i < font.Locations.Length; i++)
            {
                float delta = store.CreateScalars(font.Locations[i].Coordinates).GetDelta(outer, inner);
                float reference = expected.Deltas[i][outer][inner];
                if (MathF.Abs(delta - reference) > DeltaTolerance)
                {
                    differences.Add($"glyph {glyph} at {font.Locations[i].Name}: {delta}, expected {reference}");
                }
            }
        }

        // fontTools gives every glyph its entry. The file stops at the last glyph that differs from the one before it,
        // so a glyph past the last entry takes the last entry.
        await Assert.That(read).IsTrue();
        await Assert.That(map.Count).IsBetween(1, expected.AdvanceWidthMap.Length);
        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task ValueRecordsOfMvarGiveTheDeltasOfFontTools()
    {
        List<string> differences = [];

        foreach ((string fontName, FontReference font) in FontTools.Fonts)
        {
            StoreReference expected = font.Stores["MVAR"];
            ItemVariationStore store = ReadStore(fontName, "MVAR");
            foreach (JsonElement[] record in expected.ValueRecords!)
            {
                int varIdx = record[1].GetInt32();
                for (int i = 0; i < font.Locations.Length; i++)
                {
                    float delta = store.CreateScalars(font.Locations[i].Coordinates).GetDelta(varIdx >> 16, varIdx & 0xFFFF);
                    float reference = expected.Deltas[i][varIdx >> 16][varIdx & 0xFFFF];
                    if (MathF.Abs(delta - reference) > DeltaTolerance)
                    {
                        differences.Add($"{fontName} {record[0].GetString()} at {font.Locations[i].Name}: {delta}, expected {reference}");
                    }
                }
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task TheCff2StoreGivesBlendTheScalarsOfTheRegionsThatAVsindexNames()
    {
        FontReference font = FontTools.Fonts["SourceSans3VF-Upright.otf"];
        StoreReference expected = font.Stores["CFF2"];
        ItemVariationStore store = ReadStore("SourceSans3VF-Upright.otf", "CFF2");
        List<string> differences = [];

        for (int i = 0; i < font.Locations.Length; i++)
        {
            ItemVariationScalars scalars = store.CreateScalars(font.Locations[i].Coordinates);
            for (int vsindex = 0; vsindex < expected.Data.Length; vsindex++)
            {
                int[] regionIndexes = expected.Data[vsindex].RegionIndexes;
                float[] actual = new float[regionIndexes.Length];
                if (!scalars.TryGetRegionScalars(vsindex, actual, out int count) || count != regionIndexes.Length)
                {
                    differences.Add($"{font.Locations[i].Name}, vsindex {vsindex}: {count} scalars, expected {regionIndexes.Length}");
                    continue;
                }

                for (int k = 0; k < count; k++)
                {
                    float reference = expected.Scalars[i][regionIndexes[k]];
                    if (MathF.Abs(actual[k] - reference) > ScalarTolerance)
                    {
                        differences.Add($"{font.Locations[i].Name}, vsindex {vsindex}, region {k}: {actual[k]}, expected {reference}");
                    }
                }
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [Arguments("Teko-Variable.ttf", "Regular")]
    [Arguments("SourceSans3VF-Upright.otf", "Semibold")]
    public async Task TheReferenceHasTheAxisMinimumANamedInstanceAndTheAxisMaximum(string fontName, string instance)
    {
        // Both fonts have one axis, and its default is its minimum.
        Location[] locations = FontTools.Fonts[fontName].Locations;

        await Assert.That(locations.Any(location => location.Coordinates[0] == 0)).IsTrue();
        await Assert.That(locations.Any(location => location.Name == instance && location.Coordinates[0] is > 0 and < 16384)).IsTrue();
        await Assert.That(locations.Any(location => location.Coordinates[0] == 16384)).IsTrue();
    }

    private static void CompareScalars(string location, ItemVariationScalars scalars, StoreReference expected, float[] reference, List<string> differences)
    {
        if (scalars.Regions.Length != expected.RegionCount)
        {
            differences.Add($"{location}: {scalars.Regions.Length} scalars, expected {expected.RegionCount}");
            return;
        }

        for (int region = 0; region < reference.Length; region++)
        {
            if (MathF.Abs(scalars.Regions[region] - reference[region]) > ScalarTolerance)
            {
                differences.Add($"{location}, region {region}: {scalars.Regions[region]}, expected {reference[region]}");
            }
        }
    }

    private static void CompareDeltas(string location, ItemVariationScalars scalars, StoreReference expected, float[][] reference, List<string> differences)
    {
        for (int outer = 0; outer < reference.Length; outer++)
        {
            for (int inner = 0; inner < reference[outer].Length; inner++)
            {
                float delta = scalars.GetDelta(outer, inner);
                if (MathF.Abs(delta - reference[outer][inner]) > DeltaTolerance)
                {
                    differences.Add($"{location}, delta ({outer}, {inner}): {delta}, expected {reference[outer][inner]}");
                }
            }

            // The row after the last one is not in the store.
            if (scalars.GetDelta(outer, reference[outer].Length) != 0)
            {
                differences.Add($"{location}, delta ({outer}, {reference[outer].Length}) past the last row is not 0");
            }
        }
    }

    private static byte[] ReadTable(string fontName, string table)
    {
        string path = TestFonts.GetPath(FontTools.Fonts[fontName].File);
        uint tag = BinaryPrimitives.ReadUInt32BigEndian(Encoding.ASCII.GetBytes(table.PadRight(4)));
        return SfntDirectory.Read(File.ReadAllBytes(path)).GetTable(tag, table).ToArray();
    }

    // Finds the store as each table says. HVAR and the GDEF 1.3 header give a 32-bit offset, and MVAR gives a 16-bit one.
    private static ItemVariationStore ReadStore(string fontName, string table)
    {
        byte[] bytes = ReadTable(fontName, table);
        bool read;
        ItemVariationStore store;
        switch (table)
        {
            case "HVAR":
                read = ItemVariationStore.TryRead(bytes, (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4)), out store);
                break;

            case "MVAR":
                read = ItemVariationStore.TryRead(bytes, BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(10)), out store);
                break;

            case "GDEF":
                // The version is 1.3: the header ends with the offset of the store, after markGlyphSetsDefOffset.
                read = ItemVariationStore.TryRead(bytes, (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(14)), out store);
                break;

            default:
                Cff2TopDict.TryGetVariationStoreOffset(bytes, out int offset);
                read = ItemVariationStore.TryReadCff2(bytes, offset, out store);
                break;
        }

        return read ? store : throw new InvalidOperationException($"{fontName} {table}: the store is malformed.");
    }

    private sealed record Reference(string FontTools, Dictionary<string, FontReference> Fonts);

    private sealed record FontReference(string File, Location[] Locations, Dictionary<string, StoreReference> Stores);

    private sealed record Location(string Name, short[] Coordinates);

    private sealed record DataReference(int ItemCount, int[] RegionIndexes);

    private sealed record StoreReference(
        int AxisCount,
        int RegionCount,
        DataReference[] Data,
        float[][] Scalars,
        float[][][] Deltas,
        int[]? AdvanceWidthMap,
        JsonElement[][]? ValueRecords);
}
