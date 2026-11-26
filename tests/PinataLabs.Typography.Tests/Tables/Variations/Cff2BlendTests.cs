// Copyright (c) PinataLabs.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Threading.Tasks;
using PinataLabs.Typography.Tables.Cff;
using PinataLabs.Typography.Tests.FontPrograms;

namespace PinataLabs.Typography.Tests.Tables.Variations;

/// <summary>
/// Evaluates hand-assembled CFF2 charstrings with the <c>blend</c> operator. fontTools does not run a charstring that
/// the font does not hold, so these cases check the arithmetic, and Cff2VariationOutlineTests checks the fonts.
/// </summary>
public class Cff2BlendTests
{
    private const int Vsindex = 15;
    private const int Blend = 16;
    private const int Rmoveto = 21;
    private const int Rlineto = 5;

    [Test]
    public async Task BlendAddsTheDeltasOfEachValueScaledByTheirRegions()
    {
        // Two values, 10 and 20, with two deltas each: 4 and 8 for the first, -2 and 16 for the second.
        float[]?[] scalars = [[0.5F, 0.25F]];

        string? outline = Evaluate(scalars, 0, Numbers(10, 20, 4, 8, -2, 16, 2), Op(Blend), Op(Rmoveto), Numbers(30, 40), Op(Rlineto));

        // 10 + 0.5 * 4 + 0.25 * 8 = 14 and 20 + 0.5 * -2 + 0.25 * 16 = 23.
        await Assert.That(outline).IsEqualTo("M 14 23 L 44 63 Z");
    }

    [Test]
    public async Task BlendWithScalarsOfZeroKeepsTheDefaultValues()
    {
        // The default instance has the scalar 0 for every region, whatever the deltas.
        float[]?[] scalars = [[0F, 0F]];

        string? outline = Evaluate(scalars, 0, Numbers(10, 20, 4, 8, -2, 16, 2), Op(Blend), Op(Rmoveto), Numbers(30, 40), Op(Rlineto));

        await Assert.That(outline).IsEqualTo("M 10 20 L 40 60 Z");
    }

    [Test]
    public async Task BlendWithoutDeltasKeepsTheValues()
    {
        // A subtable with no regions has no deltas, and the n values stay.
        float[]?[] scalars = [[]];

        string? outline = Evaluate(scalars, 0, Numbers(10, 20, 2), Op(Blend), Op(Rmoveto), Numbers(30, 40), Op(Rlineto));

        await Assert.That(outline).IsEqualTo("M 10 20 L 40 60 Z");
    }

    [Test]
    public async Task VsindexSelectsTheScalarsOfItsSubtable()
    {
        float[]?[] scalars = [[1F], [0.5F, 0.5F]];

        // Subtable 1 has two regions: 10 + 0.5 * 2 + 0.5 * 4 = 13 and 20 + 0.5 * 6 + 0.5 * 8 = 27.
        string? outline = Evaluate(scalars, 0, Numbers(1), Op(Vsindex), Numbers(10, 20, 2, 4, 6, 8, 2), Op(Blend), Op(Rmoveto));

        await Assert.That(outline).IsEqualTo("M 13 27 Z");
    }

    [Test]
    public async Task TheVsindexOfTheFontDictionaryStartsEachGlyph()
    {
        float[]?[] scalars = [[1F], [0.5F, 0.5F]];

        string? outline = Evaluate(scalars, 1, Numbers(10, 20, 2, 4, 6, 8, 2), Op(Blend), Op(Rmoveto));

        await Assert.That(outline).IsEqualTo("M 13 27 Z");
    }

    [Test]
    public async Task BlendsOfOneGlyphScaleEachWithTheSubtableThatIsCurrent()
    {
        float[]?[] scalars = [[1F], [0.5F, 0.5F]];

        string? outline = Evaluate(
            scalars,
            0,
            Numbers(10, 5, 1),
            Op(Blend),
            Numbers(1),
            Op(Vsindex),
            Numbers(20, 2, 6, 1),
            Op(Blend),
            Op(Rmoveto));

        // 10 + 5 = 15, then 20 + 0.5 * 2 + 0.5 * 6 = 24.
        await Assert.That(outline).IsEqualTo("M 15 24 Z");
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(-1)]
    public async Task ASubtableThatTheStoreLacksFailsTheGlyph(int vsindex)
    {
        // Subtable 1 is malformed, which gives it no scalars, and there is no subtable 2 or -1.
        float[]?[] scalars = [[1F], null];

        string? outline = Evaluate(scalars, 0, Numbers(vsindex), Op(Vsindex), Numbers(10, 20, 2, 4, 2), Op(Blend), Op(Rmoveto));

        await Assert.That(outline).IsNull();
    }

    [Test]
    [Arguments(3)]
    [Arguments(-1)]
    public async Task BlendWithMoreValuesThanTheStackHoldsFails(int count)
    {
        // Three values and their deltas need six operands, and the stack has four before the count.
        float[]?[] scalars = [[1F]];

        string? outline = Evaluate(scalars, 0, Numbers(10, 20, 4, 8, count), Op(Blend), Op(Rmoveto));

        await Assert.That(outline).IsNull();
    }

    private static string? Evaluate(float[]?[]? scalars, int vsindex, params byte[][] parts)
    {
        List<byte> charString = [];
        foreach (byte[] part in parts)
        {
            charString.AddRange(part);
        }

        CffGlyphData glyph = new(0, default, default, 0, charString.ToArray(), scalars, vsindex);
        GlyphOutline outline = new();
        return glyph.TryWriteOutline(ref outline) ? outline.ToString() : null;
    }

    private static byte[] Op(int code) => [(byte)code];

    // Each number is a 16-bit integer, which has the same bytes in every version of the charstring format.
    private static byte[] Numbers(params int[] values)
    {
        List<byte> bytes = [];
        foreach (int value in values)
        {
            bytes.AddRange([28, (byte)(value >> 8), (byte)value]);
        }

        return bytes.ToArray();
    }
}
