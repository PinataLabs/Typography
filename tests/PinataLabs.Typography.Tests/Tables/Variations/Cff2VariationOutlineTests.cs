// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
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
/// The CFF2 variation instances of Source Sans 3 draw the outlines and have the bounds that fontTools gives for the same
/// F2Dot14 coordinates, at the default instance (wght 200), at the named instances Regular and Bold, at the axis
/// maximum (wght 900) and between them. The glyphs blend in the charstring, in a subroutine and with vsindex 1.
/// tests/tools/generate_cff2_outline_reference.py writes the reference.
/// </summary>
public class Cff2VariationOutlineTests
{
    // The reference rounds a coordinate to 4 digits, and the library adds the blend deltas in single precision to
    // charstring coordinates of a few hundred units, which is 1e-4 at the worst of these glyphs.
    private const double Tolerance = 1e-3;

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference FontTools = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.Cff2OutlinesFontToolsReference),
        Options)!;

    private static readonly FontFamily Family = new FontCollection().Add(TestFonts.SourceSans3VariableCff2File);

    public static IEnumerable<object[]> Locations => FontTools.Locations.Select(location => new object[] { location.Name });

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task OutlinesMatchFontTools(string locationName)
    {
        int index = Array.FindIndex(FontTools.Locations, location => location.Name == locationName);
        Font font = CreateFont(FontTools.Locations[index]);
        List<string> differences = [];

        foreach (GlyphReference glyph in FontTools.Glyphs)
        {
            string calls = LayoutOutlineRecorder.Render(font, glyph.Id);
            string? difference = GlyphOutline.Compare(
                GlyphOutline.Parse(glyph.Outlines[index]),
                GlyphOutline.Identity,
                GlyphOutline.Parse(LayoutOutlineRecorder.ToGlyphSpaceOutline(calls)),
                GlyphOutline.Identity,
                Tolerance);

            if (difference is not null)
            {
                differences.Add($"{glyph.Name}: {difference}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task BoundsAreTheOutlineBoundsRoundedOutwards(string locationName)
    {
        // The library finds the bounds of the points of an outline and of two points inside each curve, then rounds them
        // outwards to whole font units. fontTools finds the exact bounds, so the library is inside them by at most the
        // rounding, and by no more when a curve has its extremes at its end points, as the curves of these glyphs have.
        int index = Array.FindIndex(FontTools.Locations, location => location.Name == locationName);
        Font font = CreateFont(FontTools.Locations[index]);
        List<string> differences = [];

        foreach (GlyphReference glyph in FontTools.Glyphs)
        {
            double[] expected = glyph.Bounds[index];
            GlyphMetrics metrics = GetMetrics(font, glyph.Id);
            double[] actual = expected.Length == 0
                ? []
                : [metrics.Bounds.Min.X, metrics.Bounds.Min.Y, metrics.Bounds.Max.X, metrics.Bounds.Max.Y];

            bool matches = expected.Length == actual.Length
                && Enumerable.Range(0, expected.Length).All(i => i < 2
                    ? actual[i] <= expected[i] + 0.01 && actual[i] > expected[i] - 1.01
                    : actual[i] >= expected[i] - 0.01 && actual[i] < expected[i] + 1.01);
            if (!matches)
            {
                differences.Add($"{glyph.Name}: [{string.Join(", ", actual)}], expected [{string.Join(", ", expected)}]");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task AdvancesAreTheAdvancesOfFontToolsRoundedOnce(string locationName)
    {
        int index = Array.FindIndex(FontTools.Locations, location => location.Name == locationName);
        Font font = CreateFont(FontTools.Locations[index]);
        List<string> differences = [];

        foreach (GlyphReference glyph in FontTools.Glyphs)
        {
            int actual = GetMetrics(font, glyph.Id).AdvanceWidth;
            double expected = Math.Floor(glyph.Advances[index] + 0.5);
            if (actual != expected)
            {
                differences.Add($"{glyph.Name}: {actual}, expected {expected}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task TheLeftSideBearingIsWhereTheOutlineStartsSinceHvarDoesNotMapIt(string locationName)
    {
        // A CFF glyph has no phantom points, so without an HVAR mapping its left side bearing is the left edge of its
        // varied outline, which fontTools finds exactly and the library rounds down.
        int index = Array.FindIndex(FontTools.Locations, location => location.Name == locationName);
        Font font = CreateFont(FontTools.Locations[index]);
        List<string> differences = [];

        foreach (GlyphReference glyph in FontTools.Glyphs)
        {
            double expected = glyph.Bounds[index].Length == 0 ? 0 : glyph.Bounds[index][0];
            GlyphMetrics metrics = GetMetrics(font, glyph.Id);
            if (!(metrics.LeftSideBearing <= expected + 0.01 && metrics.LeftSideBearing > expected - 1.01))
            {
                differences.Add($"{glyph.Name}: {metrics.LeftSideBearing}, expected {expected}");
            }

            if (metrics.RightSideBearing != metrics.AdvanceWidth - metrics.LeftSideBearing - metrics.Width)
            {
                differences.Add($"{glyph.Name}: the right side bearing is {metrics.RightSideBearing}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task TheReferenceCoordinatesAreTheOnesTheLibraryNormalisesTo()
    {
        List<string> differences = [];
        foreach (Location location in FontTools.Locations)
        {
            short[] actual = CreateFont(location).FontMetrics.Coordinates.Values.ToArray();
            if (!actual.SequenceEqual(location.Coordinates))
            {
                differences.Add($"{location.Name}: [{string.Join(", ", actual)}], expected [{string.Join(", ", location.Coordinates)}]");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task TheDefaultInstanceIsTheFaceAndTheOtherLocationsAreInstancesOfIt()
    {
        FontMetrics face = Family.Faces[0];

        foreach (Location location in FontTools.Locations)
        {
            FontMetrics metrics = CreateFont(location).FontMetrics;
            await Assert.That(metrics.Face).IsSameReferenceAs(face);
            await Assert.That(ReferenceEquals(metrics, face)).IsEqualTo(location.Coordinates.All(value => value == 0));
        }
    }

    [Test]
    public async Task TheLocationsOfTheReferenceDrawDifferentOutlines()
    {
        // The glyphs blend, so a reference whose locations draw alike would test nothing.
        int blending = FontTools.Glyphs.Count(glyph => glyph.Outlines.Distinct().Count() == glyph.Outlines.Length);

        await Assert.That(blending).IsGreaterThan(FontTools.Glyphs.Length / 2);
    }

    [Test]
    public async Task AnInstanceDoesNotChangeTheOutlinesOfTheDefaultInstance()
    {
        // The instances share the tables of the face, so drawing one must leave the face as it was.
        FontMetrics face = Family.Faces[0];
        Location black = FontTools.Locations.Single(location => location.Name == "Black");
        GlyphReference glyph = FontTools.Glyphs.Single(glyph => glyph.Name == "H");
        Font light = CreateFont(FontTools.Locations[0]);

        string before = LayoutOutlineRecorder.Render(light, glyph.Id);
        string instance = LayoutOutlineRecorder.Render(CreateFont(black), glyph.Id);
        string after = LayoutOutlineRecorder.Render(light, glyph.Id);

        await Assert.That(light.FontMetrics).IsSameReferenceAs(face);
        await Assert.That(instance).IsNotEqualTo(before);
        await Assert.That(after).IsEqualTo(before);
    }

    [Test]
    public async Task TheTopSideBearingOfAGlyphBelowTheBaselineClampsAtTheLimitOfTheType()
    {
        // The font has no 'vmtx' table, so the top side bearing of a glyph is the distance from the ascender to the top of its outline.
        // An ascender of 32767 and an outline below the baseline put that distance beyond a 16-bit integer, which a cast would wrap
        // into a negative bearing.
        byte[] bytes = File.ReadAllBytes(TestFonts.SourceSans3VariableCff2File);
        byte[] hhea = SfntRewriter.GetTable(bytes, "hhea")!;
        byte[] os2 = SfntRewriter.GetTable(bytes, "OS/2")!;
        BinaryPrimitives.WriteInt16BigEndian(hhea.AsSpan(4), short.MaxValue);
        BinaryPrimitives.WriteInt16BigEndian(os2.AsSpan(68), short.MaxValue);
        FontFamily family = VariationFontBytes.Load(SfntRewriter.Rewrite(bytes, new Dictionary<string, byte[]?> { ["hhea"] = hhea, ["OS/2"] = os2 }));
        Font font = family.CreateFont(FontTools.UnitsPerEm, default, new FontVariation(Tag.Parse("wght"), 650F));

        List<string> differences = [];
        int below = 0;
        foreach (int codePoint in new[] { '_', 0x2017 })
        {
            if (!font.FontMetrics.TryGetGlyphMetrics(new CodePoint(codePoint), TextAttributes.None, TextDecorations.None, LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out IReadOnlyList<GlyphMetrics>? glyphs)
                || glyphs[0].Bounds.Max.Y >= 0)
            {
                continue;
            }

            below++;
            if (glyphs[0].TopSideBearing != short.MaxValue)
            {
                differences.Add($"U+{codePoint:X4}: {glyphs[0].TopSideBearing}, with the top of the outline at {glyphs[0].Bounds.Max.Y}");
            }
        }

        await Assert.That(font.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo(short.MaxValue);
        await Assert.That(differences).IsEmpty();
        await Assert.That(below).IsGreaterThan(0);
    }

    private static Font CreateFont(Location location)
        => Family.CreateFont(
            FontTools.UnitsPerEm,
            default,
            [.. location.User.Select(axis => new FontVariation(Tag.Parse(axis.Key), (float)axis.Value))]);

    private static GlyphMetrics GetMetrics(Font font, ushort glyphId)
        => font.FontMetrics.GetGlyphMetrics(
            new CodePoint('A'),
            glyphId,
            TextAttributes.None,
            TextDecorations.None,
            LayoutMode.HorizontalTopBottom,
            ColorFontSupport.None)[0];

    private sealed class Reference
    {
        public string FontTools { get; init; } = string.Empty;

        public ushort UnitsPerEm { get; init; }

        public Location[] Locations { get; init; } = [];

        public GlyphReference[] Glyphs { get; init; } = [];
    }

    private sealed class Location
    {
        public string Name { get; init; } = string.Empty;

        public Dictionary<string, double> User { get; init; } = [];

        public short[] Coordinates { get; init; } = [];
    }

    private sealed class GlyphReference
    {
        public string Name { get; init; } = string.Empty;

        public ushort Id { get; init; }

        public string[] Outlines { get; init; } = [];

        public double[][] Bounds { get; init; } = [];

        public double[] Advances { get; init; } = [];
    }
}
