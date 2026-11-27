// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Cff;

namespace PinataLabs.Typography.Tests.FontPrograms;

/// <summary>
/// What the evaluation engine tells an observer while it runs a charstring: the subroutines that the charstring calls, and the
/// glyphs that a seac endchar draws.
/// </summary>
public class CffCallObserverTests
{
    [Test]
    public async Task ACharstringReportsEachSubroutineThatItCallsAndTheOnesThatThoseCall()
    {
        CffFont font = LoadCff("FontPrograms/cff-type2.otf");
        ushort glyph = FindGlyph(font, "subrs");
        Recorder recorder = new();

        // The glyph calls local subroutine 0, which calls global subroutine 0 and returns, and then local subroutine 1.
        await Assert.That(font.TryObserveCalls(glyph, recorder)).IsTrue();

        await Assert.That(recorder.Events).IsEquivalentTo(["local 0", "global 0", "local 1"]);
    }

    [Test]
    public async Task AGlyphThatEndsWithSeacReportsItsBaseAndItsAccent()
    {
        CffFont font = LoadCff("FontPrograms/cff-seac.otf");
        Recorder recorder = new();

        await Assert.That(font.TryObserveCalls(FindGlyph(font, "Aacute"), recorder)).IsTrue();

        await Assert.That(recorder.Events).IsEquivalentTo([$"seac {FindGlyph(font, "A")}", $"seac {FindGlyph(font, "acute")}"]);
    }

    [Test]
    public async Task AGlyphWithoutCallsReportsNothing()
    {
        CffFont font = LoadCff("FontPrograms/cff-seac.otf");
        Recorder recorder = new();

        await Assert.That(font.TryObserveCalls(FindGlyph(font, "A"), recorder)).IsTrue();

        await Assert.That(recorder.Events).IsEmpty();
    }

    [Test]
    public async Task AGlyphWithAMissingSeacComponentFailsAndAGlyphIdPastTheLastGlyphFails()
    {
        CffFont font = LoadCff("FontPrograms/cff-seac-missing.otf");

        await Assert.That(font.TryObserveCalls(FindGlyph(font, "Aring"), new Recorder())).IsFalse();
        await Assert.That(font.TryObserveCalls(font.GlyphCount, new Recorder())).IsFalse();
    }

    private static CffFont LoadCff(string fontFile)
    {
        return CffParser.Load(SfntRewriter.GetTable(File.ReadAllBytes(TestFonts.GetPath(fontFile)), "CFF ")!);
    }

    private static ushort FindGlyph(CffFont font, string name)
    {
        font.TryGetGlyphId(name, out ushort glyphId);
        return glyphId;
    }

    private sealed class Recorder : ICffCallObserver
    {
        public List<string> Events { get; } = [];

        public void SubrCalled(bool isGlobal, int index) => this.Events.Add($"{(isGlobal ? "global" : "local")} {index}");

        public void SeacComponentUsed(int glyphId) => this.Events.Add($"seac {glyphId}");
    }
}
