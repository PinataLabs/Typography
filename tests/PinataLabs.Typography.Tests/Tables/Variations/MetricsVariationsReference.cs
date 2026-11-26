// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// The fontTools reference for the metrics of variation instances, with the fonts it describes and the ways to draw them.
/// tests/tools/generate_metrics_variation_reference.py builds the fonts and writes the reference.
/// </summary>
internal static class MetricsVariationsReference
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, FontReference> FontReferences = JsonSerializer.Deserialize<ReferenceFile>(
        File.ReadAllText(TestFonts.MetricsVariationsFontToolsReference),
        Options)!.Fonts;

    private static readonly Dictionary<string, FontFamily> Families = FontReferences.ToDictionary(
        font => font.Key,
        font => new FontCollection().Add(TestFonts.GetPath(font.Value.File)));

    /// <summary>
    /// Gets the reference of each font by its file name.
    /// </summary>
    public static IReadOnlyDictionary<string, FontReference> Fonts => FontReferences;

    /// <summary>
    /// Gets the font and the location of each location of the reference that a font has, as data for a test.
    /// </summary>
    public static IEnumerable<object[]> GetLocations(Func<FontReference, bool> include)
        => Fonts
            .Where(font => include(font.Value))
            .SelectMany(font => font.Value.Locations, (font, location) => new object[] { font.Key, location.Name });

    public static int IndexOf(FontReference reference, string locationName)
        => Array.FindIndex(reference.Locations, location => location.Name == locationName);

    /// <summary>
    /// Creates a font of a family at the user coordinates of a location.
    /// </summary>
    public static Font CreateFont(string fontName, Location location)
        => Families[fontName].CreateFont(
            Families[fontName].Faces[0].UnitsPerEm,
            default,
            [.. location.User.Select(axis => new FontVariation(Tag.Parse(axis.Key), (float)axis.Value))]);

    public static GlyphMetrics GetMetrics(Font font, ushort glyphId)
        => font.FontMetrics.GetGlyphMetrics(
            new CodePoint('A'),
            glyphId,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None)[0];

    internal sealed class ReferenceFile
    {
        public string FontTools { get; init; } = string.Empty;

        public Dictionary<string, FontReference> Fonts { get; init; } = [];
    }

    internal sealed class FontReference
    {
        public string File { get; init; } = string.Empty;

        public int NumberOfHMetrics { get; init; }

        public Location[] Locations { get; init; } = [];

        public int[][] AdvanceWidths { get; init; } = [];

        public int[][]? AdvanceHeights { get; init; }

        public int[][]? LeftSideBearings { get; init; }

        public int[][]? TopSideBearings { get; init; }

        public Dictionary<string, int>[] Metrics { get; init; } = [];

        public Dictionary<string, int>[] Mvar { get; init; } = [];
    }

    internal sealed class Location
    {
        public string Name { get; init; } = string.Empty;

        public Dictionary<string, double> User { get; init; } = [];

        public short[] Coordinates { get; init; } = [];
    }
}
