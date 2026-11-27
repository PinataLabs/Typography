// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static PinataLabs.Typography.Tests.Tables.Variations.MetricsVariationsReference;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// The variation instances of the two variable test fonts and of two built fonts have the advances and side bearings that
/// fontTools gives for the same F2Dot14 coordinates, at the axis minimum, a named instance, the axis maximum and between
/// them. The advances of the shipped fonts cover every glyph that hmtx lists, with HVAR alone for Teko and with an HVAR
/// advance width map for Source Sans 3. The built fonts add the left and top side bearing maps and VVAR.
/// tests/tools/generate_metrics_variation_reference.py builds the fonts and writes the reference.
/// </summary>
public class MetricsVariationReferenceTests
{
    public static IEnumerable<object[]> FontNames => Fonts.Select(font => new object[] { font.Key });

    public static IEnumerable<object[]> FontsAndLocations => GetLocations(_ => true);

    public static IEnumerable<object[]> FontsWithVerticalMetrics => GetLocations(font => font.AdvanceHeights is not null);

    public static IEnumerable<object[]> FontsWithLeftSideBearingMaps => GetLocations(font => font.LeftSideBearings is not null);

    public static IEnumerable<object[]> FontsWithTopSideBearingMaps => GetLocations(font => font.TopSideBearings is not null);

    [Test]
    [MethodDataSource(nameof(FontNames))]
    public async Task TheReferenceCoordinatesAreTheOnesTheLibraryNormalisesTo(string fontName)
    {
        List<string> differences = [];
        foreach (Location location in Fonts[fontName].Locations)
        {
            short[] actual = CreateFont(fontName, location).FontMetrics.Coordinates.Values.ToArray();
            if (!actual.SequenceEqual(location.Coordinates))
            {
                differences.Add($"{location.Name}: [{string.Join(", ", actual)}], expected [{string.Join(", ", location.Coordinates)}]");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(FontsAndLocations))]
    public async Task AdvanceWidthsMatchFontTools(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);
        List<string> differences = [];

        for (int glyph = 0; glyph < reference.AdvanceWidths[index].Length; glyph++)
        {
            int actual = GetMetrics(font, (ushort)glyph).AdvanceWidth;
            if (actual != reference.AdvanceWidths[index][glyph])
            {
                differences.Add($"glyph {glyph}: {actual}, expected {reference.AdvanceWidths[index][glyph]}");
            }
        }

        await Assert.That(reference.AdvanceWidths[index].Length).IsEqualTo(reference.NumberOfHMetrics);
        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(FontsWithVerticalMetrics))]
    public async Task AdvanceHeightsMatchFontTools(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);
        List<string> differences = [];

        for (int glyph = 0; glyph < reference.AdvanceHeights![index].Length; glyph++)
        {
            int actual = GetMetrics(font, (ushort)glyph).AdvanceHeight;
            if (actual != reference.AdvanceHeights[index][glyph])
            {
                differences.Add($"glyph {glyph}: {actual}, expected {reference.AdvanceHeights[index][glyph]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(FontsWithLeftSideBearingMaps))]
    public async Task LeftSideBearingsFollowTheHvarMapping(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);
        List<string> differences = [];

        for (int glyph = 0; glyph < reference.LeftSideBearings![index].Length; glyph++)
        {
            short actual = GetMetrics(font, (ushort)glyph).LeftSideBearing;
            if (actual != reference.LeftSideBearings[index][glyph])
            {
                differences.Add($"glyph {glyph}: {actual}, expected {reference.LeftSideBearings[index][glyph]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(FontsWithTopSideBearingMaps))]
    public async Task TopSideBearingsFollowTheVvarMapping(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);
        List<string> differences = [];

        for (int glyph = 0; glyph < reference.TopSideBearings![index].Length; glyph++)
        {
            short actual = GetMetrics(font, (ushort)glyph).TopSideBearing;
            if (actual != reference.TopSideBearings[index][glyph])
            {
                differences.Add($"glyph {glyph}: {actual}, expected {reference.TopSideBearings[index][glyph]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task TheReferenceVariesTheAdvancesAndBearingsThatItChecks()
    {
        // A reference whose locations all give the values of the default instance would test nothing.
        FontReference built = Fonts["VariationMetrics.ttf"];

        await Assert.That(built.AdvanceWidths.Distinct(new SequenceComparer()).Count()).IsGreaterThan(8);
        await Assert.That(built.AdvanceHeights!.Distinct(new SequenceComparer()).Count()).IsGreaterThan(8);
        await Assert.That(built.LeftSideBearings!.Distinct(new SequenceComparer()).Count()).IsGreaterThan(2);
        await Assert.That(built.TopSideBearings!.Distinct(new SequenceComparer()).Count()).IsGreaterThan(8);
    }

    private sealed class SequenceComparer : IEqualityComparer<int[]>
    {
        public bool Equals(int[]? x, int[]? y) => x!.SequenceEqual(y!);

        public int GetHashCode(int[] obj) => obj.Aggregate(17, (hash, value) => (hash * 31) + value);
    }
}
