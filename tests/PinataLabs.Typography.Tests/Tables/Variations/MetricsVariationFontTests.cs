// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using static PinataLabs.Typography.Tests.Tables.Variations.MetricsVariationsReference;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Tests for how a font loads its HVAR and VVAR tables, on a built font whose tables the test then breaks. The built font
/// has the axes wght 100 to 900 and wdth 75 to 125. At wght 900 and wdth 100 every region that the stores use has the
/// scalar 0 or 1, so a delta is a value in the store. tests/tools/generate_metrics_variation_reference.py lists them.
/// </summary>
public class MetricsVariationFontTests
{
    private const string FontFile = "Variations/VariationMetrics.ttf";
    private const ushort A = 2;

    private static readonly FontVariation[] Heavy = [new(Tag.Parse("wght"), 900), new(Tag.Parse("wdth"), 100)];

    [Test]
    public async Task AFontThatIsIntactVariesWhatEachTableVaries()
    {
        FontFamily family = VariationFontBytes.Load(VariationFontBytes.Read(FontFile));
        Font face = family.CreateFont(1000, default, [.. family.Faces[0].Variations]);
        Font heavy = family.CreateFont(1000, default, Heavy);

        // The advance width of A is 600, and HVAR adds 40 at wght 900. Its advance height is 900, and VVAR adds 50.
        await Assert.That(face.FontMetrics).IsSameReferenceAs(family.Faces[0]);
        await Assert.That(GetMetrics(face, A).AdvanceWidth).IsEqualTo(600);
        await Assert.That(GetMetrics(face, A).AdvanceHeight).IsEqualTo(900);
        await Assert.That(GetMetrics(heavy, A).AdvanceWidth).IsEqualTo(640);
        await Assert.That(GetMetrics(heavy, A).AdvanceHeight).IsEqualTo(950);
    }

    [Test]
    [Arguments("HVAR", "the major version")]
    [Arguments("HVAR", "the store")]
    [Arguments("VVAR", "the major version")]
    [Arguments("VVAR", "the store")]
    public async Task AMalformedTableIsAbsentAndTheOtherOneStillVaries(string table, string defect)
    {
        byte[] bytes = VariationFontBytes.Read(FontFile);
        Span<byte> data = VariationFontBytes.Table(bytes, table);
        if (defect == "the major version")
        {
            BinaryPrimitives.WriteUInt16BigEndian(data, 2);
        }
        else
        {
            // Offset32 of the store.
            BinaryPrimitives.WriteUInt32BigEndian(data.Slice(4), (uint)data.Length + 1);
        }

        FontFamily family = VariationFontBytes.Load(bytes);
        Font heavy = family.CreateFont(1000, default, Heavy);
        GlyphMetrics metrics = GetMetrics(heavy, A);

        // The metrics that the broken table varies stay as the face has them, and the others change as they do in the intact font.
        await Assert.That(metrics.AdvanceWidth).IsEqualTo(table == "HVAR" ? 600 : 640);
        await Assert.That(metrics.AdvanceHeight).IsEqualTo(table == "VVAR" ? 900 : 950);
    }

    [Test]
    public async Task AFontWithoutTheVariationTablesDrawsTheMetricsOfTheFace()
    {
        // An instance of a font that has fvar but none of the metrics tables has the metrics of its face.
        FontFamily family = new FontCollection().Add(TestFonts.VariationAxesFile);
        Font face = family.CreateFont(1000, default, [.. family.Faces[0].Variations]);
        Font bold = family.CreateFont(1000, FontStyle.Bold);

        await Assert.That(bold.FontMetrics).IsNotSameReferenceAs(face.FontMetrics);
        await Assert.That(GetMetrics(bold, A).AdvanceWidth).IsEqualTo(GetMetrics(face, A).AdvanceWidth);
        await Assert.That(bold.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo(face.FontMetrics.HorizontalMetrics.Ascender);
        await Assert.That(bold.FontMetrics.StrikeoutPosition).IsEqualTo(face.FontMetrics.StrikeoutPosition);
    }
}
