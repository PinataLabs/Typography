// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.Variations;

public class NormalizedCoordinatesTests
{
    [Test]
    public async Task CoordinatesWithTheSameValuesAreEqualAndHaveTheSameHashCode()
    {
        NormalizedCoordinates a = new([100, -200, 0]);
        NormalizedCoordinates b = new([100, -200, 0]);

        await Assert.That(a == b).IsTrue();
        await Assert.That(a.Equals((object)b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    [Test]
    [Arguments(new short[] { 100, -200, 1 })]
    [Arguments(new short[] { 100, -200 })]
    [Arguments(new short[] { 100, -200, 0, 0 })]
    [Arguments(new short[] { -200, 100, 0 })]
    [Arguments(new short[] { })]
    public async Task CoordinatesThatDifferInAnyValueOrInTheNumberOfAxesAreNotEqual(short[] other)
    {
        NormalizedCoordinates coordinates = new([100, -200, 0]);

        await Assert.That(coordinates != new NormalizedCoordinates(other)).IsTrue();
        await Assert.That(coordinates.Equals(new NormalizedCoordinates(other))).IsFalse();
    }

    [Test]
    public async Task CoordinatesWorkAsDictionaryKeys()
    {
        Dictionary<NormalizedCoordinates, string> map = new()
        {
            [new([3452])] = "regular",
            [new([16384])] = "bold",
        };

        await Assert.That(map[new NormalizedCoordinates([3452])]).IsEqualTo("regular");
        await Assert.That(map.ContainsKey(new NormalizedCoordinates([3453]))).IsFalse();
    }

    [Test]
    [Arguments(new short[] { }, true)]
    [Arguments(new short[] { 0 }, true)]
    [Arguments(new short[] { 0, 0, 0 }, true)]
    [Arguments(new short[] { 0, 0, 1 }, false)]
    [Arguments(new short[] { -1 }, false)]
    public async Task OnlyCoordinatesThatAreAllZeroAreTheDefaultInstance(short[] values, bool expected)
    {
        await Assert.That(new NormalizedCoordinates(values).IsDefault).IsEqualTo(expected);
    }

    [Test]
    public async Task ADefaultValueHasNoAxes()
    {
        NormalizedCoordinates coordinates = default;

        await Assert.That(coordinates.Values.Length).IsEqualTo(0);
        await Assert.That(coordinates.IsDefault).IsTrue();
        await Assert.That(coordinates.Equals(new NormalizedCoordinates([]))).IsTrue();
    }
}
