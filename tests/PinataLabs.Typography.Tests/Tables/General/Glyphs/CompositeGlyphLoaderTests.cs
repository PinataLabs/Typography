// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.TrueType.Glyphs;

namespace PinataLabs.Typography.Tests.Tables.General.Glyphs;

public class CompositeGlyphLoaderTests
{
    [Test]
    public async Task SelfReferencingCompositeDoesNotOverflowTheStack()
    {
        var bounds = new Bounds(0, 0, 100, 100);
        GlyphTable table = new(new GlyphLoader[]
        {
            new SimpleGlyphLoader(bounds),
            CreateComposite(bounds, 1), // refers to itself
        });

        GlyphVector glyph = table.GetGlyph(1);

        await Assert.That(glyph.ControlPoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CyclicCompositesDoNotOverflowTheStack()
    {
        var bounds = new Bounds(0, 0, 100, 100);
        GlyphTable table = new(new GlyphLoader[]
        {
            new SimpleGlyphLoader(bounds),
            CreateComposite(bounds, 2),
            CreateComposite(bounds, 3),
            CreateComposite(bounds, 1),
        });

        GlyphVector glyph = table.GetGlyph(1);

        await Assert.That(glyph.ControlPoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task CompositeComponentOutOfRangeIsIgnored()
    {
        var bounds = new Bounds(0, 0, 100, 100);
        GlyphTable table = new(new GlyphLoader[]
        {
            new SimpleGlyphLoader(bounds),
            CreateComposite(bounds, 500),
        });

        GlyphVector glyph = table.GetGlyph(1);

        await Assert.That(glyph.ControlPoints.Count).IsEqualTo(0);
    }

    private static CompositeGlyphLoader CreateComposite(in Bounds bounds, ushort componentIndex)
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteUInt16(0); // flags: no more components
        writer.WriteUInt16(componentIndex);
        writer.WriteUInt8(0); // dx
        writer.WriteUInt8(0); // dy
        return CompositeGlyphLoader.LoadCompositeGlyph(writer.GetReader(), in bounds);
    }

    [Test]
    public async Task LoadSingleGlyphWithUInt16Offset_unsigned_short()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteUInt16((ushort)CompositeGlyphFlags.Args1And2AreWords); // 16bit unsigned
        writer.WriteUInt16(1); // glyph id

        writer.WriteUInt16(short.MaxValue + 1); // dx
        writer.WriteUInt16(short.MaxValue + 2); // dy
        writer.GetReader();

        var bounds = new Bounds(0, 0, 100, 100);
        var glyph = CompositeGlyphLoader.LoadCompositeGlyph(writer.GetReader(), in bounds);

        var tbl = new GlyphTable(new[]
        {
            new SimpleGlyphLoader(bounds), // padding
            new SimpleGlyphLoader(new ControlPoint[] { new(new Vector2(20, 21), true) }, new ushort[] { 1 }, bounds, Array.Empty<byte>())
        });

        GlyphVector finalGlyph = glyph.CreateGlyph(tbl);

        ControlPoint point = await Assert.That(finalGlyph.ControlPoints).HasSingleItem();
        await Assert.That(point.Point).IsEqualTo(new Vector2(short.MaxValue + 1 + 20, short.MaxValue + 2 + 21));
    }

    [Test]
    public async Task LoadSingleGlyphWithInt16Offset_signed_short()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteUInt16((ushort)(CompositeGlyphFlags.Args1And2AreWords /* 16bit */ | CompositeGlyphFlags.ArgsAreXYValues /* signed */)); // flags
        writer.WriteUInt16(1); // glyph id

        writer.WriteInt16(short.MinValue + 1); // dx
        writer.WriteInt16(short.MinValue + 2); // dy
        writer.GetReader();

        var bounds = new Bounds(0, 0, 100, 100);
        var glyph = CompositeGlyphLoader.LoadCompositeGlyph(writer.GetReader(), in bounds);

        var tbl = new GlyphTable(new[]
        {
            new SimpleGlyphLoader(bounds), // padding
            new SimpleGlyphLoader(new ControlPoint[] { new(new Vector2(20, 21), true) }, new ushort[] { 1 }, bounds, Array.Empty<byte>())
        });

        GlyphVector finalGlyph = glyph.CreateGlyph(tbl);

        ControlPoint point = await Assert.That(finalGlyph.ControlPoints).HasSingleItem();
        await Assert.That(point.Point).IsEqualTo(new Vector2(short.MinValue + 1 + 20, short.MinValue + 2 + 21));
    }

    [Test]
    public async Task LoadSingleGlyphWithUInt8Offset_unsigned_byte()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteUInt16(0); // 8bit unsigned
        writer.WriteUInt16(1); // glyph id

        writer.WriteUInt8(sbyte.MaxValue + 1); // dx
        writer.WriteUInt8(sbyte.MaxValue + 2); // dy
        writer.GetReader();

        var bounds = new Bounds(0, 0, 100, 100);
        var glyph = CompositeGlyphLoader.LoadCompositeGlyph(writer.GetReader(), in bounds);

        var tbl = new GlyphTable(new[]
        {
            new SimpleGlyphLoader(bounds), // padding
            new SimpleGlyphLoader(new ControlPoint[] { new(new Vector2(20, 21), true) }, new ushort[] { 1 }, bounds, Array.Empty<byte>())
        });

        GlyphVector finalGlyph = glyph.CreateGlyph(tbl);

        ControlPoint point = await Assert.That(finalGlyph.ControlPoints).HasSingleItem();
        await Assert.That(point.Point).IsEqualTo(new Vector2(sbyte.MaxValue + 1 + 20, sbyte.MaxValue + 2 + 21));
    }

    [Test]
    public async Task LoadSingleGlyphWithInt8Offset_signed_byte()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteUInt16((ushort)CompositeGlyphFlags.ArgsAreXYValues); // signed byte
        writer.WriteUInt16(1); // glyph id

        writer.WriteInt8(sbyte.MinValue + 1); // dx
        writer.WriteInt8(sbyte.MinValue + 2); // dy
        writer.GetReader();

        var bounds = new Bounds(0, 0, 100, 100);
        var glyph = CompositeGlyphLoader.LoadCompositeGlyph(writer.GetReader(), in bounds);

        var tbl = new GlyphTable(new[]
        {
            new SimpleGlyphLoader(bounds), // padding
            new SimpleGlyphLoader(new ControlPoint[] { new(new Vector2(20, 21), true) }, new ushort[] { 1 }, bounds, Array.Empty<byte>())
        });

        GlyphVector finalGlyph = glyph.CreateGlyph(tbl);

        ControlPoint point = await Assert.That(finalGlyph.ControlPoints).HasSingleItem();
        await Assert.That(point.Point).IsEqualTo(new Vector2(sbyte.MinValue + 1 + 20, sbyte.MinValue + 2 + 21));
    }
}
