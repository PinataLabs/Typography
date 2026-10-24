// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.Cff;
using PinataLabs.Typography.Tables.General;
using PinataLabs.Typography.Tables.TrueType.Glyphs;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.Hardening;

public class OutOfRangeIndexTests
{
    private static readonly Tag Kern = Tag.Parse("kern");

    [Test]
    public async Task TrueTypeGlyphIdAboveGlyphCountReturnsNotdef()
    {
        Bounds notdef = new(1, 2, 3, 4);
        GlyphTable table = new([new FixedGlyphLoader(notdef), new FixedGlyphLoader(new Bounds(5, 6, 7, 8))]);

        GlyphVector glyph = table.GetGlyph(50);

        await Assert.That(glyph.Bounds).IsEqualTo(notdef);
    }

    [Test]
    public async Task TrueTypeNegativeGlyphIdReturnsNotdef()
    {
        Bounds notdef = new(1, 2, 3, 4);
        GlyphTable table = new([new FixedGlyphLoader(notdef)]);

        await Assert.That(table.GetGlyph(-1).Bounds).IsEqualTo(notdef);
    }

    [Test]
    public async Task Cff1GlyphIdAboveGlyphCountReturnsNotdef()
    {
        Cff1Table table = new(new CffFont(
            "test",
            new CffTopDictionary(),
            [CreateCffGlyph(0), CreateCffGlyph(1)]));

        CffGlyphData glyph = table.GetGlyph(50);

        await Assert.That(glyph.GlyphIndex).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task GSubFeatureAndLookupIndicesOutOfRangeAreSkipped()
    {
        GSubTable table = GSubTable.Load(CreateLayoutTable());

        bool found = table.TryGetFeatureLookups(in Kern, ScriptClass.Latin, out var lookups);

        await Assert.That(found).IsTrue();
        await Assert.That(lookups!.Count).IsEqualTo(1);
        await Assert.That(lookups[0].Index).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task GPosFeatureAndLookupIndicesOutOfRangeAreSkipped()
    {
        GPosTable table = GPosTable.Load(CreateLayoutTable());

        bool found = table.TryGetFeatureLookups(in Kern, ScriptClass.Latin, out var lookups);

        await Assert.That(found).IsTrue();
        await Assert.That(lookups!.Count).IsEqualTo(1);
        await Assert.That(lookups[0].Index).IsEqualTo((ushort)0);
    }

    [Test]
    public async Task EmptyHorizontalMetricsReturnZero()
    {
        BigEndianBinaryWriter writer = new();
        HorizontalMetricsTable table = HorizontalMetricsTable.Load(writer.GetReader(), 0, 0);

        await Assert.That(table.GetAdvancedWidth(0)).IsEqualTo((ushort)0);
        await Assert.That(table.GetAdvancedWidth(7)).IsEqualTo((ushort)0);
        await Assert.That(table.GetLeftSideBearing(0)).IsEqualTo((short)0);
        await Assert.That(table.GetLeftSideBearing(7)).IsEqualTo((short)0);
    }

    [Test]
    public async Task HorizontalMetricsWithNoLongMetricsReturnZeroAdvance()
    {
        BigEndianBinaryWriter writer = new();
        writer.WriteInt16(11);
        writer.WriteInt16(22);
        HorizontalMetricsTable table = HorizontalMetricsTable.Load(writer.GetReader(), 0, 2);

        await Assert.That(table.GetAdvancedWidth(1)).IsEqualTo((ushort)0);
        await Assert.That(table.GetLeftSideBearing(1)).IsEqualTo((short)22);
        await Assert.That(table.GetLeftSideBearing(9)).IsEqualTo((short)11);
    }

    private static CffGlyphData CreateCffGlyph(ushort id)
        => new(id, [], [], 0, []);

    // One script (DFLT) with a default LangSys referencing feature indices [7, 0]; only feature 0 exists.
    // Feature 0 ('kern') references lookup indices [9, 0]; only lookup 0 exists (it has no subtables).
    private static BigEndianBinaryReader CreateLayoutTable()
    {
        BigEndianBinaryWriter w = new();

        // Header v1.0 (10 bytes).
        w.WriteUInt16(1);
        w.WriteUInt16(0);
        w.WriteUInt16(10); // ScriptList
        w.WriteUInt16(32); // FeatureList
        w.WriteUInt16(48); // LookupList

        // ScriptList @10.
        w.WriteUInt16(1);
        w.WriteUInt32("DFLT");
        w.WriteUInt16(8);

        // Script table: default LangSys at +4, no other LangSys.
        w.WriteUInt16(4);
        w.WriteUInt16(0);

        // LangSys.
        w.WriteUInt16(0);
        w.WriteUInt16(0xFFFF);
        w.WriteUInt16(2);
        w.WriteUInt16(7);
        w.WriteUInt16(0);

        // FeatureList @32.
        w.WriteUInt16(1);
        w.WriteUInt32("kern");
        w.WriteUInt16(8);

        // Feature table.
        w.WriteUInt16(0);
        w.WriteUInt16(2);
        w.WriteUInt16(9);
        w.WriteUInt16(0);

        // LookupList @48.
        w.WriteUInt16(1);
        w.WriteUInt16(4);

        // Lookup: type 1, no flags, no subtables.
        w.WriteUInt16(1);
        w.WriteUInt16(0);
        w.WriteUInt16(0);

        return w.GetReader();
    }

    private sealed class FixedGlyphLoader : GlyphLoader
    {
        private readonly Bounds bounds;

        public FixedGlyphLoader(Bounds bounds) => this.bounds = bounds;

        public override GlyphVector CreateGlyph(GlyphTable table) => GlyphVector.Empty(this.bounds);
    }
}
