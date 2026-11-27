// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests.Tables.General;

/// <summary>
/// The NoOs2 fonts have no <c>OS/2</c> table, as AppleGothic and AppleMyungjo on macOS have none (issue #148).
/// One face has TrueType outlines and one has CFF outlines. Each loads and takes its style from <c>head.macStyle</c>
/// and its metrics from <c>hhea</c>, <c>post</c> and the em, as HarfBuzz does.
/// </summary>
public class MissingOs2TableTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.NoOs2HarfBuzzReference),
        Options)!;

    public static IEnumerable<string> Files() => HarfBuzz.Rows.Select(r => r.File);

    [Test]
    public async Task BothFacesJoinOneFamily()
    {
        FontFamily family = Family();

        await Assert.That(family.Faces.Count).IsEqualTo(2);
    }

    [Test]
    [MethodDataSource(nameof(Files))]
    public async Task StyleMatchesHarfBuzz(string file)
    {
        ReferenceFace expected = HarfBuzz.Rows.Single(r => r.File == file);
        FontStyle style = new(
            new FontWeight(expected.StyleValues.Weight),
            new FontWidth(expected.StyleValues.Width),
            expected.StyleValues.Italic == 1F ? FontSlope.Italic : FontSlope.Normal);

        FontMetrics face = Family().MatchFace(style);

        await Assert.That(Path.GetFileName(((FileFontMetrics)face).Path)).IsEqualTo(file);
        await Assert.That(face.Description.StyleRange.MinWeight.Value).IsEqualTo(expected.StyleValues.Weight);
        await Assert.That(face.Description.StyleRange.MinWidth.Percent).IsEqualTo(expected.StyleValues.Width);
        await Assert.That(face.Description.StyleRange.HasItalic).IsEqualTo(expected.StyleValues.Italic == 1F);
    }

    [Test]
    [MethodDataSource(nameof(Files))]
    public async Task MetricsMatchHarfBuzz(string file)
    {
        ReferenceMetrics expected = HarfBuzz.Rows.Single(r => r.File == file).Metrics;
        FontMetrics metrics = Load(file).FontMetrics;

        await Assert.That((int)metrics.HorizontalMetrics.Ascender).IsEqualTo(expected.Ascender);
        await Assert.That((int)metrics.HorizontalMetrics.Descender).IsEqualTo(expected.Descender);
        await Assert.That((int)metrics.HorizontalMetrics.LineGap).IsEqualTo(expected.LineGap);
        await Assert.That((int)metrics.StrikeoutSize).IsEqualTo(expected.StrikeoutSize);
        await Assert.That((int)metrics.StrikeoutPosition).IsEqualTo(expected.StrikeoutPosition);
        await Assert.That((int)metrics.UnderlineThickness).IsEqualTo(expected.UnderlineThickness);
        await Assert.That((int)metrics.UnderlinePosition).IsEqualTo(expected.UnderlinePosition);
        await Assert.That((int)metrics.SubscriptXSize).IsEqualTo(expected.SubscriptXSize);
        await Assert.That((int)metrics.SubscriptYSize).IsEqualTo(expected.SubscriptYSize);
        await Assert.That((int)metrics.SubscriptXOffset).IsEqualTo(expected.SubscriptXOffset);
        await Assert.That((int)metrics.SubscriptYOffset).IsEqualTo(expected.SubscriptYOffset);
        await Assert.That((int)metrics.SuperscriptXSize).IsEqualTo(expected.SuperscriptXSize);
        await Assert.That((int)metrics.SuperscriptYSize).IsEqualTo(expected.SuperscriptYSize);
        await Assert.That((int)metrics.SuperscriptXOffset).IsEqualTo(expected.SuperscriptXOffset);
        await Assert.That((int)metrics.SuperscriptYOffset).IsEqualTo(expected.SuperscriptYOffset);
    }

    [Test]
    [MethodDataSource(nameof(Files))]
    public async Task TextIsLaidOutAsHarfBuzzShapesIt(string file)
    {
        // One em is 1000 font units, so at 1000 pixels per em a pixel is a font unit.
        Font font = Load(file);

        foreach (ReferenceText expected in HarfBuzz.Rows.Single(r => r.File == file).Texts)
        {
            GlyphRenderer renderer = new();
            TextRenderer.RenderTextTo(renderer, expected.Text, new TextOptions(font));
            FontRectangle advance = TextMeasurer.MeasureAdvance(expected.Text, new TextOptions(font));

            await Assert.That(renderer.GlyphKeys.Select(k => (int)k.GlyphId)).IsEquivalentTo(expected.GlyphIds);
            await Assert.That(advance.Width).IsEqualTo(expected.Advance);
        }
    }

    private static FontFamily Family()
    {
        FontCollection collection = new();
        foreach (string file in Files())
        {
            collection.Add(TestFonts.NoOs2File(file["NoOs2-".Length..]));
        }

        return collection.Get(HarfBuzz.Family);
    }

    private static Font Load(string file)
        => new FontCollection().Add(TestFonts.NoOs2File(file["NoOs2-".Length..])).CreateFont(1000);

    private sealed class Reference
    {
        public string Family { get; set; } = string.Empty;

        public ReferenceFace[] Rows { get; set; } = [];
    }

    private sealed class ReferenceFace
    {
        public string File { get; set; } = string.Empty;

        public ReferenceStyle StyleValues { get; set; } = new();

        public ReferenceMetrics Metrics { get; set; } = new();

        public ReferenceText[] Texts { get; set; } = [];
    }

    private sealed class ReferenceStyle
    {
        public float Weight { get; set; }

        public float Width { get; set; }

        public float Italic { get; set; }
    }

    private sealed class ReferenceMetrics
    {
        public int Ascender { get; set; }

        public int Descender { get; set; }

        public int LineGap { get; set; }

        public int StrikeoutSize { get; set; }

        public int StrikeoutPosition { get; set; }

        public int UnderlineThickness { get; set; }

        public int UnderlinePosition { get; set; }

        public int SubscriptXSize { get; set; }

        public int SubscriptYSize { get; set; }

        public int SubscriptXOffset { get; set; }

        public int SubscriptYOffset { get; set; }

        public int SuperscriptXSize { get; set; }

        public int SuperscriptYSize { get; set; }

        public int SuperscriptXOffset { get; set; }

        public int SuperscriptYOffset { get; set; }
    }

    private sealed class ReferenceText
    {
        public string Text { get; set; } = string.Empty;

        public int[] GlyphIds { get; set; } = [];

        public int Advance { get; set; }
    }
}
