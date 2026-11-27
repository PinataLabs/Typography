// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tests.FontPrograms;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// The glyphs of Teko-Variable.ttf at a variation instance have the outlines, the bounds, the left side bearings and the
/// advances that fontTools gives, except for the advances of the phantom points, which HarfBuzz gives because it takes the phantom
/// points of a composite from the component that has the USE_MY_METRICS flag, as FreeType does, where fontTools does not.
/// fontTools applies the 'gvar' deltas with interpolation of untouched points, the deltas of the component offsets of composite
/// glyphs, and the deltas of the phantom points. Teko has an HVAR table, which gives the advances of a font that has one, and
/// the phantom points give them for a font that has none. The reference holds both, for the font as it is and for a copy
/// without HVAR. It has every glyph of the font at the default instance, the SemiBold named instance, the maximum and a
/// user location, and the outline of a sample of the glyphs that has the Basic Latin glyphs, composites and empty glyphs.
/// tests/tools/generate_gvar_reference.py writes it.
/// </summary>
public class GvarReferenceTests
{
    // The library draws in single precision, so a point is a hundredth of a font unit from the reference at worst.
    private const double OutlineTolerance = 0.01;
    private const float BoundsTolerance = 0.01F;

    // The font has one more glyph than it has horizontal metrics. The base reads the advance of such a glyph from the
    // first metric, where fontTools and the specification read the last, so the advance test leaves the glyph out.
    private const int HorizontalMetricCount = 1024;

    // The glyphs whose HVAR advance differs from the advance of their phantom points away from the default instance:
    // Edieresis, Odieresis, Ohungarumlaut, Uhungarumlaut, Ydieresis and ydieresis. Each is a composite of a mark and a letter
    // that has USE_MY_METRICS. At the default instance, hmtx gives it the advance of the letter, and so do HVAR and the phantom
    // points. Away from it HVAR has a delta for the composite, and 'gvar' moves the phantom points of the letter, which are the
    // phantom points of the composite, and the font gives the two different deltas.
    private static readonly int[] CompositesWhereHvarAndPhantomPointsDiffer = [24, 61, 63, 88, 97, 199];

    private static readonly Tag Weight = Tag.Parse("wght");

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly FontReference Teko = JsonSerializer.Deserialize<Reference>(File.ReadAllText(TestFonts.GvarFontToolsReference), Options)!.Fonts["Teko-Variable.ttf"];

    public static IEnumerable<object[]> Instances => Teko.Instances.Select((_, index) => new object[] { index });

    [Test]
    [MethodDataSource(nameof(Instances))]
    public async Task OutlinesMatchFontTools(int instance)
    {
        InstanceReference expected = Teko.Instances[instance];
        Font font = CreateFont(expected);
        List<string> differences = [];

        foreach ((string glyph, string outline) in expected.Outlines)
        {
            GlyphOutline reference = GlyphOutline.Parse(outline);
            GlyphOutline actual = GlyphOutline.Parse(LayoutOutlineRecorder.ToGlyphSpaceOutline(LayoutOutlineRecorder.Render(font, ushort.Parse(glyph))));
            string? difference = GlyphOutline.Compare(reference, GlyphOutline.Identity, actual, GlyphOutline.Identity, OutlineTolerance);
            if (difference is not null)
            {
                differences.Add($"glyph {glyph}: {difference}");
            }
        }

        await Assert.That(expected.Outlines.Count).IsGreaterThan(100);
        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Instances))]
    public async Task BoundsMatchFontTools(int instance)
    {
        InstanceReference expected = Teko.Instances[instance];
        Font font = CreateFont(expected);
        List<string> differences = [];

        for (int glyph = 0; glyph < Teko.GlyphCount; glyph++)
        {
            float[]? box = expected.Bounds[glyph];
            if (box is null)
            {
                continue;
            }

            Bounds actual = GetMetrics(font, glyph).Bounds;
            if (Math.Abs(actual.Min.X - box[0]) > BoundsTolerance || Math.Abs(actual.Min.Y - box[1]) > BoundsTolerance
                || Math.Abs(actual.Max.X - box[2]) > BoundsTolerance || Math.Abs(actual.Max.Y - box[3]) > BoundsTolerance)
            {
                differences.Add($"glyph {glyph}: {actual.Min} {actual.Max}, expected [{string.Join(", ", box)}]");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Instances))]
    public async Task LeftSideBearingsMatchFontTools(int instance)
    {
        InstanceReference expected = Teko.Instances[instance];
        Font font = CreateFont(expected);
        List<string> differences = [];

        for (int glyph = 0; glyph < Teko.GlyphCount; glyph++)
        {
            short actual = GetMetrics(font, glyph).LeftSideBearing;
            if (actual != expected.LeftSideBearings[glyph])
            {
                differences.Add($"glyph {glyph}: {actual}, expected {expected.LeftSideBearings[glyph]}");
            }
        }

        await Assert.That(differences).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Instances))]
    public async Task AdvancesComeFromHvarWhenTheFontHasIt(int instance)
    {
        // Teko as it is has HVAR, which HarfBuzz and fontTools read, and it wins over the phantom points.
        InstanceReference expected = Teko.Instances[instance];
        Font font = CreateFont(expected);

        await Assert.That(GetAdvanceDifferences(font, expected.HvarAdvances)).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Instances))]
    public async Task AdvancesComeFromThePhantomPointsWhenTheFontHasNoHvar(int instance)
    {
        // Teko has an HVAR table, so HarfBuzz reads the reference advances from a copy that lacks it, and so does this test.
        InstanceReference expected = Teko.Instances[instance];
        byte[] withoutHvar = SfntRewriter.Remove(File.ReadAllBytes(TestFonts.TekoVariableFile), "HVAR");
        Font font = CreateFont(VariationFontBytes.Load(withoutHvar), expected);

        await Assert.That(GetAdvanceDifferences(font, expected.Advances)).IsEmpty();
    }

    [Test]
    [Arguments(1, "the major version")]
    [Arguments(1, "the store")]
    [Arguments(2, "the major version")]
    [Arguments(2, "the store")]
    public async Task AMalformedHvarCountsAsAbsentSoThePhantomPointsGiveTheAdvances(int instance, string defect)
    {
        InstanceReference expected = Teko.Instances[instance];
        byte[] bytes = File.ReadAllBytes(TestFonts.TekoVariableFile);
        Span<byte> hvar = VariationFontBytes.Table(bytes, "HVAR");
        if (defect == "the major version")
        {
            BinaryPrimitives.WriteUInt16BigEndian(hvar, 2);
        }
        else
        {
            // Offset32 of the store.
            BinaryPrimitives.WriteUInt32BigEndian(hvar.Slice(4), (uint)hvar.Length + 1);
        }

        Font font = CreateFont(VariationFontBytes.Load(bytes), expected);

        await Assert.That(GetAdvanceDifferences(font, expected.Advances)).IsEmpty();
    }

    [Test]
    [MethodDataSource(nameof(Instances))]
    public async Task HvarAndThePhantomPointsGiveTheSameAdvancesExceptForSixComposites(int instance)
    {
        // Teko has both sources, and they agree for every glyph but the six composites listed above. The default instance
        // is the face, which draws from hmtx, so the two agree for all of them.
        InstanceReference expected = Teko.Instances[instance];
        Font withHvar = CreateFont(expected);
        byte[] withoutHvar = SfntRewriter.Remove(File.ReadAllBytes(TestFonts.TekoVariableFile), "HVAR");
        Font withoutTable = CreateFont(VariationFontBytes.Load(withoutHvar), expected);

        int[] differing = [.. Enumerable.Range(0, HorizontalMetricCount)
            .Where(glyph => GetMetrics(withHvar, glyph).AdvanceWidth != GetMetrics(withoutTable, glyph).AdvanceWidth)];

        await Assert.That(differing).IsEquivalentTo(expected.Coordinates.All(value => value == 0) ? [] : CompositesWhereHvarAndPhantomPointsDiffer);
    }

    [Test]
    public async Task AnInstanceVariesTheGlyphsItDraws()
    {
        // The reference is of no use if the default and the maximum draw the same.
        InstanceReference light = Teko.Instances[0];
        InstanceReference bold = Teko.Instances[2];

        await Assert.That(light.Advances.SequenceEqual(bold.Advances)).IsFalse();
        await Assert.That(light.Outlines.Count(pair => pair.Value != bold.Outlines[pair.Key])).IsGreaterThan(100);
    }

    private static Font CreateFont(InstanceReference instance)
        => CreateFont(new FontCollection().Add(TestFonts.TekoVariableFile), instance);

    private static List<string> GetAdvanceDifferences(Font font, int[] expected)
    {
        List<string> differences = [];
        for (int glyph = 0; glyph < HorizontalMetricCount; glyph++)
        {
            int actual = GetMetrics(font, glyph).AdvanceWidth;
            if (actual != expected[glyph])
            {
                differences.Add($"glyph {glyph}: {actual}, expected {expected[glyph]}");
            }
        }

        return differences;
    }

    private static Font CreateFont(FontFamily family, InstanceReference instance)
    {
        // The font is at one unit to the font unit, so a recorded outline is in font units.
        Font font = family.CreateFont(1000, FontStyle.Regular, new FontVariation(Weight, instance.User["wght"]));
        if (!font.FontMetrics.Coordinates.Values.SequenceEqual(instance.Coordinates.AsSpan()))
        {
            throw new InvalidOperationException($"{instance.Name} has the coordinates {string.Join(", ", font.FontMetrics.Coordinates.Values.ToArray())}.");
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
        public Dictionary<string, FontReference> Fonts { get; set; } = [];
    }

    private sealed class FontReference
    {
        public int GlyphCount { get; set; }

        public InstanceReference[] Instances { get; set; } = [];
    }

    private sealed class InstanceReference
    {
        public string Name { get; set; } = string.Empty;

        public Dictionary<string, float> User { get; set; } = [];

        public short[] Coordinates { get; set; } = [];

        public float[]?[] Bounds { get; set; } = [];

        public int[] Advances { get; set; } = [];

        public int[] HvarAdvances { get; set; } = [];

        public int[] LeftSideBearings { get; set; } = [];

        public Dictionary<string, string> Outlines { get; set; } = [];
    }
}
