// Copyright (c) Six Labors.
// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;

namespace PinataLabs.Typography.Tests;

public class FontRectangleTests
{
    [Test]
    public async Task DefaultConstructorTest()
    {
        await Assert.That(FontRectangle.Empty).IsEqualTo(default);
    }

    [Test]
    [Arguments(0, 0, 0, 0)]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, 0, 0, float.MaxValue)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task NonDefaultConstructorTest(float x, float y, float width, float height)
    {
        var rect1 = new FontRectangle(x, y, width, height);
        var p = new Vector2(x, y);
        var s = new Vector2(width, height);
        var rect2 = new FontRectangle(p, s);

        await Assert.That(rect2).IsEqualTo(rect1);
    }

    [Test]
    [Arguments(0, 0, 0, 0)]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, 0, 0, float.MaxValue)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task FromLTRBTest(float left, float top, float right, float bottom)
    {
        var expected = new FontRectangle(left, top, right - left, bottom - top);
        var actual = FontRectangle.FromLTRB(left, top, right, bottom);

        await Assert.That(actual).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, 0, 0, 0)]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, 0, 0, float.MaxValue)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task DimensionsTest(float x, float y, float width, float height)
    {
        var rect = new FontRectangle(x, y, width, height);
        var p = new Vector2(x, y);
        var s = new Vector2(width, height);

        await Assert.That(rect.Location).IsEqualTo(p);
        await Assert.That(rect.Size).IsEqualTo(s);
        await Assert.That(rect.X).IsEqualTo(x);
        await Assert.That(rect.Y).IsEqualTo(y);
        await Assert.That(rect.Width).IsEqualTo(width);
        await Assert.That(rect.Height).IsEqualTo(height);
        await Assert.That(rect.Left).IsEqualTo(x);
        await Assert.That(rect.Top).IsEqualTo(y);
        await Assert.That(rect.Right).IsEqualTo(x + width);
        await Assert.That(rect.Bottom).IsEqualTo(y + height);
    }

    [Test]
    public async Task IsEmptyTest()
    {
        await Assert.That(FontRectangle.Empty.IsEmpty).IsTrue();
        await Assert.That(default(FontRectangle).IsEmpty).IsTrue();
        await Assert.That(new FontRectangle(1, -2, -10, 10).IsEmpty).IsTrue();
        await Assert.That(new FontRectangle(1, -2, 10, -10).IsEmpty).IsTrue();
        await Assert.That(new FontRectangle(1, -2, 0, 0).IsEmpty).IsTrue();

        await Assert.That(new FontRectangle(0, 0, 10, 10).IsEmpty).IsFalse();
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(float.MaxValue, float.MinValue)]
    public async Task LocationSetTest(float x, float y)
    {
        var point = new Vector2(x, y);
        var rect = new FontRectangle(point.X, point.Y, 10, 10);
        await Assert.That(rect.Location).IsEqualTo(point);
        await Assert.That(rect.X).IsEqualTo(point.X);
        await Assert.That(rect.Y).IsEqualTo(point.Y);
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(float.MaxValue, float.MinValue)]
    public async Task SizeSetTest(float x, float y)
    {
        var size = new Vector2(x, y);
        var rect = new FontRectangle(10, 10, size.X, size.Y);
        await Assert.That(rect.Size).IsEqualTo(size);
        await Assert.That(rect.Width).IsEqualTo(size.X);
        await Assert.That(rect.Height).IsEqualTo(size.Y);
    }

    [Test]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, 0, 0, float.MaxValue)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task EqualityTest(float x, float y, float width, float height)
    {
        var rect1 = new FontRectangle(x, y, width, height);
        var rect2 = new FontRectangle(width, height, x, y);

        await Assert.That(rect1 != rect2).IsTrue();
        await Assert.That(rect1 == rect2).IsFalse();
        await Assert.That(rect1.Equals(rect2)).IsFalse();
        await Assert.That(rect1.Equals((object)rect2)).IsFalse();
    }

    [Test]
    public async Task GetHashCodeTest()
    {
        var rect1 = new FontRectangle(10, 10, 10, 10);
        var rect2 = new FontRectangle(10, 10, 10, 10);
        await Assert.That(rect2.GetHashCode()).IsEqualTo(rect1.GetHashCode());
        await Assert.That(new FontRectangle(20, 10, 10, 10).GetHashCode()).IsNotEqualTo(rect1.GetHashCode());
        await Assert.That(new FontRectangle(10, 20, 10, 10).GetHashCode()).IsNotEqualTo(rect1.GetHashCode());
        await Assert.That(new FontRectangle(10, 10, 20, 10).GetHashCode()).IsNotEqualTo(rect1.GetHashCode());
        await Assert.That(new FontRectangle(10, 10, 10, 20).GetHashCode()).IsNotEqualTo(rect1.GetHashCode());
    }

    [Test]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task ContainsTest(float x, float y, float width, float height)
    {
        var rect = new FontRectangle(x, y, width, height);
        float x1 = (x + width) / 2;
        float y1 = (y + height) / 2;
        var p = new Vector2(x1, y1);
        var r = new FontRectangle(x1, y1, width / 2, height / 2);

        await Assert.That(rect.Contains(x1, y1)).IsFalse();
        await Assert.That(rect.Contains(p)).IsFalse();
        await Assert.That(rect.Contains(r)).IsFalse();
    }

    [Test]
    [Arguments(0, 0, 0, 0)]
    [Arguments(float.MaxValue / 2, float.MinValue / 2, float.MinValue / 2, float.MaxValue / 2)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task InflateTest(float x, float y, float width, float height)
    {
        var rect = new FontRectangle(x, y, width, height);
        var inflatedRect = new FontRectangle(x - width, y - height, width + (2 * width), height + (2 * height));

        rect = rect.Inflate(width, height);
        await Assert.That(rect).IsEqualTo(inflatedRect);

        var s = new Vector2(x, y);
        inflatedRect = FontRectangle.Inflate(rect, x, y);

        rect = rect.Inflate(s);
        await Assert.That(rect).IsEqualTo(inflatedRect);
    }

    [Test]
    [Arguments(float.MaxValue, float.MinValue, float.MaxValue / 2, float.MinValue / 2)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task IntersectTest(float x, float y, float width, float height)
    {
        var rect1 = new FontRectangle(x, y, width, height);
        var rect2 = new FontRectangle(y, x, width, height);
        var expectedRect = FontRectangle.Intersect(rect1, rect2);
        rect1 = rect1.Intersect(rect2);
        await Assert.That(rect1).IsEqualTo(expectedRect);
        await Assert.That(rect1.IntersectsWith(expectedRect)).IsFalse();
    }

    [Test]
    public async Task IntersectIntersectingRectsTest()
    {
        var rect1 = new FontRectangle(0, 0, 5, 5);
        var rect2 = new FontRectangle(1, 1, 3, 3);
        var expected = new FontRectangle(1, 1, 3, 3);

        await Assert.That(FontRectangle.Intersect(rect1, rect2)).IsEqualTo(expected);
    }

    [Test]
    [Arguments(0, 0, 0, 0)]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, 0, 0, float.MaxValue)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task UnionTest(float x, float y, float width, float height)
    {
        var a = new FontRectangle(x, y, width, height);
        var b = new FontRectangle(width, height, x, y);

        float x1 = Math.Min(a.X, b.X);
        float x2 = Math.Max(a.X + a.Width, b.X + b.Width);
        float y1 = Math.Min(a.Y, b.Y);
        float y2 = Math.Max(a.Y + a.Height, b.Y + b.Height);

        var expectedRectangle = new FontRectangle(x1, y1, x2 - x1, y2 - y1);

        await Assert.That(FontRectangle.Union(a, b)).IsEqualTo(expectedRectangle);
    }

    [Test]
    [Arguments(0, 0, 0, 0)]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, 0, 0, float.MaxValue)]
    [Arguments(0, float.MinValue, float.MaxValue, 0)]
    public async Task OffsetTest(float x, float y, float width, float height)
    {
        var r1 = new FontRectangle(x, y, width, height);
        var expectedRect = new FontRectangle(x + width, y + height, width, height);
        var p = new Vector2(width, height);

        r1 = r1.Offset(p);
        await Assert.That(r1).IsEqualTo(expectedRect);

        expectedRect = expectedRect.Offset(p);
        r1 = r1.Offset(width, height);
        await Assert.That(r1).IsEqualTo(expectedRect);
    }

    [Test]
    public async Task ToStringTest()
    {
        var r = new FontRectangle(5, 5.1F, 1.3F, 1);
        await Assert.That(r.ToString()).IsEqualTo(string.Format(CultureInfo.CurrentCulture, "FontRectangle [ X={0}, Y={1}, Width={2}, Height={3} ]", r.X, r.Y, r.Width, r.Height));
    }

    [Test]
    [Arguments(float.MinValue, float.MaxValue, float.MaxValue, float.MaxValue)]
    [Arguments(float.MinValue, float.MaxValue, float.MaxValue, float.MinValue)]
    [Arguments(float.MinValue, float.MaxValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MinValue, float.MaxValue, float.MinValue, float.MinValue)]
    [Arguments(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue)]
    [Arguments(float.MinValue, float.MinValue, float.MaxValue, float.MinValue)]
    [Arguments(float.MinValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MinValue, float.MinValue, float.MinValue, float.MinValue)]
    [Arguments(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue)]
    [Arguments(float.MaxValue, float.MaxValue, float.MaxValue, float.MinValue)]
    [Arguments(float.MaxValue, float.MaxValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue)]
    [Arguments(float.MaxValue, float.MinValue, float.MaxValue, float.MaxValue)]
    [Arguments(float.MaxValue, float.MinValue, float.MaxValue, float.MinValue)]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MaxValue)]
    [Arguments(float.MaxValue, float.MinValue, float.MinValue, float.MinValue)]
    [Arguments(0, 0, 0, 0)]
    public async Task DeconstructTest(float x, float y, float width, float height)
    {
        (float dx, float dy, float dw, float dh) = new FontRectangle(x, y, width, height);

        await Assert.That(dx).IsEqualTo(x);
        await Assert.That(dy).IsEqualTo(y);
        await Assert.That(dw).IsEqualTo(width);
        await Assert.That(dh).IsEqualTo(height);
    }
}
