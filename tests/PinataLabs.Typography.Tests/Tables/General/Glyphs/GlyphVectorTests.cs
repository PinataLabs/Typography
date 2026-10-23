// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.TrueType.Glyphs;

namespace PinataLabs.Typography.Tests.Tables.General.Glyphs;

public class GlyphVectorTests
{
    [Test]
    public async Task CloneIsDeep()
    {
        // arrange
        ControlPoint[] controlPoints = { new(new Vector2(1.0f), true), new(new Vector2(2.0f), false) };
        ushort[] endPoints = { 1, 2, 3 };
        var bounds = new Bounds(1.0f, 2.0f, 3.0f, 4.0f);
        var outline = new GlyphVector(controlPoints, endPoints, bounds, Array.Empty<byte>(), false);

        // act
        var clone = GlyphVector.DeepClone(outline);

        // assert
        await Assert.That(outline.ControlPoints.Equals(clone.ControlPoints)).IsFalse();
        await Assert.That(outline.ControlPoints.SequenceEqual(clone.ControlPoints)).IsTrue();
        await Assert.That(outline.EndPoints.Equals(clone.EndPoints)).IsFalse();
        await Assert.That(outline.EndPoints.SequenceEqual(clone.EndPoints)).IsTrue();
    }

    [Test]
    public async Task TtfOffsetXy_Works()
    {
        // arrange
        ControlPoint[] controlPoints = { new(new Vector2(1.0f), true), new(new Vector2(2.0f), false) };
        ushort[] endPoints = { 1, 2, 3 };
        var bounds = new Bounds(1.0f, 2.0f, 3.0f, 4.0f);
        var expectedBounds = new Bounds(11.0f, 12.0f, 13.0f, 14.0f);
        var glyphVector = new GlyphVector(controlPoints, endPoints, bounds, Array.Empty<byte>(), false);

        // act
        Matrix3x2 matrix = Matrix3x2.Identity;
        matrix.Translation = new Vector2(10, 10);
        GlyphVector.TransformInPlace(ref glyphVector, matrix);

        // assert
        await Assert.That(glyphVector.Bounds).IsEqualTo(expectedBounds);
    }

    [Test]
    public async Task TtfTransformWith2x2Matrix_Works()
    {
        // arrange
        int precision = 2;
        ControlPoint[] controlPoints =
        {
            new(new Vector2(653.0f, 791.0f), true),
            new(new Vector2(1065.0f, 791.0f), false),
            new(new Vector2(1065.0f, 653.0f), true),
            new(new Vector2(653.0f, 653.0f), false),
            new(new Vector2(653.0f, 227.0f), true),
            new(new Vector2(514.0f, 227.0f), false),
            new(new Vector2(514.0f, 653.0f), true),
            new(new Vector2(104.0f, 653.0f), false),
            new(new Vector2(104.0f, 791.0f), true),
            new(new Vector2(514.0f, 791.0f), false),
            new(new Vector2(514.0f, 1219.0f), true),
            new(new Vector2(653.0f, 1219.0f), false),
            new(new Vector2(104.0f, 1.0f), true),
            new(new Vector2(104.0f, 139.0f), false),
            new(new Vector2(1065.0f, 139.0f), true),
            new(new Vector2(1065.0f, 1.0f), false),
        };

        ushort[] endPoints = { 1, 2, 3 };
        var bounds = new Bounds(16130.0f, 260.0f, 26624.0f, 28928.0f);
        var expectedBounds = new Bounds(19876f, 013684f, 89804.8f, 108083.2f);
        var glyphVector = new GlyphVector(controlPoints, endPoints, bounds, Array.Empty<byte>(), false);

        // act
        Matrix3x2 matrix = Matrix3x2.Identity;
        matrix.M11 = 1.2F;
        matrix.M12 = 0.8F;
        matrix.M21 = 2.0F;
        matrix.M22 = 3.0F;
        GlyphVector.TransformInPlace(ref glyphVector, matrix);

        // assert
        Bounds transformedBounds = glyphVector.Bounds;
        await Assert.That(Math.Round(transformedBounds.Min.X, precision)).IsEqualTo(Math.Round(expectedBounds.Min.X, precision));
        await Assert.That(Math.Round(transformedBounds.Min.Y, precision)).IsEqualTo(Math.Round(expectedBounds.Min.Y, precision));
        await Assert.That(Math.Round(transformedBounds.Max.X, precision)).IsEqualTo(Math.Round(expectedBounds.Max.X, precision));
        await Assert.That(Math.Round(transformedBounds.Max.Y, precision)).IsEqualTo(Math.Round(expectedBounds.Max.Y, precision));
    }
}
