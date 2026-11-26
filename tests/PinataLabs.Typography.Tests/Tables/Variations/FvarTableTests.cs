// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.General.Name;
using PinataLabs.Typography.Tables.Variations;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.Variations;

public class FvarTableTests
{
    // The header of fvar: uint16 majorVersion, minorVersion, Offset16 axesArrayOffset, uint16 reserved,
    // axisCount, axisSize, instanceCount, instanceSize. The first axis record follows at the axes offset:
    // Tag axisTag, Fixed minValue, defaultValue, maxValue, uint16 flags, axisNameID.
    private const int MajorVersion = 0;
    private const int AxisCount = 8;
    private const int AxisSize = 10;
    private const int InstanceCount = 12;
    private const int InstanceSize = 14;
    private const int FirstAxis = 16;
    private const int FirstAxisMinimum = FirstAxis + 4;
    private const int FirstAxisDefault = FirstAxis + 8;

    [Test]
    [Arguments(MajorVersion, 2)]
    [Arguments(AxisCount, 0)]
    [Arguments(AxisCount, 0xFFFF)]
    [Arguments(AxisSize, 21)]
    [Arguments(AxisSize, 19)]
    [Arguments(InstanceCount, 0xFFFF)]
    [Arguments(InstanceSize, 7)]
    public async Task AFontWithAMalformedFvarLoadsAsAStaticFont(int field, int value)
    {
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        BinaryPrimitives.WriteUInt16BigEndian(VariationFontBytes.Table(font, "fvar").Slice(field), (ushort)value);

        FontMetrics face = VariationFontBytes.Load(font).Faces[0];

        await Assert.That(face.VariationAxes).IsEmpty();
        await Assert.That(face.NamedInstances).IsEmpty();
        await Assert.That(face.Variations).IsEmpty();
        await Assert.That(face.Description.StyleRange.MinWeight.Value).IsEqualTo(300F);
        await Assert.That(face.Description.StyleRange.MaxWeight.Value).IsEqualTo(300F);

        // The font still draws.
        await Assert.That(face.TryGetGlyphMetrics(new CodePoint('A'), TextAttributes.None, TextDecorations.None, LayoutMode.HorizontalTopBottom, ColorFontSupport.None, out _)).IsTrue();
    }

    [Test]
    [Arguments(FirstAxisMinimum, 0x01900000)]
    [Arguments(FirstAxisDefault, 0x03200000)]
    public async Task AFontWhoseAxisRangeDoesNotHoldItsDefaultLoadsAsAStaticFont(int field, int value)
    {
        // Teko has the axis 300, 300, 700. A minimum of 400 is above the default, and a default of 800 is above the maximum.
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        BinaryPrimitives.WriteInt32BigEndian(VariationFontBytes.Table(font, "fvar").Slice(field), value);

        FontMetrics face = VariationFontBytes.Load(font).Faces[0];

        await Assert.That(face.VariationAxes).IsEmpty();
    }

    [Test]
    public async Task AFvarThatEndsBeforeItsInstancesIsMalformed()
    {
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        byte[] fvar = VariationFontBytes.Table(font, "fvar").ToArray();
        NameTable name = ReadName(font);

        await Assert.That(FvarTable.Parse(fvar, name, null)).IsNotNull();
        await Assert.That(FvarTable.Parse(fvar.AsSpan(0, fvar.Length - 1), name, null)).IsNull();
        await Assert.That(FvarTable.Parse(fvar.AsSpan(0, FirstAxis + 20), name, null)).IsNull();
        await Assert.That(FvarTable.Parse(fvar.AsSpan(0, FirstAxis + 19), name, null)).IsNull();
        await Assert.That(FvarTable.Parse([], name, null)).IsNull();
    }

    [Test]
    public async Task AFvarWithoutInstancesIsStillVariable()
    {
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        byte[] fvar = VariationFontBytes.Table(font, "fvar").ToArray();
        BinaryPrimitives.WriteUInt16BigEndian(fvar.AsSpan(InstanceCount), 0);

        FvarTable? table = FvarTable.Parse(fvar, ReadName(font), null);

        await Assert.That(table).IsNotNull();
        await Assert.That(table!.Axes.Count).IsEqualTo(1);
        await Assert.That(table.NamedInstances).IsEmpty();
    }

    [Test]
    public async Task ARecordLongerThanTheLengthInTheHeaderIsRead()
    {
        // instanceSize is 4 + 4 * axisCount, or 2 bytes more when the record has a PostScript name ID.
        // A longer record has data that this version of the table does not define, and the next record starts after it.
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        byte[] fvar = VariationFontBytes.Table(font, "fvar").ToArray();
        int instanceSize = BinaryPrimitives.ReadUInt16BigEndian(fvar.AsSpan(InstanceSize));
        int instances = BinaryPrimitives.ReadUInt16BigEndian(fvar.AsSpan(InstanceCount));
        int axes = FirstAxis + 20;
        byte[] longer = new byte[axes + (instances * (instanceSize + 4))];
        fvar.AsSpan(0, axes).CopyTo(longer);
        for (int i = 0; i < instances; i++)
        {
            fvar.AsSpan(axes + (i * instanceSize), instanceSize).CopyTo(longer.AsSpan(axes + (i * (instanceSize + 4))));
        }

        BinaryPrimitives.WriteUInt16BigEndian(longer.AsSpan(InstanceSize), (ushort)(instanceSize + 4));
        FvarTable? table = FvarTable.Parse(longer, ReadName(font), null);

        await Assert.That(table!.NamedInstances.Select(x => x.Variations[0].Value)).IsEquivalentTo([300F, 400F, 500F, 600F, 700F], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("Teko/Teko-Variable.ttf", "fvar")]
    [Arguments("Teko/Teko-Variable.ttf", "avar")]
    [Arguments("SourceSans3/SourceSans3VF-Upright.otf", "fvar")]
    [Arguments("SourceSans3/SourceSans3VF-Upright.otf", "avar")]
    [Arguments("Variations/VariationAxes.ttf", "fvar")]
    [Arguments("Variations/VariationAxes.ttf", "avar")]
    public async Task NoTruncationOrChangedByteOfTheTablesMakesTheReaderThrow(string path, string table)
    {
        byte[] font = VariationFontBytes.Read(path);
        byte[] bytes = VariationFontBytes.Table(font, table).ToArray();
        NameTable name = ReadName(font);
        AvarTable? avar = AvarTable.Parse(VariationFontBytes.Table(font, "avar"));
        int parsed = 0;

        for (int length = 0; length <= bytes.Length; length++)
        {
            parsed += Parse(table, bytes.AsSpan(0, length), name, avar) ? 1 : 0;
        }

        foreach (byte value in new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFF })
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                byte[] changed = (byte[])bytes.Clone();
                changed[i] = value;
                parsed += Parse(table, changed, name, avar) ? 1 : 0;
            }
        }

        // The complete table reads, so the loop did something.
        await Assert.That(parsed).IsGreaterThan(0);
    }

    [Test]
    public async Task AxesGetTheValuesOfTheStyleTheSizeAndTheVariationsInThatOrder()
    {
        FvarTable fvar = LoadFvar("Variations/VariationAxes.ttf");

        // The axes are wght 100 to 900, wdth 75 to 125, opsz 8 to 144, ital 0 to 1 and GRAD -100 to 100.
        await Assert.That(fvar.CreateUserCoordinates(FontStyle.Regular, 12F, [])).IsEquivalentTo([400D, 100D, 12D, 0D, 0D], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fvar.CreateUserCoordinates(FontStyle.Bold, 12F, [])).IsEquivalentTo([700D, 100D, 12D, 0D, 0D], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fvar.CreateUserCoordinates(FontStyle.BoldItalic, 30F, [])).IsEquivalentTo([700D, 100D, 30D, 1D, 0D], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fvar.CreateUserCoordinates(FontStyle.Regular.WithWidth(FontWidth.Condensed).WithWeight(new FontWeight(250F)), 12F, [])).IsEquivalentTo([250D, 75D, 12D, 0D, 0D], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(fvar.CreateUserCoordinates(FontStyle.Regular, 12F, [new(Tag.Parse("wght"), 650F), new(Tag.Parse("GRAD"), -40F)])).IsEquivalentTo([650D, 100D, 12D, 0D, -40D], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ValuesAreClampedToTheAxisAndAnAxisThatTheFontLacksIsIgnored()
    {
        FvarTable fvar = LoadFvar("Variations/VariationAxes.ttf");

        double[] coordinates = fvar.CreateUserCoordinates(
            FontStyle.Regular.WithWeight(new FontWeight(1000F)),
            200F,
            [new(Tag.Parse("wdth"), 10F), new(Tag.Parse("XXXX"), 5F), new(Tag.Parse("GRAD"), float.PositiveInfinity)]);

        await Assert.That(coordinates).IsEquivalentTo([900D, 75D, 144D, 0D, 100D], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task TheLastValueOfARepeatedAxisWins()
    {
        FvarTable fvar = LoadFvar("Variations/VariationAxes.ttf");

        double[] coordinates = fvar.CreateUserCoordinates(
            FontStyle.Regular,
            12F,
            [new(Tag.Parse("wght"), 300F), new(Tag.Parse("wght"), 500F)]);

        await Assert.That(coordinates[0]).IsEqualTo(500D);
    }

    [Test]
    public async Task ASizeOrValueThatIsNotANumberGivesTheDefaultOfTheAxis()
    {
        FvarTable fvar = LoadFvar("Variations/VariationAxes.ttf");

        double[] coordinates = fvar.CreateUserCoordinates(FontStyle.Regular, float.NaN, [new(Tag.Parse("GRAD"), float.NaN)]);

        await Assert.That(coordinates[2]).IsEqualTo(14D);
        await Assert.That(coordinates[4]).IsEqualTo(0D);
    }

    [Test]
    public async Task ItalicSetsTheItalAxisWhenThereIsOneAndOtherwiseTheSlantOf14Degrees()
    {
        // VariationSlant has wght 300 to 800 and slnt -12 to 0, and no ital axis.
        FvarTable slant = LoadFvar("Variations/VariationSlant.ttf");

        // The slant is clamped to the axis, so an italic request reaches -12.
        await Assert.That(slant.CreateUserCoordinates(FontStyle.Italic, 12F, [])[1]).IsEqualTo(-12D);
        await Assert.That(slant.CreateUserCoordinates(FontStyle.Regular.WithSlope(FontSlope.Oblique(10F)), 12F, [])[1]).IsEqualTo(-10D);
        await Assert.That(slant.CreateUserCoordinates(FontStyle.Regular.WithSlope(FontSlope.Oblique(-5F)), 12F, [])[1]).IsEqualTo(0D);
        await Assert.That(slant.CreateUserCoordinates(FontStyle.Regular, 12F, [])[1]).IsEqualTo(0D);

        // With an ital axis, an oblique request does not set it and an italic request sets it.
        FvarTable axes = LoadFvar("Variations/VariationAxes.ttf");
        await Assert.That(axes.CreateUserCoordinates(FontStyle.Regular.WithSlope(FontSlope.Oblique(10F)), 12F, [])[3]).IsEqualTo(0D);
        await Assert.That(axes.CreateUserCoordinates(FontStyle.Italic, 12F, [])[3]).IsEqualTo(1D);
    }

    [Test]
    [Arguments(-1000F, 0)]
    [Arguments(0F, 0)]
    [Arguments(300F, 0)]
    [Arguments(700F, 16384)]
    [Arguments(5000F, 16384)]
    public async Task UserCoordinatesAreClampedToTheAxisWhenTheyAreNormalised(float wght, int expected)
    {
        FvarTable fvar = LoadFvar("Teko/Teko-Variable.ttf");

        await Assert.That((int)fvar.Normalize([wght]).Values[0]).IsEqualTo(expected);
    }

    [Test]
    public async Task ANormalisedCoordinateIsRoundedHalfUpToF2Dot14()
    {
        // The specification rounds a fraction of 0.5 and higher up, so -0.5 rounds to 0 and 0.5 rounds to 1, as
        // fontTools floatToFixed does. Round half to even would give 0 for 0.5, and round half away from zero would give -1 for -0.5.
        // Teko without avar has the axis 300, 300, 700, so 300 + 25/2048 is 1 / 32768 of the way along, which is 0.5 in F2Dot14.
        byte[] font = VariationFontBytes.Read("Teko/Teko-Variable.ttf");
        FvarTable teko = FvarTable.Parse(VariationFontBytes.Table(font, "fvar"), ReadName(font), null)!;

        // VariationSlant has the axis 300, 400, 800 and no avar, so 400 - 25/8192 is 1 / 32768 of the way down.
        FvarTable slant = LoadFvar("Variations/VariationSlant.ttf");

        await Assert.That((int)teko.Normalize([300D + (25D / 2048D)]).Values[0]).IsEqualTo(1);
        await Assert.That((int)teko.Normalize([300D + (24.9D / 2048D)]).Values[0]).IsEqualTo(0);
        await Assert.That((int)slant.Normalize([400D - (25D / 8192D), 0D]).Values[0]).IsEqualTo(0);
        await Assert.That((int)slant.Normalize([400D - (26D / 8192D), 0D]).Values[0]).IsEqualTo(-1);
    }

    [Test]
    public async Task TheFirstNamedInstanceAtSomeCoordinatesIsFound()
    {
        FvarTable fvar = LoadFvar("Teko/Teko-Variable.ttf");

        bool bold = fvar.TryFindNamedInstance(fvar.Normalize([700D]), out FontNamedInstance? instance);
        bool between = fvar.TryFindNamedInstance(fvar.Normalize([450D]), out _);

        await Assert.That(bold).IsTrue();
        await Assert.That(instance!.Name(System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo("Bold");
        await Assert.That(between).IsFalse();
        await Assert.That(fvar.TryFindNamedInstance(fvar.DefaultCoordinates, out FontNamedInstance? light)).IsTrue();
        await Assert.That(light!.Name(System.Globalization.CultureInfo.InvariantCulture)).IsEqualTo("Light");
    }

    private static bool Parse(string table, ReadOnlySpan<byte> bytes, NameTable name, AvarTable? avar)
        => table == "fvar"
            ? FvarTable.Parse(bytes, name, avar) is not null
            : AvarTable.Parse(bytes) is not null;

    private static NameTable ReadName(byte[] font)
        => NameTable.Load(new BigEndianBinaryReader(new System.IO.MemoryStream(VariationFontBytes.Table(font, "name").ToArray()), false));

    private static FvarTable LoadFvar(string path)
        => new FontCollection().Add(TestFonts.GetPath(path)).Faces[0].Description.Fvar!;
}
