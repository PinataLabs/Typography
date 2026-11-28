// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Unicode;

namespace PinataLabs.Typography.Tests;

public class GlyphPositioningCollectionTests
{
    // An update that finds no metric of the glyph, as for a color glyph whose metrics are its layers, moves nothing. The
    // glyph then keeps the change to its offset for the next update that finds one.
    [Test]
    public async Task AnUpdateThatMovesNoMetricLeavesTheOffsetForTheNext()
    {
        Font font = FakeFont.CreateFont("A");
        TextOptions options = new(font);
        GlyphSubstitutionCollection substitutions = new(options);
        substitutions.AddGlyph(1, new CodePoint('A'), TextDirection.LeftToRight, new TextRun { Start = 0, End = 1 }, 0, 0, 0);
        GlyphPositioningCollection collection = new(options);
        collection.TryAdd(font, substitutions);

        GlyphShapingData data = collection[0];
        ushort glyphId = data.GlyphId;
        data.GlyphId = (ushort)(glyphId + 1);
        data.Bounds.X = 100;
        data.Bounds.Y = 50;
        collection.UpdatePosition(font.FontMetrics, 0);
        data.GlyphId = glyphId;
        collection.UpdatePosition(font.FontMetrics, 0);

        await Assert.That(collection.GetMetrics(0).Offset).IsEqualTo(new Vector2(100, 50));
    }

    // HarfBuzz hides a default-ignorable character that the font lacks, so a font that lacks only U+034F after a needs no
    // fallback font, and the layout takes no fallback pass.
    [Test]
    public async Task AFontThatLacksOnlyADefaultIgnorableCharacterNeedsNoFallbackFont()
    {
        Font font = new FontCollection().Add(TestFonts.FontRunContextFile).CreateFont(12);
        TextOptions options = new(font);
        GlyphSubstitutionCollection substitutions = new(options);
        TextRun textRun = new() { Start = 0, End = 1, Font = font };
        CodePoint[] codePoints = [new('a'), new(0x034F)];
        for (int i = 0; i < codePoints.Length; i++)
        {
            font.FontMetrics.TryGetGlyphId(codePoints[i], out ushort glyphId);
            substitutions.AddGlyph(glyphId, codePoints[i], TextDirection.LeftToRight, textRun, i, i, i);
        }

        bool isComplete = new GlyphPositioningCollection(options).TryAdd(font, substitutions);

        await Assert.That(isComplete).IsTrue();
    }

    // The layout adds the glyphs of each text run in turn and takes a fallback pass if any run needs one, so each call
    // tells only about the glyphs that it adds, and does not look at the glyphs of the runs before it again.
    [Test]
    public async Task AddingTellsWhetherTheGlyphsItAddsNeedAFallbackFont()
    {
        Font font = new FontCollection().Add(TestFonts.FontRunContextFile).CreateFont(12);
        TextOptions options = new(font);
        GlyphPositioningCollection collection = new(options);

        bool isFirstComplete = collection.TryAdd(font, Substitutions(font, options, 'x', 0));
        bool isSecondComplete = collection.TryAdd(font, Substitutions(font, options, 'a', 1));

        await Assert.That(isFirstComplete).IsFalse();
        await Assert.That(isSecondComplete).IsTrue();
    }

    private static GlyphSubstitutionCollection Substitutions(Font font, TextOptions options, char character, int offset)
    {
        GlyphSubstitutionCollection substitutions = new(options);
        font.FontMetrics.TryGetGlyphId(new CodePoint(character), out ushort glyphId);
        TextRun textRun = new() { Start = offset, End = offset + 1, Font = font };
        substitutions.AddGlyph(glyphId, new CodePoint(character), TextDirection.LeftToRight, textRun, offset, offset, offset);
        return substitutions;
    }

    // A control character needs no fallback font, so it keeps the .notdef of the font of the text when a fallback font
    // draws another character of the text.
    [Test]
    public async Task AControlCharacterKeepsTheFontOfTheTextInAFallbackPass()
    {
        FontCollection collection = new();
        Font font = collection.Add(TestFonts.FontRunContextFile).CreateFont(12);
        FontFamily fallback = collection.Add(TestFonts.FontRunContextFallbackFile);
        TextOptions options = new(font) { FallbackFontFamilies = [fallback] };

        IReadOnlyList<GlyphLayout> layout = TextLayout.GenerateLayout("ax\u0001".AsSpan(), options);

        GlyphMetrics a = layout.Single(g => g.Glyph.GlyphMetrics.CodePoint.Value == 'a').Glyph.GlyphMetrics;
        GlyphMetrics control = layout.Single(g => g.Glyph.GlyphMetrics.CodePoint.Value == 1).Glyph.GlyphMetrics;
        await Assert.That(control.FontMetrics).IsSameReferenceAs(a.FontMetrics);
    }
}
