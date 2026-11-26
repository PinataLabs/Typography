// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Fuzz;
using PinataLabs.Typography.Tables.Variations;
using static PinataLabs.Typography.Fuzz.TupleVariationBytes;
using static PinataLabs.Typography.Fuzz.VariationBytes;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// The 'gvar' table sums the deltas of a glyph and interpolates the points that its tuples leave out. The expected deltas of the
/// interpolation are those of fontTools <c>iup_delta</c> for the same points and deltas.
/// </summary>
public class GvarTableTests
{
    private static readonly short One = F2Dot14(1);

    private static readonly Vector2[] Phantoms = [default, default, default, default];

    // A square, from its first corner clockwise.
    private static readonly Vector2[] Square = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];

    [Test]
    public async Task ThePointsBetweenTwoTouchedPointsMoveWithTheNearerOne()
    {
        // Points 0 and 2 have deltas. On x, point 1 is at the x of point 2 and point 3 is at the x of point 0.
        // On y, point 1 is at the y of point 0 and point 3 is at the y of point 2.
        Vector2[] deltas = GetDeltas(Square, [3], Sparse([0, 2], [10, 30], [1, 5]));

        await Assert.That(deltas).IsEquivalentTo(Expected((10, 1), (30, 1), (30, 5), (10, 5), (0, 0), (0, 0), (0, 0), (0, 0)));
    }

    [Test]
    public async Task AContourWithOneTouchedPointMovesAsAWhole()
    {
        Vector2[] deltas = GetDeltas(Square, [3], Sparse([1], [7], [-3]));

        await Assert.That(deltas).IsEquivalentTo(Expected((7, -3), (7, -3), (7, -3), (7, -3), (0, 0), (0, 0), (0, 0), (0, 0)));
    }

    [Test]
    public async Task AContourWithNoTouchedPointDoesNotMove()
    {
        Vector2[] points = [new(0, 0), new(100, 0), new(100, 100), new(0, 100), new(200, 0), new(300, 0), new(300, 100)];

        Vector2[] deltas = GetDeltas(points, [3, 6], Sparse([1, 2], [4, 8], [0, 0]));

        await Assert.That(deltas.Take(7)).IsEquivalentTo(Expected((0, 0), (4, 0), (8, 0), (0, 0), (0, 0), (0, 0), (0, 0)));
    }

    [Test]
    public async Task TouchedPointsOnTheSameCoordinateMoveThePointsBetweenThemOnlyWhenTheyAgree()
    {
        // Points 0 and 2 are on x 50 and have the deltas 10 and 20 on x, so the points between them stay. On y they differ.
        Vector2[] points = [new(50, 0), new(50, 50), new(50, 100), new(0, 0)];

        Vector2[] deltas = GetDeltas(points, [3], Sparse([0, 2], [10, 20], [0, 0]));

        await Assert.That(deltas).IsEquivalentTo(Expected((10, 0), (0, 0), (20, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)));
    }

    [Test]
    public async Task APointBetweenTwoTouchedPointsMovesInProportion()
    {
        Vector2[] points = [new(0, 0), new(50, 100), new(100, 0)];

        Vector2[] deltas = GetDeltas(points, [2], Sparse([0, 2], [0, 100], [0, -40]));

        await Assert.That(deltas).IsEquivalentTo(Expected((0, 0), (50, 0), (100, -40), (0, 0), (0, 0), (0, 0), (0, 0)));
    }

    [Test]
    public async Task ThePointsOutsideTheTouchedPointsWrapAroundTheContour()
    {
        Vector2[] points = [new(0, 0), new(60, 0), new(120, 0), new(120, 60), new(60, 120), new(0, 120)];

        Vector2[] deltas = GetDeltas(points, [5], Sparse([2, 3], [12, 24], [6, -6]));

        await Assert.That(deltas).IsEquivalentTo(Expected((0, 6), (0, 6), (12, 6), (24, -6), (0, -6), (0, -6), (0, 0), (0, 0), (0, 0), (0, 0)));
    }

    [Test]
    public async Task APhantomPointKeepsItsOwnDeltaAndIsNotInterpolated()
    {
        // Points 4 to 7 are the phantom points.
        Vector2[] deltas = GetDeltas(Square, [3], Sparse([0, 2, 4, 5, 6, 7], [1, 2, 5, 7, 9, 11], [1, 2, 6, 8, 10, 12]));

        await Assert.That(deltas).IsEquivalentTo(Expected((1, 1), (2, 1), (2, 2), (1, 2), (5, 6), (7, 8), (9, 10), (11, 12)));
    }

    [Test]
    public async Task ACompositeGlyphHasNoInterpolation()
    {
        // The points of a composite are its component offsets, and a composite has no contours.
        Vector2[] points = [new(10, 10), new(20, 20), new(30, 30)];

        Vector2[] deltas = GetDeltas(points, [], Sparse([0], [3], [4]));

        await Assert.That(deltas).IsEquivalentTo(Expected((3, 4), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0), (0, 0)));
    }

    [Test]
    public async Task ATupleWithEveryPointGivesTheDeltaOfEveryPointIncludingThePhantomPoints()
    {
        byte[] glyph = TupleVariationBytes.Glyph(null, All([One], [1, 2, 3, 4, 5, 6, 7, 8], [8, 7, 6, 5, 4, 3, 2, 1]));

        Vector2[] deltas = GetDeltas(Square, [3], glyph);

        await Assert.That(deltas).IsEquivalentTo(Expected((1, 8), (2, 7), (3, 6), (4, 5), (5, 4), (6, 3), (7, 2), (8, 1)));
    }

    [Test]
    public async Task TheDeltasOfTheTuplesAddUpAtTheirScalars()
    {
        // At 0.25 the first tuple has the scalar 0.25 and the second, which peaks at 0.5, has the scalar 0.5.
        byte[] glyph = TupleVariationBytes.Glyph(
            null,
            All([One], [100, 0, 0, 0, 0, 0, 0, 0], [0, 0, 0, 0, 0, 0, 0, 0]),
            new TupleData([F2Dot14(0.5)], [0], [One], null, [40, 0, 0, 0, 0, 0, 0, 0], [8, 0, 0, 0, 0, 0, 0, 0]));

        Vector2[] deltas = GetDeltas(Square, [3], glyph, F2Dot14(0.25));

        await Assert.That(deltas[0]).IsEqualTo(new Vector2(45, 4));
    }

    [Test]
    public async Task ATupleThatTheInstanceIsOutsideOfDoesNotMoveThePoints()
    {
        byte[] glyph = TupleVariationBytes.Glyph(null, All([(short)-One], [9, 9, 9, 9, 9, 9, 9, 9], [9, 9, 9, 9, 9, 9, 9, 9]));

        Vector2[] deltas = GetDeltas(Square, [3], glyph);

        await Assert.That(deltas.All(delta => delta == default)).IsTrue();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AGlyphThatHasNoDataAndAGlyphPastTheLastHaveNoDeltas(bool longOffsets)
    {
        byte[] glyph = TupleVariationBytes.Glyph(null, All([One], [1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1, 1, 1]));
        GvarTable table = Parse(1, [], [[], glyph], longOffsets);
        Vector2[] original = [.. Square, .. Phantoms];
        Vector2[] deltas = [.. Enumerable.Repeat(new Vector2(5), 8)];

        bool empty = table.TryGetDeltas(0, [One], original, [3], deltas);
        bool emptyIsZero = deltas.All(delta => delta == default);
        bool beyond = table.TryGetDeltas(2, [One], original, [3], deltas);
        bool beyondIsZero = deltas.All(delta => delta == default);
        bool present = table.TryGetDeltas(1, [One], original, [3], deltas);

        await Assert.That(empty && emptyIsZero).IsTrue();
        await Assert.That(beyond && beyondIsZero).IsTrue();
        await Assert.That(present).IsTrue();
        await Assert.That(deltas.All(delta => delta == Vector2.One)).IsTrue();
    }

    [Test]
    public async Task AGlyphWhoseDataIsMalformedGivesNoDeltas()
    {
        byte[] glyph = TupleVariationBytes.Glyph(null, All([One], [1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1, 1, 1]));
        byte[] table = TupleVariationBytes.Gvar(1, [], [glyph]);

        // The offsets say that the data of the glyph ends 3 bytes after the table does.
        GvarTable truncated = GvarTable.Parse(table.AsMemory(0, table.Length - 3), 1)!;
        Vector2[] deltas = [.. Enumerable.Repeat(new Vector2(5), 8)];

        bool read = truncated.TryGetDeltas(0, [One], [.. Square, .. Phantoms], [3], deltas);

        await Assert.That(read).IsFalse();
        await Assert.That(deltas.All(delta => delta == default)).IsTrue();
    }

    [Test]
    public async Task ATupleWhoseDataIsMalformedGivesNoDeltas()
    {
        // Eight points need eight deltas on x, and the run says nine.
        byte[] glyph = TupleVariationBytes.Glyph(null, All([One], [1, 1, 1, 1, 1, 1, 1, 1], [1, 1, 1, 1, 1, 1, 1, 1]));
        glyph[10] = 0x08;
        GvarTable table = Parse(1, [], [glyph]);
        Vector2[] deltas = new Vector2[8];

        bool read = table.TryGetDeltas(0, [One], [.. Square, .. Phantoms], [3], deltas);

        await Assert.That(read).IsFalse();
    }

    [Test]
    public async Task ASharedTupleOfTheTableGivesThePeak()
    {
        byte[] glyph = TupleVariationBytes.Glyph(null, new TupleData(null, null, null, null, [4, 4, 4, 4, 4, 4, 4, 4], [0, 0, 0, 0, 0, 0, 0, 0], 1));
        GvarTable table = Parse(1, [[(short)-One], [One]], [glyph]);
        Vector2[] deltas = new Vector2[8];

        table.TryGetDeltas(0, [F2Dot14(0.5)], [.. Square, .. Phantoms], [3], deltas);

        await Assert.That(deltas[0]).IsEqualTo(new Vector2(2, 0));
    }

    [Test]
    public async Task AHeaderThatDoesNotFitIsNotRead()
    {
        byte[] glyph = TupleVariationBytes.Glyph(null, All([One], [1], null));
        byte[] valid = TupleVariationBytes.Gvar(1, [[One]], [glyph]);

        await Assert.That(GvarTable.Parse(valid, 1)).IsNotNull();
        await Assert.That(GvarTable.Parse(valid, 2)).IsNull();
        await Assert.That(GvarTable.Parse(valid.AsMemory(0, 19), 1)).IsNull();
        await Assert.That(GvarTable.Parse(valid.AsMemory(0, 22), 1)).IsNull();

        // A version that is not 1, a shared tuple count that runs past the table, and an offset of the glyph data that does.
        byte[] version = [.. valid];
        version[1] = 2;
        byte[] sharedBeyond = [.. valid];
        sharedBeyond[7] = 200;
        byte[] dataBeyond = [.. valid];
        dataBeyond[16] = 0x7F;
        await Assert.That(GvarTable.Parse(version, 1)).IsNull();
        await Assert.That(GvarTable.Parse(sharedBeyond, 1)).IsNull();
        await Assert.That(GvarTable.Parse(dataBeyond, 1)).IsNull();
    }

    // The deltas of the points of a glyph and of its four phantom points, for an instance at 1 on its axis.
    private static Vector2[] GetDeltas(Vector2[] points, ushort[] endPoints, byte[] glyph, short coordinate = 16384)
    {
        GvarTable table = Parse(1, [], [glyph]);
        Vector2[] deltas = new Vector2[points.Length + Phantoms.Length];
        if (!table.TryGetDeltas(0, [coordinate], [.. points, .. Phantoms], endPoints, deltas))
        {
            throw new InvalidOperationException("The deltas were not read.");
        }

        return deltas;
    }

    private static Vector2[] Expected(params (float X, float Y)[] deltas) => [.. deltas.Select(delta => new Vector2(delta.X, delta.Y))];

    // A tuple at 1 that has the deltas of some of the points.
    private static byte[] Sparse(int[] points, int[] deltaX, int[] deltaY)
        => TupleVariationBytes.Glyph(null, new TupleData([One], null, null, points, deltaX, deltaY));

    // A tuple with a delta for every point.
    private static TupleData All(short[] peak, int[] deltaX, int[]? deltaY) => new(peak, null, null, null, deltaX, deltaY);

    private static GvarTable Parse(int axisCount, short[][] sharedTuples, byte[][] glyphs, bool longOffsets = false)
        => GvarTable.Parse(TupleVariationBytes.Gvar(axisCount, sharedTuples, glyphs, longOffsets), axisCount)!;
}
