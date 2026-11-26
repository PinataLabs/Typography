// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

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
