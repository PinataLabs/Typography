// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using static PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos.GposVariationReference;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

/// <summary>
/// The kerning and the mark anchors of the two variable test fonts point to <c>VariationIndex</c> device tables, which
/// add a delta of the GDEF Item Variation Store to a value record or an anchor (issue #129). A variation instance
/// positions glyphs as HarfBuzz positions them at the same weight, at the default instance, at masters and between them,
/// where the deltas are fractional. Teko has mark anchors with deltas and no kerning. Source Sans 3 has both, and
/// mark-to-mark anchors. GposAnchorVariations.ttf has a delta of a known size in every kind of anchor and in value records,
/// including mark-to-ligature and cursive attachment, which the other two lack, and tests each delta against each other
/// one at the scalars where rounding the sum and rounding the parts differ.
/// </summary>
public class GposVariationTests
{
    private static readonly Tag Weight = Tag.Parse("wght");

    [Test]
    [Arguments("Teko/Teko-Variable.ttf", "mark")]
    [Arguments("SourceSans3/SourceSans3VF-Upright.otf", "pair")]
    [Arguments("SourceSans3/SourceSans3VF-Upright.otf", "static")]
    [Arguments("SourceSans3/SourceSans3VF-Upright.otf", "mark")]
    [Arguments("SourceSans3/SourceSans3VF-Upright.otf", "chain")]
    [Arguments("Variations/GposAnchorVariations.ttf", "single")]
    [Arguments("Variations/GposAnchorVariations.ttf", "mark")]
    [Arguments("Variations/GposAnchorVariations.ttf", "markmark")]
    [Arguments("Variations/GposAnchorVariations.ttf", "ligature")]
    [Arguments("Variations/GposAnchorVariations.ttf", "ligature0")]
    [Arguments("Variations/GposAnchorVariations.ttf", "cursive")]
    public async Task InstancesPositionAsHarfBuzzPositionsThem(string path, string kind)
    {
        (int[] weights, List<Row> rows) = Get(path, kind);
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(path));

        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        float size = 72 * family.CreateFont(1).FontMetrics.UnitsPerEm;
        List<string> different = [];
        for (int weight = 0; weight < weights.Length; weight++)
        {
            Font font = family.CreateFont(size, FontStyle.Regular, new FontVariation(Weight, weights[weight]));
            foreach (Row row in rows)
            {
                Positioned[] expected = GposOnly(row.Locations[weight]);
                Positioned[] actual = GposOnly(Shape(font, row.Text));
                if (!expected.SequenceEqual(actual))
                {
                    different.Add($"wght {weights[weight]} [{CodePoints(row.Text)}] expected [{string.Join(' ', expected)}] actual [{string.Join(' ', actual)}]");
                }
            }
        }

        await Assert.That(rows).IsNotEmpty();
        await Assert.That(different).IsEmpty();
    }
}
