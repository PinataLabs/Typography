// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

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
        substitutions.AddGlyph(1, new CodePoint('A'), TextDirection.LeftToRight, new TextRun { Start = 0, End = 1 }, 0, 0);
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
}
