// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Variations;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Tests for how a varied value is rounded to the integer that a public type stores.
/// </summary>
public class VariationRoundingTests
{
    [Test]
    [Arguments(2.5, 3)]
    [Arguments(-2.5, -2)]
    [Arguments(2.4999, 2)]
    [Arguments(-2.5001, -3)]
    [Arguments(0.5, 1)]
    [Arguments(-0.5, 0)]
    [Arguments(612, 612)]
    public async Task AHalfRoundsUpAsFontToolsOtRoundDoes(double value, int expected)
        => await Assert.That(VariationRounding.Round(value)).IsEqualTo(expected);

    [Test]
    [Arguments(-5.0, 0)]
    [Arguments(70000.0, 65535)]
    [Arguments(65534.6, 65535)]
    [Arguments(12.5, 13)]
    public async Task AnAdvanceStaysInTheRangeOfAnUnsignedShort(double value, int expected)
        => await Assert.That(VariationRounding.ToUInt16(value)).IsEqualTo((ushort)expected);

    [Test]
    [Arguments(-40000.0, -32768)]
    [Arguments(40000.0, 32767)]
    [Arguments(-3.5, -3)]
    public async Task ABearingStaysInTheRangeOfAShort(double value, int expected)
        => await Assert.That(VariationRounding.ToInt16(value)).IsEqualTo((short)expected);
}
