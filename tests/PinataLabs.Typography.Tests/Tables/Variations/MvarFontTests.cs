// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using static PinataLabs.Typography.Tests.Tables.Variations.MetricsVariationsReference;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Tests for how a font loads its MVAR table and applies it to the metrics that rules choose from other tables, on a built
/// font whose tables the test then breaks. The built font has the axes wght 100 to 900 and wdth 75 to 125. At wght 900 and
/// wdth 100 every region that the stores use has the scalar 0 or 1, so a delta is a value in the store.
/// tests/tools/generate_metrics_variation_reference.py lists them.
/// </summary>
public class MvarFontTests
{
    private const string FontFile = "Variations/VariationMetrics.ttf";
    private const ushort A = 2;

    private static readonly FontVariation[] Heavy = [new(Tag.Parse("wght"), 900), new(Tag.Parse("wdth"), 100)];

    [Test]
    public async Task AFontThatIsIntactVariesTheMetricsThatMvarNames()
    {
        FontFamily family = VariationFontBytes.Load(VariationFontBytes.Read(FontFile));
        Font face = family.CreateFont(1000, default, [.. family.Faces[0].Variations]);
        Font heavy = family.CreateFont(1000, default, Heavy);

        // The hhea ascender is 800, and MVAR adds 30 at wght 900.
        await Assert.That(face.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo((short)800);
        await Assert.That(heavy.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo((short)830);
    }

    [Test]
    [Arguments("the major version")]
    [Arguments("the store")]
    public async Task AMalformedMvarIsAbsentAndTheOtherTablesStillVary(string defect)
    {
        byte[] bytes = VariationFontBytes.Read(FontFile);
        Span<byte> table = VariationFontBytes.Table(bytes, "MVAR");
        if (defect == "the major version")
        {
            BinaryPrimitives.WriteUInt16BigEndian(table, 2);
        }
        else
        {
            // Offset16 of the store, after the header and the value records.
            BinaryPrimitives.WriteUInt16BigEndian(table.Slice(10), (ushort)(table.Length + 1));
        }

        FontFamily family = VariationFontBytes.Load(bytes);
        Font heavy = family.CreateFont(1000, default, Heavy);

        await Assert.That(heavy.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo((short)800);
        await Assert.That(GetMetrics(heavy, A).AdvanceWidth).IsEqualTo((ushort)640);
    }

    [Test]
    public async Task AFontWithNeitherHheaNorTypoMetricsTakesTheWinMetricsAndMvarVariesThose()
    {
        // The rules choose from the values of the font, so the win values stay chosen when MVAR moves the hhea ascender.
        // At wght 900 MVAR adds 25 to hcla, 14 to hcld and 6 to hlgp. The win ascent is 900 and the win descent 300.
        // The line gap of hhea stays the line gap, and the win line height has no line gap in it.
        byte[] bytes = VariationFontBytes.Read(FontFile);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "hhea").Slice(4), 0);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "hhea").Slice(6), 0);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "OS/2").Slice(68), 0);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "OS/2").Slice(70), 0);
        FontFamily family = VariationFontBytes.Load(bytes);
        Font face = family.CreateFont(1000, default, [.. family.Faces[0].Variations]);

        Font heavy = family.CreateFont(1000, default, Heavy);
        HorizontalMetrics varied = heavy.FontMetrics.HorizontalMetrics;

        await Assert.That(face.FontMetrics.HorizontalMetrics.Ascender).IsEqualTo((short)900);
        await Assert.That(face.FontMetrics.HorizontalMetrics.Descender).IsEqualTo((short)-300);
        await Assert.That(face.FontMetrics.HorizontalMetrics.LineHeight).IsEqualTo((short)1200);
        await Assert.That(varied.Ascender).IsEqualTo((short)925);
        await Assert.That(varied.Descender).IsEqualTo((short)-314);
        await Assert.That(varied.LineGap).IsEqualTo((short)56);
        await Assert.That(varied.LineHeight).IsEqualTo((short)1239);
    }

    [Test]
    public async Task ADescenderThatVheaStoresAsAPositiveNumberIsVariedAsStored()
    {
        // Some fonts store the vhea descent as a positive number. The library reads it as negative, and MVAR adds its
        // delta to the number that the table holds. MVAR adds -12 at wght 900, so 500 gives 488, which is -488 as the library
        // reads it. Changing the sign first would give -500 - 12.
        byte[] bytes = VariationFontBytes.Read(FontFile);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "vhea").Slice(6), 500);
        FontFamily family = VariationFontBytes.Load(bytes);
        Font face = family.CreateFont(1000, default, [.. family.Faces[0].Variations]);

        Font heavy = family.CreateFont(1000, default, Heavy);

        await Assert.That(face.FontMetrics.VerticalMetrics.Descender).IsEqualTo((short)-500);
        await Assert.That(heavy.FontMetrics.VerticalMetrics.Descender).IsEqualTo((short)-488);
    }

    [Test]
    [Arguments(500, 100, -512)]
    [Arguments(500, 400, -500)]
    [Arguments(500, 900, -488)]
    [Arguments(5, 100, -17)]
    [Arguments(5, 900, -7)]
    [Arguments(-5, 100, -7)]
    [Arguments(-5, 900, -17)]
    [Arguments(0, 100, -12)]
    [Arguments(0, 900, -12)]
    public async Task AVheaDescenderIsVariedAsStoredAndThenTakenAsNegativeAsHarfBuzzDoes(short stored, float weight, short expected)
    {
        // The expected values are those of HarfBuzz, which tools/generate_metrics_variation_reference.py checks for the stored
        // values 500, 5, -5 and 0. MVAR adds 12 at wght 100 and -12 at wght 900. A sum above zero is negated, so 5 and 12 give
        // -17 and 5 and -12 give -7, where changing the sign of 5 first would give -5 + 12 = 7.
        byte[] bytes = VariationFontBytes.Read(FontFile);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "vhea").Slice(6), stored);
        FontFamily family = VariationFontBytes.Load(bytes);

        Font font = family.CreateFont(1000, default, [new(Tag.Parse("wght"), weight), new(Tag.Parse("wdth"), 100)]);

        await Assert.That(font.FontMetrics.VerticalMetrics.Descender).IsEqualTo(expected);
    }

    [Test]
    [Arguments(40000, 40000, 400, 32767, -32768, 32767)]
    [Arguments(32770, 32770, 100, 32755, -32756, 32767)]
    [Arguments(900, 300, 400, 900, -300, 1200)]
    [Arguments(40000, 300, 900, 32767, -314, 32767)]
    public async Task TheWinMetricsAreUnsignedAndClampedOnceAfterMvarVariesThem(
        ushort winAscent,
        ushort winDescent,
        float weight,
        short ascender,
        short descender,
        short lineHeight)
    {
        // The win ascent and descent are unsigned. A value above 32767 must not wrap through a short, so the sum of the value
        // and its delta is made in int and clamped to the range of a short once. MVAR adds 25 to hcla and 14 to hcld at wght 900,
        // and -15 and -14 at wght 100. The line height is the sum of the two, clamped too.
        byte[] bytes = VariationFontBytes.Read(FontFile);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "hhea").Slice(4), 0);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "hhea").Slice(6), 0);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "OS/2").Slice(68), 0);
        BinaryPrimitives.WriteInt16BigEndian(VariationFontBytes.Table(bytes, "OS/2").Slice(70), 0);
        BinaryPrimitives.WriteUInt16BigEndian(VariationFontBytes.Table(bytes, "OS/2").Slice(74), winAscent);
        BinaryPrimitives.WriteUInt16BigEndian(VariationFontBytes.Table(bytes, "OS/2").Slice(76), winDescent);
        FontFamily family = VariationFontBytes.Load(bytes);

        Font font = family.CreateFont(1000, default, [new(Tag.Parse("wght"), weight), new(Tag.Parse("wdth"), 100)]);
        HorizontalMetrics metrics = font.FontMetrics.HorizontalMetrics;

        await Assert.That(metrics.Ascender).IsEqualTo(ascender);
        await Assert.That(metrics.Descender).IsEqualTo(descender);
        await Assert.That(metrics.LineHeight).IsEqualTo(lineHeight);
    }
}
