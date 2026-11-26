// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static PinataLabs.Typography.Tests.Tables.Variations.MetricsVariationsReference;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// The variation instances of the two variable test fonts and of two built fonts have the global metrics that fontTools
/// and HarfBuzz give for the same F2Dot14 coordinates: the value of the table that holds a metric, plus the MVAR delta of
/// its tag, rounded once. The variable test fonts have MVAR records for the strikeout position and the heights only, so the
/// built fonts, which have a record for every metric that the library reads, cover the rest.
/// tests/tools/generate_metrics_variation_reference.py builds the fonts and writes the reference.
/// </summary>
public class GlobalMetricsReferenceTests
{
    public static IEnumerable<object[]> FontsAndLocations => GetLocations(_ => true);

    [Test]
    [MethodDataSource(nameof(FontsAndLocations))]
    public async Task GlobalMetricsMatchFontTools(string fontName, string locationName)
    {
        FontReference reference = Fonts[fontName];
        int index = IndexOf(reference, locationName);
        FontMetrics metrics = CreateFont(fontName, reference.Locations[index]).FontMetrics;
        Dictionary<string, int> expected = reference.Metrics[index];
        Dictionary<string, int> actual = GetGlobalMetrics(metrics);
        List<string> differences = [];

        foreach ((string name, int value) in expected)
        {
            if (!actual.TryGetValue(name, out int found) || found != value)
            {
                differences.Add($"{name}: {(actual.TryGetValue(name, out int missing) ? missing.ToString() : "missing")}, expected {value}");
            }
        }

        await Assert.That(actual.Keys).IsEquivalentTo(expected.Keys);
        await Assert.That(differences).IsEmpty();
    }

    [Test]
    public async Task TheReferenceVariesEveryMetricThatItChecks()
    {
        // A reference whose locations all give the values of the default instance would test nothing.
        FontReference built = Fonts["VariationMetrics.ttf"];
        List<string> constant = [.. built.Metrics[0].Keys.Where(name => built.Metrics.All(metrics => metrics[name] == built.Metrics[0][name]))];

        await Assert.That(constant).IsEmpty();
    }

    [Test]
    public async Task TheHhheaValuesAndTheTypoValuesAreOnlyTakenWhenTheirRulesChooseThem()
    {
        // The two built fonts differ in the USE_TYPO_METRICS bit alone, so the metrics that come from hhea or from the typo
        // values differ, and the others do not.
        FontReference hhea = Fonts["VariationMetrics.ttf"];
        FontReference typo = Fonts["VariationMetricsTypo.ttf"];

        await Assert.That(typo.Metrics[0]["ascender"]).IsNotEqualTo(hhea.Metrics[0]["ascender"]);
        await Assert.That(typo.Metrics[0]["lineGap"]).IsNotEqualTo(hhea.Metrics[0]["lineGap"]);
        await Assert.That(typo.Metrics[0]["verticalAscender"]).IsEqualTo(hhea.Metrics[0]["verticalAscender"]);
        await Assert.That(typo.Metrics[0]["underlinePosition"]).IsEqualTo(hhea.Metrics[0]["underlinePosition"]);
    }

    private static Dictionary<string, int> GetGlobalMetrics(FontMetrics metrics)
        => new()
        {
            ["ascender"] = metrics.HorizontalMetrics.Ascender,
            ["descender"] = metrics.HorizontalMetrics.Descender,
            ["lineGap"] = metrics.HorizontalMetrics.LineGap,
            ["lineHeight"] = metrics.HorizontalMetrics.LineHeight,
            ["verticalAscender"] = metrics.VerticalMetrics.Ascender,
            ["verticalDescender"] = metrics.VerticalMetrics.Descender,
            ["verticalLineGap"] = metrics.VerticalMetrics.LineGap,
            ["verticalLineHeight"] = metrics.VerticalMetrics.LineHeight,
            ["subscriptXSize"] = metrics.SubscriptXSize,
            ["subscriptYSize"] = metrics.SubscriptYSize,
            ["subscriptXOffset"] = metrics.SubscriptXOffset,
            ["subscriptYOffset"] = metrics.SubscriptYOffset,
            ["superscriptXSize"] = metrics.SuperscriptXSize,
            ["superscriptYSize"] = metrics.SuperscriptYSize,
            ["superscriptXOffset"] = metrics.SuperscriptXOffset,
            ["superscriptYOffset"] = metrics.SuperscriptYOffset,
            ["strikeoutSize"] = metrics.StrikeoutSize,
            ["strikeoutPosition"] = metrics.StrikeoutPosition,
            ["underlinePosition"] = metrics.UnderlinePosition,
            ["underlineThickness"] = metrics.UnderlineThickness,
        };
}
