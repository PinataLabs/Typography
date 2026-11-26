// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.FontPrograms;
using static PinataLabs.Typography.Tests.Tables.Variations.MetricsVariationsReference;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// A TrueType font that has 'gvar' as well as HVAR and VVAR takes the advances and side bearings from the tables, as HarfBuzz
/// does, and from the phantom points of 'gvar' for what the tables leave out. VariationMetricsGvar.ttf has deltas for its
/// phantom points that are not those of HVAR and VVAR, so the two sources give different values, and the reference holds both.
/// The tests break or remove a table, or the mapping of a side bearing, to see the phantom points take over. A table that is
/// malformed counts as absent.
/// tests/tools/generate_metrics_variation_reference.py builds the font and writes the reference.
/// </summary>
public class MetricsVariationPhantomPointTests
{
    private const string FontName = "VariationMetricsGvar.ttf";
    private const string FontFile = "Variations/VariationMetricsGvar.ttf";

    // The header of HVAR has the Offset32 of the advance width map, then of the left side bearing map. VVAR has the same with the
    // advance height map and the top side bearing map, after the version and the store.
    private const int LeadingBearingMapOffset = 12;
    private const int AdvanceMapOffset = 8;

    public static IEnumerable<object[]> Locations => GetLocations(font => font.Phantom is not null);

    [Test]
    public async Task ThePhantomPointsGiveOtherValuesThanTheTablesDo()
    {
        // A font whose two sources agreed would test nothing.
        FontReference reference = Fonts[FontName];

        await Assert.That(reference.Phantom!.AdvanceWidths.Where((widths, index) => !widths.SequenceEqual(reference.AdvanceWidths[index])).Count()).IsGreaterThan(8);
        await Assert.That(reference.Phantom.AdvanceHeights.Where((heights, index) => !heights.SequenceEqual(reference.AdvanceHeights![index])).Count()).IsGreaterThan(8);
        await Assert.That(reference.Phantom.LeftSideBearings.Where((bearings, index) => !bearings.SequenceEqual(reference.LeftSideBearings![index])).Count()).IsGreaterThan(2);
        await Assert.That(reference.Phantom.TopSideBearings.Where((bearings, index) => !bearings.SequenceEqual(reference.TopSideBearings![index])).Count()).IsGreaterThan(8);
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task TheTablesWinOverThePhantomPoints(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.AdvanceWidths[index], reference.AdvanceHeights![index], reference.LeftSideBearings![index], reference.TopSideBearings![index])).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task WithoutTheTablesThePhantomPointsGiveEveryValue(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        byte[] bytes = SfntRewriter.Remove(VariationFontBytes.Read(FontFile), "HVAR", "VVAR");
        Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.Phantom!.AdvanceWidths[index], reference.Phantom.AdvanceHeights[index], reference.Phantom.LeftSideBearings[index], reference.Phantom.TopSideBearings[index])).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task AMalformedHvarOrVvarCountsAsAbsentAndTheOtherOneStillWins(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);

        foreach (string table in new[] { "HVAR", "VVAR" })
        {
            byte[] bytes = VariationFontBytes.Read(FontFile);
            BinaryPrimitives.WriteUInt16BigEndian(VariationFontBytes.Table(bytes, table), 2);
            Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

            // The broken table gives its advance and its bearing to the phantom points, and the other table keeps its own.
            bool horizontal = table == "HVAR";
            int[] advanceWidths = horizontal ? reference.Phantom!.AdvanceWidths[index] : reference.AdvanceWidths[index];
            int[] leftSideBearings = horizontal ? reference.Phantom!.LeftSideBearings[index] : reference.LeftSideBearings![index];
            int[] advanceHeights = horizontal ? reference.AdvanceHeights![index] : reference.Phantom!.AdvanceHeights[index];
            int[] topSideBearings = horizontal ? reference.TopSideBearings![index] : reference.Phantom!.TopSideBearings[index];

            await Assert.That(GetDifferences(font, advanceWidths, advanceHeights, leftSideBearings, topSideBearings)).IsEmpty();
        }
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task WithoutTheLeftSideBearingMapTheBearingIsThePhantomPointsAndTheAdvanceStaysWithHvar(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        byte[] bytes = VariationFontBytes.Read(FontFile);
        BinaryPrimitives.WriteUInt32BigEndian(VariationFontBytes.Table(bytes, "HVAR").Slice(LeadingBearingMapOffset), 0);
        Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.AdvanceWidths[index], reference.AdvanceHeights![index], reference.Phantom!.LeftSideBearings[index], reference.TopSideBearings![index])).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task WithoutTheTopSideBearingMapTheBearingIsThePhantomPointsAndTheAdvanceStaysWithVvar(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        byte[] bytes = VariationFontBytes.Read(FontFile);
        BinaryPrimitives.WriteUInt32BigEndian(VariationFontBytes.Table(bytes, "VVAR").Slice(LeadingBearingMapOffset), 0);
        Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.AdvanceWidths[index], reference.AdvanceHeights![index], reference.LeftSideBearings![index], reference.Phantom!.TopSideBearings[index])).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task WithoutVmtxTheAdvanceHeightIsTheSynthesizedDefaultAtEveryInstance(string fontName, string locationName)
    {
        // HarfBuzz gives every glyph the advance height of the font, and neither VVAR nor the phantom points of 'gvar' move it. The font
        // without VVAR has only the phantom points to take it from, and the font with VVAR has a table that 'vmtx' does not go with.
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        int[] expected = reference.HarfBuzz!.WithoutVmtx.AdvanceHeights[index];

        await Assert.That(expected.Distinct().Count()).IsEqualTo(1);
        foreach (string[] removed in new[] { new[] { "vmtx" }, new[] { "vmtx", "VVAR" } })
        {
            Font font = CreateFont(VariationFontBytes.Load(SfntRewriter.Remove(VariationFontBytes.Read(FontFile), removed)), reference.Locations[index]);

            await Assert.That(Enumerable.Range(0, expected.Length).Select(glyph => (int)GetMetrics(font, (ushort)glyph).AdvanceHeight)).IsEquivalentTo(expected);
        }
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task WithoutVmtxVvarDoesNotApplyAndTheTopSideBearingIsThePhantomPoints(string fontName, string locationName)
    {
        // VVAR is read only with 'vmtx', so a font without one has the top side bearings of the phantom points whether or not it has
        // a VVAR top side bearing map. The font without VVAR is the reference.
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        Font withVvar = CreateFont(VariationFontBytes.Load(SfntRewriter.Remove(VariationFontBytes.Read(FontFile), "vmtx")), reference.Locations[index]);
        Font withoutVvar = CreateFont(VariationFontBytes.Load(SfntRewriter.Remove(VariationFontBytes.Read(FontFile), "vmtx", "VVAR")), reference.Locations[index]);
        int count = reference.AdvanceWidths[index].Length;
        Font defaultInstance = CreateFont(VariationFontBytes.Load(SfntRewriter.Remove(VariationFontBytes.Read(FontFile), "vmtx")), reference.Locations[IndexOf(reference, "Regular")]);

        int[] expected = GetTopSideBearings(withoutVvar, count);

        await Assert.That(GetTopSideBearings(withVvar, count)).IsEquivalentTo(expected);
        await Assert.That(expected.Zip(GetTopSideBearings(defaultInstance, count), (varied, original) => varied != original).Any(differs => differs)).IsEqualTo(locationName != "Regular");
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task AnAdvanceWidthMapOfNoEntriesIsAsNoMap(string fontName, string locationName)
    {
        // HarfBuzz reads such a map as it reads none, with the glyph index as the index in the first subtable.
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        byte[] bytes = WithEmptyMap(VariationFontBytes.Read(FontFile), "HVAR", AdvanceMapOffset);
        Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.HarfBuzz!.EmptyAdvanceWidthMap.AdvanceWidths[index], reference.AdvanceHeights![index], reference.LeftSideBearings![index], reference.TopSideBearings![index])).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task AnAdvanceHeightMapOfNoEntriesIsAsNoMap(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        byte[] bytes = WithEmptyMap(VariationFontBytes.Read(FontFile), "VVAR", AdvanceMapOffset);
        Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.AdvanceWidths[index], reference.HarfBuzz!.EmptyAdvanceHeightMap.AdvanceHeights[index], reference.LeftSideBearings![index], reference.TopSideBearings![index])).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task ALeftSideBearingMapOfNoEntriesIsAsNoMapSoTheBearingFollowsTheVariedOutline(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        byte[] bytes = WithEmptyMap(VariationFontBytes.Read(FontFile), "HVAR", LeadingBearingMapOffset);
        Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.AdvanceWidths[index], reference.AdvanceHeights![index], reference.Phantom!.LeftSideBearings[index], reference.TopSideBearings![index])).IsEmpty();
        await Assert.That(GetBoundsDifferences(font, reference.Phantom.LeftSideBearings[index])).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task ATopSideBearingMapOfNoEntriesIsAsNoMapSoTheBearingIsThePhantomPoints(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        byte[] bytes = WithEmptyMap(VariationFontBytes.Read(FontFile), "VVAR", LeadingBearingMapOffset);
        Font font = CreateFont(VariationFontBytes.Load(bytes), reference.Locations[index]);

        await Assert.That(GetDifferences(font, reference.AdvanceWidths[index], reference.AdvanceHeights![index], reference.LeftSideBearings![index], reference.Phantom!.TopSideBearings[index])).IsEmpty();
    }

    // Points the Offset32 at `position` in a table at a DeltaSetIndexMap of format 0 that has no entries: the format, the entry format
    // and a count of 0.
    private static byte[] WithEmptyMap(byte[] font, string table, int position)
    {
        byte[] original = SfntRewriter.GetTable(font, table)!;
        byte[] changed = [.. original, 0, 0, 0, 0];
        BinaryPrimitives.WriteUInt32BigEndian(changed.AsSpan(position), (uint)original.Length);
        return SfntRewriter.Replace(font, table, changed);
    }

    private static int[] GetTopSideBearings(Font font, int count)
        => [.. Enumerable.Range(0, count).Select(glyph => (int)GetMetrics(font, (ushort)glyph).TopSideBearing)];

    // The left side bearing is the left edge of the bounds, to the unit that HarfBuzz rounds it to.
    private static List<string> GetBoundsDifferences(Font font, int[] leftSideBearings)
    {
        List<string> differences = [];
        for (int glyph = 0; glyph < leftSideBearings.Length; glyph++)
        {
            float left = GetMetrics(font, (ushort)glyph).Bounds.Min.X;
            if (Math.Abs(left - leftSideBearings[glyph]) > 0.51F)
            {
                differences.Add($"glyph {glyph} bounds start at {left}, expected {leftSideBearings[glyph]}");
            }
        }

        return differences;
    }

    private static List<string> GetDifferences(Font font, int[] advanceWidths, int[] advanceHeights, int[] leftSideBearings, int[] topSideBearings)
    {
        List<string> differences = [];
        for (int glyph = 0; glyph < advanceWidths.Length; glyph++)
        {
            GlyphMetrics metrics = GetMetrics(font, (ushort)glyph);
            Compare(differences, "advance width", glyph, metrics.AdvanceWidth, advanceWidths[glyph]);
            Compare(differences, "advance height", glyph, metrics.AdvanceHeight, advanceHeights[glyph]);
            Compare(differences, "left side bearing", glyph, metrics.LeftSideBearing, leftSideBearings[glyph]);
            Compare(differences, "top side bearing", glyph, metrics.TopSideBearing, topSideBearings[glyph]);
        }

        return differences;
    }

    private static void Compare(List<string> differences, string name, int glyph, int actual, int expected)
    {
        if (actual != expected)
        {
            differences.Add($"glyph {glyph} {name}: {actual}, expected {expected}");
        }
    }
}
