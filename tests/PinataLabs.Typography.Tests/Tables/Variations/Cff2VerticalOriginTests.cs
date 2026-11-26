// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// A CFF2 glyph has no phantom points, so its vertical origin is where its default outline puts it: the top of that outline
/// plus the top side bearing of vmtx. When VVAR does not map the top side bearing, the bearing of a varied glyph is the
/// distance from that origin to the top of the varied outline, so the origin does not move with the outline. A VVAR map gives
/// the bearing when there is one. The tops and the advance heights are those of fontTools, which HarfBuzz gives too.
/// tests/tools/generate_metrics_variation_reference.py builds the fonts and writes the reference.
/// </summary>
public class Cff2VerticalOriginTests
{
    private const string FontWithoutAMap = "VariationCff2Vertical.otf";

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, FontReference> References = JsonSerializer.Deserialize<ReferenceFile>(
        File.ReadAllText(TestFonts.Cff2VerticalOriginsFontToolsReference),
        Options)!.Fonts;

    private static readonly Dictionary<string, FontFamily> Families = References.ToDictionary(
        font => font.Key,
        font => new FontCollection().Add(TestFonts.GetPath(font.Value.File)));

    public static IEnumerable<object[]> FontsAndLocations
        => References.SelectMany(font => font.Value.Locations, (font, location) => new object[] { font.Key, location.Name });

    public static IEnumerable<object[]> FontWithoutAMapAndLocations
        => FontsAndLocations.Where(row => (string)row[0] == FontWithoutAMap);

    [Test]
    [MethodDataSource(nameof(FontsAndLocations))]
    public async Task TheTopSideBearingMatchesTheReference(string fontName, string locationName)
    {
        FontReference reference = References[fontName];
        int index = reference.Locations.ToList().FindIndex(location => location.Name == locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);
        List<string> differences = [];

        foreach (GlyphReference glyph in reference.Glyphs)
        {
            GlyphMetrics metrics = GetMetrics(font, glyph.Id);
            if (metrics.TopSideBearing != glyph.TopSideBearings[index] || metrics.AdvanceHeight != glyph.AdvanceHeights[index])
            {
                differences.Add(
                    $"{glyph.Name}: bearing {metrics.TopSideBearing}, height {metrics.AdvanceHeight}, expected {glyph.TopSideBearings[index]}, {glyph.AdvanceHeights[index]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(FontsAndLocations))]
    public async Task TheTopOfTheOutlineMatchesTheReference(string fontName, string locationName)
    {
        FontReference reference = References[fontName];
        int index = reference.Locations.ToList().FindIndex(location => location.Name == locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);
        List<string> differences = [];

        foreach (GlyphReference glyph in reference.Glyphs)
        {
            float top = GetMetrics(font, glyph.Id).Bounds.Max.Y;
            if (top != glyph.Tops[index])
            {
                differences.Add($"{glyph.Name}: {top}, expected {glyph.Tops[index]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(FontWithoutAMapAndLocations))]
    public async Task WithoutAMapTheOriginStaysWhereTheDefaultOutlinePutsIt(string fontName, string locationName)
    {
        // The origin is the top of the outline plus the bearing, which layout adds to place the glyph.
        FontReference reference = References[fontName];
        int index = reference.Locations.ToList().FindIndex(location => location.Name == locationName);
        Font font = CreateFont(fontName, reference.Locations[index]);
        List<string> differences = [];

        foreach (GlyphReference glyph in reference.Glyphs)
        {
            GlyphMetrics metrics = GetMetrics(font, glyph.Id);
            float origin = metrics.Bounds.Max.Y + metrics.TopSideBearing;
            if (origin != glyph.Origin)
            {
                differences.Add($"{glyph.Name}: {origin}, expected {glyph.Origin}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    private static Font CreateFont(string fontName, Location location)
        => Families[fontName].CreateFont(
            Families[fontName].Faces[0].UnitsPerEm,
            default,
            [.. location.User.Select(axis => new FontVariation(Tag.Parse(axis.Key), (float)axis.Value))]);

    private static GlyphMetrics GetMetrics(Font font, ushort glyphId)
        => font.FontMetrics.GetGlyphMetrics(
            new CodePoint('A'),
            glyphId,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.VerticalLeftRight,
            ColorFontSupport.None)[0];

    private sealed class ReferenceFile
    {
        public Dictionary<string, FontReference> Fonts { get; init; } = [];
    }

    private sealed class FontReference
    {
        public string File { get; init; } = string.Empty;

        public Location[] Locations { get; init; } = [];

        public GlyphReference[] Glyphs { get; init; } = [];
    }

    private sealed class Location
    {
        public string Name { get; init; } = string.Empty;

        public Dictionary<string, double> User { get; init; } = [];
    }

    private sealed class GlyphReference
    {
        public string Name { get; init; } = string.Empty;

        public ushort Id { get; init; }

        public int Origin { get; init; }

        public int[] Tops { get; init; } = [];

        public int[] AdvanceHeights { get; init; } = [];

        public int[] TopSideBearings { get; init; } = [];
    }
}
