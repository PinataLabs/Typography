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
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Tests.FontPrograms;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// A variation instance of HintedVariable.ttf draws the outlines that fontTools and FreeType give, hints them with the control
/// values that the 'cvar' table varies, and runs the instructions GETVARIATION and GETINFO with the coordinates of the instance.
/// The font has two axes, 'gvar' tuples with interpolation of untouched points, intermediate regions and components, and hinted
/// glyphs whose instructions depend on the control values and on the coordinates. tests/tools/generate_hinted_variable_font.py
/// builds the font and writes the reference: the control values and outlines of fontTools, and the outlines of FreeType, with
/// and without its version 35 hinting interpreter.
/// </summary>
public class HintedVariableFontTests
{
    // The library draws in single precision, so a point is a hundredth of a unit from the reference at worst, and a hinted
    // point is a fiftieth of a pixel from FreeType, which rounds to 1/64 of a pixel.
    private const double OutlineTolerance = 0.01;
    private const double HintedTolerance = 0.02;

    // HarfBuzz rounds an extent to a unit of the font.
    private const double HarfBuzzTolerance = 0.51;

    private static readonly Tag Weight = Tag.Parse("wght");
    private static readonly Tag Width = Tag.Parse("wdth");

    private static readonly string[] GlyphNames = [".notdef", "space", "A", "B", "C", "D", "H", "P", "V", "L", "M", "N"];

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference FontTools = JsonSerializer.Deserialize<Reference>(File.ReadAllText(TestFonts.HintedVariableFontToolsReference), Options)!;

    public static IEnumerable<object[]> Locations => FontTools.Locations.Select((_, index) => new object[] { index });

    public static IEnumerable<object[]> HintedLocations
        => Enumerable.Range(0, FontTools.Locations.Count).SelectMany(index => FontTools.HintedSizes, (index, size) => new object[] { index, size });

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task TheControlValuesOfAnInstanceAreTheCvtValuesPlusTheCvarDeltas(int location)
    {
        LocationReference expected = FontTools.Locations[location];
        StreamFontMetrics face = StreamFontMetrics.LoadFont(TestFonts.HintedVariableFile);

        StreamFontMetrics instance = (StreamFontMetrics)face.GetInstance(new NormalizedCoordinates(expected.Coordinates), [expected.User["wght"], expected.User["wdth"]]);

        await Assert.That(instance.GetControlValues()!).IsEquivalentTo(expected.ControlValues);
    }

    [Test]
    public async Task TheFaceHasTheCvtValues()
    {
        StreamFontMetrics face = StreamFontMetrics.LoadFont(TestFonts.HintedVariableFile);

        await Assert.That(face.GetControlValues()!).IsEquivalentTo(FontTools.ControlValues);
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task OutlinesMatchFreeType(int location)
    {
        LocationReference expected = FontTools.Locations[location];
        Font font = CreateFont(new FontCollection().Add(TestFonts.HintedVariableFile), expected, 1024);

        await Assert.That(Compare(font, expected, glyph => glyph.Unhinted, HintingMode.None, OutlineTolerance)).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task OutlinesMatchFontTools(int location)
    {
        LocationReference expected = FontTools.Locations[location];
        Font font = CreateFont(new FontCollection().Add(TestFonts.HintedVariableFile), expected, 1024);

        // fontTools has no outline for the glyphs whose components it places differently.
        await Assert.That(Compare(font, expected, glyph => glyph.Outline, HintingMode.None, OutlineTolerance)).IsEmpty();
        await Assert.That(expected.Glyphs.Values.Count(glyph => glyph.Outline is not null)).IsGreaterThan(5);
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task BoundsAndMetricsMatchFontTools(int location)
    {
        // The advance, the side bearings and the bounds come from the points and the phantom points of the instance.
        LocationReference expected = FontTools.Locations[location];
        Font font = CreateFont(new FontCollection().Add(TestFonts.HintedVariableFile), expected, 12);
        List<string> differences = [];

        for (int id = 0; id < GlyphNames.Length; id++)
        {
            GlyphReference reference = expected.Glyphs[GlyphNames[id]];
            GlyphMetrics metrics = GetMetrics(font, id);
            Bounds bounds = metrics.Bounds;

            // fontTools keeps the phantom points of a composite glyph that has a component with USE_MY_METRICS, where HarfBuzz and
            // FreeType take those of the component. The HarfBuzz test covers M and N.
            if (GlyphNames[id] is "M" or "N")
            {
                continue;
            }

            if (reference.AdvanceWidth != metrics.AdvanceWidth || reference.AdvanceHeight != metrics.AdvanceHeight)
            {
                differences.Add($"{GlyphNames[id]}: advance {metrics.AdvanceWidth} x {metrics.AdvanceHeight}, expected {reference.AdvanceWidth} x {reference.AdvanceHeight}");
            }

            if ((reference.LeftSideBearing is { } lsb && lsb != metrics.LeftSideBearing) || (reference.TopSideBearing is { } tsb && tsb != metrics.TopSideBearing))
            {
                differences.Add($"{GlyphNames[id]}: side bearings {metrics.LeftSideBearing} and {metrics.TopSideBearing}, expected {reference.LeftSideBearing} and {reference.TopSideBearing}");
            }

            if (reference.Bounds is { } box
                && (Math.Abs(bounds.Min.X - box[0]) > OutlineTolerance || Math.Abs(bounds.Min.Y - box[1]) > OutlineTolerance
                    || Math.Abs(bounds.Max.X - box[2]) > OutlineTolerance || Math.Abs(bounds.Max.Y - box[3]) > OutlineTolerance))
            {
                differences.Add($"{GlyphNames[id]}: bounds {bounds.Min} {bounds.Max}, expected [{string.Join(", ", box)}]");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task AdvancesBearingsAndBoundsMatchHarfBuzz(int location)
    {
        // HarfBuzz moves the outline by the first phantom point (L), and gives a composite the phantom points of the component whose
        // metrics it uses (M and N). At the default instance it applies no variation and reads the advance of N from the 'hmtx' table,
        // where FreeType and this library take that of the component.
        LocationReference expected = FontTools.Locations[location];
        Font font = CreateFont(new FontCollection().Add(TestFonts.HintedVariableFile), expected, 12);
        List<string> differences = [];

        for (int id = 0; id < GlyphNames.Length; id++)
        {
            string name = GlyphNames[id];
            HarfBuzzReference reference = expected.Glyphs[name].HarfBuzz;
            GlyphMetrics metrics = GetMetrics(font, id);
            if (name == "N" && location == 0)
            {
                continue;
            }

            if (reference.AdvanceWidth != metrics.AdvanceWidth || reference.AdvanceHeight != metrics.AdvanceHeight
                || reference.LeftSideBearing != metrics.LeftSideBearing || reference.TopSideBearing != metrics.TopSideBearing)
            {
                differences.Add($"{name}: advance {metrics.AdvanceWidth} x {metrics.AdvanceHeight}, bearings {metrics.LeftSideBearing} and {metrics.TopSideBearing}, expected {reference.AdvanceWidth} x {reference.AdvanceHeight}, {reference.LeftSideBearing} and {reference.TopSideBearing}");
            }

            // HarfBuzz rounds each edge to a unit, and an empty glyph has no extents.
            Bounds bounds = metrics.Bounds;
            if (reference.Width > 0
                && (Math.Abs(bounds.Min.X - reference.LeftSideBearing) > HarfBuzzTolerance
                    || Math.Abs(bounds.Max.X - (reference.LeftSideBearing + reference.Width)) > HarfBuzzTolerance
                    || Math.Abs(bounds.Max.Y - reference.YMax) > HarfBuzzTolerance
                    || Math.Abs(bounds.Min.Y - (reference.YMax - reference.Height)) > HarfBuzzTolerance))
            {
                differences.Add($"{name}: bounds {bounds.Min} {bounds.Max}, expected x {reference.LeftSideBearing} to {reference.LeftSideBearing + reference.Width}, y {reference.YMax - reference.Height} to {reference.YMax}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task ACompositeTakesThePhantomPointsOfTheComponentThatUsesItsMetrics()
    {
        // At the maximum, L has the deltas 20 + 16 and 20 on its first two phantom points, and M has -24 and 140 on its own.
        // N has the advance 800 in the 'hmtx' table and L has 720.
        LocationReference maximum = FontTools.Locations[2];
        Font font = CreateFont(new FontCollection().Add(TestFonts.HintedVariableFile), maximum, 12);

        GlyphMetrics l = GetMetrics(font, Array.IndexOf(GlyphNames, "L"));
        GlyphMetrics m = GetMetrics(font, Array.IndexOf(GlyphNames, "M"));
        GlyphMetrics n = GetMetrics(font, Array.IndexOf(GlyphNames, "N"));

        await Assert.That(l.AdvanceWidth).IsEqualTo((ushort)704);
        await Assert.That(m.AdvanceWidth).IsEqualTo(l.AdvanceWidth);
        await Assert.That(n.AdvanceWidth).IsEqualTo(l.AdvanceWidth);
        await Assert.That(m.AdvanceHeight).IsEqualTo(l.AdvanceHeight);
    }

    [Test]
    public async Task TheOutlineMovesBackByTheFirstPhantomPoint()
    {
        // L is a box from 64 to 576 units, and a delta of 36 on the first phantom point puts its left edge at 28.
        LocationReference maximum = FontTools.Locations[2];
        Font font = CreateFont(new FontCollection().Add(TestFonts.HintedVariableFile), maximum, 1024);

        GlyphMetrics metrics = GetMetrics(font, Array.IndexOf(GlyphNames, "L"));
        string outline = LayoutOutlineRecorder.ToGlyphSpaceOutline(LayoutOutlineRecorder.Render(font, (ushort)Array.IndexOf(GlyphNames, "L"), HintingMode.None));

        await Assert.That(metrics.Bounds.Min.X).IsEqualTo(28F);
        await Assert.That(metrics.Bounds.Max.X).IsEqualTo(540F);
        await Assert.That(metrics.LeftSideBearing).IsEqualTo((short)28);
        await Assert.That(outline).StartsWith("M 28 ");
    }

    [Test]
    public async Task AnEmptyGlyphWithoutDeltasKeepsTheBearingsOfTheDefaultInstance()
    {
        // Without a 'vmtx' table the top side bearing is measured against the bounds that the default instance uses, which for an
        // empty glyph are those of glyph 0. The space has a delta for the advance on the width axis and none for its bearings.
        FontFamily family = VariationFontBytes.Load(SfntRewriter.Remove(File.ReadAllBytes(TestFonts.HintedVariableFile), "vhea", "vmtx"));
        Font face = family.CreateFont(12);
        Font instance = family.CreateFont(12, FontStyle.Regular, new FontVariation(Weight, 400), new FontVariation(Width, 192));
        int space = Array.IndexOf(GlyphNames, "space");

        GlyphMetrics defaults = GetMetrics(face, space);
        GlyphMetrics varied = GetMetrics(instance, space);

        await Assert.That(varied.AdvanceWidth).IsEqualTo((ushort)(defaults.AdvanceWidth - 32));
        await Assert.That(varied.LeftSideBearing).IsEqualTo(defaults.LeftSideBearing);
        await Assert.That(varied.TopSideBearing).IsEqualTo(defaults.TopSideBearing);
        await Assert.That(varied.Bounds).IsEqualTo(defaults.Bounds);
        await Assert.That(defaults.TopSideBearing).IsEqualTo((short)160);
    }

    [Test]
    [MethodDataSource(nameof(HintedLocations))]
    public async Task HintedOutlinesMatchFreeType(int location, int size)
    {
        // H goes to a control value that 'cvar' varies, P goes to one that 'prep' computes from it, and V goes to
        // the coordinates of the instance and to what GETINFO says about variations.
        LocationReference expected = FontTools.Locations[location];
        Font font = CreateFont(new FontCollection().Add(TestFonts.HintedVariableFile), expected, size);
        Dictionary<string, string> reference = expected.Hinted[size.ToString()];

        List<string> differences = [];
        foreach ((string glyph, string outline) in reference)
        {
            string? difference = CompareOutline(font, Array.IndexOf(GlyphNames, glyph), outline, HintingMode.Standard, HintedTolerance);
            if (difference is not null)
            {
                differences.Add($"{glyph}: {difference}");
            }
        }

        await Assert.That(differences).IsEmpty();
        await Assert.That(reference.Count).IsEqualTo(4);
    }

    [Test]
    public async Task HintingAnInstanceDoesNotChangeTheDefaultInstance()
    {
        FontFamily family = new FontCollection().Add(TestFonts.HintedVariableFile);
        LocationReference maximum = FontTools.Locations[2];
        LocationReference light = FontTools.Locations[3];
        Font instance = CreateFont(family, maximum, 16);
        Font other = CreateFont(family, light, 16);
        Font face = CreateFont(family, FontTools.Locations[0], 16);

        // Each instance has the interpreter and the control values of its own, whichever is hinted first.
        string[] first = [Hinted(instance, "H"), Hinted(other, "H"), Hinted(face, "H"), Hinted(instance, "H")];

        await Assert.That(first[0]).IsEqualTo(first[3]);
        await Assert.That(Heights(first[0])).IsEquivalentTo(new[] { 12.25 });
        await Assert.That(Heights(first[1])).IsEquivalentTo(new[] { 10.0 });
        await Assert.That(Heights(first[2])).IsEquivalentTo(new[] { 10.25 });
    }

    [Test]
    [MethodDataSource(nameof(Locations))]
    public async Task AFontWithoutCvarHintsWithTheCvtValues(int location)
    {
        // The control value 0 is 656, which is 10.25 pixels at 16 to the em, at every instance.
        LocationReference expected = FontTools.Locations[location];
        FontFamily family = VariationFontBytes.Load(SfntRewriter.Remove(File.ReadAllBytes(TestFonts.HintedVariableFile), "cvar"));

        string outline = Hinted(CreateFont(family, expected, 16), "H");

        await Assert.That(Heights(outline)).IsEquivalentTo(new[] { 10.25 });
    }

    [Test]
    [Arguments(0)]
    [Arguments(3)]
    [Arguments(100)]
    public async Task AMalformedCvarTableCountsAsAbsent(int length)
    {
        byte[] font = File.ReadAllBytes(TestFonts.HintedVariableFile);
        byte[] cvar = SfntRewriter.GetTable(font, "cvar")!;
        FontFamily family = VariationFontBytes.Load(SfntRewriter.Replace(font, "cvar", cvar.AsSpan(0, Math.Min(length, 20)).ToArray()));

        string outline = Hinted(CreateFont(family, FontTools.Locations[2], 16), "H");

        await Assert.That(Heights(outline)).IsEquivalentTo(new[] { 10.25 });
    }

    [Test]
    public async Task AGlyphWhoseGvarDataIsMalformedDrawsAtTheDefaultInstance()
    {
        // A and B, which has A as a component, draw as they do at the default instance. H, which is another glyph, is still varied.
        byte[] font = File.ReadAllBytes(TestFonts.HintedVariableFile);
        byte[] gvar = SfntRewriter.GetTable(font, "gvar")!;
        CorruptGlyph(gvar, 2);
        FontFamily family = VariationFontBytes.Load(SfntRewriter.Replace(font, "gvar", gvar));
        LocationReference maximum = FontTools.Locations[2];
        Font instance = CreateFont(family, maximum, 1024);

        await Assert.That(CompareOutline(instance, 2, FontTools.Locations[0].Glyphs["A"].Unhinted, HintingMode.None, OutlineTolerance)).IsNull();
        await Assert.That(CompareOutline(instance, 3, FontTools.Locations[0].Glyphs["B"].Unhinted, HintingMode.None, OutlineTolerance)).IsNull();
        await Assert.That(CompareOutline(instance, 6, maximum.Glyphs["H"].Unhinted, HintingMode.None, OutlineTolerance)).IsNull();
    }

    [Test]
    public async Task AGvarTableWithAMalformedHeaderCountsAsAbsent()
    {
        byte[] font = File.ReadAllBytes(TestFonts.HintedVariableFile);
        FontFamily family = VariationFontBytes.Load(SfntRewriter.Replace(font, "gvar", [0, 1, 0, 0, 0, 2]));
        Font instance = CreateFont(family, FontTools.Locations[2], 1024);

        for (int id = 0; id < GlyphNames.Length; id++)
        {
            await Assert.That(CompareOutline(instance, id, FontTools.Locations[0].Glyphs[GlyphNames[id]].Unhinted, HintingMode.None, OutlineTolerance)).IsNull();
        }
    }

    [Test]
    public async Task AGlyphThatUsesGetVariationInAFontWithoutAxesDrawsUnhinted()
    {
        // A font with no axes has no coordinates to give, so the instruction is unknown to it, as it is in any font program that
        // does not define it. The glyph keeps its outline.
        byte[] font = SfntRewriter.Remove(File.ReadAllBytes(TestFonts.HintedVariableFile), "fvar", "gvar", "cvar");
        FontFamily family = VariationFontBytes.Load(font);

        string outline = Hinted(family.CreateFont(16), "V");

        await Assert.That(Heights(outline)).IsEquivalentTo(new[] { 10.0 });
    }

    // Draws a glyph at the font size, hinted, as glyph-space commands with the y axis up.
    private static string Hinted(Font font, string glyph)
        => LayoutOutlineRecorder.ToGlyphSpaceOutline(LayoutOutlineRecorder.Render(font, (ushort)Array.IndexOf(GlyphNames, glyph), HintingMode.Standard));

    // The y coordinates of the points of an outline that are above the baseline.
    private static double[] Heights(string outline)
    {
        string[] tokens = outline.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(token => token.Length > 0 && !char.IsLetter(token[0])).ToArray();
        return [.. tokens.Where((_, index) => index % 2 == 1).Select(token => double.Parse(token, System.Globalization.CultureInfo.InvariantCulture)).Where(y => y > 5).Distinct()];
    }

    private static List<string> Compare(Font font, LocationReference expected, Func<GlyphReference, string?> select, HintingMode hinting, double tolerance)
    {
        List<string> differences = [];
        for (int id = 0; id < GlyphNames.Length; id++)
        {
            string? outline = select(expected.Glyphs[GlyphNames[id]]);
            string? difference = outline is null ? null : CompareOutline(font, id, outline, hinting, tolerance);
            if (difference is not null)
            {
                differences.Add($"{GlyphNames[id]}: {difference}");
            }
        }

        return differences;
    }

    private static string? CompareOutline(Font font, int glyphId, string outline, HintingMode hinting, double tolerance)
    {
        GlyphOutline reference = GlyphOutline.Parse(outline);
        GlyphOutline actual = GlyphOutline.Parse(LayoutOutlineRecorder.ToGlyphSpaceOutline(LayoutOutlineRecorder.Render(font, (ushort)glyphId, hinting)));
        return GlyphOutline.Compare(reference, GlyphOutline.Identity, actual, GlyphOutline.Identity, tolerance);
    }

    // Makes the variation data of a glyph claim more tuples than it has room for.
    private static void CorruptGlyph(byte[] gvar, int glyphId)
    {
        bool longOffsets = (BinaryPrimitives.ReadUInt16BigEndian(gvar.AsSpan(14)) & 1) != 0;
        uint dataOffset = BinaryPrimitives.ReadUInt32BigEndian(gvar.AsSpan(16));
        uint start = longOffsets
            ? BinaryPrimitives.ReadUInt32BigEndian(gvar.AsSpan(20 + (4 * glyphId)))
            : BinaryPrimitives.ReadUInt16BigEndian(gvar.AsSpan(20 + (2 * glyphId))) * 2U;
        BinaryPrimitives.WriteUInt16BigEndian(gvar.AsSpan((int)(dataOffset + start)), 0x0FFF);
    }

    private static Font CreateFont(FontFamily family, LocationReference location, float size)
    {
        Font font = family.CreateFont(size, FontStyle.Regular, new FontVariation(Weight, location.User["wght"]), new FontVariation(Width, location.User["wdth"]));
        if (!font.FontMetrics.Coordinates.Values.SequenceEqual(location.Coordinates.AsSpan()))
        {
            throw new InvalidOperationException($"{location.Name} has the coordinates {string.Join(", ", font.FontMetrics.Coordinates.Values.ToArray())}.");
        }

        return font;
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
        public short[] ControlValues { get; set; } = [];

        public int[] HintedSizes { get; set; } = [];

        public List<LocationReference> Locations { get; set; } = [];
    }

    private sealed class LocationReference
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<string, float> User { get; set; } = [];

        public short[] Coordinates { get; set; } = [];

        public short[] ControlValues { get; set; } = [];

        public Dictionary<string, GlyphReference> Glyphs { get; set; } = [];

        public Dictionary<string, Dictionary<string, string>> Hinted { get; set; } = [];
    }

    private sealed class GlyphReference
    {
        public string Unhinted { get; set; } = string.Empty;

        public string? Outline { get; set; }

        public float[]? Bounds { get; set; }

        public int AdvanceWidth { get; set; }

        public int AdvanceHeight { get; set; }

        public int? LeftSideBearing { get; set; }

        public int? TopSideBearing { get; set; }

        public HarfBuzzReference HarfBuzz { get; set; } = new();
    }

    private sealed class HarfBuzzReference
    {
        public int AdvanceWidth { get; set; }

        public int AdvanceHeight { get; set; }

        public int LeftSideBearing { get; set; }

        public int TopSideBearing { get; set; }

        public int YMax { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }
}
