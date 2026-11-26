// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
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
/// advances that fontTools gives. fontTools applies the 'gvar' deltas with interpolation of untouched points, the deltas of
/// the component offsets of composite glyphs, and the deltas of the phantom points. The reference has every glyph of the font
/// at the default instance, the SemiBold named instance, the maximum and a user location, and the outline of a sample of
/// the glyphs that has the Basic Latin glyphs, composites and empty glyphs. tests/tools/generate_gvar_reference.py writes it.
/// </summary>
public class GvarReferenceTests
{
    // The library draws in single precision, so a point is a hundredth of a font unit from the reference at worst.
    private const double OutlineTolerance = 0.01;
    private const float BoundsTolerance = 0.01F;

    // The font has one more glyph than it has horizontal metrics. The base reads the advance of such a glyph from the
    // first metric, where fontTools and the specification read the last, so the advance test leaves the glyph out.
    private const int HorizontalMetricCount = 1024;

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
    public async Task AdvancesComeFromThePhantomPointsWhenTheFontHasNoHvar(int instance)
    {
        // Teko has an HVAR table, so fontTools reads the reference advances from a copy that lacks it, and so does this test.
        InstanceReference expected = Teko.Instances[instance];
        byte[] withoutHvar = SfntRewriter.Remove(File.ReadAllBytes(TestFonts.TekoVariableFile), "HVAR");
        Font font = CreateFont(VariationFontBytes.Load(withoutHvar), expected);
        List<string> differences = [];

        for (int glyph = 0; glyph < HorizontalMetricCount; glyph++)
        {
            ushort actual = GetMetrics(font, glyph).AdvanceWidth;
            if (actual != expected.Advances[glyph])
            {
                differences.Add($"glyph {glyph}: {actual}, expected {expected.Advances[glyph]}");
            }
        }

        await Assert.That(differences).IsEmpty();
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

        public int[] LeftSideBearings { get; set; } = [];

        public Dictionary<string, string> Outlines { get; set; } = [];
    }
}
