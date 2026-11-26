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
using PinataLabs.Typography.Tests.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic;

/// <summary>
/// An instance of a variable font uses the feature tables of the first FeatureVariations record whose conditions hold for
/// its coordinates, in GSUB and in GPOS (issue #131). FeatureVariations.ttf has records on both tables, with the condition
/// formats 1 to 5, and HarfBuzz shapes the texts at locations on both sides of the boundary of each condition, and on it.
/// tests/tools/generate_feature_variations_font.py builds the font and writes the reference.
/// </summary>
public class FeatureVariationsTests
{
    private const string FontPath = "Variations/FeatureVariations.ttf";

    private static readonly Tag Weight = Tag.Parse("wght");
    private static readonly Tag Width = Tag.Parse("wdth");

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static readonly Reference HarfBuzz = JsonSerializer.Deserialize<Reference>(
        File.ReadAllText(TestFonts.GetPath("Variations/FeatureVariations.harfbuzz.json")),
        Options)!;

    [Test]
    public async Task InstancesShapeAsHarfBuzzShapesThem()
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(FontPath));
        List<string> different = [];
        foreach (JsonElement[] row in HarfBuzz.Rows)
        {
            string text = row[0].GetString()!;
            float weight = row[1].GetSingle();
            float width = row[2].GetSingle();
            (int Id, int X, int Y)[] expected = row[3].EnumerateArray().Select(g => (g[0].GetInt32(), g[1].GetInt32(), g[2].GetInt32())).ToArray();

            (int Id, int X, int Y)[] actual = Shape(CreateFont(family, weight, width), text, out int advance);
            if (!actual.SequenceEqual(expected) || advance != row[4].GetInt32())
            {
                different.Add($"wght {weight} wdth {width} [{text}] expected [{string.Join(' ', expected)}] {row[4].GetInt32()} actual [{string.Join(' ', actual)}] {advance}");
            }
        }

        await Assert.That(HarfBuzz.Rows).IsNotEmpty();
        await Assert.That(different.Take(10)).IsEmpty();
    }

    [Test]
    public async Task GsubRvrnSubstitutesGlyphsFromTheBoundaryOfItsCondition()
    {
        // rvrn gives C.rvrn from wght 650, which is 0.5 normalised and is in the range, and B.rvrn from wdth 112.5.
        // The glyphs are B 3, C 4, B.rvrn 6 and C.rvrn 7.
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(FontPath));

        await Assert.That(Glyphs(family, 649, 100, "BC")).IsEquivalentTo(new[] { 3, 4 });
        await Assert.That(Glyphs(family, 650, 100, "BC")).IsEquivalentTo(new[] { 3, 7 });
        await Assert.That(Glyphs(family, 400, 112, "BC")).IsEquivalentTo(new[] { 3, 4 });
        await Assert.That(Glyphs(family, 400, 112.5F, "BC")).IsEquivalentTo(new[] { 6, 4 });
        await Assert.That(Glyphs(family, 650, 112.5F, "BC")).IsEquivalentTo(new[] { 6, 7 });
    }

    [Test]
    public async Task GposKernSwapsItsLookupsFromTheBoundaryOfItsCondition()
    {
        // The default lookup kerns A V by -100. wght 650 swaps in the lookup that kerns it by -300, and wght 250 the one that
        // kerns it by -20. wght 500 and wdth 112.5 give both of the lookups of the second record, so the pair is kerned by -600.
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(FontPath));

        await Assert.That(Kerning(family, 600, 100)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 649, 100)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 650, 100)).IsEqualTo(-300);
        await Assert.That(Kerning(family, 499, 112.5F)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 500, 112.5F)).IsEqualTo(-600);
        await Assert.That(Kerning(family, 500, 112)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 251, 100)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 250, 100)).IsEqualTo(-20);
        await Assert.That(Kerning(family, 100, 75)).IsEqualTo(-20);
    }

    [Test]
    public async Task TheFaceSelectsFromTheDefaultCoordinates()
    {
        // The default instance is at wght 0 and wdth 0, which the conditions of the sixth record hold, as they do for HarfBuzz.
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(FontPath));

        await Assert.That(CreateFont(family, 400, 100).FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(Kerning(family, 400, 100)).IsEqualTo(-430);
    }

    [Test]
    public async Task TheRecordThatComesFirstDecides()
    {
        // wght 700 and wdth 125 meet the conditions of the first and the second record. The first one decides.
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(FontPath));

        await Assert.That(Kerning(family, 700, 125)).IsEqualTo(-300);
    }

    [Test]
    public async Task TheConditionFormatsOfVersion19HoldAsTheSpecificationSays()
    {
        // The records 4 to 7 need wght -0.25 to 0.125, which is 325 to 462.5, and they kern A V by -410, -420, -430 and -440.
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(FontPath));

        // Format 2, a value of the GDEF store: -50 plus 100 times the normalised wdth, which must be above 0 after
        // the sum is truncated to a whole number. 112.5 is 0.5, so the sum is 0, and 113 is 0.52.
        await Assert.That(Kerning(family, 400, 113)).IsEqualTo(-410);
        await Assert.That(Kerning(family, 400, 125)).IsEqualTo(-410);

        // Format 3, AND: wdth 0.25 to 1 and wght -0.25 to 0.125, from the third record on.
        await Assert.That(Kerning(family, 400, 112.5F)).IsEqualTo(-420);
        await Assert.That(Kerning(family, 400, 106.25F)).IsEqualTo(-420);
        await Assert.That(Kerning(family, 400, 106)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 325, 107)).IsEqualTo(-420);
        await Assert.That(Kerning(family, 462.5F, 107)).IsEqualTo(-420);
        await Assert.That(Kerning(family, 324, 107)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 463, 107)).IsEqualTo(-100);

        // Format 4, OR: wdth -1 to -0.75, or wdth -0.25 to 0.125.
        await Assert.That(Kerning(family, 400, 75)).IsEqualTo(-430);
        await Assert.That(Kerning(family, 400, 81.25F)).IsEqualTo(-430);
        await Assert.That(Kerning(family, 400, 93.75F)).IsEqualTo(-430);
        await Assert.That(Kerning(family, 400, 103.125F)).IsEqualTo(-430);
        await Assert.That(Kerning(family, 400, 104)).IsEqualTo(-100);

        // Format 5, NOT: not wdth -0.5 to 0.5, which holds below -0.5 here, because the earlier records have the rest.
        await Assert.That(Kerning(family, 400, 82)).IsEqualTo(-440);
        await Assert.That(Kerning(family, 400, 87)).IsEqualTo(-440);
        await Assert.That(Kerning(family, 400, 87.5F)).IsEqualTo(-100);
    }

    [Test]
    public async Task AnInstanceSelectsItsFeatureTablesOnce()
    {
        FontFamily family = new FontCollection().Add(TestFonts.GetPath(FontPath));
        FontMetrics heavy = CreateFont(family, 900, 100).FontMetrics;
        FontMetrics light = CreateFont(family, 300, 100).FontMetrics;
        FontMetrics other = CreateFont(family, 350, 100).FontMetrics;

        // Instances that no record selects for share the table of the face, and a record gives one table.
        await Assert.That(heavy.GetGSubTable()).IsSameReferenceAs(heavy.GetGSubTable());
        await Assert.That(heavy.GetGSubTable()).IsNotSameReferenceAs(light.GetGSubTable());
        await Assert.That(light.GetGSubTable()).IsSameReferenceAs(other.GetGSubTable());
        await Assert.That(light.GetGSubTable()).IsSameReferenceAs(family.Faces[0].GetGSubTable());
    }

    [Test]
    public async Task ARecordWithAnAxisThatTheFontDoesNotHaveSeesTheDefault()
    {
        // The first condition of the first GPOS record is changed to the axis 9, with the range -1 to 1. The font has two
        // axes, so the coordinate is 0 and the record holds everywhere, even at the default instance.
        byte[] font = VariationFontBytes.Read(FontPath);
        Span<byte> condition = GposCondition(font, 0, 0);
        BinaryPrimitives.WriteUInt16BigEndian(condition.Slice(2), 9);
        BinaryPrimitives.WriteInt16BigEndian(condition.Slice(4), -16384);
        BinaryPrimitives.WriteInt16BigEndian(condition.Slice(6), 16384);
        FontFamily family = VariationFontBytes.Load(font);

        await Assert.That(Kerning(family, 400, 100)).IsEqualTo(-300);
        await Assert.That(Kerning(family, 100, 75)).IsEqualTo(-300);
    }

    [Test]
    public async Task AConditionOfAnUnknownFormatNeverHolds()
    {
        // The condition of the first GPOS record gets the format 9. It does not hold, and the other records still do.
        byte[] font = VariationFontBytes.Read(FontPath);
        BinaryPrimitives.WriteUInt16BigEndian(GposCondition(font, 0, 0), 9);
        FontFamily family = VariationFontBytes.Load(font);

        await Assert.That(Kerning(family, 900, 100)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 100, 75)).IsEqualTo(-20);
    }

    [Test]
    [Arguments("the offset of the table is not in the table")]
    [Arguments("the table has another major version")]
    [Arguments("the record count is more than the table holds")]
    [Arguments("the offset of a condition set is not in the table")]
    [Arguments("the offset of a condition is not in the table")]
    [Arguments("the offset of a substitution table is not in the table")]
    [Arguments("the offset of an alternate feature is not in the table")]
    [Arguments("the substitution table has another major version")]
    [Arguments("an alternate feature has more lookups than the table holds")]
    public async Task AMalformedTableCountsAsAbsent(string malformation)
    {
        // The records of the GPOS table are all gone, including the ones that the malformation did not touch: the light
        // record would kern A V by -20 at wght 100, and the heavy record by -300 at wght 900.
        byte[] font = VariationFontBytes.Read(FontPath);
        Malform(font, malformation);
        FontFamily family = VariationFontBytes.Load(font);

        await Assert.That(Kerning(family, 100, 75)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 900, 125)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 400, 100)).IsEqualTo(-100);
    }

    [Test]
    public async Task ARecordWithNoSubstitutionTableHoldsAndChangesNothing()
    {
        // The first record has no substitution table. It holds at wght 900, so the later records do not, and A V keeps its default kerning.
        byte[] font = VariationFontBytes.Read(FontPath);
        Span<byte> gpos = VariationFontBytes.Table(font, "GPOS");
        int variations = (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(10));
        BinaryPrimitives.WriteUInt32BigEndian(gpos.Slice(variations + 12), 0);
        FontFamily family = VariationFontBytes.Load(font);

        await Assert.That(Kerning(family, 900, 100)).IsEqualTo(-100);
        await Assert.That(Kerning(family, 100, 75)).IsEqualTo(-20);
    }

    private static Font CreateFont(FontFamily family, float weight, float width)
    {
        // The layout scales font units by size / (72 * UnitsPerEm), so at this size it keeps them unscaled.
        float size = 72 * family.CreateFont(1).FontMetrics.UnitsPerEm;
        return family.CreateFont(size, FontStyle.Regular, new FontVariation(Weight, weight), new FontVariation(Width, width));
    }

    private static (int Id, int X, int Y)[] Shape(Font font, string text, out int advance)
    {
        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout(text.AsSpan(), new TextOptions(font));
        advance = (int)MathF.Round(layout[^1].PenLocation.X - layout[0].PenLocation.X + layout[^1].AdvanceX);
        return layout
            .Select(g => (
                (int)g.Glyph.GlyphMetrics.GlyphId,
                (int)MathF.Round(g.PenLocation.X - layout[0].PenLocation.X + g.Glyph.GlyphMetrics.Offset.X),
                (int)MathF.Round(g.Glyph.GlyphMetrics.Offset.Y)))
            .ToArray();
    }

    private static int[] Glyphs(FontFamily family, float weight, float width, string text)
        => Shape(CreateFont(family, weight, width), text, out _).Select(g => g.Id).ToArray();

    // The kerning that A V gets, which is the advance of A less its own: 600 units.
    private static int Kerning(FontFamily family, float weight, float width)
    {
        Shape(CreateFont(family, weight, width), "AV", out int advance);
        return advance - (600 + 660);
    }

    // The condition of a record of the GPOS FeatureVariations table, as a span over the font. The condition set and the
    // condition offsets are from the start of the table that holds them.
    private static Span<byte> GposCondition(byte[] font, int record, int condition)
    {
        Span<byte> gpos = VariationFontBytes.Table(font, "GPOS");
        int variations = (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(10));
        int conditionSet = variations + (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(variations + 8 + (8 * record)));
        int offset = (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(conditionSet + 2 + (4 * condition)));
        return gpos.Slice(conditionSet + offset);
    }

    private static void Malform(byte[] font, string malformation)
    {
        Span<byte> gpos = VariationFontBytes.Table(font, "GPOS");
        int variations = (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(10));
        int substitutions = variations + (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(variations + 12));
        switch (malformation)
        {
            case "the offset of the table is not in the table":
                BinaryPrimitives.WriteUInt32BigEndian(gpos.Slice(10), (uint)gpos.Length + 64);
                break;
            case "the table has another major version":
                BinaryPrimitives.WriteUInt16BigEndian(gpos.Slice(variations), 2);
                break;
            case "the record count is more than the table holds":
                BinaryPrimitives.WriteUInt32BigEndian(gpos.Slice(variations + 4), 0xFFFFFFFF);
                break;
            case "the offset of a condition set is not in the table":
                BinaryPrimitives.WriteUInt32BigEndian(gpos.Slice(variations + 8), (uint)gpos.Length);
                break;
            case "the offset of a condition is not in the table":
                int conditionSet = variations + (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(variations + 8));
                BinaryPrimitives.WriteUInt32BigEndian(gpos.Slice(conditionSet + 2), (uint)gpos.Length);
                break;
            case "the offset of a substitution table is not in the table":
                BinaryPrimitives.WriteUInt32BigEndian(gpos.Slice(variations + 12), (uint)gpos.Length);
                break;
            case "the offset of an alternate feature is not in the table":
                BinaryPrimitives.WriteUInt32BigEndian(gpos.Slice(substitutions + 8), (uint)gpos.Length);
                break;
            case "the substitution table has another major version":
                BinaryPrimitives.WriteUInt16BigEndian(gpos.Slice(substitutions), 2);
                break;
            case "an alternate feature has more lookups than the table holds":
                int feature = substitutions + (int)BinaryPrimitives.ReadUInt32BigEndian(gpos.Slice(substitutions + 8));
                BinaryPrimitives.WriteUInt16BigEndian(gpos.Slice(feature + 2), 0xFFFF);
                break;
            default:
                throw new ArgumentException(malformation);
        }
    }

    private sealed class Reference
    {
        public JsonElement[][] Rows { get; set; } = [];
    }
}
