// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using PinataLabs.Typography.FontPrograms;
using PinataLabs.Typography.Tables;
using PinataLabs.Typography.Tables.TrueType.Glyphs;
using PinataLabs.Typography.Tests.FontPrograms;

namespace PinataLabs.Typography.Tests.Tables.General.Glyphs;

public class GlyphTableTests
{
    private const int ArgsAreWords = 0x0001;
    private const int ArgsAreXYValues = 0x0002;
    private const int ScaledComponentOffset = 0x0800;

    private static readonly byte[] Triangle = TrueTypeBuilder.SimpleGlyph([(0, 0, true), (50, 100, true), (100, 0, true)]);

    private static readonly byte[] Square = TrueTypeBuilder.SimpleGlyph([(10, 10, true), (10, 20, true), (20, 20, true), (20, 10, true)]);

    [Test]
    public async Task SelfReferencingCompositeFails()
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            TrueTypeBuilder.CompositeGlyph(TrueTypeBuilder.Component(1, ArgsAreXYValues, 0, 0)));
        GlyphOutline outline = new();

        await Assert.That(table.TryWriteOutline(1, ref outline)).IsFalse();
        await Assert.That(table.TryReadPoints(1, out _)).IsFalse();
        await Assert.That(table.TryGetPoint(1, 0, out _)).IsFalse();
    }

    [Test]
    public async Task CyclicCompositesFail()
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            TrueTypeBuilder.CompositeGlyph(TrueTypeBuilder.Component(2, ArgsAreXYValues, 0, 0)),
            TrueTypeBuilder.CompositeGlyph(TrueTypeBuilder.Component(3, ArgsAreXYValues, 0, 0)),
            TrueTypeBuilder.CompositeGlyph(TrueTypeBuilder.Component(1, ArgsAreXYValues, 0, 0)));
        GlyphOutline outline = new();

        await Assert.That(table.TryWriteOutline(1, ref outline)).IsFalse();
        await Assert.That(table.TryReadPoints(1, out _)).IsFalse();
    }

    [Test]
    public async Task ComponentOutsideTheFontFailsAfterTheComponentsBeforeIt()
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            TrueTypeBuilder.CompositeGlyph(
                TrueTypeBuilder.Component(0, ArgsAreXYValues, 0, 0),
                TrueTypeBuilder.Component(500, ArgsAreXYValues, 0, 0)));
        GlyphOutline outline = new();

        await Assert.That(table.TryWriteOutline(1, ref outline)).IsFalse();
        await Assert.That(outline.ToString()).IsEqualTo("M 10 10 L 10 20 L 20 20 L 20 10 Z");
        await Assert.That(table.TryReadPoints(1, out _)).IsFalse();
    }

    [Test]
    [Arguments(ArgsAreXYValues | ArgsAreWords, short.MinValue + 1, short.MaxValue - 30)]
    [Arguments(ArgsAreXYValues, sbyte.MinValue + 1, sbyte.MaxValue)]
    public async Task ComponentOffsetIsSigned(int flags, int dx, int dy)
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            TrueTypeBuilder.CompositeGlyph(TrueTypeBuilder.Component(0, flags, dx, dy)));

        await Assert.That(table.TryGetPoint(1, 0, out Vector2 point)).IsTrue();
        await Assert.That(point).IsEqualTo(new Vector2(10 + dx, 10 + dy));
    }

    [Test]
    [Arguments(0)]
    [Arguments(ArgsAreWords)]
    public async Task ComponentWithPointNumbersMovesItsPointOntoThePointOfTheComposite(int flags)
    {
        // The triangle is at (200, 0). Point 2 of the square, (20, 20), moves onto point 1 of the composite, (250, 100).
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            Triangle,
            TrueTypeBuilder.CompositeGlyph(
                TrueTypeBuilder.Component(1, ArgsAreXYValues | ArgsAreWords, 200, 0),
                TrueTypeBuilder.Component(0, flags, 1, 2)));
        GlyphOutline outline = new();

        await Assert.That(table.TryWriteOutline(2, ref outline)).IsTrue();
        await Assert.That(outline.ToString()).IsEqualTo("M 200 0 L 250 100 L 300 0 Z M 240 90 L 240 100 L 250 100 L 250 90 Z");
    }

    [Test]
    public async Task ComponentWithAPointNumberOutsideTheGlyphFails()
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            Triangle,
            TrueTypeBuilder.CompositeGlyph(
                TrueTypeBuilder.Component(1, ArgsAreXYValues, 0, 0),
                TrueTypeBuilder.Component(0, 0, 3, 0)));
        GlyphOutline outline = new();

        await Assert.That(table.TryWriteOutline(2, ref outline)).IsFalse();
    }

    [Test]
    [Arguments(0, 100, 40)]
    [Arguments(ScaledComponentOffset, 50, 20)]
    public async Task ComponentOffsetIsScaledOnlyWhenTheFlagSaysSo(int flags, float dx, float dy)
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            TrueTypeBuilder.CompositeGlyph(TrueTypeBuilder.Component(0, flags | ArgsAreXYValues, 100, 40, 0.5F)));

        await Assert.That(table.TryGetPoint(1, 0, out Vector2 point)).IsTrue();
        await Assert.That(point).IsEqualTo(new Vector2(5 + dx, 5 + dy));
    }

    [Test]
    public async Task PointsOfACompositeAreThePointsOfItsComponentsInOrder()
    {
        byte[] instructions = [1, 2, 3];
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            Square,
            Triangle,
            TrueTypeBuilder.CompositeGlyphWithInstructions(
                instructions,
                TrueTypeBuilder.Component(1, ArgsAreXYValues | ArgsAreWords, 200, 0),
                TrueTypeBuilder.Component(0, 0, 1, 2)));

        await Assert.That(table.TryReadPoints(2, out GlyphVector vector)).IsTrue();

        Vector2[] expected = [new(200, 0), new(250, 100), new(300, 0), new(240, 90), new(240, 100), new(250, 100), new(250, 90)];
        await Assert.That(vector.ControlPoints.Select(x => x.Point)).IsEquivalentTo(expected);
        await Assert.That(vector.ControlPoints.All(x => x.OnCurve)).IsTrue();
        await Assert.That(vector.EndPoints).IsEquivalentTo((ushort[])[2, 6]);
        await Assert.That(vector.IsComposite).IsTrue();
        await Assert.That(vector.Instructions.ToArray()).IsEquivalentTo(instructions);
    }

    [Test]
    public async Task PointsOfASimpleGlyphComeWithItsInstructions()
    {
        byte[] instructions = [9, 8, 7, 6];
        GlyphTable table = TrueTypeBuilder.GlyphTable(
            TrueTypeBuilder.SimpleGlyph(instructions, [(0, 0, true), (50, 100, false), (100, 0, true)], [(1, 1, true), (2, 2, true)]));

        await Assert.That(table.TryReadPoints(0, out GlyphVector vector)).IsTrue();

        await Assert.That(vector.ControlPoints.Length).IsEqualTo(5);
        await Assert.That(vector.ControlPoints[1]).IsEqualTo(new ControlPoint(new Vector2(50, 100), false));
        await Assert.That(vector.EndPoints).IsEquivalentTo((ushort[])[2, 4]);
        await Assert.That(vector.IsComposite).IsFalse();
        await Assert.That(vector.Instructions.ToArray()).IsEquivalentTo(instructions);
    }

    [Test]
    public async Task EmptyGlyphHasNoPointsAndNoOutline()
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(Square, []);
        GlyphOutline outline = new();

        await Assert.That(table.TryWriteOutline(1, ref outline)).IsTrue();
        await Assert.That(outline.ToString()).IsEqualTo(string.Empty);
        await Assert.That(table.TryReadPoints(1, out GlyphVector vector)).IsTrue();
        await Assert.That(vector.ControlPoints).IsEmpty();
    }

    [Test]
    public async Task EmptyGlyphTakesTheBoundsOfGlyphZero()
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(Triangle, []);

        await Assert.That(table.GetBounds(1)).IsEqualTo(new Bounds(0, 0, 100, 100));
    }

    [Test]
    public async Task EmptyGlyphZeroHasEmptyBounds()
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable([], []);

        await Assert.That(table.GetBounds(1)).IsEqualTo(Bounds.Empty);
    }

    [Test]
    [Arguments(50)]
    [Arguments(-1)]
    public async Task GlyphIdOutsideTheFontSelectsGlyphZero(int glyphId)
    {
        GlyphTable table = TrueTypeBuilder.GlyphTable(Triangle, Square);
        GlyphOutline outline = new();

        await Assert.That(table.GetBounds(glyphId)).IsEqualTo(new Bounds(0, 0, 100, 100));
        await Assert.That(table.TryWriteOutline(glyphId, ref outline)).IsTrue();
        await Assert.That(outline.ToString()).IsEqualTo("M 0 0 L 50 100 L 100 0 Z");
    }

    [Test]
    public async Task LoadReturnsNullWhenTheFontHasNoGlyphTable()
    {
        BigEndianBinaryWriter writer = new();
        writer.WriteTrueTypeFileHeader();

        using MemoryStream stream = writer.GetStream();

        await Assert.That(GlyphTable.Load(new FontReader(stream))).IsNull();
    }

    [Test]
    public async Task LoadThrowsWhenTheFontHasNoHeadTable()
    {
        BigEndianBinaryWriter writer = new();
        writer.WriteTrueTypeFileHeader(new TableHeader("glyf", 0, 0, 0));

        using MemoryStream stream = writer.GetStream();
        MissingFontTableException? exception = await Assert.That(
            () => GlyphTable.Load(new FontReader(stream))).ThrowsExactly<MissingFontTableException>();

        await Assert.That(exception!.Table).IsEqualTo("head");
    }

    [Test]
    public async Task LoadThrowsWhenTheFontHasNoLocationTable()
    {
        byte[] data = File.ReadAllBytes(TestFonts.OpenSansFile);
        RenameTable(data, "loca", "xxxx");
        using MemoryStream stream = new(data);

        MissingFontTableException? exception = await Assert.That(
            () => GlyphTable.Load(new FontReader(stream))).ThrowsExactly<MissingFontTableException>();

        await Assert.That(exception!.Table).IsEqualTo("loca");
    }

    [Test]
    public async Task LoadThrowsWhenTheLocationFormatIsUnknown()
    {
        byte[] data = File.ReadAllBytes(TestFonts.OpenSansFile);
        int head = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(FindTable(data, "head") + 8));
        BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(head + 50), 7);
        using MemoryStream stream = new(data);
        InvalidFontTableException? exception = await Assert.That(
            () => GlyphTable.Load(new FontReader(stream))).ThrowsExactly<InvalidFontTableException>();

        await Assert.That(exception!.Table).IsEqualTo("head");
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("Roboto-Regular.ttf")]
    [Arguments("DejaVuSans.ttf")]
    [Arguments("TimesNewRoman.ttf")]
    public async Task OutlineOfThePointsOfEveryGlyphIsItsStreamedOutline(string fontFile)
    {
        using FileStream stream = File.OpenRead(TestFonts.GetPath(fontFile));
        GlyphTable table = new FontReader(stream).GetTable<GlyphTable>();
        List<string> differences = [];

        for (int i = 0; i < table.GlyphCount && differences.Count < 10; i++)
        {
            GlyphOutline streamed = new();
            GlyphOutline fromPoints = new();
            if (!table.TryWriteOutline(i, ref streamed) || !table.TryReadPoints(i, out GlyphVector vector))
            {
                differences.Add($"glyph {i}: malformed");
                continue;
            }

            TrueTypeGlyphDecoder.WriteOutline(vector.ControlPoints, vector.EndPoints, ref fromPoints);
            if (streamed.ToString() != fromPoints.ToString())
            {
                differences.Add($"glyph {i}: expected '{streamed}' but was '{fromPoints}'");
            }
        }

        await Assert.That(table.GlyphCount).IsGreaterThan(0);
        await Assert.That(differences).IsEmpty();
    }

    // A WOFF2 file holds the glyphs in a transformed table. The table that layout builds from it
    // must give every glyph the outline, the bounding box and the instructions of the TrueType file.
    [Test]
    [Arguments("OpenSans-Regular.woff")]
    [Arguments("OpenSans-Regular.woff2")]
    public async Task EveryGlyphOfAWebFontIsTheGlyphOfTheTrueTypeFont(string fontFile)
    {
        using FileStream expectedStream = File.OpenRead(TestFonts.OpenSansFile);
        using FileStream actualStream = File.OpenRead(TestFonts.GetPath(fontFile));
        GlyphTable expected = new FontReader(expectedStream).GetTable<GlyphTable>();
        GlyphTable actual = new FontReader(actualStream).GetTable<GlyphTable>();
        List<string> differences = [];

        for (int i = 0; i < expected.GlyphCount && differences.Count < 10; i++)
        {
            GlyphOutline expectedOutline = new();
            GlyphOutline actualOutline = new();
            bool read = expected.TryWriteOutline(i, ref expectedOutline) & actual.TryWriteOutline(i, ref actualOutline)
                & expected.TryReadPoints(i, out GlyphVector expectedVector) & actual.TryReadPoints(i, out GlyphVector actualVector);

            if (!read)
            {
                differences.Add($"glyph {i}: malformed");
            }
            else if (expectedOutline.ToString() != actualOutline.ToString())
            {
                differences.Add($"glyph {i}: expected '{expectedOutline}' but was '{actualOutline}'");
            }
            else if (expected.GetBounds(i) != actual.GetBounds(i))
            {
                differences.Add($"glyph {i}: bounds");
            }
            else if (!expectedVector.Instructions.Span.SequenceEqual(actualVector.Instructions.Span))
            {
                differences.Add($"glyph {i}: instructions");
            }
        }

        await Assert.That(actual.GlyphCount).IsEqualTo(expected.GlyphCount);
        await Assert.That(differences).IsEmpty();
    }

    private static void RenameTable(byte[] data, string tag, string newTag)
        => Encoding.ASCII.GetBytes(newTag, 0, 4, data, FindTable(data, tag));

    // Finds the directory entry of a table: its tag, checksum, offset and length.
    private static int FindTable(byte[] data, string tag)
    {
        int tableCount = (data[4] << 8) | data[5];
        for (int i = 0; i < tableCount; i++)
        {
            int entry = 12 + (i * 16);
            if (Encoding.ASCII.GetString(data, entry, 4) == tag)
            {
                return entry;
            }
        }

        throw new InvalidDataException($"The font has no '{tag}' table.");
    }
}
