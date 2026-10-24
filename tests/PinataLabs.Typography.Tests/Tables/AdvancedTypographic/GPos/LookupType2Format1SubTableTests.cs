// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables.AdvancedTypographic;
using PinataLabs.Typography.Tables.AdvancedTypographic.GPos;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests.Tables.AdvancedTypographic.GPos;

public class LookupType2Format1SubTableTests
{
    [Test]
    public async Task CoverageIndexBeyondPairSetsDoesNotThrow()
    {
        // The coverage table claims every glyph sits at index 5, but the subtable has no pair sets.
        Font font = FakeFont.CreateFont("AB");
        TextOptions options = new(font);
        TextRun run = new() { Start = 0, End = 2 };

        GlyphSubstitutionCollection substitutions = new(options);
        substitutions.AddGlyph(1, new CodePoint('A'), TextDirection.LeftToRight, run, 0);
        substitutions.AddGlyph(2, new CodePoint('B'), TextDirection.LeftToRight, run, 1);

        GlyphPositioningCollection positionings = new(options);
        positionings.TryAdd(font, substitutions);

        LookupType2SubTable.LookupType2Format1SubTable subTable = new(new FixedCoverageTable(5), [], default);

        bool updated = subTable.TryUpdatePosition(font.FontMetrics, null!, positionings, default, 0, 2);

        await Assert.That(updated).IsFalse();
    }

    private sealed class FixedCoverageTable(int index) : CoverageTable
    {
        public override int CoverageIndexOf(ushort glyphId) => index;
    }
}
