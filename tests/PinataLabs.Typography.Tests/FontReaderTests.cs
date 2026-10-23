// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.TrueType.Glyphs;

namespace PinataLabs.Typography.Tests;

public class FontReaderTests
{
    [Test]
    public async Task ReadTrueTypeOutlineType()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteTrueTypeFileHeader(0, 0, 0, 0);

        var reader = new FontReader(writer.GetStream());
        await Assert.That(reader.OutlineType).IsEqualTo(OutlineType.TrueType);
    }

    [Test]
    public async Task ReadCffOutlineType()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteCffFileHeader(0, 0, 0, 0);

        var reader = new FontReader(writer.GetStream());
        await Assert.That(reader.OutlineType).IsEqualTo(OutlineType.CFF);
    }

    [Test]
    public async Task ReadTableHeaders()
    {
        var writer = new BigEndianBinaryWriter();
        writer.WriteTrueTypeFileHeader(2, 0, 0, 0);
        writer.WriteTableHeader("name", 0, 10, 0);
        writer.WriteTableHeader("cmap", 0, 1, 0);

        var reader = new FontReader(writer.GetStream());

        await Assert.That(reader.Headers.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ReadCMapTable()
    {
        var writer = new BigEndianBinaryWriter();

        writer.WriteTrueTypeFileHeader(new TableHeader("cmap", 0, 0, 20));

        writer.WriteCMapTable(
            new[]
            {
                new Typography.Tables.General.CMap.Format0SubTable(
                    0,
                    WellKnownIds.PlatformIDs.Macintosh,
                    1,
                    new byte[] { 2, 9 })
            });

        var reader = new FontReader(writer.GetStream());
        CMapTable cmap = reader.GetTable<CMapTable>();
        await Assert.That(cmap).IsNotNull();
    }

    [Test]
    public async Task ReadFont_WithWoffFormat_EqualsTtf()
    {
        var fontReaderTtf = new FontReader(TestFonts.OpenSansTtfData());
        var fontReaderWoff = new FontReader(TestFonts.OpensSansWoff1Data());

        await Assert.That(fontReaderWoff.Headers.Count).IsEqualTo(fontReaderTtf.Headers.Count);
        foreach (string key in fontReaderTtf.Headers.Keys)
        {
            await Assert.That(fontReaderWoff.Headers.ContainsKey(key)).IsTrue();
        }
    }

    [Test]
    public async Task GlyphsCount_WithWoffFormat_EqualsTtf()
    {
        var fontReaderWoff = new FontReader(TestFonts.OpensSansWoff1Data());
        GlyphTable glyphsWoff = fontReaderWoff.GetTable<GlyphTable>();
        var fontReaderTtf = new FontReader(TestFonts.OpenSansTtfData());
        GlyphTable glyphsTtf = fontReaderTtf.GetTable<GlyphTable>();

        await Assert.That(glyphsWoff.GlyphCount).IsEqualTo(glyphsTtf.GlyphCount);
    }

    [Test]
    public async Task ReadFont_WithWoff2Format_EqualsTtf()
    {
        var fontReaderTtf = new FontReader(TestFonts.OpenSansTtfData());
        var fontReaderWoff = new FontReader(TestFonts.OpensSansWoff2Data());

        await Assert.That(fontReaderWoff.Headers.Count).IsEqualTo(fontReaderTtf.Headers.Count);
        foreach (string key in fontReaderTtf.Headers.Keys)
        {
            await Assert.That(fontReaderWoff.Headers.ContainsKey(key)).IsTrue();
        }
    }

    [Test]
    public async Task GlyphsCount_WithWoff2Format_EqualsTtf()
    {
        var fontReaderWoff = new FontReader(TestFonts.OpensSansWoff2Data());
        GlyphTable glyphsWoff = fontReaderWoff.GetTable<GlyphTable>();
        var fontReaderTtf = new FontReader(TestFonts.OpenSansTtfData());
        GlyphTable glyphsTtf = fontReaderTtf.GetTable<GlyphTable>();

        await Assert.That(glyphsWoff.GlyphCount).IsEqualTo(glyphsTtf.GlyphCount);
    }
}
