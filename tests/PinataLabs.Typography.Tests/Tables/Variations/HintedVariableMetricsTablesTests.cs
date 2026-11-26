// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tests.FontPrograms;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// A glyph that is hinted in a font that has 'HVAR' and 'VVAR' as well as 'gvar' takes its phantom points from the tables, as FreeType does:
/// the deltas of the phantom points of 'gvar' do not move the outline, and the advance is that of the table. Unhinted, the glyph is what
/// HarfBuzz gives, which moves the outline by the first phantom point whether or not the font has the tables.
/// HintedVariableMetricsTables.ttf has the glyphs R and T, which go to the phantom points, and deltas for the phantom points that
/// are not those of the tables. tests/tools/generate_hinted_variable_font.py builds the font and writes the reference.
/// </summary>
public class HintedVariableMetricsTablesTests
{
    // A hinted point is a fiftieth of a pixel from FreeType at worst, which rounds to 1/64 of a pixel.
    private const double HintedTolerance = 0.02;

    // HarfBuzz rounds an extent to a unit of the font.
    private const double HarfBuzzTolerance = 0.51;

    private static readonly Tag Weight = Tag.Parse("wght");
    private static readonly Tag Width = Tag.Parse("wdth");

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference FreeType = JsonSerializer.Deserialize<Reference>(File.ReadAllText(TestFonts.HintedVariableMetricsTablesReference), Options)!;

    private static readonly FontFamily Family = new FontCollection().Add(TestFonts.HintedVariableMetricsTablesFile);

    public static IEnumerable<object[]> HintedLocations
        => Enumerable.Range(0, FreeType.Locations.Count).SelectMany(index => FreeType.HintedSizes, (index, size) => new object[] { index, size });

    public static IEnumerable<object[]> Locations => FreeType.Locations.Select((_, index) => new object[] { index });

    [Test]
    [MethodDataSource(nameof(HintedLocations))]
    public async Task HintedOutlinesMatchFreeType(int location, int size)
    {
        // L moves its first two phantom points together, R goes to the end of the advance, and T to the top and bottom of the vertical advance.
        LocationReference expected = FreeType.Locations[location];
        Font font = CreateFont(expected, size);
        List<string> differences = [];

        foreach ((string glyph, string outline) in expected.Hinted[size.ToString()])
        {
            string? difference = CompareOutline(font, Array.IndexOf(FreeType.Glyphs, glyph), outline, HintedTolerance);
            if (difference is not null)
            {
                differences.Add($"{glyph}: {difference}");
            }
        }

        await Assert.That(differences).IsEmpty();
        await Assert.That(expected.Hinted[size.ToString()].Count).IsEqualTo(4);
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task UnhintedTheAdvancesAreThoseOfTheTablesAndTheOutlineMovesByTheFirstPhantomPoint(int location)
    {
        LocationReference expected = FreeType.Locations[location];
        Font font = CreateFont(expected, 12);
        List<string> differences = [];

        for (int id = 0; id < FreeType.Glyphs.Length; id++)
        {
            string name = FreeType.Glyphs[id];
            HarfBuzzReference reference = expected.HarfBuzz[name];
            GlyphMetrics metrics = GetMetrics(font, id);
            if (reference.AdvanceWidth != metrics.AdvanceWidth || reference.AdvanceHeight != metrics.AdvanceHeight)
            {
                differences.Add($"{name}: advance {metrics.AdvanceWidth} x {metrics.AdvanceHeight}, expected {reference.AdvanceWidth} x {reference.AdvanceHeight}");
            }

            if (reference.Width > 0
                && (Math.Abs(metrics.Bounds.Min.X - reference.LeftSideBearing) > HarfBuzzTolerance
                    || Math.Abs(metrics.Bounds.Max.X - (reference.LeftSideBearing + reference.Width)) > HarfBuzzTolerance))
            {
                differences.Add($"{name}: bounds {metrics.Bounds.Min.X} to {metrics.Bounds.Max.X}, expected {reference.LeftSideBearing} to {reference.LeftSideBearing + reference.Width}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task TheDeltasOfThePhantomPointsAreNotThoseOfTheTables()
    {
        // At the maximum, R has the deltas 32 and 44 on its first two phantom points, so the phantom points would end its advance at
        // 748 where the table gives 788, and HarfBuzz moves the outline left by the first of them.
        LocationReference maximum = FreeType.Locations[2];
        Font font = CreateFont(maximum, 12);
        int r = Array.IndexOf(FreeType.Glyphs, "R");

        await Assert.That(GetMetrics(font, r).AdvanceWidth).IsEqualTo((ushort)788);
        await Assert.That(maximum.HarfBuzz["R"].LeftSideBearing).IsEqualTo(32);
    }

    private static Font CreateFont(LocationReference location, float size)
    {
        Font font = Family.CreateFont(size, FontStyle.Regular, new FontVariation(Weight, location.User["wght"]), new FontVariation(Width, location.User["wdth"]));
        if (!font.FontMetrics.Coordinates.Values.SequenceEqual(location.Coordinates.AsSpan()))
        {
            throw new InvalidOperationException($"{location.Name} has the coordinates {string.Join(", ", font.FontMetrics.Coordinates.Values.ToArray())}.");
        }

        return font;
    }

    private static string? CompareOutline(Font font, int glyphId, string outline, double tolerance)
    {
        GlyphOutline reference = GlyphOutline.Parse(outline);
        GlyphOutline actual = GlyphOutline.Parse(LayoutOutlineRecorder.ToGlyphSpaceOutline(LayoutOutlineRecorder.Render(font, (ushort)glyphId, HintingMode.Standard)));
        return GlyphOutline.Compare(reference, GlyphOutline.Identity, actual, GlyphOutline.Identity, tolerance);
    }

    private static GlyphMetrics GetMetrics(Font font, int glyphId)
        => font.FontMetrics.GetGlyphMetrics(
            new CodePoint('A'),
            (ushort)glyphId,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None)[0];

    private sealed class Reference
    {
        public int[] HintedSizes { get; set; } = [];

        public string[] Glyphs { get; set; } = [];

        public List<LocationReference> Locations { get; set; } = [];
    }

    private sealed class LocationReference
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<string, float> User { get; set; } = [];

        public short[] Coordinates { get; set; } = [];

        public Dictionary<string, HarfBuzzReference> HarfBuzz { get; set; } = [];

        public Dictionary<string, Dictionary<string, string>> Hinted { get; set; } = [];
    }

    private sealed class HarfBuzzReference
    {
        public int AdvanceWidth { get; set; }

        public int AdvanceHeight { get; set; }

        public int LeftSideBearing { get; set; }

        public int Width { get; set; }
    }
}
