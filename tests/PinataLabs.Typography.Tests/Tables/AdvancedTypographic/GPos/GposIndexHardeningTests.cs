// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.GPos;
using PinataLabs.Typography.Tests.Fakes;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

/// <summary>
/// Out-of-range indices in GPOS pair format 2 and mark attachment subtables mean "subtable does not apply".
/// </summary>
public class GposIndexHardeningTests
{
    [Test]
    public async Task PairFormat2ClassDef1BeyondClass1CountDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType2SubTable.LookupType2Format2SubTable subTable = CreatePairFormat2(classDef1: 3, classDef2: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 0, 2)).IsFalse();
    }

    [Test]
    public async Task PairFormat2ClassDef2BeyondClass2CountDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType2SubTable.LookupType2Format2SubTable subTable = CreatePairFormat2(classDef1: 0, classDef2: 3);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 0, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToBaseMarkCoverageBeyondMarkRecordsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType4SubTable.LookupType4Format1SubTable subTable = CreateType4(markIndex: 5, baseIndex: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToBaseBaseCoverageBeyondBaseRecordsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType4SubTable.LookupType4Format1SubTable subTable = CreateType4(markIndex: 0, baseIndex: 5);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToBaseMarkClassBeyondBaseAnchorsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType4SubTable.LookupType4Format1SubTable subTable = CreateType4(markIndex: 0, baseIndex: 0, markClass: 4);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToBaseInRangeIndicesApply()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType4SubTable.LookupType4Format1SubTable subTable = CreateType4(markIndex: 0, baseIndex: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsTrue();
    }

    [Test]
    public async Task MarkToLigatureMarkCoverageBeyondMarkRecordsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType5SubTable.LookupType5Format1SubTable subTable = CreateType5(markIndex: 5, ligatureIndex: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToLigatureCoverageBeyondLigatureAttachTablesDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType5SubTable.LookupType5Format1SubTable subTable = CreateType5(markIndex: 0, ligatureIndex: 5);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToLigatureMarkClassBeyondLigatureAnchorsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType5SubTable.LookupType5Format1SubTable subTable = CreateType5(markIndex: 0, ligatureIndex: 0, markClass: 4);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToLigatureWithZeroCodePointsDoesNotApplyOrThrow()
    {
        // compIndex would be -1.
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        collection[0].CodePointCount = 0;
        LookupType5SubTable.LookupType5Format1SubTable subTable = CreateType5(markIndex: 0, ligatureIndex: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToLigatureWithFewerComponentsThanCodePointsAttachesToLastComponent()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        collection[0].CodePointCount = 3;
        LookupType5SubTable.LookupType5Format1SubTable subTable = CreateType5(markIndex: 0, ligatureIndex: 0, componentCount: 1);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsTrue();
    }

    [Test]
    public async Task MarkToLigatureWithNoComponentsDoesNotApplyOrThrow()
    {
        (Font font, GlyphPositioningCollection collection) = Setup("AB");
        LookupType5SubTable.LookupType5Format1SubTable subTable = CreateType5(markIndex: 0, ligatureIndex: 0, componentCount: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToMarkMark1CoverageBeyondMarkRecordsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = SetupMarks();
        LookupType6SubTable.LookupType6Format1SubTable subTable = CreateType6(mark1Index: 5, mark2Index: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToMarkMark2CoverageBeyondMark2RecordsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = SetupMarks();
        LookupType6SubTable.LookupType6Format1SubTable subTable = CreateType6(mark1Index: 0, mark2Index: 5);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToMarkMarkClassBeyondMarkAnchorsDoesNotThrow()
    {
        (Font font, GlyphPositioningCollection collection) = SetupMarks();
        LookupType6SubTable.LookupType6Format1SubTable subTable = CreateType6(mark1Index: 0, mark2Index: 0, markClass: 4);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsFalse();
    }

    [Test]
    public async Task MarkToMarkInRangeIndicesApply()
    {
        (Font font, GlyphPositioningCollection collection) = SetupMarks();
        LookupType6SubTable.LookupType6Format1SubTable subTable = CreateType6(mark1Index: 0, mark2Index: 0);

        await Assert.That(subTable.TryUpdatePosition(font.FontMetrics, null!, collection, default, 1, 2)).IsTrue();
    }

    private static (Font Font, GlyphPositioningCollection Collection) Setup(string text)
        => Build(FakeFont.CreateFont(text), text);

    private static (Font Font, GlyphPositioningCollection Collection) SetupMarks()
    {
        // Every glyph is reported as a GDEF mark glyph.
        IFontMetricsCollection fc = new FontCollection();
        Font font = fc.AddMetrics(new MarkFontInstance("AB"), CultureInfo.InvariantCulture).CreateFont(12);
        return Build(font, "AB");
    }

    private static (Font Font, GlyphPositioningCollection Collection) Build(Font font, string text)
    {
        TextOptions options = new(font);
        TextRun run = new() { Start = 0, End = text.Length };

        GlyphSubstitutionCollection substitutions = new(options);
        for (int i = 0; i < text.Length; i++)
        {
            substitutions.AddGlyph((ushort)(i + 1), new CodePoint(text[i]), TextDirection.LeftToRight, run, i);
        }

        GlyphPositioningCollection positionings = new(options);
        positionings.TryAdd(font, substitutions);
        return (font, positionings);
    }

    private static LookupType2SubTable.LookupType2Format2SubTable CreatePairFormat2(int classDef1, int classDef2)
    {
        // One Class1Record holding one Class2Record, no value formats.
        BigEndianBinaryWriter writer = new();
        Class1Record record = Class1Record.Load(writer.GetReader(), 1, default, default);
        return new(new FixedCoverageTable(0), [record], new FixedClassDef(classDef1), new FixedClassDef(classDef2), default);
    }

    private static LookupType4SubTable.LookupType4Format1SubTable CreateType4(int markIndex, int baseIndex, ushort markClass = 0)
    {
        // Single mark record, single base record, one mark class.
        BigEndianBinaryWriter markArray = new();
        WriteMarkArray(markArray, markClass);
        BigEndianBinaryWriter baseArray = new();
        baseArray.WriteUInt16(1); // baseCount
        baseArray.WriteOffset16(4); // anchor for class 0
        WriteAnchor(baseArray);

        return new(
            new FixedCoverageTable(markIndex),
            new FixedCoverageTable(baseIndex),
            new MarkArrayTable(markArray.GetReader(), 0),
            new BaseArrayTable(baseArray.GetReader(), 0, 1),
            default);
    }

    private static LookupType5SubTable.LookupType5Format1SubTable CreateType5(int markIndex, int ligatureIndex, ushort markClass = 0, int componentCount = 1)
    {
        BigEndianBinaryWriter markArray = new();
        WriteMarkArray(markArray, markClass);

        // LigatureArray: count, one offset, then the LigatureAttach table (componentCount, one anchor offset per component, anchor).
        BigEndianBinaryWriter ligArray = new();
        ligArray.WriteUInt16(1);
        ligArray.WriteOffset16(4);
        ligArray.WriteUInt16((ushort)componentCount);
        for (int i = 0; i < componentCount; i++)
        {
            ligArray.WriteOffset16((ushort)(2 + (2 * componentCount))); // relative to the attach table
        }

        WriteAnchor(ligArray);

        return new(
            new FixedCoverageTable(markIndex),
            new FixedCoverageTable(ligatureIndex),
            new MarkArrayTable(markArray.GetReader(), 0),
            new LigatureArrayTable(ligArray.GetReader(), 0, 1),
            default);
    }

    private static LookupType6SubTable.LookupType6Format1SubTable CreateType6(int mark1Index, int mark2Index, ushort markClass = 0)
    {
        BigEndianBinaryWriter mark1Array = new();
        WriteMarkArray(mark1Array, markClass);
        BigEndianBinaryWriter mark2Array = new();
        mark2Array.WriteUInt16(1); // mark2Count
        mark2Array.WriteOffset16(4); // anchor for class 0
        WriteAnchor(mark2Array);

        return new(
            new FixedCoverageTable(mark1Index),
            new FixedCoverageTable(mark2Index),
            new MarkArrayTable(mark1Array.GetReader(), 0),
            new Mark2ArrayTable(mark2Array.GetReader(), 1, 0),
            default);
    }

    private static void WriteMarkArray(BigEndianBinaryWriter writer, ushort markClass)
    {
        writer.WriteUInt16(1); // markCount
        writer.WriteUInt16(markClass);
        writer.WriteOffset16(6);
        WriteAnchor(writer);
    }

    private static void WriteAnchor(BigEndianBinaryWriter writer)
    {
        writer.WriteUInt16(1); // anchorFormat
        writer.Write((short)100);
        writer.Write((short)200);
    }

    private sealed class FixedCoverageTable(int index) : CoverageTable
    {
        public override int CoverageIndexOf(ushort glyphId) => index;
    }

    private sealed class FixedClassDef(int classIndex) : ClassDefinitionTable
    {
        public override int ClassIndexOf(ushort glyphId) => classIndex;
    }

    private sealed class MarkFontInstance(string text) : FakeFontInstance(text)
    {
        internal override bool TryGetGlyphClass(ushort glyphId, [NotNullWhen(true)] out GlyphClassDef? glyphClass)
        {
            glyphClass = GlyphClassDef.MarkGlyph;
            return true;
        }
    }
}
