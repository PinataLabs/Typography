// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tests.FontPrograms;

namespace PinataLabs.Typography.Tests.FontSubsetting;

/// <summary>
/// Subsets of TrueType fonts: the 'glyf' and 'loca' tables, and the components of composite glyphs.
/// </summary>
public class FontSubsetterTrueTypeTests
{
    private const ushort MoreComponents = 0x0020;
    private const ushort ArgsAreXyValues = 0x0002;

    [Test]
    public async Task ACompositeGlyphKeepsItsComponentsAtAnyDepth()
    {
        // 'nested' draws scaledoffset, twobytwo and dot, and the first two draw box, round and alloff.
        // 'plain' and 'pointmatch' are composites too, and nothing that was asked for needs them.
        byte[] font = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/tt-composite.ttf"));
        FontProgram program = FontProgram.Load(font);
        program.TryGetGlyphId("nested", out ushort nested);

        byte[] subset = FontSubsetter.Subset(font, [nested]);

        List<string> differences = SubsetComparison.Compare(font, subset, [nested], out int dropped);
        await Assert.That(differences).IsEmpty();
        await Assert.That(dropped).IsEqualTo(2);
        FontProgram reloaded = FontProgram.Load(subset);
        foreach (string name in new[] { "box", "round", "alloff", "dot", "scaledoffset", "twobytwo", "nested" })
        {
            reloaded.TryGetGlyphId(name, out ushort component);
            await Assert.That(Describe(reloaded, component)).IsEqualTo(Describe(program, component));
            await Assert.That(Describe(reloaded, component)).IsNotEmpty();
        }

        foreach (string name in new[] { "plain", "pointmatch" })
        {
            reloaded.TryGetGlyphId(name, out ushort other);
            await Assert.That(Describe(reloaded, other)).IsEmpty();
        }
    }

    [Test]
    public async Task ACycleOfCompositeGlyphsEnds()
    {
        // Glyph 1 draws itself, glyphs 2 and 3 draw each other, and glyph 4 draws glyph 2 and a simple glyph, 5.
        byte[] font = TrueTypeBuilder.Build([Simple(), Composite(1), Composite(3), Composite(2), Composite(2, 5), Simple(), Simple()]);

        byte[] viaCycle = FontSubsetter.Subset(font, [4]);
        byte[] viaSelf = FontSubsetter.Subset(font, [1]);

        await Assert.That(ReadGlyphLengths(viaCycle).Select((length, id) => (length, id)).Where(x => x.length > 0).Select(x => x.id)).IsEquivalentTo([0, 2, 3, 4, 5]);
        await Assert.That(ReadGlyphLengths(viaSelf).Select((length, id) => (length, id)).Where(x => x.length > 0).Select(x => x.id)).IsEquivalentTo([0, 1]);
        await Assert.That(FontProgram.Load(viaCycle).GlyphCount).IsEqualTo(7);
    }

    [Test]
    public async Task AKeptGlyphThatDoesNotEndOnAFourByteBoundaryIsPaddedToOne()
    {
        // The composite of glyphs 2 and 5 is 22 bytes long, and the long form of 'loca' puts each glyph on a 4-byte boundary.
        byte[] font = TrueTypeBuilder.Build([Simple(), Simple(), Composite(2, 5), Simple(), Simple(), Simple()]);

        byte[] subset = FontSubsetter.Subset(font, [2]);

        int[] lengths = ReadGlyphLengths(subset);
        int[] original = ReadGlyphLengths(font);
        await Assert.That(original[2]).IsEqualTo(22);
        await Assert.That(lengths.Select((length, id) => (length, id)).Where(x => x.length > 0).Select(x => x.id)).IsEquivalentTo([0, 2, 5]);
        await Assert.That(lengths.All(length => length % 4 == 0)).IsTrue();
        await Assert.That(lengths[2]).IsEqualTo(24);
        await Assert.That(lengths[5]).IsEqualTo((original[5] + 3) & ~3);
    }

    [Test]
    public async Task AComponentThatTheFontDoesNotHaveIsRefused()
    {
        byte[] font = TrueTypeBuilder.Build([Simple(), Composite(1, 9), Simple()]);

        InvalidFontFileException exception = (await Assert.That(() => FontSubsetter.Subset(font, [1])).Throws<InvalidFontFileException>())!;

        await Assert.That(exception.Message).Contains("glyph 1");
        await Assert.That(exception.Message).Contains("glyph 9");

        // The composite is not asked for, and nothing else needs it, so nothing refuses it.
        await Assert.That(FontSubsetter.Subset(font, [2]).Length).IsGreaterThan(0);
    }

    [Test]
    public async Task ACompositeGlyphThatRunsPastItsDataIsRefused()
    {
        byte[] truncated = Composite(1, 1)[..^3];
        byte[] font = TrueTypeBuilder.Build([Simple(), truncated]);

        InvalidFontFileException exception = (await Assert.That(() => FontSubsetter.Subset(font, [1])).Throws<InvalidFontFileException>())!;

        await Assert.That(exception.Message).Contains("glyph 1");
    }

    [Test]
    [Arguments("OpenSans-Regular.ttf")]
    [Arguments("DejaVuSans.ttf")]
    [Arguments("FontPrograms/tt-composite.ttf")]
    public async Task ADroppedGlyphHasNoDataAndAKeptGlyphHasItsDataAndTheFormatOfLocaStays(string fontFile)
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        bool longFormat = IsLongFormat(font);
        int alignment = longFormat ? 4 : 2;

        byte[] subset = FontSubsetter.Subset(font, [3, 4, 5]);

        int[] before = ReadGlyphLengths(font);
        int[] after = ReadGlyphLengths(subset);
        List<string> failures = [];
        int kept = 0;
        for (int i = 0; i < before.Length; i++)
        {
            if (after[i] == 0)
            {
                continue;
            }

            kept++;
            if (after[i] != ((before[i] + alignment - 1) & -alignment))
            {
                failures.Add($"glyph {i} has {after[i]} bytes, and had {before[i]}");
            }
        }

        await Assert.That(IsLongFormat(subset)).IsEqualTo(longFormat);
        await Assert.That(after.Length).IsEqualTo(before.Length);
        await Assert.That(kept).IsLessThan(before.Count(length => length > 0));
        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task ASubsetOfALargeFontOfCompositesKeepsItsGlyphIds()
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath("kaiu.ttf"));
        FontProgram program = FontProgram.Load(font);
        ushort[] requested = [.. new[] { 'A', '中', '文', '漢' }.Select(c => program.TryGetGlyphIdFromCmap(3, 1, c, out ushort id) ? id : (ushort)1)];

        byte[] subset = FontSubsetter.Subset(font, requested);
        SubsetExport.Write("kaiu.ttf", requested, subset);

        List<string> differences = SubsetComparison.Compare(font, subset, requested, out int dropped);
        await Assert.That(differences).IsEmpty();
        await Assert.That(dropped).IsGreaterThan(program.GlyphCount / 2);
        await Assert.That(subset.Length).IsLessThan(font.Length / 2);
    }

    private static byte[] Simple()
        => TrueTypeBuilder.SimpleGlyph(new (short X, short Y, bool OnCurve)[] { (0, 0, true), (100, 0, true), (100, 100, true) });

    // A composite glyph that draws each of the glyphs, in order, without an offset.
    private static byte[] Composite(params ushort[] components)
    {
        List<byte> glyph = [0xFF, 0xFF, 0, 0, 0, 0, 0, 0, 0, 0];
        for (int i = 0; i < components.Length; i++)
        {
            ushort flags = (ushort)(ArgsAreXyValues | (i + 1 < components.Length ? MoreComponents : 0));
            glyph.AddRange([(byte)(flags >> 8), (byte)flags, (byte)(components[i] >> 8), (byte)components[i], 0, 0]);
        }

        return [.. glyph];
    }

    private static bool IsLongFormat(byte[] font)
        => BinaryPrimitives.ReadInt16BigEndian(SfntRewriter.GetTable(font, "head")!.AsSpan(50)) != 0;

    // The length of the data of each glyph, from 'loca'.
    private static int[] ReadGlyphLengths(byte[] font)
    {
        byte[] loca = SfntRewriter.GetTable(font, "loca")!;
        bool longFormat = IsLongFormat(font);
        int count = (loca.Length / (longFormat ? 4 : 2)) - 1;
        uint Offset(int i) => longFormat ? BinaryPrimitives.ReadUInt32BigEndian(loca.AsSpan(i * 4)) : BinaryPrimitives.ReadUInt16BigEndian(loca.AsSpan(i * 2)) * 2U;
        return [.. Enumerable.Range(0, count).Select(i => (int)(Offset(i + 1) - Offset(i)))];
    }

    private static string Describe(FontProgram program, ushort glyphId)
    {
        GlyphOutline outline = new();
        return program.TryWriteOutline(glyphId, ref outline) ? outline.ToString() : "failed";
    }
}
