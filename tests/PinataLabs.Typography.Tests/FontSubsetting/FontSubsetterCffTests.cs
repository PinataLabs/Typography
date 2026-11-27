// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Cff;
using PinataLabs.Typography.Tests.FontPrograms;

namespace PinataLabs.Typography.Tests.FontSubsetting;

/// <summary>
/// Subsets of CFF fonts: an OpenType file, and a bare CFF font as a PDF embeds it. What follows from the closure of a CFF glyph:
/// the glyphs that a seac glyph draws, and the subroutines that the kept charstrings call.
/// </summary>
public class FontSubsetterCffTests
{
    private const string Otf = "SourceSansPro-Regular.otf";

    [Test]
    [Arguments(Otf, 3)]
    [Arguments("FDArrayTest257.otf", 1)]
    public async Task ASubsetOfAFewGlyphsIsSmallerAndDropsTheRest(string fontFile, int shrinkBy)
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath(fontFile));
        ushort[] requested = [3, 20, 40];

        byte[] subset = FontSubsetter.Subset(font, requested);

        List<string> differences = SubsetComparison.Compare(font, subset, requested, out int dropped);
        await Assert.That(differences).IsEmpty();
        await Assert.That(dropped).IsGreaterThan(FontProgram.Load(font).GlyphCount / 2);
        await Assert.That(subset.Length).IsLessThan(font.Length / shrinkBy);
    }

    [Test]
    public async Task AGlyphThatEndsWithSeacKeepsItsBaseAndItsAccent()
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/cff-seac.otf"));
        FontProgram program = FontProgram.Load(font);
        program.TryGetGlyphId("Aacute", out ushort aacute);
        program.TryGetGlyphId("A", out ushort a);
        program.TryGetGlyphId("acute", out ushort acute);
        program.TryGetGlyphId("Aacute.width", out ushort other);

        FontProgram subset = FontProgram.Load(FontSubsetter.Subset(font, [aacute]));

        await Assert.That(DrawsLike(program, subset, aacute)).IsTrue();
        await Assert.That(DrawsLike(program, subset, a)).IsTrue();
        await Assert.That(DrawsLike(program, subset, acute)).IsTrue();
        await Assert.That(Describe(subset, a)).IsNotEmpty();
        await Assert.That(Describe(subset, other)).IsEmpty();
    }

    [Test]
    public async Task AComponentAloneDoesNotKeepTheGlyphThatUsesIt()
    {
        // The composite needs its components, and they do not need it.
        byte[] font = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/cff-seac.otf"));
        FontProgram program = FontProgram.Load(font);
        program.TryGetGlyphId("Aacute", out ushort aacute);
        program.TryGetGlyphId("A", out ushort a);

        FontProgram subset = FontProgram.Load(FontSubsetter.Subset(font, [a]));

        await Assert.That(DrawsLike(program, subset, a)).IsTrue();
        await Assert.That(Describe(subset, aacute)).IsEmpty();
    }

    [Test]
    public async Task ARandomSetOfGlyphsDrawsAsItDidWhateverSubroutinesItCalls()
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath(Otf));
        int glyphCount = FontProgram.Load(font).GlyphCount;
        Random random = new(155);
        List<string> failures = [];

        for (int round = 0; round < 12; round++)
        {
            ushort[] requested = [.. Enumerable.Range(0, 1 + (round * 5)).Select(_ => (ushort)random.Next(glyphCount))];
            byte[] subset = FontSubsetter.Subset(font, requested);
            failures.AddRange(SubsetComparison.Compare(font, subset, requested, out _).Select(difference => $"round {round}: {difference}"));
        }

        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task AKeptCharstringIsCopiedAsItWasAndADroppedOneIsALoneEndchar()
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath(Otf));
        CffFont before = CffParser.Load(SfntRewriter.GetTable(font, "CFF ")!);
        CffFont after = CffParser.Load(SfntRewriter.GetTable(FontSubsetter.Subset(font, [10, 500]), "CFF ")!);
        List<string> failures = [];

        for (int i = 0; i < before.GlyphCount; i++)
        {
            after.CharStrings.TryGet(i, out ReadOnlyMemory<byte> charString);
            before.CharStrings.TryGet(i, out ReadOnlyMemory<byte> original);
            if (i is 0 or 10 or 500)
            {
                if (!charString.Span.SequenceEqual(original.Span))
                {
                    failures.Add($"kept glyph {i} changed");
                }
            }
            else if (!charString.Span.SequenceEqual(original.Span) && !charString.Span.SequenceEqual([(byte)0x0E]))
            {
                failures.Add($"glyph {i} is neither as it was nor a lone endchar");
            }
        }

        await Assert.That(after.GlyphCount).IsEqualTo(before.GlyphCount);
        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task ASubroutineThatNoKeptGlyphCallsIsALoneReturnAndTheOthersStayInPlace()
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath(Otf));
        CffFont before = CffParser.Load(SfntRewriter.GetTable(font, "CFF ")!);
        CffFont after = CffParser.Load(SfntRewriter.GetTable(FontSubsetter.Subset(font, [10, 500]), "CFF ")!);
        CallSet calls = new();
        before.TryObserveCalls(0, calls);
        before.TryObserveCalls(10, calls);
        before.TryObserveCalls(500, calls);
        List<string> failures = [];

        for (int i = 0; i < before.GlobalSubrs.Count; i++)
        {
            Check(before.GlobalSubrs, after.GlobalSubrs, i, calls.Global.Contains(i), $"global subroutine {i}", failures);
        }

        for (int i = 0; i < before.GetLocalSubrs(0).Count; i++)
        {
            Check(before.GetLocalSubrs(0), after.GetLocalSubrs(0), i, calls.Local.Contains(i), $"local subroutine {i}", failures);
        }

        await Assert.That(after.GlobalSubrs.Count).IsEqualTo(before.GlobalSubrs.Count);
        await Assert.That(after.GetLocalSubrs(0).Count).IsEqualTo(before.GetLocalSubrs(0).Count);
        await Assert.That(calls.Local.Count + calls.Global.Count).IsGreaterThan(0);
        await Assert.That(calls.Local.Count).IsLessThan(before.GetLocalSubrs(0).Count);
        await Assert.That(failures).IsEmpty();
    }

    [Test]
    public async Task ASubsetOfALargeCidFontKeepsItsGlyphIds()
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath("NotoSansKR-Regular.otf"));
        FontProgram program = FontProgram.Load(font);
        ushort[] requested = [.. new[] { '한', '글', 'A', '가', '힣' }.Select(c => program.TryGetGlyphIdFromCmap(3, 10, c, out ushort id) || program.TryGetGlyphIdFromCmap(3, 1, c, out id) ? id : (ushort)1)];

        byte[] subset = FontSubsetter.Subset(font, requested);
        SubsetExport.Write("NotoSansKR-Regular.otf", requested, subset);

        // The font keeps one offset in its CharStrings INDEX for each of its 24,869 glyphs, and its cmap, and that is most of what is left.
        await Assert.That(subset.Length).IsLessThan(font.Length / 10);
        FontProgram reloaded = FontProgram.Load(subset);
        await Assert.That(reloaded.GlyphCount).IsEqualTo(program.GlyphCount);
        foreach (ushort id in requested)
        {
            await Assert.That(DrawsLike(program, reloaded, id)).IsTrue();
        }

        // A CID-keyed font keeps its charset, so a CID still names its glyph.
        await Assert.That(reloaded.TryGetGlyphIdForCid(1, out ushort gid)).IsEqualTo(program.TryGetGlyphIdForCid(1, out ushort expected));
        await Assert.That(gid).IsEqualTo(expected);
    }

    [Test]
    public async Task ABareCffFontStaysBare()
    {
        byte[] font = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/bare-SourceSansPro-Regular.cff"));

        byte[] subset = FontSubsetter.Subset(font, [1, 2]);

        await Assert.That(subset[0]).IsEqualTo((byte)1);
        await Assert.That(subset.Length).IsLessThan(font.Length / 3);
        await Assert.That(FontProgram.Load(subset).Format).IsEqualTo(FontProgramFormat.Cff);
    }

    [Test]
    public async Task AGlyphWhoseCharstringIsMalformedIsRefusedByName()
    {
        // The seac glyphs of this font name a base or an accent that the font does not have.
        byte[] font = File.ReadAllBytes(TestFonts.GetPath("FontPrograms/cff-seac-missing.otf"));
        FontProgram program = FontProgram.Load(font);
        ushort broken = Enumerable.Range(0, program.GlyphCount).Select(i => (ushort)i).First(i => Describe(program, i) == "failed");

        InvalidFontFileException exception = (await Assert.That(() => FontSubsetter.Subset(font, [broken])).Throws<InvalidFontFileException>())!;

        await Assert.That(exception.Message).Contains($"glyph {broken}");
    }

    private static void Check(CffIndex before, CffIndex after, int index, bool called, string name, List<string> failures)
    {
        before.TryGet(index, out ReadOnlyMemory<byte> original);
        after.TryGet(index, out ReadOnlyMemory<byte> actual);
        ReadOnlySpan<byte> expected = called ? original.Span : [0x0B];
        if (!actual.Span.SequenceEqual(expected))
        {
            failures.Add($"{name} {(called ? "is called and changed" : "is not called and is not a lone return")}");
        }
    }

    private static bool DrawsLike(FontProgram expected, FontProgram actual, ushort glyphId)
        => Describe(expected, glyphId) == Describe(actual, glyphId);

    private static string Describe(FontProgram program, ushort glyphId)
    {
        GlyphOutline outline = new();
        return program.TryWriteOutline(glyphId, ref outline) ? outline.ToString() : "failed";
    }

    private sealed class CallSet : ICffCallObserver
    {
        public HashSet<int> Global { get; } = [];

        public HashSet<int> Local { get; } = [];

        public void SubrCalled(bool isGlobal, int index) => (isGlobal ? this.Global : this.Local).Add(index);

        public void SeacComponentUsed(int glyphId)
        {
        }
    }
}
